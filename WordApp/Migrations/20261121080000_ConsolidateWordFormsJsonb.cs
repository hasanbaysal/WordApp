using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordApp.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateWordFormsJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forms",
                table: "words",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(@"
                UPDATE words
                SET word = BTRIM(word)
                WHERE word <> BTRIM(word);
            ");

            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT
                        id,
                        word,
                        ROW_NUMBER() OVER (
                            PARTITION BY word
                            ORDER BY
                                is_learned DESC,
                                (next_review_date IS NOT NULL) DESC,
                                next_review_date ASC NULLS LAST,
                                (notes IS NOT NULL AND BTRIM(notes) <> '') DESC,
                                id ASC
                        ) AS rn
                    FROM words
                ),
                keepers AS (
                    SELECT id AS keep_id, word
                    FROM ranked
                    WHERE rn = 1
                ),
                distinct_forms AS (
                    SELECT DISTINCT word, type, level
                    FROM words
                ),
                forms_agg AS (
                    SELECT
                        word,
                        jsonb_agg(
                            jsonb_build_object('type', type, 'level', level)
                            ORDER BY level ASC, type ASC
                        ) AS forms
                    FROM distinct_forms
                    GROUP BY word
                ),
                distinct_notes AS (
                    SELECT word, BTRIM(notes) AS note, MIN(id) AS min_id
                    FROM words
                    WHERE notes IS NOT NULL AND BTRIM(notes) <> ''
                    GROUP BY word, BTRIM(notes)
                ),
                notes_agg AS (
                    SELECT
                        word,
                        string_agg(note, '<hr />' ORDER BY min_id ASC) AS merged_notes
                    FROM distinct_notes
                    GROUP BY word
                ),
                review_pick AS (
                    SELECT DISTINCT ON (word)
                        word,
                        next_review_date,
                        interval_days
                    FROM words
                    WHERE is_learned = TRUE AND next_review_date IS NOT NULL
                    ORDER BY word, next_review_date ASC, id ASC
                ),
                uk_pick AS (
                    SELECT DISTINCT ON (word)
                        word,
                        uk_mp3
                    FROM words
                    WHERE uk_mp3 IS NOT NULL AND BTRIM(uk_mp3) <> ''
                    ORDER BY word, id ASC
                ),
                us_pick AS (
                    SELECT DISTINCT ON (word)
                        word,
                        us_mp3
                    FROM words
                    WHERE us_mp3 IS NOT NULL AND BTRIM(us_mp3) <> ''
                    ORDER BY word, id ASC
                ),
                word_stats AS (
                    SELECT
                        word,
                        BOOL_OR(is_learned) AS is_learned,
                        MAX(last_reviewed_at) AS last_reviewed_at,
                        MIN(created_at) AS created_at
                    FROM words
                    GROUP BY word
                ),
                merged AS (
                    SELECT
                        k.keep_id,
                        fa.forms,
                        uk.uk_mp3,
                        us.us_mp3,
                        ws.is_learned,
                        na.merged_notes,
                        rp.next_review_date,
                        COALESCE(rp.interval_days, 0) AS interval_days,
                        ws.last_reviewed_at,
                        ws.created_at
                    FROM keepers k
                    JOIN forms_agg fa ON fa.word = k.word
                    JOIN word_stats ws ON ws.word = k.word
                    LEFT JOIN notes_agg na ON na.word = k.word
                    LEFT JOIN review_pick rp ON rp.word = k.word
                    LEFT JOIN uk_pick uk ON uk.word = k.word
                    LEFT JOIN us_pick us ON us.word = k.word
                )
                UPDATE words w
                SET
                    forms = m.forms,
                    uk_mp3 = m.uk_mp3,
                    us_mp3 = m.us_mp3,
                    is_learned = m.is_learned,
                    notes = m.merged_notes,
                    next_review_date = m.next_review_date,
                    interval_days = m.interval_days,
                    last_reviewed_at = m.last_reviewed_at,
                    created_at = m.created_at
                FROM merged m
                WHERE w.id = m.keep_id;
            ");

            migrationBuilder.Sql(@"
                DELETE FROM words
                WHERE jsonb_array_length(forms) = 0;
            ");

            migrationBuilder.DropColumn(
                name: "type",
                table: "words");

            migrationBuilder.DropColumn(
                name: "level",
                table: "words");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_words_forms ON words USING gin (forms jsonb_path_ops);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_words_word ON words (word);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_words_word;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_words_forms;");

            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "words",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "words",
                type: "integer",
                nullable: false,
                defaultValue: 11);

            migrationBuilder.Sql(@"
                UPDATE words
                SET
                    type = COALESCE((forms->0->>'type')::integer, 11),
                    level = COALESCE((forms->0->>'level')::integer, 0)
                WHERE jsonb_array_length(forms) > 0;
            ");

            migrationBuilder.DropColumn(
                name: "forms",
                table: "words");
        }
    }
}
