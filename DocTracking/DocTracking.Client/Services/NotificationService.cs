using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using DocTracking.Client.Models;
using System.Net.Http.Json;

namespace DocTracking.Client.Services
{
    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _hub;
        private readonly NavigationManager _navigation;
        private readonly HttpClient _http;
        private bool _historyLoaded = false;

        public List<AppNotification> Notifications { get; } = new();
        public int UnreadCount => Notifications.Count(n => !n.IsRead);
        public bool IsConnected => _hub?.State == HubConnectionState.Connected;
        public event Action? OnChange;
        public event Action? OnRoleChanged;

        public NotificationService(NavigationManager navigation, HttpClient http)
        {
            _navigation = navigation;
            _http = http;
        }

        public async Task ConnectAsync(string userGroup, IEnumerable<string>? additionalGroups = null)
        {
            if (_hub == null)
            {
                _hub = new HubConnectionBuilder()
                    .WithUrl(_navigation.ToAbsoluteUri("/hubs/notifications"))
                    .WithAutomaticReconnect()
                    .Build();

                _hub.On<string, string>("ReceiveNotification", (message, docName) =>
                {
                    Notifications.Insert(0, new AppNotification
                    {
                        Message = message,
                        DocumentName = docName,
                        Time = DateTime.UtcNow
                    });
                    OnChange?.Invoke();
                });

                _hub.On("RoleChanged", () =>
                {
                    OnRoleChanged?.Invoke();
                });

                _hub.Reconnected += async _ =>
                {
                    try
                    {
                        await _hub.InvokeAsync("JoinGroup", userGroup);
                        if (additionalGroups != null)
                            foreach (var g in additionalGroups)
                                await _hub.InvokeAsync("JoinGroup", g);

                        var history = await _http.GetFromJsonAsync<List<AppNotification>>("api/notifications");
                        if (history != null)
                        {
                            Notifications.Clear();
                            Notifications.AddRange(history);
                        }
                        _historyLoaded = true;
                        OnChange?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NotifService] Reconnect handler error: {ex.Message}");
                    }
                };
            }

            if (_hub.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _hub.StartAsync();
                    await _hub.InvokeAsync("JoinGroup", userGroup);
                    if (additionalGroups != null)
                        foreach (var group in additionalGroups)
                            await _hub.InvokeAsync("JoinGroup", group);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NotifService] Initial connect failed: {ex.Message}");
                    return;
                }
            }

            if (!_historyLoaded)
            {
                try
                {
                    var fresh = await _http.GetFromJsonAsync<List<AppNotification>>("api/notifications");
                    if (fresh != null)
                    {
                        Notifications.Clear();
                        Notifications.AddRange(fresh);
                    }
                    _historyLoaded = true;
                    OnChange?.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NotifService] History load failed: {ex.Message}");
                }
            }
        }
        public async Task DismissAsync(AppNotification notif)
        {
            Notifications.Remove(notif);
            OnChange?.Invoke();
            try { await _http.DeleteAsync($"api/notifications/{notif.Id}"); }
            catch (Exception ex) { Console.WriteLine($"[NotifService] Dismiss failed: {ex.Message}"); }
        }

        public async Task MarkAllReadAsync()
        {
            Notifications.ForEach(n => n.IsRead = true);
            OnChange?.Invoke();
            try { await _http.PutAsync("api/notifications/read-all", null); }
            catch (Exception ex) { Console.WriteLine($"[NotifService] MarkAllRead failed: {ex.Message}"); }
        }
        public async Task ToggleReadAsync(AppNotification notif)
        {
            notif.IsRead = !notif.IsRead;
            OnChange?.Invoke();
            try { await _http.PutAsync($"api/notifications/{notif.Id}/toggle-read", null); }
            catch (Exception ex) { Console.WriteLine($"[NotifService] ToggleRead failed: {ex.Message}"); }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub != null) await _hub.DisposeAsync();
        }
    }
}
