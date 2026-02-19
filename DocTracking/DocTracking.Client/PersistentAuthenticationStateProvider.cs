using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace DocTracking.Client
{
    public class PersistentAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly Task<AuthenticationState> _unauthenticatedTask =
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        private readonly Task<AuthenticationState> _authenticationStateTask;

        public PersistentAuthenticationStateProvider(PersistentComponentState state)
        {
            if (!state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) || userInfo is null)
            {
                _authenticationStateTask = _unauthenticatedTask;
            }                                                                               
            else
            {
                Claim[] claims = [
                    new Claim(ClaimTypes.NameIdentifier, userInfo.UserId ?? ""),
                    new Claim(ClaimTypes.Name, userInfo.Email ?? ""),
                    new Claim(ClaimTypes.Email, userInfo.Email ?? "") ];

                _authenticationStateTask = Task.FromResult(
                    new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "authentication"))));
            }
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
    }
}