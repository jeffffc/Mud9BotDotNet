using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Modules.Conversations;
using Mud9Bot.Services;
using Quartz;

namespace Mud9Bot.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddQuartzJobsFromAssembly(this IServiceCollectionQuartzConfigurator q, Assembly assembly)
    {
        // Use a temp logger for job registration
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("QuartzRegistration");
        
        var jobType = typeof(IJob);
        var jobs = assembly.GetTypes()
            .Where(t => jobType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var job in jobs)
        {
            var attr = job.GetCustomAttribute<QuartzJobAttribute>();
            if (attr == null || attr.Inactive) continue;

            var jobKey = new JobKey(attr.Name, attr.Group);

            q.AddJob(job, jobKey, opts => opts.WithDescription(attr.Description));

            // FIX: Check for CronExpression before defaulting to SimpleSchedule
            q.AddTrigger(opts =>
            {
                opts = opts
                    .ForJob(jobKey)
                    .WithIdentity($"{attr.Name}-trigger", attr.Group)
                    .WithDescription(attr.Description);

                if (!string.IsNullOrEmpty(attr.CronInterval))
                {
                    // Use Cron Schedule (e.g. "0 0 0 * * ?")
                    opts.WithCronSchedule(attr.CronInterval);
                }
                else
                {
                    // Use Interval Schedule (e.g. Every 60 seconds)
                    opts.WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(attr.IntervalSeconds)
                        .RepeatForever());
                }
            });
            
            // NEW: Support for modular "Run On Startup" jobs
            var runOnStartupProp = attr.GetType().GetProperty("RunOnStartup");
            bool runsOnStartup = runOnStartupProp != null && runOnStartupProp.GetValue(attr) is true;
            
            if (runsOnStartup)
            {
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity($"{attr.Name}-startup-trigger", attr.Group)
                    .StartNow()); // This trigger fires immediately and only once
            }
            
            var scheduleDisplay = string.IsNullOrEmpty(attr.CronInterval) ? $"{attr.IntervalSeconds}s" : attr.CronInterval;
            logger.LogInformation($"[+] Job Registered: {attr.Name} ({scheduleDisplay})");
        }
    }

    public static void AddBotServicesAndModules(this IServiceCollection services, Assembly assembly)
    {
        // Create a temporary logger for the startup phase
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("ServiceRegistration");

        // Register Metadata Service first so others can inject it
        var metadata = new BotMetadataService();
        services.AddSingleton<IBotMetadataService>(metadata);
        
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();

        // 2. Eagerly count Commands and Callbacks via Reflection (PRE-RESOLVE STATS)
        // Using robust null checks instead of pattern matching to ensure it doesn't fail silently
        metadata.CommandCount = types.SelectMany(t => t.GetMethods())
            .Count(m => {
                var attr = m.GetCustomAttribute<CommandAttribute>();
                return attr != null && !attr.Inactive;
            });
        
        metadata.CallbackCount = types.SelectMany(t => t.GetMethods())
            .Count(m => m.GetCustomAttribute<CallbackQueryAttribute>() != null);
        
        // 3. Register Registries (Singleton)
        var registries = types.Where(t => t.Name.EndsWith("Registry") && !t.Name.StartsWith("System"));
        foreach (var registry in registries)
        {
            services.AddSingleton(registry);
        }
        
        // 4. Register Modules (Transient)
        // These are resolved by CommandRegistry and CallbackQueryRegistry for every command execution.
        var modules = types.Where(t => t.Name.EndsWith("Module") && !t.Name.StartsWith("System"));
        foreach (var module in modules)
        {
            services.AddTransient(module);
        }

        // 5. Register Services
        logger.LogInformation("--- Registering Services ---");
        
        // FIX: Exclude BotMetadataService so it doesn't overwrite our pre-filled singleton instance!
        var serviceClasses = types.Where(t => t.Name.EndsWith("Service") 
            && !t.Name.StartsWith("System") 
            && t.Name != nameof(BotMetadataService));
            
        int sCount = 0;
        
        foreach (var implType in serviceClasses)
        {
            // Skip services that are likely part of the framework or generated code
            if (implType.Namespace?.StartsWith("Microsoft") == true || implType.Namespace?.StartsWith("System") == true) 
                continue;

            // Find matching interface: ITrafficService for TrafficService
            var interfaceType = implType.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{implType.Name}");

            if (interfaceType != null)
            {
                // Heuristic: If it takes a DbContext, it MUST be scoped.
                bool needsScope = implType.GetConstructors()
                    .Any(c => c.GetParameters().Any(p => p.ParameterType.Name.Contains("DbContext")));

                if (needsScope)
                {
                    services.AddScoped(interfaceType, implType);
                    logger.LogInformation($"[+] Service (Scoped): {interfaceType.Name} -> {implType.Name}");
                }
                else
                {
                    services.AddSingleton(interfaceType, implType);
                    logger.LogInformation($"[+] Service (Singleton): {interfaceType.Name} -> {implType.Name}");
                }
                sCount++;
            }
            // Note: If a service doesn't have an interface (like StartupNotificationService), 
            // it's usually a HostedService which is handled manually or via a different mechanism.
            
        }
        metadata.ServiceCount = sCount;
        logger.LogInformation($"Registered {sCount} Services.");
        
        // 6. Register Conversations (NEW)
        logger.LogInformation("--- Registering Conversations ---");
        
        // Find all classes that implement IConversation (Converted to List for accurate counting)
        var conversationTypes = types.Where(t => typeof(IConversation).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();

        foreach (var convType in conversationTypes)
        {
            // Register as 'IConversation' so we can inject IEnumerable<IConversation> later
            services.AddSingleton(typeof(IConversation), convType);
            logger.LogInformation($"[+] Conversation: {convType.Name}");
        }
        
        metadata.ConversationCount = conversationTypes.Count;
        logger.LogInformation($"Registered {metadata.ConversationCount} Conversations.");

        // 7. Capture Job Count for Metadata
        // Since Quartz is registered separately, we scan the assembly here to get the count for the notification
        var jobType = typeof(IJob);
        metadata.JobCount = types.Count(t => {
            if (!jobType.IsAssignableFrom(t) || t.IsInterface || t.IsAbstract) return false;
            var attr = t.GetCustomAttribute<QuartzJobAttribute>();
            return attr != null && !attr.Inactive;
        });
        
        // 8. Register the Manager
        services.AddSingleton<ConversationManager>();
        
        logger.LogInformation("----------------------------");
    }
}