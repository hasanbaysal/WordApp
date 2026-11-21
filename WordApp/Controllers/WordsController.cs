using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Controllers
{
    [Authorize]
    public class WordsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WordsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Words/Index (Dashboard View)
        public async Task<IActionResult> Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Fetch summary stats
            ViewBag.TotalWords = await _context.Words.CountAsync();
            ViewBag.LearnedWords = await _context.Words.CountAsync(w => w.IsLearned);
            ViewBag.DueWords = await _context.Words.CountAsync(w => w.IsLearned && w.NextReviewDate != null && w.NextReviewDate.Value <= today);
            ViewBag.UnlearnedWords = await _context.Words.CountAsync(w => !w.IsLearned);

            return View();
        }

        // GET: Words/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return NotFound();

            return View(wordItem);
        }

        // GET: Words/GetWords (AJAX dynamic listing — paginated)
        [HttpGet]
        public async Task<IActionResult> GetWords(
            string? search, WordType? type, WordLevel? level,
            bool? isLearned, bool? dueOnly,
            int page = 1, int pageSize = 100, bool random = false)
        {
            var query = _context.Words.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower().Trim();
                query = query.Where(w => w.Word.ToLower().Contains(searchLower));
            }

            if (isLearned.HasValue)
                query = query.Where(w => w.IsLearned == isLearned.Value);

            if (dueOnly.HasValue && dueOnly.Value)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                query = query.Where(w => w.IsLearned && w.NextReviewDate != null && w.NextReviewDate.Value <= today);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            if (random) { page = 1; pageSize = 1; }

            int totalCount;
            List<WordItem> words;

            if (_context.Database.IsNpgsql())
            {
                using var formsFilter = BuildFormsFilter(type, level);
                if (formsFilter != null)
                    query = query.Where(w => EF.Functions.JsonContains(w.FormsJson, formsFilter));

                totalCount = await query.CountAsync();

                var orderedQuery = random
                    ? query.OrderBy(w => EF.Functions.Random())
                    : query.OrderBy(w => w.Word);
                words = await orderedQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            else
            {
                // InMemory fallback for local development (where PostgreSQL jsonb/random functions aren't available)
                IEnumerable<WordItem> inMemoryItems = await query.ToListAsync();

                if (type.HasValue || level.HasValue)
                {
                    inMemoryItems = inMemoryItems.Where(w =>
                        w.GetForms().Any(f =>
                            (!type.HasValue || f.Type == type.Value) &&
                            (!level.HasValue || f.Level == level.Value)));
                }

                var filteredList = inMemoryItems.ToList();
                totalCount = filteredList.Count;

                var orderedItems = random
                    ? filteredList.OrderBy(_ => Random.Shared.Next())
                    : filteredList.OrderBy(w => w.Word, StringComparer.OrdinalIgnoreCase);

                words = orderedItems
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            var today2 = DateOnly.FromDateTime(DateTime.Today);

            var result = words.Select(w => new
            {
                w.Id,
                w.Word,
                Forms = w.GetForms().Select(f => new
                {
                    Type = f.Type.ToString(),
                    TypeLabel = f.Type.ToTurkishLabel(),
                    Level = f.Level.ToString()
                }).ToList(),
                w.UkMp3,
                w.UsMp3,
                w.IsLearned,
                w.Notes,
                NextReviewDate = w.NextReviewDate?.ToString("yyyy-MM-dd"),
                w.IntervalDays,
                IsDue = w.IsLearned && w.NextReviewDate != null && w.NextReviewDate.Value <= today2
            });

            return Json(new
            {
                items = result,
                hasMore = !random && (page * pageSize) < totalCount,
                totalCount
            });
        }

        // GET: Words/GetStats (lightweight stats endpoint for metric cards)
        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return Json(new
            {
                total    = await _context.Words.CountAsync(),
                learned  = await _context.Words.CountAsync(w => w.IsLearned),
                due      = await _context.Words.CountAsync(w => w.IsLearned && w.NextReviewDate != null && w.NextReviewDate.Value <= today),
                unlearned = await _context.Words.CountAsync(w => !w.IsLearned)
            });
        }

        // GET: Words/Create
        public IActionResult Create()
        {
            var wordItem = new WordItem();
            wordItem.SetForms(new[]
            {
                new WordForm { Type = WordType.Noun, Level = WordLevel.A1 }
            });
            return View(wordItem);
        }

        // POST: Words/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WordItem wordItem, List<WordForm>? forms)
        {
            ModelState.Remove(nameof(WordItem.FormsJson));

            wordItem.Word = (wordItem.Word ?? string.Empty).Trim();
            var normalizedForms = WordFormSerializer.Normalize(forms);
            wordItem.SetForms(normalizedForms);

            if (normalizedForms.Count == 0)
            {
                ModelState.AddModelError("Forms", "En az bir tür ve seviye formu eklemelisiniz.");
            }

            if (!string.IsNullOrEmpty(wordItem.Word) && await _context.Words.AnyAsync(w => w.Word == wordItem.Word))
            {
                ModelState.AddModelError(nameof(WordItem.Word), "Bu kelime sistemde zaten kayıtlı. Yeni form eklemek için mevcut kelimeyi düzenleyebilirsiniz.");
            }

            if (ModelState.IsValid)
            {
                wordItem.CreatedAt = DateTime.UtcNow;
                wordItem.IsLearned = false; // Initially unlearned
                wordItem.NextReviewDate = null;
                wordItem.IntervalDays = 0;

                _context.Add(wordItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(wordItem);
        }

        // GET: Words/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return NotFound();

            return View(wordItem);
        }

        // POST: Words/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WordItem wordItem, List<WordForm>? forms)
        {
            if (id != wordItem.Id) return NotFound();

            ModelState.Remove(nameof(WordItem.FormsJson));

            wordItem.Word = (wordItem.Word ?? string.Empty).Trim();
            var normalizedForms = WordFormSerializer.Normalize(forms);
            wordItem.SetForms(normalizedForms);

            if (normalizedForms.Count == 0)
            {
                ModelState.AddModelError("Forms", "En az bir tür ve seviye formu eklemelisiniz.");
            }

            if (!string.IsNullOrEmpty(wordItem.Word) && await _context.Words.AnyAsync(w => w.Id != id && w.Word == wordItem.Word))
            {
                ModelState.AddModelError(nameof(WordItem.Word), "Bu kelime sistemde başka bir kayıtta zaten mevcut.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingWord = await _context.Words.FindAsync(id);
                    if (existingWord == null) return NotFound();

                    existingWord.Word = wordItem.Word;
                    existingWord.SetForms(normalizedForms);
                    existingWord.UkMp3 = wordItem.UkMp3;
                    existingWord.UsMp3 = wordItem.UsMp3;
                    existingWord.Notes = wordItem.Notes;

                    _context.Update(existingWord);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WordItemExists(wordItem.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(wordItem);
        }

        // POST: Words/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem != null)
            {
                _context.Words.Remove(wordItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Kelime bulunamadı." });
        }

        // POST: Words/MarkAsLearned/5
        [HttpPost]
        public async Task<IActionResult> MarkAsLearned(int id, bool enableReview = true)
        {
            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return Json(new { success = false, message = "Kelime bulunamadı." });

            wordItem.IsLearned = true;
            if (enableReview)
            {
                wordItem.IntervalDays = 1; // Start with 1 day review cycle
                wordItem.NextReviewDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            }
            else
            {
                wordItem.IntervalDays = 0;
                wordItem.NextReviewDate = null;
            }
            wordItem.LastReviewedAt = null;

            _context.Update(wordItem);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                enableReview,
                nextReviewDate = wordItem.NextReviewDate?.ToString("yyyy-MM-dd")
            });
        }

        // POST: Words/Unlearn/5
        [HttpPost]
        public async Task<IActionResult> Unlearn(int id)
        {
            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return Json(new { success = false, message = "Kelime bulunamadı." });

            wordItem.IsLearned = false;
            wordItem.IntervalDays = 0;
            wordItem.NextReviewDate = null;
            wordItem.LastReviewedAt = null;

            _context.Update(wordItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: Words/SubmitReview
        [HttpPost]
        public async Task<IActionResult> SubmitReview(int id, int? score, int? days)
        {
            if (!ModelState.IsValid || (days.HasValue ? days < 1 || days > 3650 : !score.HasValue || score < 1 || score > 10))
            {
                return Json(new { success = false, message = "Puan 1–10, tekrar günü 1–3650 arasında tam sayı olmalıdır." });
            }

            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return Json(new { success = false, message = "Kelime bulunamadı." });

            if (!wordItem.IsLearned || wordItem.NextReviewDate == null)
                return Json(new { success = false, message = "Önce kelimeyi öğrenildi olarak işaretleyin ve tekrarı açın." });

            int newInterval = days ?? ReviewSchedule.DaysForScore(score!.Value);

            wordItem.IntervalDays = newInterval;
            wordItem.NextReviewDate = DateOnly.FromDateTime(DateTime.Today.AddDays(newInterval));
            wordItem.LastReviewedAt = DateTime.UtcNow;

            _context.Update(wordItem);
            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                nextReviewDate = wordItem.NextReviewDate?.ToString("yyyy-MM-dd"),
                intervalDays = newInterval
            });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleReview(int id)
        {
            var word = await _context.Words.FindAsync(id);
            if (word == null || !word.IsLearned)
                return Json(new { success = false, message = "Öğrenilmiş kelime bulunamadı." });

            if (word.NextReviewDate.HasValue)
            {
                word.NextReviewDate = null;
                word.IntervalDays = 0;
            }
            else
            {
                word.IntervalDays = 1;
                word.NextReviewDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            }
            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                reviewEnabled = word.NextReviewDate.HasValue,
                nextReviewDate = word.NextReviewDate?.ToString("yyyy-MM-dd")
            });
        }

        private static JsonDocument? BuildFormsFilter(WordType? type, WordLevel? level)
        {
            if (type.HasValue && level.HasValue)
            {
                return JsonDocument.Parse(
                    JsonSerializer.Serialize(new[]
                    {
                        new { type = (int)type.Value, level = (int)level.Value }
                    }));
            }

            if (type.HasValue)
            {
                return JsonDocument.Parse(
                    JsonSerializer.Serialize(new[]
                    {
                        new { type = (int)type.Value }
                    }));
            }

            if (level.HasValue)
            {
                return JsonDocument.Parse(
                    JsonSerializer.Serialize(new[]
                    {
                        new { level = (int)level.Value }
                    }));
            }

            return null;
        }

        private bool WordItemExists(int id)
        {
            return _context.Words.Any(e => e.Id == id);
        }
    }
}
