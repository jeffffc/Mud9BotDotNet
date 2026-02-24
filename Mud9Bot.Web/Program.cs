using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Data.Interfaces;
using Mud9Bot.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. 資料庫連線
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Register Settings Service (Shared with Bot project)
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IBlacklistService, BlacklistService>();

builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();


// 🚀 3. Initialize Settings Cache on Startup
using (var scope = app.Services.CreateScope())
{
    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    await settings.InitializeAsync();
}

// 🚀 加入 XForwardedHost，確保 .NET 能讀取到 NPM 轉發的真實網域
app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                       ForwardedHeaders.XForwardedProto | 
                       ForwardedHeaders.XForwardedHost
});

app.UseCors("AllowAll");

// 🚀 新增：攔截直接訪問 API 的中介軟體 (Sec-Fetch-Mode 檢查)
app.Use(async (context, next) =>
{
    // 只有針對 /api 開頭的路徑進行檢查
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        // 取得瀏覽器原生的 Sec-Fetch-Mode 標頭
        var fetchMode = context.Request.Headers["Sec-Fetch-Mode"].ToString();

        // 如果是 'navigate'，代表使用者是直接在網址列輸入或點擊一般連結進入
        if (string.Equals(fetchMode, "navigate", StringComparison.OrdinalIgnoreCase))
        {
            // 將使用者重新導向回首頁，而不是讓他們看到醜陋的 JSON
            context.Response.Redirect("/");
            return; // 終止後續處理
        }
    }

    // 如果不是直接訪問，或者根本沒有這個標頭 (例如某些舊版瀏覽器或 Server-to-Server 請求)，則繼續放行
    await next();
});

app.UseDefaultFiles(); 
app.UseStaticFiles(); 

// 🚀 核心邏輯：根據請求的 Host (網域) 決定首頁要顯示哪個檔案
app.MapGet("/", async (context) => {
    context.Response.ContentType = "text/html";
    string host = context.Request.Host.Host.ToLower();

    if (host.StartsWith("admin."))
    {
        await context.Response.SendFileAsync("wwwroot/admin.html");
    }
    
    // 如果網域包含 site 或 stats，就給他看數據儀表板
    if (host.StartsWith("stats."))
    {
        await context.Response.SendFileAsync("wwwroot/dashboard.html");
    }
    else
    {
        // 否則預設 (mud9bot.info) 顯示產品介紹頁
        await context.Response.SendFileAsync("wwwroot/index.html");
    }
});


if (builder.Environment.IsProduction())
{
// 保留這條路由，以防有人直接打 /dashboard
    app.MapGet("/dashboard", (context) =>
    {
        // Force redirect to the subdomain
        context.Response.Redirect("https://stats.mud9bot.info", permanent: true);
        return Task.CompletedTask;
    });

    app.MapGet("/admin", (context) =>
    {
        context.Response.Redirect("https://admin.mud9bot.info", permanent: true);
        return Task.CompletedTask;
    });
}


// ---------------------------------------------------------
// 🚀 Admin Auth: Telegram Login Widget Verification
// ---------------------------------------------------------
app.MapPost("/api/admin/auth", async (HttpContext context, IConfiguration config) =>
{
    var form = await context.Request.ReadFormAsync();
    var botToken = config["BotConfiguration:BotToken"] ?? "";
    
    // Support both array and comma-separated string formats for DevIds
    var devIds = config.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? [];
    if (!devIds.Any() && config["BotConfiguration:DevIds"] is string devStr)
    {
        devIds = new HashSet<long>(devStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse));
    }

    // 1. Extract Telegram Data
    var authData = form.ToDictionary(x => x.Key, x => x.Value.ToString());
    if (!authData.ContainsKey("hash")) return Results.BadRequest("Missing hash");

    // 2. Validate HMAC Signature (Telegram Standard)
    var hash = authData["hash"];
    authData.Remove("hash");
    var dataCheckString = string.Join("\n", authData.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));

    using var sha256 = SHA256.Create();
    var secretKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(botToken));
    using var hmac = new HMACSHA256(secretKey);
    var checkHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).Replace("-", "").ToLower();

    if (checkHash != hash) return Results.Unauthorized();

    // 3. Verify if user is in Dev List
    var userId = long.Parse(authData["id"]);
    if (!devIds.Contains(userId)) return Results.Forbid();

    // 4. Return success (In production, consider setting a secure Cookie or JWT here)
    return Results.Ok(new { success = true, user = authData["first_name"] });
});

