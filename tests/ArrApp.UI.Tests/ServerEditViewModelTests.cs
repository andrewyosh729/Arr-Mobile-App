using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArrApp.UI.Tests;

public class ServerEditViewModelTests {
    private sealed class FakeTester(Func<ArrType, string, string, ConnectionTestResult> respond) : IArrConnectionTester {
        public List<(ArrType Type, string Url, string Key)> Calls { get; } = [];

        public Task<ConnectionTestResult> TestAsync(ArrType expected, string baseUrl, string apiKey, CancellationToken cancellationToken = default) {
            Calls.Add((expected, baseUrl, apiKey));
            return Task.FromResult(respond(expected, baseUrl, apiKey));
        }
    }

    private sealed class MemoryRepository : IServerConnectionRepository {
        public List<ServerConnection> Items { get; } = [];
        public List<(int Id, string Version)> Connected { get; } = [];
        private int _nextId = 1;

        public Task<IReadOnlyList<ServerConnection>> GetAllAsync() => Task.FromResult<IReadOnlyList<ServerConnection>>(Items.ToList());
        public Task<ServerConnection?> GetAsync(int id) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
        public Task<ServerConnection?> GetDefaultAsync() => Task.FromResult(Items.FirstOrDefault(s => s.IsDefault));

        public Task AddAsync(ServerConnection connection) {
            connection.Id = _nextId++;
            Items.Add(connection);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ServerConnection connection) => Task.CompletedTask;
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task SetDefaultAsync(int id) => Task.CompletedTask;
        public Task ReorderAsync(IReadOnlyList<int> orderedIds) => Task.CompletedTask;

        public Task MarkConnectedAsync(int id, string version, DateTimeOffset when) {
            Connected.Add((id, version));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNavigation : INavigationService {
        public int Pops { get; private set; }
        public ViewModelBase? Current => null;
        public bool CanGoBack => true;
        public void SetRoot(ViewModelBase root) { }
        public TViewModel Push<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase => throw new NotSupportedException();
        public bool Pop() { Pops++; return true; }
    }

    private sealed record Fixture(ServerEditViewModel Vm, FakeTester Tester, MemoryRepository Repo, FakeNavigation Nav, ToastService Toasts);

    private static readonly ConnectionTestResult Ok = ConnectionTestResult.Ok("Sonarr", "4.0.9.2244");

    private static Fixture Create(ConnectionTestResult? result = null) {
        var tester = new FakeTester((_, _, _) => result ?? Ok);
        var repo = new MemoryRepository();
        var nav = new FakeNavigation();
        var toasts = new ToastService();
        var vm = new ServerEditViewModel(repo, tester, nav, toasts, new ErrorReporter(NullLogger<ErrorReporter>.Instance, toasts));
        return new Fixture(vm, tester, repo, nav, toasts);
    }

    private static void Fill(ServerEditViewModel vm, string url = "192.168.1.10:8989", string key = " KEY ", string name = "Home") {
        vm.Name = name;
        vm.Url = url;
        vm.ApiKey = key;
    }

    [Fact]
    public async Task Save_Without_A_Test_Runs_The_Test_Then_Saves_And_Closes() {
        var f = Create();
        Fill(f.Vm);

        await f.Vm.SaveCommand.ExecuteAsync(null);

        var call = Assert.Single(f.Tester.Calls);
        Assert.Equal((ArrType.Sonarr, "http://192.168.1.10:8989", "KEY"), call);
        var saved = Assert.Single(f.Repo.Items);
        Assert.Equal("http://192.168.1.10:8989", saved.BaseUrl);
        Assert.Equal("KEY", saved.ApiKey);
        Assert.Equal("Home", saved.Name);
        Assert.Equal((saved.Id, "4.0.9.2244"), Assert.Single(f.Repo.Connected));
        Assert.Equal(1, f.Nav.Pops);
    }

    [Fact]
    public async Task Save_After_A_Successful_Test_Does_Not_Test_Again() {
        var f = Create();
        Fill(f.Vm);
        await f.Vm.TestConnectionCommand.ExecuteAsync(null);

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(f.Tester.Calls);
        Assert.Single(f.Repo.Items);
        Assert.Equal("4.0.9.2244", Assert.Single(f.Repo.Connected).Version);
    }

    [Fact]
    public async Task Editing_Any_Field_After_A_Test_Requires_Testing_Again() {
        var f = Create();
        Fill(f.Vm);
        await f.Vm.TestConnectionCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsSuccess);

        f.Vm.Url = "192.168.1.11:8989";

        Assert.Equal(TestState.Idle, f.Vm.State);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, f.Tester.Calls.Count);
    }

