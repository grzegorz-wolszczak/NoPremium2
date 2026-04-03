namespace NoPremium2.Services;

/// <summary>
/// Shared flag that is set while a consumer service (TransferConsumer or VoucherConsumer)
/// is actively using the browser. KeepaliveService checks this before navigating so that
/// it does not attempt to acquire the browser page while a consumer run is in progress.
/// </summary>
public sealed class ConsumerActivityState
{
    private volatile int _active;

    public bool IsActive => _active > 0;

    public void Enter() => Interlocked.Increment(ref _active);
    public void Exit()  => Interlocked.Decrement(ref _active);
}
