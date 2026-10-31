using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Data;
using Microsoft.Extensions.Logging;
using ArrApp.UI.Errors;
using ArrApp.UI.Logging;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddArrApp(this IServiceCollection services, string? logDirectory = null) {
        services.AddLogging(builder => {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(logDirectory ?? AppPaths.LogDirectory));
        });

        // Persistence. IApiKeyProtector is supplied per platform (F-109); nothing resolves the repository until then.
        services.AddSingleton<IAppDatabase>(_ => new AppDatabase(Path.Combine(AppPaths.DataDirectory, "arr.db")));
        services.AddSingleton<IServerConnectionRepository, ServerConnectionRepository>();

        // One shared HttpClient; ArrHttpClient enforces its own per-request timeout.
        services.AddSingleton<IArrConnectionTester>(_ => new ArrConnectionTester(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }));

        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<IErrorReporter, ErrorReporter>();

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
