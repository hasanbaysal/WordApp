using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<string, (int Attempts, DateTime LockoutUntil)> _loginAttempts = new();

        public AccountController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Words");
            }

            GenerateCaptcha();
            ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Check Rate Limit / Lockout
            if (_loginAttempts.TryGetValue(ipAddress, out var attemptInfo))
            {
                if (attemptInfo.LockoutUntil > DateTime.UtcNow)
                {
                    ModelState.AddModelError(string.Empty, $"Çok fazla başarısız deneme yaptınız. Lütfen {Math.Ceiling((attemptInfo.LockoutUntil - DateTime.UtcNow).TotalMinutes)} dakika sonra tekrar deneyin.");
                    GenerateCaptcha();
                    ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                GenerateCaptcha();
                ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
                return View(model);
            }

            // Verify CAPTCHA
            var expectedAnswer = HttpContext.Session.GetString("CaptchaAnswer");
            if (string.IsNullOrEmpty(expectedAnswer) || 
                string.IsNullOrEmpty(model.CaptchaAnswer) || 
                model.CaptchaAnswer.Trim().ToLowerInvariant() != expectedAnswer.Trim().ToLowerInvariant())
            {
                ModelState.AddModelError("CaptchaAnswer", "Güvenlik sorusu cevabı yanlıştır.");
                GenerateCaptcha();
                ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
                return View(model);
            }

            // Verify User
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user != null)
            {
                var hasher = new PasswordHasher<User>();
                var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

                if (verificationResult == PasswordVerificationResult.Success)
                {
                    // Reset rate limiting attempts on successful credentials verification
                    _loginAttempts.TryRemove(ipAddress, out _);

                    // Generate a 5-digit code
                    var random = new Random();
                    var code = random.Next(10000, 99999).ToString();

                    // Store code, username and expiry in Session (2 minutes validity)
                    var expiryTime = DateTime.UtcNow.AddMinutes(2);
                    HttpContext.Session.SetString("2fa_code", code);
                    HttpContext.Session.SetString("2fa_username", user.Username);
                    HttpContext.Session.SetString("2fa_expiry", expiryTime.ToString("O"));

                    // Send email containing ONLY the 5-digit code
                    try
                    {
                        await Send2faEmailAsync(user.Email, code);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, $"Doğrulama e-postası gönderilirken hata oluştu: {ex.Message}");
                        GenerateCaptcha();
                        ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
                        return View(model);
                    }

                    return RedirectToAction("Verify2FA");
                }
            }

            // Login failed, track attempt
            int attempts = 1;
            DateTime lockoutUntil = DateTime.MinValue;

            if (_loginAttempts.TryGetValue(ipAddress, out attemptInfo))
            {
                attempts = attemptInfo.Attempts + 1;
                if (attempts >= 5)
                {
                    lockoutUntil = DateTime.UtcNow.AddMinutes(5); // Lock for 5 minutes
                    ModelState.AddModelError(string.Empty, "Çok fazla başarısız deneme. Giriş işleminiz 5 dakika boyunca engellendi.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Geçersiz kullanıcı adı veya şifre.");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Geçersiz kullanıcı adı veya şifre.");
            }

            _loginAttempts[ipAddress] = (attempts, lockoutUntil);

            GenerateCaptcha();
            ViewBag.CaptchaQuestion = HttpContext.Session.GetString("CaptchaQuestion");
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private void GenerateCaptcha()
        {
            var random = new Random();
            int puzzleType = random.Next(0, 4); // 4 types of captcha

            string question = "";
            string answer = "";

            switch (puzzleType)
            {
                case 0: // Complex Math
                    int a = random.Next(2, 9);
                    int b = random.Next(2, 9);
                    int c = random.Next(2, 7);
                    int mathType = random.Next(0, 3);
                    
                    if (mathType == 0)
                    {
                        question = $"Hesapla: ({a} + {b}) * {c} = ?";
                        answer = ((a + b) * c).ToString();
                    }
                    else if (mathType == 1)
                    {
                        question = $"Hesapla: ({a} - {b}) * {c} = ?";
                        answer = ((a - b) * c).ToString();
                    }
                    else
                    {
                        question = $"Hesapla: {a} * {b} - {c} = ?";
                        answer = (a * b - c).ToString();
                    }
                    break;

                case 1: // String Reverse
                    var wordsToReverse = new[] { "kelime", "ogren", "hafiza", "ezber", "tekrar", "hafiz", "ders" };
                    string chosenWord = wordsToReverse[random.Next(wordsToReverse.Length)];
                    char[] charArray = chosenWord.ToCharArray();
                    Array.Reverse(charArray);
                    
                    question = $"\"{chosenWord}\" kelimesini tersten yazın:";
                    answer = new string(charArray);
                    break;

                case 2: // Letter Extraction
                    var wordsForLetters = new[] { "VOCABULARY", "REPETITION", "DICTIONARY", "MEMORY", "RECALL", "ENGLISH" };
                    string targetWord = wordsForLetters[random.Next(wordsForLetters.Length)];
                    int letterPosition = random.Next(1, 6); // 1st to 5th letter
                    
                    if (letterPosition > targetWord.Length)
                    {
                        letterPosition = targetWord.Length;
                    }

                    question = $"\"{targetWord}\" kelimesinin {letterPosition}. harfi nedir?";
                    answer = targetWord[letterPosition - 1].ToString().ToLowerInvariant();
                    break;

                case 3: // Min/Max Number Selection
                    var numbers = new List<int>();
                    while (numbers.Count < 4)
                    {
                        int num = random.Next(1, 100);
                        if (!numbers.Contains(num))
                        {
                            numbers.Add(num);
                        }
                    }

                    bool askMin = random.Next(0, 2) == 0;
                    string numListStr = string.Join(", ", numbers);

                    if (askMin)
                    {
                        question = $"En küçük sayıyı yazın: {numListStr}";
                        answer = numbers.Min().ToString();
                    }
                    else
                    {
                        question = $"En büyük sayıyı yazın: {numListStr}";
                        answer = numbers.Max().ToString();
                    }
                    break;
            }

            HttpContext.Session.SetString("CaptchaQuestion", question);
            HttpContext.Session.SetString("CaptchaAnswer", answer);
        }

        [HttpGet]
        public IActionResult Verify2FA()
        {
            var sessionCode = HttpContext.Session.GetString("2fa_code");
            var sessionUsername = HttpContext.Session.GetString("2fa_username");
            var sessionExpiryStr = HttpContext.Session.GetString("2fa_expiry");

            if (string.IsNullOrEmpty(sessionCode) || string.IsNullOrEmpty(sessionUsername) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                return RedirectToAction("Login");
            }

            // Parse expiry
            if (DateTime.TryParse(sessionExpiryStr, out var expiry) && expiry < DateTime.UtcNow)
            {
                HttpContext.Session.Remove("2fa_code");
                HttpContext.Session.Remove("2fa_username");
                HttpContext.Session.Remove("2fa_expiry");
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(string code)
        {
            var sessionCode = HttpContext.Session.GetString("2fa_code");
            var sessionUsername = HttpContext.Session.GetString("2fa_username");
            var sessionExpiryStr = HttpContext.Session.GetString("2fa_expiry");

            if (string.IsNullOrEmpty(sessionCode) || string.IsNullOrEmpty(sessionUsername) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                ModelState.AddModelError(string.Empty, "Oturum süresi dolmuş veya geçersiz istek. Lütfen tekrar giriş yapın.");
                return RedirectToAction("Login");
            }

            if (!DateTime.TryParse(sessionExpiryStr, out var expiry) || expiry < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Doğrulama kodunun süresi (2 dakika) doldu. Lütfen tekrar giriş yapın.");
                HttpContext.Session.Remove("2fa_code");
                HttpContext.Session.Remove("2fa_username");
                HttpContext.Session.Remove("2fa_expiry");
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(code) || code.Trim() != sessionCode)
            {
                ModelState.AddModelError(string.Empty, "Girdiğiniz doğrulama kodu yanlıştır.");
                return View();
            }

            // Validation successful, sign in user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == sessionUsername);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı bulunamadı.");
                return RedirectToAction("Login");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Log in with a 10 days cookie
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(10)
            });

            // Clear 2FA session variables
            HttpContext.Session.Remove("2fa_code");
            HttpContext.Session.Remove("2fa_username");
            HttpContext.Session.Remove("2fa_expiry");

            return RedirectToAction("Index", "Words");
        }

        private async Task Send2faEmailAsync(string targetEmail, string code)
        {
            var smtpSettings = _configuration.GetSection("YandexSmtpSettings");
            var host = smtpSettings["Host"] ?? "smtp.yandex.com";
            var port = smtpSettings.GetValue<int>("Port", 587);
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("SMTP kullanıcı adı veya şifresi yapılandırılmamış.");
            }

            using var smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(username),
                Subject = "Dogrulama Kodu",
                Body = code,
                IsBodyHtml = false
            };

            var recipient = string.IsNullOrEmpty(targetEmail) || targetEmail == "your-yandex-email@yandex.com"
                ? username
                : targetEmail;

            mailMessage.To.Add(recipient);

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
