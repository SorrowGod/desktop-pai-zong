using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace DesktopPet.App.Infrastructure;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listener;
    private bool _disposed;

    public SingleInstanceService(string? instanceId = null)
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value
            ?? Environment.UserName;
        var discriminator = string.IsNullOrWhiteSpace(instanceId) ? "DesktopPet" : instanceId;
        var suffix = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{identity}|{discriminator}")))[..16];
        _pipeName = $"DesktopPet-{suffix}";
        _mutex = new Mutex(true, $"Local\\DesktopPet-{suffix}", out var isFirstInstance);
        IsFirstInstance = isFirstInstance;
    }

    public bool IsFirstInstance { get; }

    public void StartListening(Func<string, Task> handleCommand)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsFirstInstance || _listener is not null)
        {
            return;
        }

        _listener = ListenAsync(handleCommand, _shutdown.Token);
    }

    public Task? ListenerTask => _listener;

    public async Task SignalPrimaryAsync(string command, CancellationToken cancellationToken = default)
    {
        if (IsFirstInstance)
        {
            return;
        }

        NamedPipeClientStream client;
        try
        {
            client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Named pipe client creation failed (0x{exception.HResult:X8}).", exception);
        }

        using (client)
        {
            try
            {
                await client.ConnectAsync(2_000, cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Named pipe connection failed (0x{exception.HResult:X8}).", exception);
            }

            try
            {
                await using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: false)
                {
                    AutoFlush = true
                };
                await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Named pipe write failed (0x{exception.HResult:X8}).", exception);
            }
        }
    }

    private async Task ListenAsync(Func<string, Task> handleCommand, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var currentUser = WindowsIdentity.GetCurrent().User
                    ?? throw new InvalidOperationException("无法读取当前用户 SID。");
                var security = new PipeSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new PipeAccessRule(
                    currentUser,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                security.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                await using var server = NamedPipeServerStreamAcl.Create(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    security);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, true, leaveOpen: true);
                var command = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    await handleCommand(command);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex.Dispose();
    }
}
