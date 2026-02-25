using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace MockDynamoDB.Tests.Samples.Backends;

/// <summary>
/// Starts a <c>motoserver/moto:5.1.21</c> Docker container via <c>docker run</c> and exposes a
/// DynamoDB client pointed at it. Sets <see cref="IsAvailable"/> to false (and skips tests)
/// when Docker is unavailable or the container fails to start.
/// </summary>
public sealed class MotoBackend : IMockBackend
{
    private string? _containerId;

    public bool IsAvailable { get; private set; }
    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            var versionResult = await RunDockerAsync(["--version"]);
            if (versionResult.ExitCode != 0) return;

            var runResult = await RunDockerAsync(
                ["run", "-d", "-p", "5000", "motoserver/moto:5.1.21"],
                TimeSpan.FromMinutes(3));
            if (runResult.ExitCode != 0 || string.IsNullOrWhiteSpace(runResult.Stdout)) return;

            _containerId = runResult.Stdout.Trim();

            await Task.Delay(500);

            var portResult = await RunDockerAsync(["port", _containerId, "5000"]);
            if (portResult.ExitCode != 0) return;

            var port = ParsePort(portResult.Stdout);
            if (port is null) return;

            var endpoint = $"http://127.0.0.1:{port}";
            await WaitForReadyAsync(endpoint);

            AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true;

            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = endpoint,
                AuthenticationRegion = "us-east-1"
            };
            Client = new AmazonDynamoDBClient(new BasicAWSCredentials("fake", "fake"), config);
            IsAvailable = true;
        }
        catch
        {
            // Docker unavailable or container failed to start — tests will skip
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_containerId is not null)
        {
            await RunDockerAsync(["rm", "-f", _containerId]);
            _containerId = null;
        }
    }

    private static async Task WaitForReadyAsync(string endpoint)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try { await http.GetAsync(endpoint + "/"); return; }
            catch (HttpRequestException) { }
            await Task.Delay(200);
        }
    }

    private static string? ParsePort(string dockerPortOutput)
    {
        var line = dockerPortOutput.Trim().Split('\n')[0].Trim();
        var parts = line.Split(':');
        return parts.Length > 0 && int.TryParse(parts[^1], out var port) ? port.ToString() : null;
    }

    internal static async Task<(int ExitCode, string Stdout)> RunDockerAsync(
        IEnumerable<string> args,
        TimeSpan? timeout = null)
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                await stdoutTask;
                return (-1, string.Empty);
            }

            return (process.ExitCode, await stdoutTask);
        }
        catch { return (-1, string.Empty); }
    }
}
