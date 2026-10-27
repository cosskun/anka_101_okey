using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// Tur ve el sonu skor, ceza ve ödül hesaplamalarını yöneten sınıf.
    /// Eşli ve Tekli modları, Katlamalı/Katlamasız cezaları, Okey ve Çiftten bitiş çarpanlarını destekler.
    /// </summary>
    public class ScoreManager
    {
        private readonly GameSettings _settings;
        private readonly TeamManager _teamManager;

        // Oyuncu bazlı kümülatif skorlar (Ceza puanları pozitif, ödül puanları negatif)
        public int[] PlayerTotalScores { get; private set; }

        // Son turun detaylı ceza/puan raporu
        public class RoundScoreResult
        {
            public int WinnerPlayerIndex { get; set; } = -1;
            public EndRoundType EndType { get; set; }
            public int[] PlayerRoundPenalties { get; set; } = new int[4];
            public int[] TeamRoundPenalties { get; set; } = new int[2];
            public Dictionary<int, List<string>> PenaltyBreakdowns { get; set; } = new();
        }

        public ScoreManager(GameSettings settings, TeamManager teamManager)
        {
            _settings = settings ?? new GameSettings();
            _teamManager = teamManager ?? new TeamManager(_settings);
            PlayerTotalScores = new int[4];
        }

        public void ResetGameScores()
        {
            Array.Clear(PlayerTotalScores, 0, PlayerTotalScores.Length);
            _teamManager.ResetScores();
        }

        /// <summary>
        /// Bir tur tamamlandığında (bir oyuncu bittiğinde veya deste tükendiğinde)
        /// her oyuncunun ve takımın ceza/ödül puanlarını hesaplar.
        /// </summary>
        public RoundScoreResult CalculateRoundScores(
            int winnerPlayerIndex,
            EndRoundType endType,
            List<PlayerHand> playerHands,
            Dictionary<int, int> wrongDiscardPenalties = null)
        {
            var result = new RoundScoreResult
            {
                WinnerPlayerIndex = winnerPlayerIndex,
                EndType = endType
            };

            for (int i = 0; i < 4; i++)
            {
                result.PenaltyBreakdowns[i] = new List<string>();
            }

            int multiplier = 1;
            int reward = _settings.WinnerRewardStandard;

            switch (endType)
            {
                case EndRoundType.OkeyFinish:
                    multiplier = _settings.OkeyMultiplier;
                    reward = _settings.WinnerRewardOkey;
                    break;
                case EndRoundType.PairsFinish:
                    multiplier = _settings.PairsFinishMultiplier;
                    reward = _settings.WinnerRewardOkey;
                    break;
                case EndRoundType.HandOutFinish:
                    multiplier = 2;
                    reward = _settings.WinnerRewardOkey;
                    break;
                case EndRoundType.HandOutOkeyFinish:
                    multiplier = 4;
                    reward = _settings.WinnerRewardOkey * 2;
                    break;
                case EndRoundType.HandOutPairsFinish:
                    multiplier = 4;
                    reward = _settings.WinnerRewardOkey * 2;
                    break;
                default:
                    multiplier = 1;
                    reward = _settings.WinnerRewardStandard;
                    break;
            }

            int unopenedBasePenalty = _settings.GetUnopenedPenalty();

            for (int i = 0; i < 4; i++)
            {
                var hand = playerHands[i];
                int roundPenalty = 0;

                // 1. Biten Oyuncu Ödülü
                if (i == winnerPlayerIndex)
                {
                    roundPenalty += reward;
                    result.PenaltyBreakdowns[i].Add($"Bitiş Ödülü: {reward} puan (Bitiş Türü: {endType})");
                }
                else
                {
                    // 2. Diğer Oyuncuların Cezaları
                    if (!hand.HasOpenedHand)
                    {
                        // Hiç el açamamış oyuncu cezası
                        int unopenedPenalty = unopenedBasePenalty * multiplier;
                        roundPenalty += unopenedPenalty;
                        result.PenaltyBreakdowns[i].Add($"El Açamama Cezası: +{unopenedPenalty} ({unopenedBasePenalty} x {multiplier})");
                    }
                    else
                    {
                        // El açmış ama bitmemiş oyuncu (Elde kalan taşların toplamı)
                        // Çift açmış olan oyuncuların elde kalan taş toplamı ayrıca x2 ile çarpılır
                        int remainingTilesSum = hand.CalculateRemainingHandScore();
                        int effectiveMultiplier = hand.HasOpenedPairs ? multiplier * _settings.PairsFinishMultiplier : multiplier;
                        int remainingPenalty = remainingTilesSum * effectiveMultiplier;
                        roundPenalty += remainingPenalty;
                        result.PenaltyBreakdowns[i].Add($"Elde Kalan Taşlar ({remainingTilesSum}) x {effectiveMultiplier}: +{remainingPenalty}");
                    }
                }

                // 3. Yere İşler Taş Atma Cezası / Gösterge Bonusu (Varsa)
                if (wrongDiscardPenalties != null && wrongDiscardPenalties.TryGetValue(i, out int wrongCount) && wrongCount != 0)
                {
                    int wrongPenalty = wrongCount * _settings.WrongDiscardPenalty;
                    roundPenalty += wrongPenalty;
                    if (wrongPenalty > 0)
                        result.PenaltyBreakdowns[i].Add($"Kural / İşler Taş Cezası ({wrongCount} adet): +{wrongPenalty}");
                    else
                        result.PenaltyBreakdowns[i].Add($"Gösterge / Bonus ({Math.Abs(wrongCount)} adet): {wrongPenalty}");
                }

                result.PlayerRoundPenalties[i] = roundPenalty;
                PlayerTotalScores[i] += roundPenalty;
            }

            // Takım skorlarını güncelle (Eşli modda)
            if (_settings.MatchMode == MatchMode.Coop)
            {
                result.TeamRoundPenalties[0] = result.PlayerRoundPenalties[0] + result.PlayerRoundPenalties[2];
                result.TeamRoundPenalties[1] = result.PlayerRoundPenalties[1] + result.PlayerRoundPenalties[3];

                _teamManager.AddTeamPenalty(0, result.TeamRoundPenalties[0]);
                _teamManager.AddTeamPenalty(1, result.TeamRoundPenalties[1]);
            }

            return result;
        }
    }
}
