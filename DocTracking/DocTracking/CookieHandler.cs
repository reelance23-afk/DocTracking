using Microsoft.AspNetCore.Http;

namespace DocTracking
{
    public class CookieHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var cookieHeader = httpContext.Request.Headers["Cookie"].ToString();
                if (!string.IsNullOrEmpty(cookieHeader))
                    request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