    [Fact]
    public async Task Failed_Test_Does_Not_Save_And_Offers_Save_Anyway() {
        var f = Create(ConnectionTestResult.Fail(ArrErrorKind.Unreachable, "Couldn't reach the server."));
        Fill(f.Vm);

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(f.Repo.Items);
        Assert.True(f.Vm.IsFailed);
        Assert.True(f.Vm.CanSaveAnyway);
        Assert.Equal("Couldn't reach the server.", f.Vm.StatusMessage);
        Assert.Equal(0, f.Nav.Pops);
    }

    [Fact]
    public async Task Save_Anyway_Saves_Without_Recording_A_Connection() {
        var f = Create(ConnectionTestResult.Fail(ArrErrorKind.Timeout, "Too slow."));
        Fill(f.Vm);
        await f.Vm.SaveCommand.ExecuteAsync(null);

        await f.Vm.SaveAnywayCommand.ExecuteAsync(null);

        Assert.Single(f.Repo.Items);
        Assert.Empty(f.Repo.Connected);
        Assert.Equal(1, f.Nav.Pops);
    }

    [Fact]
    public async Task Wrong_App_Cannot_Be_Overridden() {
        var f = Create(ConnectionTestResult.Fail(ArrErrorKind.WrongApp, "This server is Radarr, not Sonarr.", "Radarr"));
        Fill(f.Vm);

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.True(f.Vm.IsFailed);
        Assert.False(f.Vm.CanSaveAnyway);
        Assert.Empty(f.Repo.Items);
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("ftp://nope", "key")]
    [InlineData("host:8989", "")]
    [InlineData("host:8989", "   ")]
    public async Task Invalid_Input_Is_Reported_Without_Calling_The_Server(string url, string key) {
        var f = Create();
        Fill(f.Vm, url, key);

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(f.Tester.Calls);
        Assert.Empty(f.Repo.Items);
        Assert.True(f.Vm.IsFailed);
        Assert.False(f.Vm.CanSaveAnyway);
        Assert.False(string.IsNullOrEmpty(f.Vm.StatusMessage));
    }

    [Fact]
    public async Task Blank_Name_Defaults_To_The_Type() {
        var f = Create(ConnectionTestResult.Ok("Radarr", "5.1.0"));
        Fill(f.Vm, name: "  ");
        f.Vm.IsRadarr = true;

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Radarr", Assert.Single(f.Repo.Items).Name);
        Assert.Equal(ArrType.Radarr, f.Repo.Items[0].ArrType);
    }

    [Fact]
    public async Task Editing_Keeps_The_Existing_Id_And_Preloads_The_Fields() {
        var f = Create();
        var existing = new ServerConnection { Id = 7, Name = "Old", ArrType = ArrType.Sonarr, BaseUrl = "http://old:8989", ApiKey = "OLDKEY" };

        f.Vm.Load(existing);

        Assert.True(f.Vm.IsEdit);
        Assert.Equal("Edit server", f.Vm.Title);
        Assert.Equal("OLDKEY", f.Vm.ApiKey);

        f.Vm.Name = "New";
        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(f.Repo.Items); // updated in place, not added
        Assert.Equal(7, existing.Id);
        Assert.Equal("New", existing.Name);
        Assert.Equal(7, Assert.Single(f.Repo.Connected).Id);
    }

    [Fact]
    public void Type_Chips_Behave_Like_A_Radio_Group() {
        var f = Create();

        f.Vm.IsRadarr = true;
        Assert.False(f.Vm.IsSonarr);
        Assert.Equal(ArrType.Radarr, f.Vm.ArrType);

        f.Vm.IsRadarr = false; // tapping the selected chip must not leave nothing selected
        Assert.True(f.Vm.IsRadarr);
    }

    [Fact]
    public async Task Saved_Callback_Fires_After_A_Successful_Save() {
        var f = Create();
        Fill(f.Vm);
        var fired = 0;
        f.Vm.Saved = () => fired++;

        await f.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fired);
    }
}
