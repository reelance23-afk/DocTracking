using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using System.Security.Claims;
using DocTracking.Client;
using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Services
{
    public class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
    {
        private readonly PersistentComponentState _state;
        private readonly PersistingComponentStateSubscription _subscription;
        private readonly ApplicationDbContext _db;
        private Task<AuthenticationState>? _authenticationStateTask;

        public PersistingServerAuthenticationStateProvider(PersistentComponentState state,ApplicationDbContext db)
        {
            _state = state;
            _db = db;
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
                var email = user.Identity.Name ?? "";

                var office = await _db.Offices.FirstOrDefaultAsync(o => o.WorkerEmail == email);

                _state.PersistAsJson(nameof(UserInfo), new UserInfo
                {
                    UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? email,
                    Email = email,
                    Role = user.FindFirst("roles")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value,

                    OfficeId = office?.Id,
                    OfficeName = office?.Name
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