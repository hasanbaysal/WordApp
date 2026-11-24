using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WordApp.Models
{
    public static class StickyNoteConstants
    {
        public const string DailyNoteId = "system-daily-note";
        public const string DailyNoteTitle = "Günlük";
        public const string DefaultColor = "yellow";

        public static readonly HashSet<string> ValidColors = new(StringComparer.OrdinalIgnoreCase)
        {
            "yellow",
            "green",
            "pink",
            "purple",
            "blue",
            "charcoal"
        };

        public static string NormalizeColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return DefaultColor;

            var normalized = color.Trim().ToLowerInvariant();
            return ValidColors.Contains(normalized) ? normalized : DefaultColor;
        }
    }

    [Table("sticky_notes")]
    public class StickyNote
    {
        [Key]
        [MaxLength(100)]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("is_daily")]
        public bool IsDaily { get; set; }

        [MaxLength(20)]
        [Column("color")]
        public string Color { get; set; } = StickyNoteConstants.DefaultColor;

        [Column("plain_text")]
        public string PlainText { get; set; } = string.Empty;

        [Column("content_xaml")]
        public string? ContentXaml { get; set; }

        [Column("checklist", TypeName = "jsonb")]
        public string ChecklistJson { get; set; } = "[]";

        [Column("always_on_top")]
        public bool AlwaysOnTop { get; set; }

        [Column("reminder_remind_at")]
        public DateTime? ReminderRemindAt { get; set; }

        [Column("reminder_message")]
        public string? ReminderMessage { get; set; }

        [Column("reminder_dismissed")]
        public bool ReminderDismissed { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        public List<ChecklistItemDto> GetChecklist() => StickyNoteChecklistSerializer.Deserialize(ChecklistJson);

        public void SetChecklist(IEnumerable<ChecklistItemDto>? items)
        {
            ChecklistJson = StickyNoteChecklistSerializer.Serialize(items);
        }
    }

    public static class StickyNoteChecklistSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static List<ChecklistItemDto> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<ChecklistItemDto>();

            try
            {
                var items = JsonSerializer.Deserialize<List<ChecklistItemDto>>(json, SerializerOptions);
                return Normalize(items);
            }
            catch
            {
                return new List<ChecklistItemDto>();
            }
        }

        public static string Serialize(IEnumerable<ChecklistItemDto>? items)
        {
            var normalized = Normalize(items);
            return JsonSerializer.Serialize(normalized, SerializerOptions);
        }

        public static List<ChecklistItemDto> Normalize(IEnumerable<ChecklistItemDto>? items)
        {
            var result = new List<ChecklistItemDto>();
            if (items == null) return result;

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (item == null) continue;
                var id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id.Trim();
                if (!seenIds.Add(id))
                {
                    id = Guid.NewGuid().ToString();
                    seenIds.Add(id);
                }

                result.Add(new ChecklistItemDto
                {
                    Id = id,
                    Text = item.Text ?? string.Empty,
                    Done = item.Done
                });
            }

            return result;
        }
    }

    public class ChecklistItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    public class ReminderDto
    {
        [JsonPropertyName("remindAt")]
        public DateTimeOffset RemindAt { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("dismissed")]
        public bool Dismissed { get; set; }
    }

    public class NoteDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("isDaily")]
        public bool IsDaily { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = StickyNoteConstants.DefaultColor;

        [JsonPropertyName("plainText")]
        public string PlainText { get; set; } = string.Empty;

        [JsonPropertyName("contentXaml")]
        public string? ContentXaml { get; set; }

        [JsonPropertyName("checklist")]
        public List<ChecklistItemDto> Checklist { get; set; } = new();

        [JsonPropertyName("alwaysOnTop")]
        public bool AlwaysOnTop { get; set; }

        [JsonPropertyName("reminder")]
        public ReminderDto? Reminder { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        [JsonPropertyName("deletedAt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class NoteUpsertRequest
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("isDaily")]
        public bool? IsDaily { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("plainText")]
        public string? PlainText { get; set; }

        [JsonPropertyName("contentXaml")]
        public string? ContentXaml { get; set; }

        [JsonPropertyName("checklist")]
        public List<ChecklistItemDto>? Checklist { get; set; }

        [JsonPropertyName("alwaysOnTop")]
        public bool? AlwaysOnTop { get; set; }

        [JsonPropertyName("reminder")]
        public ReminderDto? Reminder { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
