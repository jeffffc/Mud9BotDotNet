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
    public static void AddQuartzJobsFromAssembly(this IServiceCollectionQuartzConfigurator q, params Assembly[] assemblies)
    {
        // Use a temp logger for job registration
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("QuartzRegistration");
        
        var jobType = typeof(IJob);
        foreach (var assembly in assemblies)
        {
            var jobs = assembly.GetTypes()
                .Where(t => jobType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var job in jobs)
            {
                var attr = job.GetCustomAttribute<QuartzJobAttribute>();
                if (attr == null || attr.Inactive) continue;

                var jobKey = new JobKey(attr.Name, attr.Group);
                q.AddJob(job, jobKey, opts => opts.WithDescription(attr.Description));

                q.AddTrigger(opts =>
                {
                    opts = opts.ForJob(jobKey)
                        .WithIdentity($"{attr.Name}-trigger", attr.Group)
                        .WithDescription(attr.Description);

                    if (!string.IsNullOrEmpty(attr.CronInterval))
                        opts.WithCronSchedule(attr.CronInterval);
                    else
                        opts.WithSimpleSchedule(x => x.WithIntervalInSeconds(attr.IntervalSeconds).RepeatForever());
                });

                if (attr.RunOnStartup)
                {
                    q.AddTrigger(opts => opts.ForJob(jobKey)
                        .WithIdentity($"{attr.Name}-startup-trigger", attr.Group)
                        .StartNow());
                }

                var scheduleDisplay = string.IsNullOrEmpty(attr.CronInterval) ? $"{attr.IntervalSeconds}s" : attr.CronInterval;
                logger.LogInformation($"[+] Job Registered: {attr.Name} ({scheduleDisplay}) [{assembly.GetName().Name}]");
            }
        }
    }

    public static void AddBotServicesAndModules(this IServiceCollection services, params Assembly[] assemblies)
    {
        // Create a temporary logger for the startup phase
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("ServiceRegistration");

        // Register Metadata Service first so others can inject it
        var serviceProvider = services.BuildServiceProvider();
        
        var metadata = serviceProvider.GetService<IBotMetadataService>() as BotMetadataService ?? new BotMetadataService();
        
        if (serviceProvider.GetService<IBotMetadataService>() == null)
        {
            services.AddSingleton<IBotMetadataService>(metadata);
        }
        
        foreach (var assembly in assemblies)
        {
            logger.LogInformation($"--- Scanning Assembly: {assembly.GetName().Name} ---");
            var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();

            // 1. Stats Calculation (Additive)
            // 計返啲數，今次係累計落去，唔係 overwrite
            metadata.CommandCount += types.SelectMany(t => t.GetMethods())
                .Count(m => m.GetCustomAttribute<CommandAttribute>() is { Inactive: false });
            
            metadata.CallbackCount += types.SelectMany(t => t.GetMethods())
                .Count(m => m.GetCustomAttribute<CallbackQueryAttribute>() != null);
            
            metadata.MessageTriggerCount += types.SelectMany(t => t.GetMethods())
                .Count(m => m.GetCustomAttribute<TextTriggerAttribute>() is { Inactive: false });

            // 2. Registries (Singleton)
            foreach (var reg in types.Where(t => t.Name.EndsWith("Registry") && !t.Name.StartsWith("System")))
                services.AddSingleton(reg);
            
            // 3. Modules (Transient)
            foreach (var mod in types.Where(t => t.Name.EndsWith("Module") && !t.Name.StartsWith("System")))
                services.AddTransient(mod);

            // 4. Services (Scoped/Singleton)
            // Note: We also look for "Directory" patterns now to catch BusDirectory
            var serviceClasses = types.Where(t => (t.Name.EndsWith("Service") || t.Name.EndsWith("Directory")) 
                && !t.Name.StartsWith("System") 
                && t.Name != nameof(BotMetadataService));

            foreach (var implType in serviceClasses)
            {
                if (implType.Namespace?.StartsWith("Microsoft") == true || implType.Namespace?.StartsWith("System") == true) continue;

                var interfaceType = implType.GetInterfaces().FirstOrDefault(i => i.Name == $"I{implType.Name}");
                
                // Handle classes without interfaces (like BusDirectory)
                var registrationType = interfaceType ?? implType;

                bool needsScope = implType.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType.Name.Contains("DbContext")));

                if (needsScope)
                    services.AddScoped(registrationType, implType);
                else
                    services.AddSingleton(registrationType, implType);

                metadata.ServiceCount++;
                logger.LogInformation($"[+] Service ({ (needsScope ? "Scoped" : "Singleton") }): {registrationType.Name} [{assembly.GetName().Name}]");
            }

            // 5. Conversations
            var convs = types.Where(t => typeof(IConversation).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();
            foreach (var conv in convs)
            {
                services.AddSingleton(typeof(IConversation), conv);
                metadata.ConversationCount++;
                logger.LogInformation($"[+] Conversation: {conv.Name}");
            }

            // 6. Job Count for Metadata
            var jobType = typeof(IJob);
            metadata.JobCount += types.Count(t => jobType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.GetCustomAttribute<QuartzJobAttribute>() is { Inactive: false });
        }
        
        // 8. Register the Manager
        services.AddSingleton<ConversationManager>();
        
        logger.LogInformation("----------------------------");
    }
}