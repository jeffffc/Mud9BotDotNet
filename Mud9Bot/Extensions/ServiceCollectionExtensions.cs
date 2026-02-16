using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Attributes;
using Quartz;

namespace Mud9Bot.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddQuartzJobsFromAssembly(this IServiceCollectionQuartzConfigurator q, Assembly assembly)
    {
        var jobType = typeof(IJob);
        var jobs = assembly.GetTypes()
            .Where(t => jobType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var job in jobs)
        {
            var attr = job.GetCustomAttribute<QuartzJobAttribute>();
            if (attr == null || attr.Inactive) continue;

            var jobKey = new JobKey(attr.Name, attr.Group);

            q.AddJob(job, jobKey, opts => opts.WithDescription(attr.Description));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{attr.Name}-trigger", attr.Group)
                .WithDescription(attr.Description)
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(attr.IntervalSeconds)
                    .RepeatForever()));
        }
    }

    public static void AddBotServicesAndModules(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();

        // 1. Register Modules (Transient)
        // These are resolved by CommandRegistry for every command execution.
        var modules = types.Where(t => t.Name.EndsWith("Module") && !t.Name.StartsWith("System"));
        foreach (var module in modules)
        {
            services.AddTransient(module);
        }

        // 2. Register Services
        var serviceClasses = types.Where(t => t.Name.EndsWith("Service") && !t.Name.StartsWith("System"));
        
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
                }
                else
                {
                    services.AddSingleton(interfaceType, implType);
                }
            }
            // Note: If a service doesn't have an interface (like StartupNotificationService), 
            // it's usually a HostedService which is handled manually or via a different mechanism.
        }
    }
}