using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using DocTracking.Client.Models;

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
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userInfo.UserId ?? ""),
                    new Claim(ClaimTypes.Name, userInfo.RealName ?? ""),
                    new Claim(ClaimTypes.Email, userInfo.Email ?? "")
                };

                if (!string.IsNullOrEmpty(userInfo.Role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, userInfo.Role ?? "User"));
                }

                if (userInfo.OfficeId.HasValue)
                {
                    claims.Add(new Claim("OfficeId", userInfo.OfficeId.ToString()));
                }


                _authenticationStateTask = Task.FromResult(
                    new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "authentication"))));
            }
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
    }
}