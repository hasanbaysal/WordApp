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

            if (!context.GrammarNotes.Any())
            {
                context.GrammarNotes.AddRange(
                    new GrammarNote
                    {
                        OrderNumber = 1,
                        Title = "Present Perfect vs. Past Simple",
                        Content = "## Ne Zaman Hangisi Kullanılır?\n\n**Present Perfect** geçmişte başlayıp bugüne etkisi süren veya zamanı belirtilmeyen durumlar için, **Past Simple** ise zamanı net olarak bitmiş geçmiş olaylar için kullanılır.\n\n### Yapı Karşılaştırması\n\n| Zaman | Formül | Örnek Cümle | Zaman İfadeleri |\n| --- | --- | --- | --- |\n| **Present Perfect** | `Subject + have/has + V3` | I **have visited** Rome twice. | `ever`, `never`, `already`, `yet`, `since`, `for` |\n| **Past Simple** | `Subject + V2` | I **visited** Rome in 2022. | `yesterday`, `last week`, `in 2022`, `ago` |\n\n### Sık Yapılan Hatalar\n\n- ~~I have seen him yesterday.~~ → **I saw him yesterday.**\n- ~~Did you ever been to London?~~ → **Have you ever been to London?**\n\n> **İpucu:** Cümlede bitmiş net bir zaman (*yesterday, last year, in 2020*) varsa mutlaka **Past Simple** kullan.",
                        CreatedAt = now.AddDays(-6),
                        UpdatedAt = now.AddDays(-2)
                    },
                    new GrammarNote
                    {
                        OrderNumber = 2,
                        Title = "Conditionals (If Clauses: Type 0, 1, 2, 3)",
                        Content = "## Koşul Cümleleri Özeti\n\nİngilizcede 4 temel koşul yapısı bulunur:\n\n| Tip | Kullanım | If Clause | Main Clause |\n| --- | --- | --- | --- |\n| **Type 0** | Genel doğrular | `Present Simple` | `Present Simple` |\n| **Type 1** | Gerçekleşebilir olasılık | `Present Simple` | `will + V1` |\n| **Type 2** | Hayali / şimdiki gerçek dışı | `Past Simple` | `would + V1` |\n| **Type 3** | Geçmiş pişmanlık | `Past Perfect` | `would have + V3` |\n\n### Örnekler\n\n1. **Type 0:** If you heat ice, it melts.\n2. **Type 1:** If it rains tomorrow, we will stay home.\n3. **Type 2:** If I **were** you, I would accept the offer.\n4. **Type 3:** If she had studied harder, she would have passed the exam.\n\n```text\nMixed Conditional (Geçmiş koşul -> Şimdiki sonuç):\nIf + Past Perfect, ... would + V1\nIf I had taken that job, I would be living in Berlin now.\n```",
                        CreatedAt = now.AddDays(-4),
                        UpdatedAt = now.AddDays(-1)
                    },
                    new GrammarNote
                    {
                        OrderNumber = 3,
                        Title = "Prepositions of Time: IN, ON, AT",
                        Content = "## Zaman Edatları Piramidi\n\nGenişten dara doğru **IN → ON → AT** kuralını aklında tut:\n\n### 1. `IN` (Geniş Zaman Dilimleri)\n- Yüzyıllar, yıllar, aylar, mevsimler: `in 2026`, `in July`, `in summer`\n- Günün bölümleri: `in the morning`, `in the afternoon`, `in the evening`\n\n### 2. `ON` (Belirli Günler ve Tarihler)\n- Haftanın günleri: `on Monday`, `on Fridays`\n- Tam tarihler: `on November 21st`, `on my birthday`\n\n### 3. `AT` (Kesin Saat ve Anlar)\n- Saatler: `at 08:30`, `at noon`, `at midnight`\n- Sabit kalıplar: `at night`, `at the weekend` *(UK)*\n\n> **Dikkat:** `next`, `last`, `this`, `every` kelimelerinden önce **in / on / at** kullanılmaz!\n> Örn: ~~on next Monday~~ → **next Monday**",
                        CreatedAt = now.AddDays(-2),
                        UpdatedAt = now
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
