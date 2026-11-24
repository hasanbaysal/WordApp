using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Services
{
    public class AuthTokenPair
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public interface IJwtTokenService
    {
        Task<AuthTokenPair> IssueTokensAsync(User user, string? deviceName = null);
        Task<AuthTokenPair?> RefreshTokensAsync(string refreshToken, string? deviceName = null);
        Task<bool> RevokeRefreshTokenAsync(string refreshToken);
        ClaimsPrincipal? ValidateAccessToken(string token);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private const string SecretSettingKey = "JwtSigningSecret";
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly object _keyLock = new();
        private byte[]? _cachedSigningKey;

        private string Issuer => _configuration["JwtSettings:Issuer"] ?? "WordApp";
        private string Audience => _configuration["JwtSettings:Audience"] ?? "MyStickyNotes";
        private int AccessTokenMinutes => Math.Clamp(_configuration.GetValue<int?>("JwtSettings:AccessTokenMinutes") ?? 60, 5, 1440);
        private int RefreshTokenDays => Math.Clamp(_configuration.GetValue<int?>("JwtSettings:RefreshTokenDays") ?? 30, 1, 365);

        public JwtTokenService(IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        public async Task<AuthTokenPair> IssueTokensAsync(User user, string? deviceName = null)
        {
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddMinutes(AccessTokenMinutes);
            var accessToken = CreateAccessToken(user, now, expiresAt);
            var rawRefreshToken = GenerateOpaqueToken();
            var refreshTokenHash = HashToken(rawRefreshToken);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Prune expired/revoked tokens older than 7 days to keep table tidy
            var cleanupCutoff = DateTime.UtcNow.AddDays(-7);
            var staleTokens = await db.RefreshTokens
                .Where(t => t.UserId == user.Id && (t.ExpiresAt < cleanupCutoff || (t.RevokedAt != null && t.RevokedAt < cleanupCutoff)))
                .ToListAsync();
            if (staleTokens.Count > 0)
            {
                db.RefreshTokens.RemoveRange(staleTokens);
            }

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                CreatedAt = now.UtcDateTime,
                ExpiresAt = now.AddDays(RefreshTokenDays).UtcDateTime,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim()[..Math.Min(deviceName.Trim().Length, 150)]
            });

            await db.SaveChangesAsync();

            return new AuthTokenPair
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                ExpiresAt = expiresAt
            };
        }

        public async Task<AuthTokenPair?> RefreshTokensAsync(string refreshToken, string? deviceName = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var tokenHash = HashToken(refreshToken.Trim());
            var nowUtc = DateTime.UtcNow;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (existing == null || existing.RevokedAt != null || existing.ExpiresAt <= nowUtc)
                return null;

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId);
            if (user == null)
                return null;

            // Rotate refresh token: revoke old one and issue a new pair
            existing.RevokedAt = nowUtc;

            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddMinutes(AccessTokenMinutes);
            var accessToken = CreateAccessToken(user, now, expiresAt);
            var newRawRefreshToken = GenerateOpaqueToken();
            var newHash = HashToken(newRawRefreshToken);

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHash,
                CreatedAt = now.UtcDateTime,
                ExpiresAt = now.AddDays(RefreshTokenDays).UtcDateTime,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? existing.DeviceName : deviceName.Trim()[..Math.Min(deviceName.Trim().Length, 150)]
            });

            await db.SaveChangesAsync();

            return new AuthTokenPair
            {
                AccessToken = accessToken,
                RefreshToken = newRawRefreshToken,
                ExpiresAt = expiresAt
            };
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var tokenHash = HashToken(refreshToken.Trim());
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            if (existing == null || existing.RevokedAt != null)
                return false;

            existing.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        }

        public ClaimsPrincipal? ValidateAccessToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            try
            {
                var headerBytes = Base64UrlDecode(parts[0]);
                var payloadBytes = Base64UrlDecode(parts[1]);
                var providedSignature = Base64UrlDecode(parts[2]);

                using var headerDoc = JsonDocument.Parse(headerBytes);
                if (!headerDoc.RootElement.TryGetProperty("alg", out var algProp) ||
                    !string.Equals(algProp.GetString(), "HS256", StringComparison.Ordinal))
                {
                    return null;
                }

                var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
                var expectedSignature = ComputeHmacSha256(signingInput, GetSigningKey());

                if (providedSignature.Length != expectedSignature.Length ||
                    !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
                {
                    return null;
                }

                using var payloadDoc = JsonDocument.Parse(payloadBytes);
                var root = payloadDoc.RootElement;

                if (!root.TryGetProperty("iss", out var issProp) ||
                    !string.Equals(issProp.GetString(), Issuer, StringComparison.Ordinal))
                {
                    return null;
                }

                if (!root.TryGetProperty("aud", out var audProp) ||
                    !string.Equals(audProp.GetString(), Audience, StringComparison.Ordinal))
                {
                    return null;
                }

                if (!root.TryGetProperty("exp", out var expProp) || !expProp.TryGetInt64(out var expUnix))
                {
                    return null;
                }

                var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // Allow 30 seconds of clock skew
                if (expUnix + 30 < nowUnix)
                {
                    return null;
                }

                var sub = root.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;
                var username = root.TryGetProperty("unique_name", out var nameProp) ? nameProp.GetString() : null;
                var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                var role = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : "Admin";

                if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(username))
                {
                    return null;
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, sub),
                    new(ClaimTypes.Name, username),
                    new(ClaimTypes.Role, role ?? "Admin")
                };

                if (!string.IsNullOrWhiteSpace(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }

                var identity = new ClaimsIdentity(claims, JwtBearerAuthenticationHandler.SchemeName);
                return new ClaimsPrincipal(identity);
            }
            catch
            {
                return null;
            }
        }

        private string CreateAccessToken(User user, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
        {
            var headerJson = JsonSerializer.Serialize(new
            {
                alg = "HS256",
                typ = "JWT"
            });

            var payloadJson = JsonSerializer.Serialize(new
            {
                sub = user.Id.ToString(),
                unique_name = user.Username,
                email = user.Email,
                role = "Admin",
                jti = Guid.NewGuid().ToString("N"),
                iss = Issuer,
                aud = Audience,
                iat = issuedAt.ToUnixTimeSeconds(),
                exp = expiresAt.ToUnixTimeSeconds()
            });

            var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signingInput = $"{encodedHeader}.{encodedPayload}";
            var signature = ComputeHmacSha256(Encoding.ASCII.GetBytes(signingInput), GetSigningKey());

            return $"{signingInput}.{Base64UrlEncode(signature)}";
        }

        private byte[] GetSigningKey()
        {
            if (_cachedSigningKey != null)
                return _cachedSigningKey;

            lock (_keyLock)
            {
                if (_cachedSigningKey != null)
                    return _cachedSigningKey;

                var configuredSecret = _configuration["JwtSettings:SecretKey"];
                if (!string.IsNullOrWhiteSpace(configuredSecret))
                {
                    var rawBytes = Encoding.UTF8.GetBytes(configuredSecret.Trim());
                    _cachedSigningKey = rawBytes.Length >= 32 ? rawBytes : SHA256.HashData(rawBytes);
                    return _cachedSigningKey;
                }

                // Auto-generate and persist a 512-bit signing secret in system_settings so deployments work out of the box
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var setting = db.SystemSettings.Find(SecretSettingKey);

                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    try
                    {
                        var decoded = Convert.FromBase64String(setting.Value);
                        if (decoded.Length >= 32)
                        {
                            _cachedSigningKey = decoded;
                            return _cachedSigningKey;
                        }
                    }
                    catch
                    {
                        // Regenerate below if corrupted
                    }
                }

                var generated = RandomNumberGenerator.GetBytes(64);
                var encoded = Convert.ToBase64String(generated);

                if (setting == null)
                {
                    db.SystemSettings.Add(new SystemSetting
                    {
                        Key = SecretSettingKey,
                        Value = encoded
                    });
                }
                else
                {
                    setting.Value = encoded;
                }

                db.SaveChanges();
                _cachedSigningKey = generated;
                return _cachedSigningKey;
            }
        }

        private static byte[] ComputeHmacSha256(byte[] data, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        private static string GenerateOpaqueToken()
        {
            return Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        }

        private static string HashToken(string rawToken)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
                case 0: break;
                default: throw new FormatException("Invalid Base64Url length.");
            }

            return Convert.FromBase64String(output);
        }
    }
}
