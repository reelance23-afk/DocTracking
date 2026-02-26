namespace DocTracking.Client.Services
{
    public class LoadingService
    {
        public bool IsLoading { get; private set; }
        public event Action? OnChange;

        public void Show()
        {
            IsLoading = true;
            NotifyStateChanged();
        }

        public void Hide()
        {
            IsLoading = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}