using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordApp.Models;
using WordApp.Services;

namespace WordApp.Controllers.Api
{
    [ApiController]
    [Route("api/notes")]
    [Authorize(AuthenticationSchemes = JwtBearerAuthenticationHandler.SchemeName)]
    public class NotesApiController : ControllerBase
    {
        private readonly IStickyNoteService _noteService;

        public NotesApiController(IStickyNoteService noteService)
        {
            _noteService = noteService;
        }

        // GET /api/notes?since=<iso>&includeDeleted=true
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? since = null, [FromQuery] bool includeDeleted = false)
        {
            DateTimeOffset? sinceParsed = null;
            if (!string.IsNullOrWhiteSpace(since))
            {
                if (!DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                {
                    return BadRequest(new
                    {
                        error = "INVALID_SINCE_PARAMETER",
                        message = "since parametresi geçerli bir ISO-8601 tarih formatında olmalıdır."
                    });
                }
                sinceParsed = parsed;
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var notes = await _noteService.GetNotesAsync(userId, sinceParsed, includeDeleted);
            return Ok(notes);
        }

        // GET /api/notes/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDeleted = false)
        {
            var userId = await _noteService.ResolveUserIdAsync(User);
            var note = await _noteService.GetByIdAsync(userId, id, includeDeleted);
            if (note == null)
            {
                return NotFound(new
                {
                    error = "NOTE_NOT_FOUND",
                    message = "Not bulunamadı."
                });
            }

            return Ok(note);
        }

        // POST /api/notes
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NoteUpsertRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    error = "INVALID_REQUEST",
                    message = "Geçersiz not verisi."
                });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var created = await _noteService.CreateAsync(userId, request, isWebEdit: false);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT /api/notes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Upsert(string id, [FromBody] NoteUpsertRequest? request)
        {
            if (string.IsNullOrWhiteSpace(id) || request == null)
            {
                return BadRequest(new
                {
                    error = "INVALID_REQUEST",
                    message = "Geçersiz not kimliği veya verisi."
                });
            }

            var userId = await _noteService.ResolveUserIdAsync(User);
            var updated = await _noteService.UpsertAsync(userId, id, request, isWebEdit: false);
            return Ok(updated);
        }

        // DELETE /api/notes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = await _noteService.ResolveUserIdAsync(User);
            try
            {
                var deleted = await _noteService.DeleteAsync(userId, id);
                if (!deleted)
                {
                    return NotFound(new
                    {
                        error = "NOTE_NOT_FOUND",
                        message = "Silinecek not bulunamadı."
                    });
                }

                return NoContent();
            }
            catch (DailyNoteDeleteForbiddenException ex)
            {
                return StatusCode(403, new
                {
                    error = "DAILY_NOTE_CANNOT_BE_DELETED",
                    message = ex.Message
                });
            }
        }
    }
}