// ---------------------------------------------------------
// 🚀 Admin API: Settings Management
// ---------------------------------------------------------
app.MapGet("/api/admin/settings", async (BotDbContext db) =>
{
    // Fetch all global toggles and thresholds
    var settings = await db.Set<SystemSetting>().ToListAsync();
    return Results.Ok(settings);
});

app.MapPost("/api/admin/maintenance", async (bool enable, ISettingsService settings, BotDbContext db) =>
{
    var val = enable ? "true" : "false";
    var entity = await db.Set<SystemSetting>().FindAsync("is_maintenance");
    
    if (entity != null)
    {
        entity.SettingValue = val;
        entity.LastUpdated = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        // Sync the RAM cache of the Web process immediately
        settings.RefreshSetting("is_maintenance", val);
        
        return Results.Ok(new { status = val });
    }
    return Results.NotFound();
});

// ---------------------------------------------------------
// 🚀 Inspector API: User Search
// ---------------------------------------------------------
app.MapGet("/api/admin/users/search", async (string query, BotDbContext db) =>
{
    // Limit to 50 results for performance
    var users = await db.Set<BotUser>()
        .Where(u => u.TelegramId.ToString().Contains(query) || 
                    (u.FirstName + " " + (u.LastName ?? "")).Contains(query) ||
                    (u.Username ?? "").Contains(query))
        .OrderByDescending(u => u.TimeAdded)
        .Take(50)
        .ToListAsync();

    return Results.Ok(users);
});

// ---------------------------------------------------------
// 🚀 Inspector API: Group Search
// ---------------------------------------------------------
app.MapGet("/api/admin/groups/search", async (string query, BotDbContext db) =>
{
    var groups = await db.Set<BotGroup>()
        .Where(g => g.TelegramId.ToString().Contains(query) || 
                    g.Title.Contains(query) ||
                    (g.Username ?? "").Contains(query))
        .OrderByDescending(g => g.TimeAdded)
        .Take(50)
        .ToListAsync();

    return Results.Ok(groups);
});

// ---------------------------------------------------------
// 🚀 Broadcast API: Status
// ---------------------------------------------------------
app.MapGet("/api/admin/broadcast/status", () => Results.Ok(new {
    state = BroadcastManager.State,
    total = BroadcastManager.Total,
    processed = BroadcastManager.Processed,
    success = BroadcastManager.Success,
    failed = BroadcastManager.Failed
}));

// ---------------------------------------------------------
// 🚀 Broadcast API: Cancel
// ---------------------------------------------------------
app.MapPost("/api/admin/broadcast/cancel", () => {
    BroadcastManager.Cts?.Cancel();
    BroadcastManager.State = "Cancelled";
    return Results.Ok();
});

// ---------------------------------------------------------
// 🚀 Broadcast API: Start
// ---------------------------------------------------------
app.MapPost("/api/admin/broadcast/start", async (BroadcastRequest req, BotDbContext db, IConfiguration config, ISettingsService settings) =>
{
    if (BroadcastManager.State == "Running") return Results.Conflict("A broadcast is already running.");

    // 1. Prepare Targets
    List<long> targetIds = new();
    if (req.Target == "users") targetIds = await db.Set<BotUser>().Select(u => u.TelegramId).ToListAsync();
    else if (req.Target == "groups") targetIds = await db.Set<BotGroup>().Select(g => g.TelegramId).ToListAsync();
    else if (req.Target == "devs") 
    {
        var devStr = config["BotConfiguration:DevIds"] ?? "";
        targetIds = devStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToList();
    }

    // 2. Initialize Task
    BroadcastManager.State = "Running";
    BroadcastManager.Total = targetIds.Count;
    BroadcastManager.Processed = 0;
    BroadcastManager.Success = 0;
    BroadcastManager.Failed = 0;
    BroadcastManager.Cts = new CancellationTokenSource();

    var token = BroadcastManager.Cts.Token;
    var botToken = config["BotConfiguration:BotToken"] ?? "";
    var delayMs = int.Parse(settings.GetSetting("broadcast_delay_ms", "35"));

    // 3. Start Background Processing (Fire and Forget)
    _ = Task.Run(async () =>
    {
        using var client = new HttpClient();
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        foreach (var id in targetIds)
        {
            if (token.IsCancellationRequested) break;

            try
            {
                var payload = new { chat_id = id, text = req.Content, parse_mode = "HTML" };
                var response = await client.PostAsJsonAsync(url, payload, token);
                
                if (response.IsSuccessStatusCode) BroadcastManager.Success++;
                else BroadcastManager.Failed++;
            }
            catch { BroadcastManager.Failed++; }

            BroadcastManager.Processed++;
            await Task.Delay(delayMs); 
        }

        if (BroadcastManager.State != "Cancelled") BroadcastManager.State = "Completed";
    });

    return Results.Accepted();
});

