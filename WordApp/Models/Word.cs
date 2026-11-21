using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordApp.Models
{
    [Table("words")]
    public class WordItem
    {
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Kelime alanı zorunludur.")]
        [Column("word")]
        public string Word { get; set; } = string.Empty;

        [Column("forms", TypeName = "jsonb")]
        public string FormsJson { get; set; } = "[]";

        [Column("uk_mp3")]
        public string? UkMp3 { get; set; }

        [Column("us_mp3")]
        public string? UsMp3 { get; set; }

        [Column("is_learned")]
        public bool IsLearned { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("next_review_date")]
        public DateOnly? NextReviewDate { get; set; }

        [Column("interval_days")]
        public int IntervalDays { get; set; } = 0;

        [Column("last_reviewed_at")]
        public DateTime? LastReviewedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<WordForm> GetForms() => WordFormSerializer.Deserialize(FormsJson);

        public void SetForms(IEnumerable<WordForm>? forms)
        {
            FormsJson = WordFormSerializer.Serialize(forms);
        }
    }
}
