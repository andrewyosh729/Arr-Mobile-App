using Microsoft.Extensions.DependencyInjection;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddArrApp(this IServiceCollection services) {
        services.AddTransient<MainViewModel>();
        return services;
    }
}
