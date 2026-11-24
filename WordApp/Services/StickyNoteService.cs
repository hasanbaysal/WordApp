using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WordApp.Data;
using WordApp.Models;

namespace WordApp.Services
{
    public class DailyNoteDeleteForbiddenException : Exception
    {
        public DailyNoteDeleteForbiddenException()
            : base("Günlük notu silinemez.")
        {
        }
    }

    public interface IStickyNoteService
    {
        Task<int> ResolveUserIdAsync(ClaimsPrincipal principal);
        Task<StickyNote> EnsureDailyNoteExistsAsync(int userId);
        Task<List<NoteDto>> GetNotesAsync(int userId, DateTimeOffset? since = null, bool includeDeleted = false);
        Task<NoteDto?> GetByIdAsync(int userId, string id, bool includeDeleted = false);
        Task<NoteDto> CreateAsync(int userId, NoteUpsertRequest request, bool isWebEdit = false);
        Task<NoteDto> UpsertAsync(int userId, string id, NoteUpsertRequest request, bool isWebEdit = false);
        Task<NoteDto?> UpdateColorAsync(int userId, string id, string color);
        Task<NoteDto?> ToggleAlwaysOnTopAsync(int userId, string id);
        Task<NoteDto?> ToggleChecklistItemAsync(int userId, string id, string itemId, bool done);
        Task<NoteDto?> CompleteReminderAsync(int userId, string id);
        Task<bool> DeleteAsync(int userId, string id);
    }

    public class StickyNoteService : IStickyNoteService
    {
        private readonly ApplicationDbContext _context;

        public StickyNoteService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> ResolveUserIdAsync(ClaimsPrincipal principal)
        {
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(idClaim) && int.TryParse(idClaim, out var parsedId) && parsedId > 0)
            {
                return parsedId;
            }

