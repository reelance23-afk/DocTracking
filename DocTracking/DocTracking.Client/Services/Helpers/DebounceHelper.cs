namespace DocTracking.Client.Services.Helpers
{
    public sealed class DebounceHelper : IDisposable
    {
        private readonly int _delayMs;
        private CancellationTokenSource? _cts;

        public DebounceHelper(int delayMs = 500)
        {
            _delayMs = delayMs;
        }

        public async void Trigger(Func<Task> action)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                await Task.Delay(_delayMs, _cts.Token);
                await action();
            }
            catch (TaskCanceledException) { }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
