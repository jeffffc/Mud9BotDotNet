using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Services;

namespace Mud9Bot.Transport.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Bus-related shared services.
    /// 加晒所有同巴士相關嘅共享服務。
    /// </summary>
    public static IServiceCollection AddBusSharedServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddScoped<IBusApiService, BusApiService>();
        services.AddScoped<IMtrApiService, MtrApiService>();
        services.AddScoped<BusDirectory>();
        
        // We can add the BusDirectory singleton here later
        return services;
    }
}