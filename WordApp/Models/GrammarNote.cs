using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordApp.Models
{
    [Table("grammar_notes")]
    public class GrammarNote
    {
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Sıra numarası zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Sıra numarası 1 veya daha büyük olmalıdır.")]
        [Display(Name = "Sıra Numarası")]
        [Column("order_number")]
        public int OrderNumber { get; set; } = 1;

        [Required(ErrorMessage = "Başlık alanı zorunludur.")]
        [Display(Name = "Başlık")]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "İçerik alanı zorunludur.")]
        [Display(Name = "İçerik")]
        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
