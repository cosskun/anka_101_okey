using System;
using Okey101.Core.Enums;

namespace Okey101.Core.Config
{
    /// <summary>
    /// 101 Okey oyununun kurallarını ve mod konfigürasyonlarını tutan ayar sınıfı.
    /// ScriptableObject olarak Unity Inspector'ında düzenlenebilir veya koddan doğrudan oluşturulabilir.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        // 1. Mod Konfigürasyonları
        public MatchMode MatchMode { get; set; } = MatchMode.Solo;
        public ScoringRule ScoringRule { get; set; } = ScoringRule.Standard;

        // 2. Baraj Kuralları
        public int BaseMinScoreToOpen { get; set; } = 101;     // Katlamasız modda açılış puanı
        public int BaseMinPairsToOpen { get; set; } = 5;       // Çift açmak için gereken min çift sayısı

        // 3. Eşli Mod Kuralları
        /// <summary>
        /// Eşli modda; ortağı 101/çift açmış olan bir oyuncu, kendisi henüz açmamış olsa bile
        /// masadaki perlere (özellikle ortağının açtığı perlere) taş işleyebilir mi?
        /// </summary>
        public bool AllowTeammateProcessingWithoutOpening { get; set; } = false;

        // 4. Ceza ve Puan Kuralları
        public int UnopenedHandPenaltyStandard { get; set; } = 101; // Katlamasız modda el açamama cezası
        public int UnopenedHandPenaltyFolded { get; set; } = 202;   // Katlamalı modda el açamama cezası (x2)
        public int WinnerRewardStandard { get; set; } = -101;       // Normal bitiş ödülü
        public int WinnerRewardOkey { get; set; } = -202;           // Okey ile bitiş ödülü
        public int WrongDiscardPenalty { get; set; } = 101;         // Yere işler taş atma cezası

        public int OkeyMultiplier { get; set; } = 2;               // Okey ile bitiş ceza çarpanı
        public int PairsFinishMultiplier { get; set; } = 2;        // Çiftten bitiş ceza çarpanı

        // 5. Dağıtım Kuralları
        public int StartingPlayerTileCount { get; set; } = 22;      // Oyuna başlayan oyuncunun taş adedi
        public int OtherPlayersTileCount { get; set; } = 21;        // Diğer 3 oyuncunun taş adedi

        public GameSettings() { }

        public GameSettings(MatchMode matchMode, ScoringRule scoringRule)
        {
            MatchMode = matchMode;
            ScoringRule = scoringRule;
        }

        /// <summary>
        /// Seçili moda göre başlangıç ceza puanını döndürür.
        /// </summary>
        public int GetUnopenedPenalty()
        {
            return ScoringRule == ScoringRule.Folded 
                ? UnopenedHandPenaltyFolded 
                : UnopenedHandPenaltyStandard;
        }
    }
}
