using System;
using System.Collections.Generic;
using System.Linq;
using WordApp.Models;

namespace WordApp.Data
{
    public static class DevDataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.UtcNow;

            if (!context.Words.Any())
            {
                var sampleWords = new List<WordItem>
                {
                    CreateWord(
                        "accomplish",
                        new[] { new WordForm { Type = WordType.Verb, Level = WordLevel.B2 } },
                        isLearned: true,
                        nextReviewDate: today,
                        intervalDays: 3,
                        lastReviewedAt: now.AddDays(-3),
                        notes: "<p><strong>Başarmak, üstesinden gelmek</strong></p><p>She accomplished all her goals ahead of schedule.</p>",
                        createdAt: now.AddDays(-12)),

                    CreateWord(
                        "run",
                        new[]
                        {
                            new WordForm { Type = WordType.Verb, Level = WordLevel.A1 },
                            new WordForm { Type = WordType.Noun, Level = WordLevel.A2 }
                        },
                        isLearned: true,
                        nextReviewDate: today.AddDays(-1),
                        intervalDays: 1,
                        lastReviewedAt: now.AddDays(-2),
                        notes: "<p><strong>Koşmak; işletmek; koşu</strong></p><p>He runs a small bakery near the station.</p>",
                        createdAt: now.AddDays(-20)),

                    CreateWord(
                        "light",
                        new[]
                        {
                            new WordForm { Type = WordType.Noun, Level = WordLevel.A1 },
                            new WordForm { Type = WordType.Adjective, Level = WordLevel.A2 },
                            new WordForm { Type = WordType.Verb, Level = WordLevel.B1 }
                        },
                        isLearned: true,
                        nextReviewDate: today.AddDays(5),
                        intervalDays: 7,
                        lastReviewedAt: now.AddDays(-2),
                        notes: "<p><strong>Işık; hafif / açık renk; yakmak</strong></p><p>Pack light clothes for the summer trip.</p>",
                        createdAt: now.AddDays(-15)),

                    CreateWord(
                        "resilient",
                        new[] { new WordForm { Type = WordType.Adjective, Level = WordLevel.B2 } },
                        isLearned: false,
                        nextReviewDate: null,
                        intervalDays: 0,
                        lastReviewedAt: null,
                        notes: "<p><strong>Dayanıklı, çabuk toparlanan</strong></p><p>Children are often more resilient than adults expect.</p>",
                        createdAt: now.AddDays(-8)),

                    CreateWord(
                        "ubiquitous",
                        new[] { new WordForm { Type = WordType.Adjective, Level = WordLevel.C1 } },
                        isLearned: false,
                        nextReviewDate: null,
                        intervalDays: 0,
                        lastReviewedAt: null,
                        notes: "<p><strong>Her yerde bulunan, çok yaygın</strong></p><p>Smartphones have become ubiquitous in modern life.</p>",
                        createdAt: now.AddDays(-6)),

                    CreateWord(
                        "nevertheless",
                        new[] { new WordForm { Type = WordType.Adverb, Level = WordLevel.B2 } },
                        isLearned: true,
                        nextReviewDate: null, // Öğrenildi ama tekrar kapalı durumu için
                        intervalDays: 0,
                        lastReviewedAt: now.AddDays(-10),
                        notes: "<p><strong>Buna rağmen, yine de</strong></p><p>The task was difficult; nevertheless, they finished on time.</p>",
                        createdAt: now.AddDays(-14)),

                    CreateWord(
                        "meticulous",
                        new[] { new WordForm { Type = WordType.Adjective, Level = WordLevel.C1 } },
                        isLearned: false,
                        nextReviewDate: null,
                        intervalDays: 0,
                        lastReviewedAt: null,
                        notes: "<p><strong>Titiz, ayrıntılara çok dikkat eden</strong></p><p>She kept meticulous notes throughout the experiment.</p>",
                        createdAt: now.AddDays(-5)),

                    CreateWord(
                        "book",
                        new[]
                        {
                            new WordForm { Type = WordType.Noun, Level = WordLevel.A1 },
                            new WordForm { Type = WordType.Verb, Level = WordLevel.A2 }
                        },
                        isLearned: true,
                        nextReviewDate: today.AddDays(12),
                        intervalDays: 14,
                        lastReviewedAt: now.AddDays(-2),
                        notes: "<p><strong>Kitap; rezervasyon yapmak</strong></p><p>We should book our flight tickets early.</p>",
                        createdAt: now.AddDays(-18)),

                    CreateWord(
                        "serendipity",
                        new[] { new WordForm { Type = WordType.Noun, Level = WordLevel.C2 } },
                        isLearned: false,
                        nextReviewDate: null,
                        intervalDays: 0,
                        lastReviewedAt: null,
                        notes: null, // Not eklenmemiş kart görünümünü test etmek için
                        createdAt: now.AddDays(-3)),

                    CreateWord(
                        "break down",
                        new[] { new WordForm { Type = WordType.Verb, Level = WordLevel.B1 } },
                        isLearned: true,
                        nextReviewDate: today,
                        intervalDays: 2,
                        lastReviewedAt: now.AddDays(-2),
                        notes: "<p><strong>Bozulmak; parçalara ayırmak</strong></p><p>Our car broke down on the highway.</p>",
                        createdAt: now.AddDays(-10))
                };

                context.Words.AddRange(sampleWords);
            }

            if (!context.Stories.Any())
            {
                context.Stories.AddRange(
                    new Story
                    {
                        Title = "A Quiet Morning in Lisbon",
                        Level = WordLevel.A2,
                        Content = "### Morning Coffee\n\nEvery morning, Clara walks down the narrow street to a **light** and cozy cafe near the square. She likes to **book** a small table by the window before the city wakes up.\n\n> \"Good mornings start with a slow breath and a warm cup.\"",
                        CreatedAt = now.AddDays(-7)
                    },
                    new Story
                    {
                        Title = "Building Resilient Habits",
                        Level = WordLevel.B2,
                        Content = "### Small Steps Matter\n\nLearning a language can feel overwhelming at first. **Nevertheless**, consistent daily practice helps you **accomplish** far more than occasional marathon sessions.\n\n- Review due words every morning\n- Read short stories slightly above your comfort zone\n- Stay **resilient** when you forget a word — forgetting is part of memory.",
                        CreatedAt = now.AddDays(-4)
                    },
                    new Story
                    {
                        Title = "The Ubiquitous Signal",
                        Level = WordLevel.C1,
                        Content = "### Noise and Focus\n\nNotifications have become **ubiquitous**, pulling our attention in dozens of directions at once. Through **meticulous** planning and a touch of **serendipity**, Leo discovered that turning off alerts for two hours each evening doubled his reading depth.",
                        CreatedAt = now.AddDays(-1)
                    });
            }

            context.SaveChanges();
        }

        private static WordItem CreateWord(
            string word,
            IEnumerable<WordForm> forms,
            bool isLearned,
            DateOnly? nextReviewDate,
            int intervalDays,
            DateTime? lastReviewedAt,
            string? notes,
            DateTime createdAt)
        {
            var item = new WordItem
            {
                Word = word,
                IsLearned = isLearned,
                NextReviewDate = nextReviewDate,
                IntervalDays = intervalDays,
                LastReviewedAt = lastReviewedAt,
                Notes = notes,
                CreatedAt = createdAt
            };
            item.SetForms(forms);
            return item;
        }
    }
}
