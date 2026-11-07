using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Controllers
{
    [Authorize]
    public class StoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Stories/Index
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalStories = await _context.Stories.CountAsync();
            ViewBag.BeginnerStories = await _context.Stories.CountAsync(s => s.Level == WordLevel.A1 || s.Level == WordLevel.A2);
            ViewBag.IntermediateStories = await _context.Stories.CountAsync(s => s.Level == WordLevel.B1 || s.Level == WordLevel.B2);
            ViewBag.AdvancedStories = await _context.Stories.CountAsync(s => s.Level == WordLevel.C1 || s.Level == WordLevel.C2);

            return View();
        }

        // GET: Stories/GetStories (AJAX dynamic listing — paginated)
        [HttpGet]
        public async Task<IActionResult> GetStories(
            string? search, WordLevel? level,
            int page = 1, int pageSize = 50)
        {
            var query = _context.Stories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower().Trim();
                query = query.Where(s => s.Title.ToLower().Contains(searchLower) || s.Content.ToLower().Contains(searchLower));
            }

            if (level.HasValue)
            {
                query = query.Where(s => s.Level == level.Value);
            }

            var totalCount = await query.CountAsync();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var stories = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = stories.Select(s => new
            {
                s.Id,
                s.Title,
                s.Content,
                Level = s.Level.ToString(),
                CreatedAt = s.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                CreatedDateShort = s.CreatedAt.ToLocalTime().ToString("dd MMM yyyy")
            });

            return Json(new
            {
                items = result,
                hasMore = (page * pageSize) < totalCount,
                totalCount
            });
        }

        // GET: Stories/GetStats
        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            return Json(new
            {
                total = await _context.Stories.CountAsync(),
                beginner = await _context.Stories.CountAsync(s => s.Level == WordLevel.A1 || s.Level == WordLevel.A2),
                intermediate = await _context.Stories.CountAsync(s => s.Level == WordLevel.B1 || s.Level == WordLevel.B2),
                advanced = await _context.Stories.CountAsync(s => s.Level == WordLevel.C1 || s.Level == WordLevel.C2)
            });
        }

        // GET: Stories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            return View(story);
        }

        // GET: Stories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Stories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Story story)
        {
            if (ModelState.IsValid)
            {
                story.CreatedAt = DateTime.UtcNow;
                _context.Add(story);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(story);
        }

        // GET: Stories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            return View(story);
        }

        // POST: Stories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Story story)
        {
            if (id != story.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingStory = await _context.Stories.FindAsync(id);
                    if (existingStory == null) return NotFound();

                    existingStory.Title = story.Title;
                    existingStory.Level = story.Level;
                    existingStory.Content = story.Content;

                    _context.Update(existingStory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StoryExists(story.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Details), new { id = story.Id });
            }
            return View(story);
        }

        // POST: Stories/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story != null)
            {
                _context.Stories.Remove(story);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Hikaye bulunamadı." });
        }

        private bool StoryExists(int id)
        {
            return _context.Stories.Any(e => e.Id == id);
        }
    }
}
