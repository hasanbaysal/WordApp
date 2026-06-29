using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WordApp.Data;
using WordApp.Models;
using WordApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddHostedService<NotificationService>();

// Register DbContext with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .LogTo(Console.WriteLine, LogLevel.Warning));

// Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(10); // Persistent login: 10 days
        options.SlidingExpiration = true; // Sliding expiration: refreshed on every visit
    });

// Configure Session (for CAPTCHA and 2FA)
builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(10);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

var app = builder.Build();

// Automatically apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();

        var adminSettings = builder.Configuration.GetSection("AdminSettings");
        var username = adminSettings["Username"] ?? "admin";
        var password = adminSettings["Password"] ?? "WordAppSecure2026!";
        var email = adminSettings["Email"] ?? "your-yandex-email@yandex.com";

        var existingAdmin = context.Users.FirstOrDefault(u => u.Username == username);
        if (existingAdmin == null)
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            var user = new User { Username = username, Email = email };
            user.PasswordHash = hasher.HashPassword(user, password);
            context.Users.Add(user);
            context.SaveChanges();
        }
        else if (string.IsNullOrEmpty(existingAdmin.Email))
        {
            existingAdmin.Email = email;
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration/seeding.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// MapStaticAssets is used in newer templates to serve web assets. Let's make sure UseStaticFiles is also supported or used.
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Enable session before authentication

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Words}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
