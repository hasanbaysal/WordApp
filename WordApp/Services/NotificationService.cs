using System;
using System.Net.Http;
using System.Text;
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
                    await CheckAndSendNotificationAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in CheckAndSendNotificationAsync.");
                }

                // Check every hour
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Background Notification Service stopping.");
        }

        private async Task CheckAndSendNotificationAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var ntfySettings = configuration.GetSection("NtfySettings");
                var enabled = ntfySettings.GetValue<bool>("Enabled");
                var topicUrl = ntfySettings.GetValue<string>("TopicUrl");

                if (!enabled || string.IsNullOrEmpty(topicUrl))
                {
                    _logger.LogDebug("Notification Service disabled or TopicUrl not configured.");
                    return;
                }

                var todayStr = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

                // Check if notification already sent today
                var lastSentSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "LastNotificationSentDate");
                if (lastSentSetting != null && lastSentSetting.Value == todayStr)
                {
                    _logger.LogDebug("Notification already sent today.");
                    return;
                }

                var today = DateOnly.FromDateTime(DateTime.Today);
                // Count words due for review today
                var dueCount = await context.Words.CountAsync(w => 
                    w.IsLearned && 
                    w.NextReviewDate != null && 
                    w.NextReviewDate.Value <= today);

                if (dueCount > 0)
                {
                    _logger.LogInformation("Found {Count} due words. Sending ntfy notification...", dueCount);

                    using (var httpClient = new HttpClient())
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, topicUrl);
                        request.Headers.Add("Title", "WordApp Review Reminder");
                        request.Headers.Add("Priority", "3");
                        request.Headers.Add("Tags", "books,memo,brain");
                        
                        string message = $"Bugun tekrar etmeniz gereken {dueCount} adet kelime bulunmaktadir. Iyi calismalar!";
                        request.Content = new StringContent(message, Encoding.UTF8, "text/plain");

                        var response = await httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            if (lastSentSetting == null)
                            {
                                lastSentSetting = new SystemSetting { Key = "LastNotificationSentDate", Value = todayStr };
                                context.SystemSettings.Add(lastSentSetting);
                            }
                            else
                            {
                                lastSentSetting.Value = todayStr;
                            }
                            await context.SaveChangesAsync();
                            _logger.LogInformation("Successfully sent review notification for today. Count: {Count}", dueCount);
                        }
                        else
                        {
                            _logger.LogError("Failed to send ntfy notification. Status: {Status}", response.StatusCode);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No due words for today. Skipping notification.");
                }
            }
        }
    }
}
