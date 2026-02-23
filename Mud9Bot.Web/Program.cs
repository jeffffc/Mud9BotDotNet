using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

// 1. 資料庫連線
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

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

// 保留這條路由，以防有人直接打 /dashboard
app.MapGet("/dashboard", (context) => {
    // Force redirect to the subdomain
    context.Response.Redirect("https://stats.mud9bot.info", permanent: true);
    return Task.CompletedTask;
});

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

    if (host.StartsWith("site.") || host.StartsWith("stats."))
        await context.Response.SendFileAsync("wwwroot/dashboard.html");
    else
        await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();