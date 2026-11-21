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
    public class GrammarNotesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GrammarNotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: GrammarNotes/Index
        public async Task<IActionResult> Index()
        {
            var total = await _context.GrammarNotes.CountAsync();
            var maxOrder = await _context.GrammarNotes.MaxAsync(g => (int?)g.OrderNumber) ?? 0;
            var latestUpdated = await _context.GrammarNotes
                .OrderByDescending(g => g.UpdatedAt)
                .Select(g => (DateTime?)g.UpdatedAt)
                .FirstOrDefaultAsync();

            ViewBag.TotalNotes = total;
            ViewBag.NextOrderNumber = maxOrder + 1;
            ViewBag.LatestUpdated = latestUpdated.HasValue
                ? latestUpdated.Value.ToLocalTime().ToString("dd.MM.yyyy")
                : "-";

            return View();
        }

        // GET: GrammarNotes/GetNotes (AJAX dynamic listing — paginated)
        [HttpGet]
        public async Task<IActionResult> GetNotes(
            string? search, string? sort = "order_asc",
            int page = 1, int pageSize = 50)
        {
            var query = _context.GrammarNotes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmed = search.Trim();
                var searchLower = trimmed.ToLower();
                var numericPart = trimmed.TrimStart('#');
                var hasOrderFilter = int.TryParse(numericPart, out var orderFilter);

                if (hasOrderFilter)
                {
                    query = query.Where(g =>
                        g.OrderNumber == orderFilter ||
                        g.Title.ToLower().Contains(searchLower) ||
                        g.Content.ToLower().Contains(searchLower));
                }
                else
                {
                    query = query.Where(g =>
                        g.Title.ToLower().Contains(searchLower) ||
                        g.Content.ToLower().Contains(searchLower));
                }
            }

            var totalCount = await query.CountAsync();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            IOrderedQueryable<GrammarNote> orderedQuery = sort switch
            {
                "order_desc" => query.OrderByDescending(g => g.OrderNumber).ThenByDescending(g => g.Id),
                "updated_desc" => query.OrderByDescending(g => g.UpdatedAt).ThenBy(g => g.OrderNumber),
                _ => query.OrderBy(g => g.OrderNumber).ThenBy(g => g.Id)
            };

            var notes = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = notes.Select(g => new
            {
                g.Id,
                g.OrderNumber,
                g.Title,
                g.Content,
                CreatedAt = g.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                UpdatedAt = g.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            });

            return Json(new
            {
                items = result,
                hasMore = (page * pageSize) < totalCount,
                totalCount
            });
        }

        // GET: GrammarNotes/GetStats
        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var total = await _context.GrammarNotes.CountAsync();
            var maxOrder = await _context.GrammarNotes.MaxAsync(g => (int?)g.OrderNumber) ?? 0;
            var latestUpdated = await _context.GrammarNotes
                .OrderByDescending(g => g.UpdatedAt)
                .Select(g => (DateTime?)g.UpdatedAt)
                .FirstOrDefaultAsync();

            return Json(new
            {
                total,
                nextOrderNumber = maxOrder + 1,
                latestUpdated = latestUpdated.HasValue
                    ? latestUpdated.Value.ToLocalTime().ToString("dd.MM.yyyy")
                    : "-"
            });
        }

        // GET: GrammarNotes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var note = await _context.GrammarNotes.FindAsync(id);
            if (note == null) return NotFound();

            ViewBag.PreviousNote = await _context.GrammarNotes
                .Where(g => g.OrderNumber < note.OrderNumber || (g.OrderNumber == note.OrderNumber && g.Id < note.Id))
                .OrderByDescending(g => g.OrderNumber)
                .ThenByDescending(g => g.Id)
                .FirstOrDefaultAsync();

            ViewBag.NextNote = await _context.GrammarNotes
                .Where(g => g.OrderNumber > note.OrderNumber || (g.OrderNumber == note.OrderNumber && g.Id > note.Id))
                .OrderBy(g => g.OrderNumber)
                .ThenBy(g => g.Id)
                .FirstOrDefaultAsync();

            return View(note);
        }

        // GET: GrammarNotes/Create
        public async Task<IActionResult> Create()
        {
            var maxOrder = await _context.GrammarNotes.MaxAsync(g => (int?)g.OrderNumber) ?? 0;
            var model = new GrammarNote
            {
                OrderNumber = maxOrder + 1
            };
            return View(model);
        }

        // POST: GrammarNotes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GrammarNote grammarNote)
        {
            if (ModelState.IsValid)
            {
                var now = DateTime.UtcNow;
                grammarNote.Title = grammarNote.Title.Trim();
                grammarNote.CreatedAt = now;
                grammarNote.UpdatedAt = now;

                _context.Add(grammarNote);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(grammarNote);
        }

        // GET: GrammarNotes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var note = await _context.GrammarNotes.FindAsync(id);
            if (note == null) return NotFound();

            return View(note);
        }

        // POST: GrammarNotes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GrammarNote grammarNote)
        {
            if (id != grammarNote.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingNote = await _context.GrammarNotes.FindAsync(id);
                    if (existingNote == null) return NotFound();

                    existingNote.OrderNumber = grammarNote.OrderNumber;
                    existingNote.Title = grammarNote.Title.Trim();
                    existingNote.Content = grammarNote.Content;
                    existingNote.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existingNote);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrammarNoteExists(grammarNote.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Details), new { id = grammarNote.Id });
            }
            return View(grammarNote);
        }

        // POST: GrammarNotes/UpdateOrder
        [HttpPost]
        public async Task<IActionResult> UpdateOrder(int id, int orderNumber)
        {
            if (orderNumber < 1)
            {
                return Json(new { success = false, message = "Sıra numarası 1 veya daha büyük olmalıdır." });
            }

            var note = await _context.GrammarNotes.FindAsync(id);
            if (note == null)
            {
                return Json(new { success = false, message = "Gramer notu bulunamadı." });
            }

            note.OrderNumber = orderNumber;
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, orderNumber = note.OrderNumber });
        }

        // POST: GrammarNotes/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var note = await _context.GrammarNotes.FindAsync(id);
            if (note != null)
            {
                _context.GrammarNotes.Remove(note);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Gramer notu bulunamadı." });
        }

        private bool GrammarNoteExists(int id)
        {
            return _context.GrammarNotes.Any(e => e.Id == id);
        }
    }
}
