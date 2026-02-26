using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Data.Interfaces;
using Mud9Bot.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Mud9Bot.Bus.Services; // Ensure this is at the top
using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Bus.Extensions;
using Mud9Bot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. SERVICES REGISTRATION / 服務註冊
// =========================================================================

// Database Connection / 資料庫連線
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- JWT CONFIGURATION / JWT 安全設定 ---
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "A_very_long_random_secret_key_at_least_32_chars";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x => {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(x => {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

// Core Logic Services (Shared with Bot project) / 核心邏輯服務 (與 Bot 專案共用)
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IBlacklistService, BlacklistService>();
builder.Services.AddBusSharedServices();

builder.Services.AddControllers();

builder.Services.AddSingleton<TelegramAuthService>();

// Utility Services / 工具類服務
builder.Services.AddHttpClient(); // Required for Telegram Broadcast / 廣播功能必要組件
builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => 
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// =========================================================================
// 2. STARTUP INITIALIZATION / 啟動初始化
// =========================================================================

// Prime the RAM caches from the Database / 從資料庫預載入記憶體快取
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<ISettingsService>().InitializeAsync();
    await services.GetRequiredService<IBlacklistService>().InitializeAsync();
}

using (var scope = app.Services.CreateScope())
{
    // Correct: Get the service from the Provider (scope.ServiceProvider)
    var busDirectory = scope.ServiceProvider.GetRequiredService<BusDirectory>();
    // This loads the data from DB into Memory
    await busDirectory.InitializeAsync();
}

app.UseAuthentication();
app.UseAuthorization();

// =========================================================================
// 3. MIDDLEWARE PIPELINE / 中介軟體管線設定
// =========================================================================

// Handle Proxy Headers (NPM/Nginx) / 處理代理標頭，確保能讀取真實網域
app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                       ForwardedHeaders.XForwardedProto | 
                       ForwardedHeaders.XForwardedHost
});

app.UseCors("AllowAll");

// API Security: Block direct browser navigation to JSON endpoints
// 安全防護：攔截直接在網址列訪問 API 終點的行為
// API Security & Subdomain Redirection
app.Use(async (context, next) =>
{
    string path = context.Request.Path.Value?.ToLower() ?? "";
    string host = context.Request.Host.Host.ToLower();

    // 1. API Security: Block direct browser access to JSON
    if (path.StartsWith("/api"))
    {
        var fetchMode = context.Request.Headers["Sec-Fetch-Mode"].ToString();
        if (string.Equals(fetchMode, "navigate", StringComparison.OrdinalIgnoreCase) && !app.Environment.IsDevelopment())
        {
            context.Response.Redirect("/");
            return; 
        }
    }

    // 2. Subdomain Redirection (Bypass for localhost/api)
    if (host != "localhost" && !path.StartsWith("/api") && !app.Environment.IsDevelopment())
    {
        string? targetSub = path switch {
            "/admin" or "/admin.html" => "admin",
            "/stats" or "/dashboard" or "/dashboard.html" => "stats",
            "/bus" or "/bus.html" => "bus",
            _ => null
        };

        if (targetSub != null && !host.StartsWith($"{targetSub}."))
        {
            string baseDomain = host.Replace("stats.", "").Replace("admin.", "").Replace("bus.", "");
            context.Response.Redirect($"{context.Request.Scheme}://{targetSub}.{baseDomain}/", false);
            return;
        }
    }
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseDefaultFiles(); 
app.UseStaticFiles(); 

app.MapControllers();

// 🚀 核心統一路由邏輯 (Root & Fallback)
var serveHtmlDelegate = async (HttpContext context) => {
    
    string host = context.Request.Host.Host.ToLower();
    string path = context.Request.Path.Value?.ToLower() ?? "";

    // IMPORTANT: If this is an API call that reached the fallback, it's a 404, NOT an HTML file.
    // 防止 API 呼叫失敗時回傳 HTML 內容，導致前端報錯。
    if (path.StartsWith("/api"))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { error = "API Endpoint Not Found" });
        return;
    }
    
    context.Response.ContentType = "text/html";
    
    if (host.StartsWith("admin.") || path.StartsWith("/admin"))
    {
        await context.Response.SendFileAsync("wwwroot/admin.html");
    }
    else if (host.StartsWith("stats.") || host.StartsWith("site.") || path.StartsWith("/stats") || path.StartsWith("/dashboard"))
    {
        await context.Response.SendFileAsync("wwwroot/dashboard.html");
    }
    else if (host.StartsWith("bus.") || path.StartsWith("/bus"))
    {
        await context.Response.SendFileAsync("wwwroot/bus.html");
    }
    else
    {
        await context.Response.SendFileAsync("wwwroot/index.html");
    }
};

