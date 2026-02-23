using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

// 1. 設定資料庫連接 (重複使用 BotDbContext)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. 啟用 CORS
builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// 3. 關鍵修正：處理反向代理（如 NPM）傳遞過來的 Header
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowAll");

// 🚀 關鍵修正：允許伺服器尋找預設檔案 (如 index.html)
app.UseDefaultFiles(); 
app.UseStaticFiles(); 

// 4. 強化後的統計 API 端點
app.MapGet("/api/stats", async (BotDbContext db) =>
{
    try 
    {
        // 獲取彙總統計 (bot_event_logs)
        var logs = await db.Set<BotEventLog>().ToListAsync();

        var totalVolume = logs.Where(l => l.Metadata == "total_volume").Sum(l => l.Count);
        var commandUsage = logs.Where(l => l.EventType == "command").Sum(l => l.Count);
        var buttonClicks = logs.Where(l => l.EventType == "interaction" && l.Metadata.StartsWith("button_")).Sum(l => l.Count);

        var topCommands = logs.Where(l => l.EventType == "command")
            .GroupBy(l => l.Metadata)
            .Select(g => new { Command = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(10);

        var chatDist = logs.GroupBy(l => l.ChatType)
            .Select(g => new { Type = g.Key, Count = g.Sum(x => x.Count) })
            .ToList();

        // 獲取實時全局數據 (從主資料表)
        var totalUsers = await db.Set<BotUser>().CountAsync();
        var totalGroups = await db.Set<BotGroup>().CountAsync();
        var totalWine = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Wine);
        var totalPlastic = await db.Set<WinePlastic>().Where(x => x.Disabled == 0).SumAsync(x => (long)x.Plastic);

        return Results.Ok(new
        {
            summary = new { totalVolume, commandUsage, buttonClicks },
            rankings = topCommands,
            distribution = chatDist,
            global = new { totalUsers, totalGroups, totalWine, totalPlastic }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// 🚀 關鍵修正：如果路徑不匹配 API，一律回傳 index.html (SPA 模式)
app.MapFallbackToFile("index.html");

app.Run();