using System;
using System.Security.Principal;
using System.Threading;

namespace TempChain.Chat;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private readonly RegisteredWaitHandle? listener;
    public bool IsFirstInstance { get; }
    public event Action? ActivationRequested;

    public SingleInstanceCoordinator()
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var name = @"Local\TempChain.Chat." + user;
        mutex = new Mutex(initiallyOwned: true, name + ".Instance", out var created);
        IsFirstInstance = created;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        if (IsFirstInstance)
            listener = ThreadPool.RegisterWaitForSingleObject(activation,
                (_, _) => ActivationRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void ActivateExisting() => activation.Set();

    public void Dispose()
    {
        listener?.Unregister(null);
        activation.Dispose();
        if (IsFirstInstance)
            mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
