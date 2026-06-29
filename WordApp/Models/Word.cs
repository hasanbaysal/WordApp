using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordApp.Models
{
    [Table("words")]
    public class WordItem
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("word")]
        public string Word { get; set; } = string.Empty;

        [Column("type")]
        public WordType Type { get; set; }

        [Column("level")]
        public WordLevel Level { get; set; }

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
    }
}
