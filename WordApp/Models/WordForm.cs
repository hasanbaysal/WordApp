using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WordApp.Models
{
    public class WordForm
    {
        [JsonPropertyName("type")]
        public WordType Type { get; set; }

        [JsonPropertyName("level")]
        public WordLevel Level { get; set; }
    }

    public static class WordFormSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static List<WordForm> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<WordForm>();
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<WordForm>>(json, Options);
                return Normalize(list);
            }
            catch (JsonException)
            {
                return new List<WordForm>();
            }
        }

        public static string Serialize(IEnumerable<WordForm>? forms)
        {
            var normalized = Normalize(forms);
            return JsonSerializer.Serialize(normalized, Options);
        }

        public static List<WordForm> Normalize(IEnumerable<WordForm>? forms)
        {
            if (forms == null)
            {
                return new List<WordForm>();
            }

            return forms
                .Where(f => f != null && Enum.IsDefined(typeof(WordType), f.Type) && Enum.IsDefined(typeof(WordLevel), f.Level))
                .GroupBy(f => (f.Type, f.Level))
                .Select(g => new WordForm { Type = g.Key.Type, Level = g.Key.Level })
                .OrderBy(f => f.Level)
                .ThenBy(f => f.Type)
                .ToList();
        }
    }
}
