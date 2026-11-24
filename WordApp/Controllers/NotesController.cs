using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordApp.Models;
using WordApp.Services;

namespace WordApp.Controllers
{
    [Authorize]
    public class NotesController : Controller
    {
        private readonly IStickyNoteService _noteService;

        public NotesController(IStickyNoteService noteService)
        {
            _noteService = noteService;
        }

        // GET: /Notes
        public async Task<IActionResult> Index()
        {
            var userId = await _noteService.ResolveUserIdAsync(User);
            await _noteService.EnsureDailyNoteExistsAsync(userId);
            ViewBag.CurrentUsername = User.Identity?.Name ?? "admin";
            return View();
        }

        // GET: /Notes/List
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = await _noteService.ResolveUserIdAsync(User);
            var notes = await _noteService.GetNotesAsync(userId, since: null, includeDeleted: false);

            var activeTasks = notes.Sum(n => n.Checklist.Count(c => !c.Done));
            var activeReminders = notes.Count(n => n.Reminder != null && !n.Reminder.Dismissed);
            var latestUpdated = notes.OrderByDescending(n => n.UpdatedAt).FirstOrDefault()?.UpdatedAt;

            return Json(new
            {
                notes,
                stats = new
                {
                    total = notes.Count,
                    activeTasks,
                    activeReminders,
                    latestUpdated
                }
            });
        }

        // POST: /Notes/Save
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] NoteUpsertRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Geçersiz not verisi." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            NoteDto saved;

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                saved = await _noteService.CreateAsync(userId, request, isWebEdit: true);
            }
            else
            {
                saved = await _noteService.UpsertAsync(userId, request.Id, request, isWebEdit: true);
            }

            return Json(new { success = true, note = saved });
        }

        // POST: /Notes/QuickColor
        [HttpPost]
        public async Task<IActionResult> QuickColor([FromBody] QuickColorRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest(new { success = false, message = "Geçersiz istek." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var updated = await _noteService.UpdateColorAsync(userId, request.Id, request.Color ?? StickyNoteConstants.DefaultColor);
            if (updated == null)
            {
                return NotFound(new { success = false, message = "Not bulunamadı." });
            }

            return Json(new { success = true, note = updated });
        }

        // POST: /Notes/TogglePin
        [HttpPost]
        public async Task<IActionResult> TogglePin([FromBody] NoteIdRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest(new { success = false, message = "Geçersiz istek." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var updated = await _noteService.ToggleAlwaysOnTopAsync(userId, request.Id);
            if (updated == null)
            {
                return NotFound(new { success = false, message = "Not bulunamadı." });
            }

            return Json(new { success = true, note = updated });
        }

        // POST: /Notes/ToggleChecklistItem
        [HttpPost]
        public async Task<IActionResult> ToggleChecklistItem([FromBody] ToggleChecklistItemRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.ItemId))
            {
                return BadRequest(new { success = false, message = "Geçersiz görev isteği." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var updated = await _noteService.ToggleChecklistItemAsync(userId, request.Id, request.ItemId, request.Done);
            if (updated == null)
            {
                return NotFound(new { success = false, message = "Not veya görev bulunamadı." });
            }

            return Json(new { success = true, note = updated });
        }

        // POST: /Notes/CompleteReminder
        [HttpPost]
        public async Task<IActionResult> CompleteReminder([FromBody] NoteIdRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest(new { success = false, message = "Geçersiz istek." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var updated = await _noteService.CompleteReminderAsync(userId, request.Id);
            if (updated == null)
            {
                return NotFound(new { success = false, message = "Not bulunamadı." });
            }

            return Json(new { success = true, note = updated });
        }

        // POST: /Notes/Delete
        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] NoteIdRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest(new { success = false, message = "Geçersiz not kimliği." });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            try
            {
                var deleted = await _noteService.DeleteAsync(userId, request.Id);
                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Not bulunamadı." });
                }

                return Json(new { success = true });
            }
            catch (DailyNoteDeleteForbiddenException ex)
            {
                return StatusCode(403, new
                {
                    success = false,
                    error = "DAILY_NOTE_CANNOT_BE_DELETED",
                    message = ex.Message
                });
            }
        }
    }

    public class NoteIdRequest
    {
        public string Id { get; set; } = string.Empty;
    }

    public class QuickColorRequest
    {
        public string Id { get; set; } = string.Empty;
        public string? Color { get; set; }
    }

    public class ToggleChecklistItemRequest
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public bool Done { get; set; }
    }
}