app.MapGet("/", serveHtmlDelegate);
app.MapFallback(serveHtmlDelegate);

// ---------------------------------------------------------
// 🔐 ADMIN AUTH & SETTINGS API / 管理員驗證與設定
// ---------------------------------------------------------
app.MapPost("/api/admin/auth", async (HttpContext context, IConfiguration config) =>
{
    var form = await context.Request.ReadFormAsync();
    var botToken = config["BotConfiguration:BotToken"] ?? "";
    
    var devIds = config.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? [];
    if (!devIds.Any() && config["BotConfiguration:DevIds"] is string devStr)
    {
        devIds = new HashSet<long>(devStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse));
    }

    var authData = form.ToDictionary(x => x.Key, x => x.Value.ToString());
    if (!authData.ContainsKey("hash")) return Results.BadRequest("Missing hash");

    var hash = authData["hash"];
    authData.Remove("hash");
    var dataCheckString = string.Join("\n", authData.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));

    using var sha256 = SHA256.Create();
    var secretKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(botToken));
    using var hmac = new HMACSHA256(secretKey);
    var checkHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).Replace("-", "").ToLower();

    bool isValid =  checkHash == hash;
    var userId = long.Parse(authData["id"]);
    
    if (isValid && devIds.Contains(userId)) {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] { 
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, authData["first_name"])
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { success = true, token = tokenHandler.WriteToken(token), user = authData["first_name"]});
    }
    return Results.Unauthorized();
    
});

app.MapGet("/api/admin/settings", async (BotDbContext db) =>
{
    var settings = await db.Set<SystemSetting>().ToListAsync();
    return Results.Ok(settings);
}).RequireAuthorization();

app.MapPost("/api/admin/maintenance", async (bool enable, ISettingsService settings, BotDbContext db, HttpContext context) =>
{
    // 🚀 Audit Logging Logic
    var adminId = long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    var adminName = context.User.Identity?.Name ?? "Unknown";
    var ip = context.Connection.RemoteIpAddress?.ToString();
    
    var val = enable ? "true" : "false";
    
    var entity = await db.Set<SystemSetting>().FindAsync("is_maintenance");
    if (entity != null) {
        entity.SettingValue = val;
        
        // Log the action
        db.Set<AdminAuditLog>().Add(new AdminAuditLog {
            AdminId = adminId,
            AdminName = adminName,
            Action = "TOGGLE_MAINTENANCE",
            Details = $"Set to: {val}",
            IpAddress = ip
        });

        await db.SaveChangesAsync();
        settings.RefreshSetting("is_maintenance", val);
        return Results.Ok(new { status = val });
    }
    return Results.NotFound();
}).RequireAuthorization();

// ---------------------------------------------------------
// 🔍 INSPECTOR (Power Actions)
// ---------------------------------------------------------
app.MapGet("/api/admin/users/search", async (string query, BotDbContext db) =>
{
    var q = query.ToLower();
    Results.Ok(await db.Set<BotUser>().Where(u =>
            u.TelegramId.ToString().Contains(q) || (u.FirstName + " " + (u.LastName ?? "")).Contains(q) ||
            (u.Username ?? "").Contains(q))
        .OrderByDescending(u => u.TimeAdded)
        .Take(50).ToListAsync()
    );
}).RequireAuthorization();

