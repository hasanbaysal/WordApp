using System;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WordApp.Data;
using WordApp.Models;
using WordApp.Services;

namespace WordApp.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        private static readonly ConcurrentDictionary<string, (int Attempts, DateTime LockoutUntil)> _loginAttempts = new();

        public AuthApiController(
            ApplicationDbContext context,
            IJwtTokenService jwtTokenService,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _configuration = configuration;
            _env = env;
        }

        private bool BypassRateLimit =>
            _env.IsDevelopment() && _configuration.GetValue<bool>("DevSettings:BypassAuthChallenges");

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequest? request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!BypassRateLimit && _loginAttempts.TryGetValue(ipAddress, out var attemptInfo))
            {
                if (attemptInfo.LockoutUntil > DateTime.UtcNow)
                {
                    var minutesLeft = Math.Ceiling((attemptInfo.LockoutUntil - DateTime.UtcNow).TotalMinutes);
                    return StatusCode(429, new
                    {
                        error = "TOO_MANY_ATTEMPTS",
                        message = $"Çok fazla başarısız giriş denemesi. Lütfen {minutesLeft} dakika sonra tekrar deneyin."
                    });
                }
            }

            var identifier = !string.IsNullOrWhiteSpace(request?.Username)
                ? request.Username.Trim()
                : !string.IsNullOrWhiteSpace(request?.Email)
                    ? request.Email.Trim()
                    : request?.Identifier?.Trim();

            var password = request?.Password;

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new
                {
                    error = "INVALID_REQUEST",
                    message = "Kullanıcı adı/e-posta ve şifre alanları zorunludur."
                });
            }

            var normalizedIdentifier = identifier.ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username.ToLower() == normalizedIdentifier ||
                u.Email.ToLower() == normalizedIdentifier);

            if (user != null)
            {
                var hasher = new PasswordHasher<User>();
                var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

                if (verificationResult == PasswordVerificationResult.Success ||
                    verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    _loginAttempts.TryRemove(ipAddress, out _);

                    if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        user.PasswordHash = hasher.HashPassword(user, password);
                        await _context.SaveChangesAsync();
                    }

                    var tokens = await _jwtTokenService.IssueTokensAsync(user, request?.DeviceName);

                    return Ok(new
                    {
                        accessToken = tokens.AccessToken,
                        refreshToken = tokens.RefreshToken,
                        expiresAt = tokens.ExpiresAt,
                        tokenType = "Bearer",
                        user = new
                        {
                            id = user.Id,
                            username = user.Username,
                            email = user.Email
                        }
                    });
                }
            }

            if (!BypassRateLimit)
            {
                var attempts = 1;
                var lockoutUntil = DateTime.MinValue;
                if (_loginAttempts.TryGetValue(ipAddress, out var failedInfo))
                {
                    attempts = failedInfo.Attempts + 1;
                    if (attempts >= 10)
                    {
                        lockoutUntil = DateTime.UtcNow.AddMinutes(5);
                    }
                }
                _loginAttempts[ipAddress] = (attempts, lockoutUntil);
            }

            return Unauthorized(new
            {
                error = "INVALID_CREDENTIALS",
                message = "Geçersiz kullanıcı adı veya şifre."
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] ApiRefreshRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new
                {
                    error = "INVALID_REQUEST",
                    message = "refreshToken alanı zorunludur."
                });
            }

            var tokens = await _jwtTokenService.RefreshTokensAsync(request.RefreshToken, request.DeviceName);
            if (tokens == null)
            {
                return Unauthorized(new
                {
                    error = "INVALID_REFRESH_TOKEN",
                    message = "Geçersiz veya süresi dolmuş refresh token."
                });
            }

            return Ok(new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken,
                expiresAt = tokens.ExpiresAt,
                tokenType = "Bearer"
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] ApiRefreshRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
            }

            return NoContent();
        }
    }

    public class ApiLoginRequest
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; set; }
    }

    public class ApiRefreshRequest
    {
        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; set; }
    }
}
