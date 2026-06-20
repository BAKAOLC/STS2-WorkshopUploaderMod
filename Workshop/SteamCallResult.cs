using Steamworks;

namespace STS2WorkshopUploader.Workshop;

internal sealed class SteamCallResult<T> : IDisposable where T : struct
{
    private readonly CallResult<T> _callResult;
    private readonly CancellationTokenRegistration _registration;
    private readonly TaskCompletionSource<T> _source = new();

    public SteamCallResult(SteamAPICall_t call, CancellationToken cancellationToken)
    {
        _callResult = CallResult<T>.Create(OnCallResult);
        _callResult.Set(call);
        _registration = cancellationToken.Register(() => _source.TrySetCanceled(cancellationToken));
    }

    public Task<T> Task => _source.Task;

    public void Dispose()
    {
        _registration.Dispose();
        _callResult.Dispose();
    }

    private void OnCallResult(T result, bool ioError)
    {
        if (ioError)
            _source.TrySetException(new IOException($"Steam CallResult IO failure for {typeof(T).Name}."));
        else
            _source.TrySetResult(result);
    }
}