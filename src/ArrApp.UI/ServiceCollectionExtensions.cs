using System;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddArrApp(this IServiceCollection services) {
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<BottomSheetService>();
        services.AddSingleton<IBottomSheetService>(sp => sp.GetRequiredService<BottomSheetService>());

        services.AddTransient<PlaceholderViewModel>();
        services.AddSingleton(sp => new TabHostViewModel([
            new TabItem("Library", Placeholder(sp, "Library")),
            new TabItem("Discover", Placeholder(sp, "Discover")),
            new TabItem("Activity", Placeholder(sp, "Activity")),
            new TabItem("Settings", Placeholder(sp, "Settings")),
        ]));
        services.AddSingleton<ShellViewModel>();
        return services;
    }

    private static PlaceholderViewModel Placeholder(IServiceProvider sp, string title) {
        var vm = sp.GetRequiredService<PlaceholderViewModel>();
        vm.Title = title;
        return vm;
    }
}
