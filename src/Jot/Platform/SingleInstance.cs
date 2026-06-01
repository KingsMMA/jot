using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Jot.Platform;

/// <summary>
/// Enforces a single running instance per user and provides a small pipe channel so a second launch
/// can hand its file to the instance that is already running. This is what makes repeat opens feel
/// instant: the warm process simply shows the requested file instead of starting from cold.
/// </summary>
public static class SingleInstance
{
    private static Mutex? _mutex;

    private static string Sid =>
        WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;

    private static string MutexName => $"Local\\Jot.{Sid}.instance";
    private static string PipeName => $"Jot.{Sid}.pipe";

    /// <summary>Becomes the primary instance, or returns false if one is already running.</summary>
    public static bool TryBecomePrimary()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew) return true;
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); }
        catch { /* not owned or already released */ }
        _mutex?.Dispose();
        _mutex = null;
    }

    /// <summary>Listens for messages from later launches and invokes <paramref name="onMessage"/>.</summary>
    public static void StartServer(Action<string> onMessage, CancellationToken cancellation = default)
    {
        _ = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(cancellation);
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadToEndAsync(cancellation);
                    if (!string.IsNullOrWhiteSpace(message))
                        onMessage(message.Trim());
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    await Task.Delay(200, CancellationToken.None);
                }
            }
        }, cancellation);
    }

    /// <summary>Sends a message to the running instance, returning false if none answered in time.</summary>
    public static bool TrySend(string message, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.Write(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Lets the running instance take the foreground when we ask it to show a file.</summary>
    public static void AllowForegroundForRunningInstance()
    {
        try { AllowSetForegroundWindow(ASFW_ANY); }
        catch { /* best effort */ }
    }

    private const int ASFW_ANY = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
