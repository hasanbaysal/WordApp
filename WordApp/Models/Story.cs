using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordApp.Models
{
    [Table("stories")]
    public class Story
    {
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Başlık alanı zorunludur.")]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hikaye metni zorunludur.")]
        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("level")]
        public WordLevel Level { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
