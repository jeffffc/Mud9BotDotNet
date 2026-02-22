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
    }
}