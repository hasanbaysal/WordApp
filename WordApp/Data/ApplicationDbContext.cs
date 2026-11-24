using Microsoft.EntityFrameworkCore;
using WordApp.Models;

namespace WordApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<WordItem> Words { get; set; }
        public DbSet<Story> Stories { get; set; }
        public DbSet<GrammarNote> GrammarNotes { get; set; }
        public DbSet<StickyNote> StickyNotes { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GrammarNote>()
                .HasIndex(g => g.OrderNumber)
                .HasDatabaseName("ix_grammar_notes_order_number");

            modelBuilder.Entity<StickyNote>()
                .HasIndex(n => new { n.UserId, n.IsDeleted, n.UpdatedAt })
                .HasDatabaseName("ix_sticky_notes_user_deleted_updated");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique()
                .HasDatabaseName("ix_refresh_tokens_token_hash");
        }
    }
}
