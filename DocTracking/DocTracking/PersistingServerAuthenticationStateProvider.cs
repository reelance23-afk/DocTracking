using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using System.Security.Claims;
using DocTracking.Client; 

namespace DocTracking.Services
{
    public class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
    {
        private readonly PersistentComponentState _state;
        private readonly PersistingComponentStateSubscription _subscription;
        private Task<AuthenticationState>? _authenticationStateTask;

        public PersistingServerAuthenticationStateProvider(PersistentComponentState state)
        {
            _state = state;
            _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveAuto);
            AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _authenticationStateTask = task;

        private async Task OnPersistingAsync()
        {
            if (_authenticationStateTask is null) return;
            var authState = await _authenticationStateTask;
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                // This is the "Sender" that packages the data for your Client file to "Take"
                _state.PersistAsJson(nameof(UserInfo), new UserInfo
                {
                    UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.Identity.Name ?? "",
                    Email = user.Identity.Name ?? ""
                });
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
            AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}