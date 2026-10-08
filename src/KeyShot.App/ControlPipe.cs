using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;

namespace KeyShot.App;

/// <summary>
/// Local control channel (\\.\pipe\KeyShot.Control) used by the second-instance
/// launcher and the VS Code extension. One text line in, one line out.
/// Commands: PING, STATUS, ENABLE, DISABLE, TOGGLE, SHOW. Restricted to the current user.
/// </summary>
internal sealed class ControlPipeServer : IDisposable
{
    public const string PipeName = "KeyShot.Control";

    private readonly Func<string, string> _handler;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cts = new();

    public ControlPipeServer(Func<string, string> handler, Dispatcher dispatcher)
    {
        _handler = handler;
        _dispatcher = dispatcher;
    }

    public void Start() => _ = Task.Run(() => RunAsync(_cts.Token));

    public void Dispose() => _cts.Cancel();

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var command = (await reader.ReadLineAsync(timeout.Token))?.Trim().ToUpperInvariant() ?? "";

                var response = await _dispatcher.InvokeAsync(() => _handler(command));
                var bytes = Encoding.UTF8.GetBytes(response + "\n");
                await server.WriteAsync(bytes, timeout.Token);
                await server.FlushAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error("Control pipe", ex);
                await Task.Delay(250, CancellationToken.None);
            }
        }
    }
}

internal static class ControlPipeClient
{
    /// <summary>Sends a command to a running KeyShot. Returns its reply, or null if none is running.</summary>
    public static string? TrySend(string command, int timeoutMs = 1500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", ControlPipeServer.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMs);
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            client.Write(bytes);
            client.Flush();
            using var reader = new StreamReader(client, Encoding.UTF8);
            return reader.ReadLine();
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
