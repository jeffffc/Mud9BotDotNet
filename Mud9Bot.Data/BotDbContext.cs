using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mud9Bot.Data.Entities;

namespace Mud9Bot.Data;

public class BotDbContext : DbContext
{
    private readonly IConfiguration _configuration;

    public BotDbContext(DbContextOptions<BotDbContext> options, IConfiguration configuration) : base(options)
    {
        _configuration = configuration;
    }

    // NOTE: Because we are registering entities dynamically, explicit DbSet properties are optional.
    // Use _context.Set<EntityName>() to access tables in your services.
    // e.g. _context.Set<BotUser>().Add(...)
    
    // You can keep these if you prefer convenient property access, but they are not required for EF to work.
    // public DbSet<BotUser> Users { get; set; }
    // public DbSet<BotGroup> Groups { get; set; }
    // public DbSet<CommandLog> CommandLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseNpgsql(connectionString);
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Automatic Entity Registration ---
        // Dynamically find all classes in the "Mud9Bot.Data.Entities" namespace
        var entityTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass 
                        && !t.IsAbstract 
                        && t.Namespace == "Mud9Bot.Data.Entities");

        foreach (var type in entityTypes)
        {
            modelBuilder.Entity(type);
        }

        // --- Configuration ---
        // You can still apply specific configurations here if needed
        modelBuilder.Entity<BotUser>()
            .HasIndex(u => u.TelegramId)
            .IsUnique();
            
        modelBuilder.Entity<BotGroup>()
            .HasIndex(g => g.TelegramId)
            .IsUnique();
        
        // 🚀 FIX: Use a FIXED date for seed data. 
        // Using DateTime.UtcNow in OnModelCreating causes EF Core 9+ to detect 
        // a "model change" every single time the app starts, causing a crash.
        var seedDate = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc);
        
        
        // Seed initial data for the Admin Console
        // Seed initial data for the Admin Console and Global Settings
        modelBuilder.Entity<SystemSetting>().HasData(
            new SystemSetting 
            { 
                SettingKey = "is_maintenance", 
                SettingValue = "false", 
                Description = "Toggle global maintenance mode",
                LastUpdated = seedDate
            },
            new SystemSetting 
            { 
                SettingKey = "maintenance_message", 
                SettingValue = "🛠 系統正在維護中，請稍後再試。 / System is under maintenance. Please try again later.", 
                Description = "Message shown to users during maintenance",
                LastUpdated = seedDate
            },
            new SystemSetting 
            { 
                SettingKey = "broadcast_delay_ms", 
                SettingValue = "35", 
                Description = "Delay between messages during global broadcast (ms)",
                LastUpdated = DateTime.UtcNow 
            },
            new SystemSetting 
            { 
                SettingKey = "web_banner_message", 
                SettingValue = "", 
                Description = "Site-wide announcement message for the web dashboard",
                LastUpdated = seedDate
            },
            new SystemSetting 
            { 
                SettingKey = "enable_gas", 
                SettingValue = "true", 
                Description = "Feature flag: Enable gas price service",
                LastUpdated = seedDate
            },
            new SystemSetting 
            { 
                SettingKey = "enable_zodiac", 
                SettingValue = "true", 
                Description = "Feature flag: Enable daily zodiac horoscopes",
                LastUpdated = seedDate
            },
            new SystemSetting 
            { 
                SettingKey = "enable_wineplastic", 
                SettingValue = "true", 
                Description = "Feature flag: Enable core wine/plastic interactions",
                LastUpdated = seedDate
            }
        );
        
    }
}