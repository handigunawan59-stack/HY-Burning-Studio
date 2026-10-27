using System.Threading;

namespace HYBurningStudio;

public sealed class SingleInstance : IDisposable
{
    private readonly string eventName;
    private readonly Mutex mutex;
    private readonly EventWaitHandle? activationEvent;
    private readonly RegisteredWaitHandle? listener;

    public bool IsPrimary { get; }

    public SingleInstance(string name, Action activate)
    {
        eventName = @"Local\" + name + ".Activate";
        mutex = new Mutex(false, @"Local\" + name + ".Instance", out bool created);
        IsPrimary = created;
        if (created)
        {
            activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            listener = ThreadPool.RegisterWaitForSingleObject(activationEvent,
                (_, _) => activate(), null, Timeout.Infinite, false);
        }
    }

    public void ActivatePrimary()
    {
        if (IsPrimary) return;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(eventName);
                signal.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(20); }
        }
    }

    public void Dispose()
    {
        listener?.Unregister(null);
        activationEvent?.Dispose();
        mutex.Dispose();
    }
}
