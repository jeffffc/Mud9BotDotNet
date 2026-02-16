using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    // Tables
    public DbSet<BotUser> Users { get; set; }
    public DbSet<BotGroup> Groups { get; set; }
    public DbSet<CommandLog> CommandLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Define unique indexes, default values, etc.
        modelBuilder.Entity<BotUser>()
            .HasIndex(u => u.TelegramId)
            .IsUnique();
            
        modelBuilder.Entity<BotGroup>()
            .HasIndex(g => g.TelegramId)
            .IsUnique();
    }
}

[Table("users")]
public class BotUser
{
    [Key]
    public int Id { get; set; } // Internal DB ID

    public long TelegramId { get; set; } // Telegram User ID
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Username { get; set; }
    
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

[Table("groups")]
public class BotGroup
{
    [Key]
    public int Id { get; set; }

    public long TelegramId { get; set; }
    public string Title { get; set; } = string.Empty;
    
    // Example: Migration of your "wquota/pquota" logic
    public int WQuota { get; set; } = 5;
    public int PQuota { get; set; } = 5;
}

[Table("command_logs")]
public class CommandLog
{
    [Key]
    public int Id { get; set; }

    public long UserId { get; set; }
    public long ChatId { get; set; } // 0 if private
    public string Command { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}