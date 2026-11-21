namespace WordApp.Models
{
    public enum WordType
    {
        Adverb = 1,
        AuxiliaryVerb = 2,
        Conjunction = 3,
        DefiniteArticle = 4,
        Determiner = 5,
        Exclamation = 6,
        IndefiniteArticle = 7,
        InfinitiveMarker = 8,
        LinkingVerb = 9,
        ModalVerb = 10,
        Noun = 11,
        Number = 12,
        OrdinalNumber = 13,
        Preposition = 14,
        Pronoun = 15,
        Verb = 16,
        Adjective = 99
    }

    public enum WordLevel
    {
        A1,
        A2,
        B1,
        B2,
        C1,
        C2
    }

    public static class WordTypeExtensions
    {
        public static string ToTurkishLabel(this WordType type) => type switch
        {
            WordType.Adverb => "Zarf",
            WordType.AuxiliaryVerb => "Yardımcı Fiil",
            WordType.Conjunction => "Bağlaç",
            WordType.DefiniteArticle => "Definite Article (the)",
            WordType.Determiner => "Belirteç (Determiner)",
            WordType.Exclamation => "Ünlem",
            WordType.IndefiniteArticle => "Indefinite Article (a/an)",
            WordType.InfinitiveMarker => "Mastar Eki (to)",
            WordType.LinkingVerb => "Bağlama Fiili",
            WordType.ModalVerb => "Modal Fiili",
            WordType.Noun => "İsim",
            WordType.Number => "Sayı",
            WordType.OrdinalNumber => "Sıra Sayısı",
            WordType.Preposition => "Edat",
            WordType.Pronoun => "Zamir",
            WordType.Verb => "Fiil",
            WordType.Adjective => "Sıfat",
            _ => type.ToString()
        };
    }
}
