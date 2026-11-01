using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Core.Images;
using ArrApp.UI.Images;
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

        // Persistence. API keys are encrypted by a platform-specific protector (F-109) that the head registers in PlatformServices.
        services.AddSingleton(_ => PlatformServices.ApiKeyProtector
            ?? throw new InvalidOperationException("No IApiKeyProtector registered. The platform head must set PlatformServices.ApiKeyProtector at startup."));
        services.AddSingleton<IAppDatabase>(_ => new AppDatabase(Path.Combine(AppPaths.DataDirectory, "arr.db")));
        services.AddSingleton<IServerConnectionRepository, ServerConnectionRepository>();
        services.AddSingleton<ISettingsStore, SettingsStore>();

        // Shared HttpClient for the Arr servers; ArrHttpClient enforces its own per-request timeout.
        // Redirects are NOT followed for the user's own server: HttpClient resends custom headers (X-Api-Key) to the
        // redirect target, so an SSO redirect to another domain would receive the key.
        services.AddSingleton(_ => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan });

        // Artwork: disk cache, then the server (with the key), then the public URL (via a separate client, no key).
        services.AddSingleton<IImageCache>(_ => new DiskImageCache(AppPaths.ImageCacheDirectory));
        services.AddSingleton<IImageLoader>(sp => new ImageLoader(
            sp.GetRequiredService<HttpClient>(), new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, sp.GetRequiredService<IImageCache>()));
        services.AddSingleton<BitmapService>();
        services.AddSingleton<IArrConnectionTester, ArrConnectionTester>();
        services.AddSingleton<IArrClientFactory, ArrClientFactory>();

        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<IErrorReporter, ErrorReporter>();

        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<BottomSheetService>();
        services.AddSingleton<IBottomSheetService>(sp => sp.GetRequiredService<BottomSheetService>());

        services.AddTransient<PlaceholderViewModel>();
        services.AddTransient<ServerEditViewModel>();
        services.AddTransient<ServersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConfirmViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddSingleton<ArrContext>();
        services.AddSingleton<ServerSwitcherViewModel>();
        services.AddTransient<ServerPickerViewModel>();
        services.AddSingleton(sp => new TabHostViewModel([
            new TabItem("Library", Placeholder(sp, "Library")),
            new TabItem("Discover", Placeholder(sp, "Discover")),
            new TabItem("Activity", Placeholder(sp, "Activity")),
            new TabItem("Settings", sp.GetRequiredService<SettingsViewModel>()),
        ], sp.GetRequiredService<ServerSwitcherViewModel>()));
        services.AddSingleton<ShellViewModel>();
        return services;
    }

    private static PlaceholderViewModel Placeholder(IServiceProvider sp, string title) {
        var vm = sp.GetRequiredService<PlaceholderViewModel>();
        vm.Title = title;
        return vm;
    }
}