// ---------------------------------------------------------
// 🚀 Blacklist API
// ---------------------------------------------------------
app.MapGet("/api/admin/blacklist", async (BotDbContext db) => 
    Results.Ok(await db.Set<BlacklistedId>().OrderByDescending(b => b.TimeAdded).ToListAsync()));

app.MapPost("/api/admin/blacklist/add", async (BlacklistAddRequest req, IBlacklistService blacklist) =>
{
    // adminId should ideally come from current session
    await blacklist.AddAsync(req.TelegramId, req.Reason ?? "No reason", 0);
    return Results.Ok();
});

app.MapPost("/api/admin/blacklist/remove", async (long id, IBlacklistService blacklist) =>
{
    await blacklist.RemoveAsync(id);
    return Results.Ok();
});

// ---------------------------------------------------------
// 📊 Public Stats API
// ---------------------------------------------------------
app.MapGet("/api/stats", async (BotDbContext db) =>
{
    try 
    {
        var logs = await db.Set<BotEventLog>().ToListAsync();

        // 彙總近期運作數據
        var totalVolume = logs.Where(l => l.EventType == "system" && l.Metadata == "total_volume").Sum(l => l.Count);
        var commandUsage = logs.Where(l => l.EventType == "command").Sum(l => l.Count);
        var buttonClicks = logs.Where(l => l.EventType == "interaction").Sum(l => l.Count);

        // 指令排行 (Top 10)
        var topCommands = logs.Where(l => l.EventType == "command")
            .GroupBy(l => l.Metadata)
            .Select(g => new { Command = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(10);

        // 🚀 新增：互動排行 (Top 10) - 統計按鈕與 Regex
        var topInteractions = logs.Where(l => l.EventType == "interaction")
            .GroupBy(l => l.Metadata)
            .Select(g => new { Function = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(10);

        var chatDist = logs.Where(l => l.EventType == "system")
            .GroupBy(l => l.ChatType)
            .Select(g => new { Type = g.Key, Count = g.Sum(x => x.Count) })
            .ToList();

        // 歷史累計數據
        var totalUsers = await db.Set<BotUser>().CountAsync();
        var totalGroups = await db.Set<BotGroup>().CountAsync();
        var totalWine = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Wine);
        var totalPlastic = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Plastic);

        return Results.Ok(new
        {
            summary = new { totalVolume, commandUsage, buttonClicks },
            rankings = topCommands,
            interactions = topInteractions, // 🚀 傳給前端
            distribution = chatDist,
            global = new { totalUsers, totalGroups, totalWine, totalPlastic }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// 🚀 替換原本的 MapFallbackToFile，讓找不到路徑的請求也能根據網域 fallback
app.MapFallback(async (context) => {
    context.Response.ContentType = "text/html";
    string host = context.Request.Host.Host.ToLower();

    if (host.StartsWith("admin."))
        await context.Response.SendFileAsync("wwwroot/admin.html");
    else if (host.StartsWith("site.") || host.StartsWith("stats."))
        await context.Response.SendFileAsync("wwwroot/dashboard.html");
    else
        await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();


// =========================================================================
// 🏗️ ADDITIONAL CLASSES & RECORDS (Must be at the bottom of the file)
// =========================================================================

public static class BroadcastManager
{
    public static string State = "Idle";
    public static int Total = 0;
    public static int Processed = 0;
    public static int Success = 0;
    public static int Failed = 0;
    public static CancellationTokenSource? Cts;
}

public record BroadcastRequest(string Content, string Target);
public record BlacklistAddRequest(long TelegramId, string? Reason);