            var username = principal.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user != null) return user.Id;
            }

            return await _context.Users.Select(u => u.Id).FirstOrDefaultAsync();
        }

        public async Task<StickyNote> EnsureDailyNoteExistsAsync(int userId)
        {
            var daily = await _context.StickyNotes
                .FirstOrDefaultAsync(n => n.Id == StickyNoteConstants.DailyNoteId);

            if (daily == null)
            {
                var now = DateTime.UtcNow;
                daily = new StickyNote
                {
                    Id = StickyNoteConstants.DailyNoteId,
                    UserId = userId,
                    Title = StickyNoteConstants.DailyNoteTitle,
                    IsDaily = true,
                    Color = StickyNoteConstants.DefaultColor,
                    PlainText = string.Empty,
                    ContentXaml = null,
                    ChecklistJson = "[]",
                    AlwaysOnTop = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                };
                _context.StickyNotes.Add(daily);
                await _context.SaveChangesAsync();
                return daily;
            }

            var needsRepair = false;
            if (daily.IsDeleted)
            {
                daily.IsDeleted = false;
                daily.DeletedAt = null;
                needsRepair = true;
            }
            if (!daily.IsDaily)
            {
                daily.IsDaily = true;
                needsRepair = true;
            }
            if (!string.Equals(daily.Title, StickyNoteConstants.DailyNoteTitle, StringComparison.Ordinal))
            {
                daily.Title = StickyNoteConstants.DailyNoteTitle;
                needsRepair = true;
            }
            if (daily.UserId == 0 && userId > 0)
            {
                daily.UserId = userId;
                needsRepair = true;
            }

            if (needsRepair)
            {
                await _context.SaveChangesAsync();
            }

            return daily;
        }

        public async Task<List<NoteDto>> GetNotesAsync(int userId, DateTimeOffset? since = null, bool includeDeleted = false)
        {
            await EnsureDailyNoteExistsAsync(userId);

            var query = _context.StickyNotes
                .Where(n => n.UserId == userId || n.UserId == 0);

            if (!includeDeleted)
            {
                query = query.Where(n => !n.IsDeleted);
            }

            if (since.HasValue)
            {
                var sinceUtc = since.Value.UtcDateTime;
                query = query.Where(n =>
                    n.UpdatedAt >= sinceUtc ||
                    (n.DeletedAt != null && n.DeletedAt >= sinceUtc));
            }

            var notes = await query
                .OrderByDescending(n => n.IsDaily)
                .ThenByDescending(n => n.UpdatedAt)
                .ToListAsync();

            return notes.Select(ToDto).ToList();
        }

        public async Task<NoteDto?> GetByIdAsync(int userId, string id, bool includeDeleted = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var trimmedId = id.Trim();
            if (string.Equals(trimmedId, StickyNoteConstants.DailyNoteId, StringComparison.Ordinal))
            {
                var daily = await EnsureDailyNoteExistsAsync(userId);
                return ToDto(daily);
            }

            var note = await _context.StickyNotes
                .FirstOrDefaultAsync(n => n.Id == trimmedId && (n.UserId == userId || n.UserId == 0));

            if (note == null || (!includeDeleted && note.IsDeleted))
                return null;

            return ToDto(note);
        }

        public async Task<NoteDto> CreateAsync(int userId, NoteUpsertRequest request, bool isWebEdit = false)
        {
            var id = string.IsNullOrWhiteSpace(request.Id)
                ? Guid.NewGuid().ToString()
                : request.Id.Trim();

            if (id.Length > 100)
                id = id[..100];

            return await UpsertInternalAsync(userId, id, request, isWebEdit);
        }

        public async Task<NoteDto> UpsertAsync(int userId, string id, NoteUpsertRequest request, bool isWebEdit = false)
        {
            var trimmedId = string.IsNullOrWhiteSpace(id)
                ? Guid.NewGuid().ToString()
                : id.Trim();

            if (trimmedId.Length > 100)
                trimmedId = trimmedId[..100];

            return await UpsertInternalAsync(userId, trimmedId, request, isWebEdit);
        }

        public async Task<NoteDto?> UpdateColorAsync(int userId, string id, string color)
        {
            var note = await FindEditableNoteAsync(userId, id);
            if (note == null) return null;

            note.Color = StickyNoteConstants.NormalizeColor(color);
            note.UpdatedAt = DateTime.UtcNow;
            // Rule 2: plainText did NOT change, so ContentXaml is preserved!
            await _context.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task<NoteDto?> ToggleAlwaysOnTopAsync(int userId, string id)
        {
            var note = await FindEditableNoteAsync(userId, id);
            if (note == null) return null;

            note.AlwaysOnTop = !note.AlwaysOnTop;
            note.UpdatedAt = DateTime.UtcNow;
            // Rule 2: plainText did NOT change, so ContentXaml is preserved!
            await _context.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task<NoteDto?> ToggleChecklistItemAsync(int userId, string id, string itemId, bool done)
        {
            var note = await FindEditableNoteAsync(userId, id);
            if (note == null) return null;

            var items = note.GetChecklist();
            var target = items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (target == null) return null;

            target.Done = done;
            note.SetChecklist(items);
            note.UpdatedAt = DateTime.UtcNow;
            // Rule 2: plainText did NOT change, so ContentXaml is preserved!
            await _context.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task<NoteDto?> CompleteReminderAsync(int userId, string id)
        {
            var note = await FindEditableNoteAsync(userId, id);
            if (note == null) return null;

            note.ReminderRemindAt = null;
            note.ReminderMessage = null;
            note.ReminderDismissed = false;
            note.UpdatedAt = DateTime.UtcNow;
            // Rule 2: plainText did NOT change, so ContentXaml is preserved!
            await _context.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task<bool> DeleteAsync(int userId, string id)
        {
            var trimmedId = (id ?? string.Empty).Trim();
            if (string.Equals(trimmedId, StickyNoteConstants.DailyNoteId, StringComparison.Ordinal))
            {
                throw new DailyNoteDeleteForbiddenException();
            }

            var note = await _context.StickyNotes
                .FirstOrDefaultAsync(n => n.Id == trimmedId && (n.UserId == userId || n.UserId == 0) && !n.IsDeleted);

            if (note == null)
                return false;

            var now = DateTime.UtcNow;
            note.IsDeleted = true;
            note.DeletedAt = now;
            note.UpdatedAt = now;

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<StickyNote?> FindEditableNoteAsync(int userId, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var trimmedId = id.Trim();

            if (string.Equals(trimmedId, StickyNoteConstants.DailyNoteId, StringComparison.Ordinal))
            {
                return await EnsureDailyNoteExistsAsync(userId);
            }

            return await _context.StickyNotes
                .FirstOrDefaultAsync(n => n.Id == trimmedId && (n.UserId == userId || n.UserId == 0) && !n.IsDeleted);
        }

        private async Task<NoteDto> UpsertInternalAsync(int userId, string id, NoteUpsertRequest request, bool isWebEdit)
        {
            var isDailyTarget = string.Equals(id, StickyNoteConstants.DailyNoteId, StringComparison.Ordinal);
            var existing = await _context.StickyNotes.FirstOrDefaultAsync(n => n.Id == id);
            var now = DateTime.UtcNow;

            var incomingPlainText = request.PlainText ?? existing?.PlainText ?? string.Empty;
            var incomingTitle = isDailyTarget
                ? StickyNoteConstants.DailyNoteTitle
                : (request.Title ?? existing?.Title ?? string.Empty).Trim();
            var incomingColor = StickyNoteConstants.NormalizeColor(request.Color ?? existing?.Color);
            var incomingAlwaysOnTop = request.AlwaysOnTop ?? existing?.AlwaysOnTop ?? false;
            var incomingChecklist = request.Checklist != null
                ? StickyNoteChecklistSerializer.Normalize(request.Checklist)
                : (existing != null ? existing.GetChecklist() : new List<ChecklistItemDto>());

            // Rule 2: contentXaml vs plainText preservation/invalidation
            string? resolvedContentXaml;
            if (isWebEdit)
            {
                if (existing == null)
                {
                    resolvedContentXaml = null;
                }
                else
                {
                    var plainTextChanged = !string.Equals(
                        NormalizeLineEndings(incomingPlainText),
                        NormalizeLineEndings(existing.PlainText),
                        StringComparison.Ordinal);

                    resolvedContentXaml = plainTextChanged ? null : existing.ContentXaml;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.ContentXaml))
                {
                    resolvedContentXaml = request.ContentXaml;
                }
                else if (existing != null)
                {
                    var plainTextChanged = !string.Equals(
                        NormalizeLineEndings(incomingPlainText),
                        NormalizeLineEndings(existing.PlainText),
                        StringComparison.Ordinal);

                    if (plainTextChanged || request.ContentXaml == string.Empty)
                    {
                        resolvedContentXaml = null;
                    }
                    else
                    {
                        resolvedContentXaml = existing.ContentXaml;
                    }
                }
                else
                {
                    resolvedContentXaml = null;
                }
            }

            // Rule 4: Reminder handling
            DateTime? reminderRemindAt = null;
            string? reminderMessage = null;
            bool reminderDismissed = false;

            if (request.Reminder != null && request.Reminder.RemindAt != default)
            {
                var incomingRemindUtc = request.Reminder.RemindAt.UtcDateTime;
                reminderRemindAt = incomingRemindUtc;
                reminderMessage = (request.Reminder.Message ?? string.Empty).Trim();

                var remindAtChanged = existing?.ReminderRemindAt == null ||
                    Math.Abs((existing.ReminderRemindAt.Value - incomingRemindUtc).TotalSeconds) >= 1;

                // When postponed to a new time from Web (or new reminder), reset dismissed = false
                reminderDismissed = isWebEdit && remindAtChanged
                    ? false
                    : request.Reminder.Dismissed;
            }

            if (existing == null)
            {
                var createdAt = request.CreatedAt?.UtcDateTime ?? now;
                var updatedAt = request.UpdatedAt?.UtcDateTime ?? now;

                var note = new StickyNote
                {
                    Id = id,
                    UserId = userId,
                    Title = incomingTitle,
                    IsDaily = isDailyTarget,
                    Color = incomingColor,
                    PlainText = incomingPlainText,
                    ContentXaml = resolvedContentXaml,
                    AlwaysOnTop = incomingAlwaysOnTop,
                    ReminderRemindAt = reminderRemindAt,
                    ReminderMessage = reminderMessage,
                    ReminderDismissed = reminderDismissed,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt,
                    IsDeleted = false,
                    DeletedAt = null
                };
                note.SetChecklist(incomingChecklist);

                _context.StickyNotes.Add(note);
                await _context.SaveChangesAsync();
                return ToDto(note);
            }
            else
            {
                existing.UserId = existing.UserId == 0 ? userId : existing.UserId;
                existing.Title = incomingTitle;
                existing.IsDaily = isDailyTarget;
                existing.Color = incomingColor;
                existing.PlainText = incomingPlainText;
                existing.ContentXaml = resolvedContentXaml;
                existing.SetChecklist(incomingChecklist);
                existing.AlwaysOnTop = incomingAlwaysOnTop;
                existing.ReminderRemindAt = reminderRemindAt;
                existing.ReminderMessage = reminderMessage;
                existing.ReminderDismissed = reminderDismissed;
                existing.IsDeleted = false;
                existing.DeletedAt = null;

                if (isWebEdit)
                {
                    existing.UpdatedAt = now;
                }
                else
                {
                    existing.UpdatedAt = request.UpdatedAt.HasValue && request.UpdatedAt.Value.UtcDateTime >= existing.UpdatedAt
                        ? request.UpdatedAt.Value.UtcDateTime
                        : now;
                }

                await _context.SaveChangesAsync();
                return ToDto(existing);
            }
        }

        private static string NormalizeLineEndings(string? value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n");
        }

        public static NoteDto ToDto(StickyNote note)
        {
            var createdUtc = DateTime.SpecifyKind(note.CreatedAt, DateTimeKind.Utc);
            var updatedUtc = DateTime.SpecifyKind(note.UpdatedAt, DateTimeKind.Utc);

            ReminderDto? reminder = null;
            if (note.ReminderRemindAt.HasValue)
            {
                var remindUtc = DateTime.SpecifyKind(note.ReminderRemindAt.Value, DateTimeKind.Utc);
                reminder = new ReminderDto
                {
                    RemindAt = new DateTimeOffset(remindUtc, TimeSpan.Zero),
                    Message = note.ReminderMessage ?? string.Empty,
                    Dismissed = note.ReminderDismissed
                };
            }

            DateTimeOffset? deletedAt = null;
            if (note.DeletedAt.HasValue)
            {
                var deletedUtc = DateTime.SpecifyKind(note.DeletedAt.Value, DateTimeKind.Utc);
                deletedAt = new DateTimeOffset(deletedUtc, TimeSpan.Zero);
            }

            return new NoteDto
            {
                Id = note.Id,
                Title = note.IsDaily ? StickyNoteConstants.DailyNoteTitle : (note.Title ?? string.Empty),
                IsDaily = note.IsDaily,
                Color = StickyNoteConstants.NormalizeColor(note.Color),
                PlainText = note.PlainText ?? string.Empty,
                ContentXaml = string.IsNullOrEmpty(note.ContentXaml) ? null : note.ContentXaml,
                Checklist = note.GetChecklist(),
                AlwaysOnTop = note.AlwaysOnTop,
                Reminder = reminder,
                CreatedAt = new DateTimeOffset(createdUtc, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(updatedUtc, TimeSpan.Zero),
                IsDeleted = note.IsDeleted,
                DeletedAt = deletedAt
            };
        }
    }
}
