using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WordApp.Services
{
    public class JwtBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Bearer";

        private readonly IJwtTokenService _jwtTokenService;

        public JwtBearerAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IJwtTokenService jwtTokenService)
            : base(options, logger, encoder)
        {
            _jwtTokenService = jwtTokenService;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var header = headerValues.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Authorization header must use Bearer scheme."));
            }

            var token = header["Bearer ".Length..].Trim();
            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult(AuthenticateResult.Fail("Bearer token is empty."));
            }

            var principal = _jwtTokenService.ValidateAccessToken(token);
            if (principal == null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid or expired JWT token."));
            }

            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json; charset=utf-8";
            Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";

            var payload = JsonSerializer.Serialize(new
            {
                error = "UNAUTHORIZED",
                message = "Geçersiz veya süresi dolmuş erişim belirteci (Bearer token)."
            });

            await Response.WriteAsync(payload);
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.ContentType = "application/json; charset=utf-8";

            var payload = JsonSerializer.Serialize(new
            {
                error = "FORBIDDEN",
                message = "Bu işlem için yetkiniz bulunmamaktadır."
            });

            await Response.WriteAsync(payload);
        }
    }
}
