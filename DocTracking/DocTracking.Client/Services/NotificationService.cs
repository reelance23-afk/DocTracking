using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using DocTracking.Client.Models;

namespace DocTracking.Client.Services
{
    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _hub;
        private readonly NavigationManager _navigation;
        public List<AppNotification> Notifications { get; } = new();
        public int UnreadCount => Notifications.Count(n => !n.IsRead);
        public event Action? OnChange;

        public NotificationService(NavigationManager navigation)
        {
            _navigation = navigation;
        }

        public async Task StartAsync(string groupName)
        {
            if (_hub != null) return;

            _hub = new HubConnectionBuilder()
                .WithUrl(_navigation.ToAbsoluteUri("/hubs/notifications"), options =>
                {
                    options.UseDefaultCredentials = true;
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.On<string, string>("ReceiveNotification", (message, docName) =>
            {
                Notifications.Insert(0, new AppNotification { Message = message, DocumentName = docName });
                OnChange?.Invoke();
            });

            await _hub.StartAsync();
            await _hub.InvokeAsync("JoinGroup", groupName);
        }

        public void MarkAllRead()
        {
            Notifications.ForEach(n => n.IsRead = true);
            OnChange?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub != null) await _hub.DisposeAsync();
        }
    }
}
