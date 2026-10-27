namespace Okey101.Core.Enums
{
    /// <summary>
    /// Taşların renk tanımları.
    /// </summary>
    public enum TileColor
    {
        Red = 0,     // Kırmızı
        Yellow = 1,  // Sarı
        Blue = 2,    // Mavi
        Black = 3,   // Siyah
        None = -1    // Geçersiz veya renksiz (Örn: Sahte okey ilk durum)
    }

    /// <summary>
    /// Eşli ve Tekli oyun modları.
    /// </summary>
    public enum MatchMode
    {
        Solo, // 4 Oyuncu bireysel yarışır (FFA)
        Coop  // 2v2 Eşli Takım Oyunu (0-2 Takım A, 1-3 Takım B)
    }

    /// <summary>
    /// Katlamalı veya Katlamasız (Standart) baraj ve ceza kuralı.
    /// </summary>
    public enum ScoringRule
    {
        Standard, // Katlamasız: 101 Puan / 5 Çift sabit baraj
        Folded    // Katlamalı: Masada açılan en yüksek puanı/çifti geçme zorunluluğu (+1)
    }

    /// <summary>
    /// Sıradaki oyuncunun tur aşaması.
    /// </summary>
    public enum TurnPhase
    {
        Draw,    // Desteden veya yan oyuncunun attığı taştan çekme aşaması
        Action,  // El açma, taş işleme veya el düzenleme aşaması
        Discard  // Yere bir taş atıp turu devretme aşaması
    }

    /// <summary>
    /// Per türleri.
    /// </summary>
    public enum PerType
    {
        Invalid,         // Geçersiz per
        RunSameColor,    // Aynı renk ardışık seri (Örn: Mavi 7-8-9 veya 11-12-13-1)
        GroupDiffColor,  // Aynı sayı farklı renkler (Örn: Kırmızı 5, Mavi 5, Siyah 5 - 3 veya 4 taş)
        Pair             // Çift (Aynı sayı ve aynı renk 2 taş)
    }

    /// <summary>
    /// Elin bitiş şekli (Ceza çarpanlarını etkiler).
    /// </summary>
    public enum EndRoundType
    {
        StandardFinish,     // Normal taş atarak bitiş (x1)
        OkeyFinish,         // Okey taşını bitiş taşı olarak atarak bitiş (x2 ceza)
        PairsFinish,        // Çiftten bitiş (x2 ceza)
        HandOutFinish,      // Elden bitiş (Hiç açmadan tek seferde bitme - x2 ceza)
        HandOutOkeyFinish,  // Elden Okey atarak bitiş (x4 ceza)
        HandOutPairsFinish, // Elden Çiftten bitiş (x4 ceza)
        DeckDepleted        // Deste bittiğinde kimse bitemeden sonlanma
    }
}
