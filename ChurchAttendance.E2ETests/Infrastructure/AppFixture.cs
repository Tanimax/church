using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;

namespace ChurchAttendance.E2ETests.Infrastructure;

// Hosts the real, already-built app as an actual OS process bound to a real TCP port
// (WebApplicationFactory's in-memory TestServer can't be driven by a real browser) against
// a throwaway Testcontainers Postgres. Program.cs's own startup code — migrations, the
// bootstrap "admin" account — runs exactly as it would in production, so the fixture itself
// needs no manual schema/seed logic. Shared for the whole E2E run: tests isolate their own
// data by using unique names rather than paying for a fresh app+database per test.
public sealed class AppFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private Process? _appProcess;
    private IPlaywright _playwright = null!;

    public string BaseUrl { get; private set; } = "";
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();
        await _container.StartAsync();

        var port = GetFreeTcpPort();
        BaseUrl = $"http://localhost:{port}";

        var appDllPath = FindAppDll();

        var psi = new ProcessStartInfo("dotnet", $"\"{appDllPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ConnectionStrings__Default"] = _container.GetConnectionString();

        _appProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the app process.");

        await WaitForHealthyAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    // Walks up from the test assembly's output directory to the repo root (marked by the
    // .sln file), then down into the main project's own build output — avoids hardcoding a
    // relative-path depth that would break if either project moves.
    private static string FindAppDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (no .sln found above the test output directory).");
        }

        var config = AppContext.BaseDirectory.Contains("Release", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
        var candidate = Path.Combine(dir.FullName, "bin", config, "net10.0", "ChurchAttendance.dll");

        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"Could not find ChurchAttendance.dll — build the main project first (dotnet build ChurchAttendance.csproj). Looked at: {candidate}");
        }

        return candidate;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task WaitForHealthyAsync()
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_appProcess!.HasExited)
            {
                var stderr = await _appProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"App process exited early (code {_appProcess.ExitCode}). Stderr:\n{stderr}");
            }

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/admin/login");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"App did not become healthy within 30s. Last error: {lastError}");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }
        _playwright?.Dispose();

        if (_appProcess is { HasExited: false })
        {
            _appProcess.Kill(entireProcessTree: true);
            _appProcess.WaitForExit(5000);
        }
        _appProcess?.Dispose();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
