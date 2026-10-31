using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

internal sealed class FakeTester(Func<ArrType, string, string, ConnectionTestResult> respond) : IArrConnectionTester {
    public List<(ArrType Type, string Url, string Key)> Calls { get; } = [];

    public Task<ConnectionTestResult> TestAsync(ArrType expected, string baseUrl, string apiKey, CancellationToken cancellationToken = default) {
        Calls.Add((expected, baseUrl, apiKey));
        return Task.FromResult(respond(expected, baseUrl, apiKey));
    }
}

/// <summary>In-memory repository with the same ordering/default rules as the SQLite one.</summary>
internal sealed class MemoryRepository : IServerConnectionRepository {
    private int _nextId = 1;

    public List<ServerConnection> Items { get; } = [];
    public List<(int Id, string Version)> Connected { get; } = [];

    public Task<IReadOnlyList<ServerConnection>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<ServerConnection>>(Items.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList());

    public Task<ServerConnection?> GetAsync(int id) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
    public Task<ServerConnection?> GetDefaultAsync() => Task.FromResult(Items.FirstOrDefault(s => s.IsDefault));

    public Task AddAsync(ServerConnection connection) {
        connection.Id = _nextId++;
        connection.IsDefault = Items.Count == 0;
        connection.SortOrder = Items.Count == 0 ? 0 : Items.Max(s => s.SortOrder) + 1;
        Items.Add(connection);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ServerConnection connection) => Task.CompletedTask;

    public Task DeleteAsync(int id) {
        var removed = Items.FirstOrDefault(s => s.Id == id);
        if (removed is null)
            return Task.CompletedTask;

        Items.Remove(removed);
        if (removed.IsDefault && Items.OrderBy(s => s.SortOrder).FirstOrDefault() is { } next)
            next.IsDefault = true;
        return Task.CompletedTask;
    }

    public Task SetDefaultAsync(int id) {
        foreach (var s in Items)
            s.IsDefault = s.Id == id;
        return Task.CompletedTask;
    }

    public Task ReorderAsync(IReadOnlyList<int> orderedIds) {
        var rest = Items.OrderBy(s => s.SortOrder).Select(s => s.Id).Where(id => !orderedIds.Contains(id));
        var order = 0;
        foreach (var id in orderedIds.Concat(rest))
            Items.First(s => s.Id == id).SortOrder = order++;
        return Task.CompletedTask;
    }

    public Task MarkConnectedAsync(int id, string version, DateTimeOffset when) {
        Connected.Add((id, version));
        return Task.CompletedTask;
    }
}

internal sealed class FakeNavigation : INavigationService {
    public int Pops { get; private set; }
    public ViewModelBase? Current => null;
    public bool CanGoBack => true;
    public void SetRoot(ViewModelBase root) { }
    public TViewModel Push<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase => throw new NotSupportedException();
    public bool Pop() { Pops++; return true; }
}

/// <summary>The real app container with storage and the network swapped for fakes, and logs sent to a temp folder.</summary>
internal static class TestApp {
    public static ServiceProvider Build(IServerConnectionRepository repository, IArrConnectionTester? tester = null) {
        var logs = Path.Combine(Path.GetTempPath(), "arrapp-ui-tests-logs");
        return new ServiceCollection()
            .AddArrApp(logs)
            .AddSingleton<IServerConnectionRepository>(repository)
            .AddSingleton<IArrConnectionTester>(tester ?? new FakeTester((type, _, _) => ConnectionTestResult.Ok(type.ToString(), "4.0.9.2244")))
            .BuildServiceProvider();
    }
}
