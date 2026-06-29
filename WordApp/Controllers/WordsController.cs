using System;
using System.Linq;
using System.Net.Http;
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

        // GET: Words/GetWords (AJAX dynamic listing — paginated)
        [HttpGet]
        public async Task<IActionResult> GetWords(
            string? search, WordType? type, WordLevel? level,
            bool? isLearned, bool? dueOnly,
            int page = 1, int pageSize = 100)
        {
            var query = _context.Words.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower().Trim();
                query = query.Where(w => w.Word.ToLower().Contains(searchLower));
            }

            if (type.HasValue)
                query = query.Where(w => w.Type == type.Value);

            if (level.HasValue)
                query = query.Where(w => w.Level == level.Value);

            if (isLearned.HasValue)
                query = query.Where(w => w.IsLearned == isLearned.Value);

            if (dueOnly.HasValue && dueOnly.Value)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                query = query.Where(w => w.IsLearned && w.NextReviewDate != null && w.NextReviewDate.Value <= today);
            }

            var totalCount = await query.CountAsync();

            var words = await query
                .OrderBy(w => w.Word)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var today2 = DateOnly.FromDateTime(DateTime.Today);

            var result = words.Select(w => new
            {
                w.Id,
                w.Word,
                Type = w.Type.ToString(),
                Level = w.Level.ToString(),
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
                hasMore = (page * pageSize) < totalCount,
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
            return View();
        }

        // POST: Words/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WordItem wordItem)
        {
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
        public async Task<IActionResult> Edit(int id, WordItem wordItem)
        {
            if (id != wordItem.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingWord = await _context.Words.FindAsync(id);
                    if (existingWord == null) return NotFound();

                    existingWord.Word = wordItem.Word;
                    existingWord.Type = wordItem.Type;
                    existingWord.Level = wordItem.Level;
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
        public async Task<IActionResult> MarkAsLearned(int id)
        {
            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return Json(new { success = false, message = "Kelime bulunamadı." });

            wordItem.IsLearned = true;
            wordItem.IntervalDays = 1; // Start with 1 day review cycle
            wordItem.NextReviewDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            wordItem.LastReviewedAt = null;

            _context.Update(wordItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, nextReviewDate = wordItem.NextReviewDate?.ToString("yyyy-MM-dd") });
        }

        // POST: Words/SubmitReview
        [HttpPost]
        public async Task<IActionResult> SubmitReview(int id, int score)
        {
            if (score < 1 || score > 10)
            {
                return Json(new { success = false, message = "Geçersiz puan. Puan 1 ile 10 arasında olmalıdır." });
            }

            var wordItem = await _context.Words.FindAsync(id);
            if (wordItem == null) return Json(new { success = false, message = "Kelime bulunamadı." });

            // Calculate next review interval in days:
            // Linear mapping: Score 1 -> 1 day, Score 10 -> 15 days.
            // Formula: 1 + (score - 1) * (14.0 / 9.0)
            double calculatedInterval = 1.0 + (score - 1) * (14.0 / 9.0);
            int newInterval = (int)Math.Clamp(Math.Round(calculatedInterval), 1, 15);

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


        private bool WordItemExists(int id)
        {
            return _context.Words.Any(e => e.Id == id);
        }
    }
}