app.MapGet("/api/admin/groups/search", async (string query, BotDbContext db) =>
{
    var q = query.ToLower();
    Results.Ok(await db.Set<BotGroup>().Where(g =>
            g.TelegramId.ToString().Contains(q) || g.Title.Contains(q) || (g.Username ?? "").Contains(q))
        .OrderByDescending(g => g.TimeAdded)
        .Take(50).ToListAsync()
    );
}).RequireAuthorization();

// 🚀 NEW: Reset user wine/plastic quota
app.MapPost("/api/admin/users/reset-quota", async (long userId, BotDbContext db) => {
    var limits = await db.Set<DailyLimit>().Where(d => d.UserId == (int)userId).ToListAsync();
    foreach(var l in limits) { l.WineLimit = 5; l.PlasticLimit = 5; }
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// ---------------------------------------------------------
// 📢 BROADCAST API / 全域廣播系統
// ---------------------------------------------------------
app.MapGet("/api/admin/broadcast/status", () => Results.Ok(new {
    state = BroadcastManager.State, total = BroadcastManager.Total, processed = BroadcastManager.Processed, success = BroadcastManager.Success, failed = BroadcastManager.Failed
})).RequireAuthorization();

app.MapPost("/api/admin/broadcast/cancel", () => {
    BroadcastManager.Cts?.Cancel();
    BroadcastManager.State = "Cancelled";
    return Results.Ok();
}).RequireAuthorization();

app.MapPost("/api/admin/broadcast/start", async (
    [FromBody] BroadcastRequest req, 
    [FromServices] BotDbContext db, 
    [FromServices] IConfiguration config, 
    [FromServices] ISettingsService settings) =>
{
    Console.WriteLine($"[Broadcast] Request received: Target={req.Target}");

    if (BroadcastManager.State == "Running") return Results.Conflict("A broadcast is already running.");

    // 1. Resolve Target IDs
    List<long> targetIds = new();
    try 
    {
        if (req.Target == "users") 
        {
            targetIds = await db.Set<BotUser>().Select(u => u.TelegramId).ToListAsync();
        }
        else if (req.Target == "groups") 
        {
            targetIds = await db.Set<BotGroup>().Select(g => g.TelegramId).ToListAsync();
        }
        else if (req.Target == "devs") 
        {
            // 🚀 Improved: Try getting as array first, then fallback to comma-string
            var devList = config.GetSection("BotConfiguration:DevIds").Get<List<long>>();
            if (devList != null && devList.Any())
            {
                targetIds = devList;
            }
            else 
            {
                var devStr = config["BotConfiguration:DevIds"] ?? "";
                targetIds = devStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToList();
            }
        }
    }
    catch (Exception ex) 
    {
        Console.WriteLine($"[Broadcast] Target Resolution Error: {ex.Message}");
        return Results.Problem("Database target resolution failed.");
    }

    if (!targetIds.Any()) 
    {
        Console.WriteLine("[Broadcast] No targets found for " + req.Target);
        return Results.BadRequest("Target list is empty.");
    }

    // 2. Initialize Manager State
    BroadcastManager.State = "Running";
    BroadcastManager.Total = targetIds.Count;
    BroadcastManager.Processed = 0; 
    BroadcastManager.Success = 0; 
    BroadcastManager.Failed = 0;
    BroadcastManager.Cts = new CancellationTokenSource();

    var token = BroadcastManager.Cts.Token;
    var botToken = config["BotConfiguration:BotToken"] ?? "";
    var delayMs = int.Parse(settings.GetSetting("broadcast_delay_ms", "35"));

    if (string.IsNullOrEmpty(botToken)) 
    {
        Console.WriteLine("[Broadcast] FAILED: BotToken is null in config.");
        BroadcastManager.State = "Error: Missing Token";
        return Results.Problem("Bot Token is missing in configuration.");
    }

    Console.WriteLine($"[Broadcast] Starting background task for {targetIds.Count} items...");

    // 3. Fire and Forget Background Task
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
                else 
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Broadcast] Item Failed ({id}): {response.StatusCode} - {errBody}");
                    BroadcastManager.Failed++;
                }
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"[Broadcast] Loop Exception: {ex.Message}");
                BroadcastManager.Failed++; 
            }

            BroadcastManager.Processed++;
            await Task.Delay(delayMs); 
        }

        BroadcastManager.State = BroadcastManager.Cts.IsCancellationRequested ? "Cancelled" : "Completed";
        Console.WriteLine($"[Broadcast] Finished. Final State: {BroadcastManager.State}");
    });

    return Results.Accepted();
}).RequireAuthorization();

