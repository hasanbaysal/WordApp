using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Services
{
    public class NotificationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IServiceProvider serviceProvider, ILogger<NotificationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        private DateTime GetTurkeyTime()
        {
            TimeZoneInfo turkeyTimeZone;
            try
            {
                turkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    turkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    // Fallback manually to UTC+3 (Turkey offset is fixed UTC+3 since 2016)
                    turkeyTimeZone = TimeZoneInfo.CreateCustomTimeZone("Turkey Standard Time", TimeSpan.FromHours(3), "Turkey Standard Time", "Turkey Standard Time");
                }
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkeyTimeZone);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Notification Service starting.");

            // Wait 15 seconds after startup before checking to let migrations/seeding finish safely
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendNotificationAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in CheckAndSendNotificationAsync.");
                }

                // Check every 1 minute
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Background Notification Service stopping.");
        }

        private async Task CheckAndSendNotificationAsync(CancellationToken stoppingToken)
        {
            var turkeyTime = GetTurkeyTime();
            
            // Check if current time falls within:
            // 1. Morning Window: 09:00 - 09:15
            // 2. Evening Window: 19:30 - 19:45
            bool isMorningWindow = turkeyTime.Hour == 9 && turkeyTime.Minute >= 0 && turkeyTime.Minute < 15;
            bool isEveningWindow = turkeyTime.Hour == 19 && turkeyTime.Minute >= 30 && turkeyTime.Minute < 45;

            if (!isMorningWindow && !isEveningWindow)
            {
                return; // Exit early without hitting the database
            }

            string key = isMorningWindow ? "LastMorningNotificationSentDate" : "LastEveningNotificationSentDate";
            string windowName = isMorningWindow ? "Morning (09:00)" : "Evening (19:30)";
            var todayStr = turkeyTime.ToString("yyyy-MM-dd");

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var telegramSettings = configuration.GetSection("TelegramSettings");
                var enabled = telegramSettings.GetValue<bool>("Enabled");
                var botToken = telegramSettings.GetValue<string>("BotToken");
                var chatId = telegramSettings.GetValue<string>("ChatId");

                if (!enabled || string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
                {
                    _logger.LogDebug("Telegram Notification Service disabled or BotToken/ChatId not configured.");
                    return;
                }

                // Check if notification already processed for this window today
                var lastSentSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, stoppingToken);
                if (lastSentSetting != null && lastSentSetting.Value == todayStr)
                {
                    return;
                }

                var today = DateOnly.FromDateTime(turkeyTime);
                
                // Count words due for review today
                var dueCount = await context.Words.CountAsync(w => 
                    w.IsLearned && 
                    w.NextReviewDate != null && 
                    w.NextReviewDate.Value <= today, stoppingToken);

                if (dueCount > 0)
                {
                    _logger.LogInformation("Found {Count} due words in {Window} window. Sending Telegram notification...", dueCount, windowName);

                    using (var httpClient = new HttpClient())
                    {
                        var telegramUrl = $"https://api.telegram.org/bot{botToken}/sendMessage";
                        string message = $"📚 WordApp Tekrar Hatırlatıcı\n\nBugün tekrar etmeniz gereken {dueCount} adet kelime bulunmaktadır. İyi çalışmalar!";

                        var payload = JsonSerializer.Serialize(new
                        {
                            chat_id = chatId,
                            text = message
                        });

                        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                        var response = await httpClient.PostAsync(telegramUrl, content, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            if (lastSentSetting == null)
                            {
                                lastSentSetting = new SystemSetting { Key = key, Value = todayStr };
                                context.SystemSettings.Add(lastSentSetting);
                            }
                            else
                            {
                                lastSentSetting.Value = todayStr;
                            }
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation("Successfully sent Telegram review notification for {Window}. Count: {Count}", windowName, dueCount);
                        }
                        else
                        {
                            var errorBody = await response.Content.ReadAsStringAsync(stoppingToken);
                            _logger.LogError("Failed to send Telegram notification. Status: {Status}, Response: {Response}", response.StatusCode, errorBody);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No due words for today in {Window} window. Marking as checked.", windowName);
                    
                    // Mark as checked to prevent checking again during this window
                    if (lastSentSetting == null)
                    {
                        lastSentSetting = new SystemSetting { Key = key, Value = todayStr };
                        context.SystemSettings.Add(lastSentSetting);
                    }
                    else
                    {
                        lastSentSetting.Value = todayStr;
                    }
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }
}
