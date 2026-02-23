using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Mud9Bot;
using Mud9Bot.Data;
using Mud9Bot.Services;
using Mud9Bot.Modules; // Import your modules namespace
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Mud9Bot.Handlers;
using Mud9Bot.Logging;
using Mud9Bot.Interfaces;
using Mud9Bot.Registries;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// 1. Setup Systemd (allows running as a Linux service)
builder.Services.AddSystemd();

// 2. Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var botToken = builder.Configuration["BotConfiguration:BotToken"];

if (string.IsNullOrEmpty(botToken))
    throw new ArgumentNullException("BotToken is missing");

// 3. Register Database (PostgreSQL)
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseNpgsql(connectionString));

// 4. Register Telegram Client
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(botToken));

// 5. Register Services (The Brains)
builder.Services.AddSingleton<IErrorReporter, ErrorReporter>(); // <--- ADD THIS LINE

// 6. HTTP Service Registration
// Standard HttpClient for general use (e.g., GitHubService, GasService)
builder.Services.AddHttpClient(); 

// --- NEW: HTTP Service Registration ---
builder.Services.AddHttpClient("Mud9BotClient", client =>
{
    // Standard Browser User-Agent to avoid blocks
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(10); // 10s Timeout
});
builder.Services.AddSingleton<IHttpService, HttpService>();

// --- DYNAMIC REGISTRATION ---
builder.Services.AddBotServicesAndModules(Assembly.GetExecutingAssembly());
// ----------------------------

// 7. Register Handler & Worker
builder.Services.AddSingleton<IInlineQueryHandler, InlineQueryHandler>();
builder.Services.AddSingleton<IUpdateHandler, UpdateHandler>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<StartupNotificationService>();

// --- QUARTZ SCHEDULER SETUP ---
builder.Services.AddQuartz(q =>
{
    // Use the dynamic registration extension
    q.AddQuartzJobsFromAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});
// -----------------------------

var host = builder.Build();

// Auto-migrate Database on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.EnsureCreated();
    db.Database.Migrate();
}

host.Run();