// ---------------------------------------------------------
// 🚫 BLACKLIST API / 黑名單管理
// ---------------------------------------------------------
app.MapGet("/api/admin/blacklist", async (BotDbContext db) => 
    Results.Ok(await db.Set<BlacklistedId>().OrderByDescending(b => b.TimeAdded).ToListAsync())).RequireAuthorization();

app.MapPost("/api/admin/blacklist/add", async (BlacklistAddRequest req, IBlacklistService blacklist) =>
{
    await blacklist.AddAsync(req.TelegramId, req.Reason ?? "No reason", 0);
    return Results.Ok();
}).RequireAuthorization();

app.MapPost("/api/admin/blacklist/remove", async (long id, IBlacklistService blacklist) =>
{
    await blacklist.RemoveAsync(id);
    return Results.Ok();
}).RequireAuthorization();

// ---------------------------------------------------------
// 📨 FEEDBACK INBOX API / 意見回饋信箱
// ---------------------------------------------------------
app.MapGet("/api/admin/feedback/list", async (BotDbContext db) =>
{
    var list = await db.Set<UserFeedback>().OrderByDescending(f => f.SubmittedAt).Take(100).ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPost("/api/admin/feedback/resolve", async (int id, BotDbContext db) =>
{
    var fb = await db.Set<UserFeedback>().FindAsync(id);
    if (fb != null) { 
        fb.IsResolved = true; 
        await db.SaveChangesAsync(); 
        return Results.Ok(); 
    }
    return Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/admin/feedback/reply", async (FeedbackReplyRequest req, BotDbContext db, IConfiguration config) =>
{
    var fb = await db.Set<UserFeedback>().FindAsync(req.Id);
    if (fb == null) return Results.NotFound();

    var botToken = config["BotConfiguration:BotToken"] ?? "";
    using var client = new HttpClient();
    var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
    
    // Format the reply message beautifully
    var msg = $"👨‍💻 <b>開發者回覆 (Developer Reply)</b>\n\n{req.ReplyMessage}\n\n<blockquote expandable><i>你原本的訊息：\n{fb.Content}</i></blockquote>";
    
    var response = await client.PostAsJsonAsync(url, new { chat_id = fb.TelegramId, text = msg, parse_mode = "HTML" });
    
    if (response.IsSuccessStatusCode) {
        fb.IsResolved = true;
        fb.AdminReply = req.ReplyMessage;
        await db.SaveChangesAsync();
        return Results.Ok();
    }
    return Results.BadRequest("Failed to send message via Telegram API.");
}).RequireAuthorization();

// ---------------------------------------------------------
// 📜 Audit & Live Logs API / 稽核與即時日誌
// ---------------------------------------------------------

app.MapGet("/api/admin/logs/activity", async (BotDbContext db) =>
{
    // Fetch the 100 most recent command executions
    // Joined with Users to show names if available
    var logs = await db.Set<CommandLog>()
        .OrderByDescending(l => l.Timestamp)
        .Take(100)
        .ToListAsync();
        
    return Results.Ok(logs);
}).RequireAuthorization();

// Optional: Aggregated Audit Log (from your existing bot_event_logs)
app.MapGet("/api/admin/logs/audit", async (BotDbContext db) =>
{
    var stats = await db.Set<BotEventLog>()
        .OrderByDescending(s => s.Count)
        .ToListAsync();
    return Results.Ok(stats);
}).RequireAuthorization();

// ---------------------------------------------------------
// 🐧 System Journal Logs API (journalctl)
// ---------------------------------------------------------

app.MapGet("/api/admin/logs/system", async (IConfiguration config) =>
{
    var serviceName = config["SystemLogs:ServiceName"] ?? "mud9bot.service";
    var lines = config["SystemLogs:LinesToFetch"] ?? "50";

    // 🚀 Check if we are running on Linux
    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "journalctl",
                Arguments = $"-u {serviceName} -n {lines} --no-pager",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return Results.Problem("Failed to start journalctl process.");

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Return as lines
            return Results.Ok(output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception ex)
        {
            return Results.Problem($"Linux shell error: {ex.Message}");
        }
    }

    // 🖥️ Localhost/Windows Fallback (Mock Data)
    return Results.Ok(new[] {
        $"[{DateTime.Now:HH:mm:ss}] (MOCK) systemd[1]: Starting Mud9Bot Service...",
        $"[{DateTime.Now:HH:mm:ss}] (MOCK) dotnet[1234]: info: Mud9Bot.Startup[0] Application Starting",
        $"[{DateTime.Now:HH:mm:ss}] (MOCK) dotnet[1234]: info: Microsoft.Hosting.Lifetime[14] Now listening on: http://localhost:5000",
        $"[{DateTime.Now:HH:mm:ss}] (MOCK) journalctl is only available on Linux production hosts."
    });
}).RequireAuthorization();

// ---------------------------------------------------------
// 📊 PUBLIC STATS API / 公開統計數據
// ---------------------------------------------------------
app.MapGet("/api/stats", async (BotDbContext db) =>
{
    try 
    {
        var logs = await db.Set<BotEventLog>().ToListAsync();

        var totalVolume = logs.Where(l => l.EventType == "system" && l.Metadata == "total_volume").Sum(l => l.Count);
        var commandUsage = logs.Where(l => l.EventType == "command").Sum(l => l.Count);
        var buttonClicks = logs.Where(l => l.EventType == "interaction").Sum(l => l.Count);

        var topCommands = logs.Where(l => l.EventType == "command")
            .GroupBy(l => l.Metadata)
            .Select(g => new { Command = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).Take(10);

        var topInteractions = logs.Where(l => l.EventType == "interaction")
            .GroupBy(l => l.Metadata)
            .Select(g => new { Function = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).Take(10);

        var chatDist = logs.Where(l => l.EventType == "system")
            .GroupBy(l => l.ChatType)
            .Select(g => new { Type = g.Key, Count = g.Sum(x => x.Count) }).ToList();

        var totalUsers = await db.Set<BotUser>().CountAsync();
        var totalGroups = await db.Set<BotGroup>().CountAsync();
        var totalWine = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Wine);
        var totalPlastic = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Plastic);

        return Results.Ok(new {
            summary = new { totalVolume, commandUsage, buttonClicks },
            rankings = topCommands, interactions = topInteractions, distribution = chatDist,
            global = new { totalUsers, totalGroups, totalWine, totalPlastic }
        });
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

// 💰 Donation Tracker API
app.MapGet("/api/admin/donations/summary", async (BotDbContext db) =>
{
    var totalStars = await db.Set<Donation>().SumAsync(d => (long)d.Stars);
    var totalLegacyHkd = await db.Set<Donation>().SumAsync(d => (long)d.Amount);
    var totalStarsApproxHkd = totalStars * 0.16;
    var totalCombinedHkd = totalLegacyHkd + totalStarsApproxHkd;
    var last30Days = await db.Set<Donation>().Where(d => d.Time > DateTime.UtcNow.AddDays(-30)).CountAsync();

    return Results.Ok(new { totalStars, totalLegacyHkd, totalStarsApproxHkd, totalCombinedHkd, recentCount = last30Days });
});

app.MapGet("/api/admin/donations/list", async (BotDbContext db) =>
{
    var list = await db.Set<Donation>().OrderByDescending(d => d.Time).Take(500).ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

app.Run();

// =========================================================================
// 🏗️ ADDITIONAL CLASSES & RECORDS (Must be at the bottom of the file)
// =========================================================================

public static class BroadcastManager
{
    public static string State = "Idle";
    public static int Total = 0, Processed = 0, Success = 0, Failed = 0;
    public static CancellationTokenSource? Cts;
}

public record BroadcastRequest(string Content, string Target);
public record BlacklistAddRequest(long TelegramId, string? Reason);
public record FeedbackReplyRequest(int Id, string ReplyMessage); // 🚀 New Record