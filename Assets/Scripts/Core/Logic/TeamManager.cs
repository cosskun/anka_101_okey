using System;
using System.Collections.Generic;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// Eşli (2v2) ve Tekli (FFA) modlarda oyuncu-takım eşleşmelerini,
    /// ortak el açma durumlarını ve ortak skor takibini yönetir.
    /// </summary>
    public class TeamManager
    {
        private readonly GameSettings _settings;
        private readonly Dictionary<int, PlayerHand> _playerHands;
        private readonly int[] _teamScores; // Takım toplam ceza skorları

        public MatchMode CurrentMatchMode => _settings.MatchMode;

        public TeamManager(GameSettings settings)
        {
            _settings = settings ?? new GameSettings();
            _playerHands = new Dictionary<int, PlayerHand>();
            _teamScores = new int[4]; // Solo için 4, Coop için 0 ve 1 indisleri kullanılır
        }

        public void RegisterPlayerHand(int playerIndex, PlayerHand hand)
        {
            _playerHands[playerIndex] = hand;
        }

        /// <summary>
        /// Oyuncunun ortağının index'ini döndürür. Solo modda -1 döner.
        /// Eşli Mod: 0 <-> 2 (Takım 0) | 1 <-> 3 (Takım 1)
        /// </summary>
        public int GetTeammateIndex(int playerIndex)
        {
            if (_settings.MatchMode != MatchMode.Coop)
            {
                return -1; // Tekli modda ortak yok
            }

            return playerIndex switch
            {
                0 => 2,
                2 => 0,
                1 => 3,
                3 => 1,
                _ => -1
            };
        }

        /// <summary>
        /// Oyuncunun ait olduğu Takım ID'sini döner.
        /// Coop modda: 0 veya 1. Solo modda: 0, 1, 2, 3.
        /// </summary>
        public int GetTeamId(int playerIndex)
        {
            if (_settings.MatchMode == MatchMode.Solo)
            {
                return playerIndex;
            }

            // Eşli modda 0 ve 2 -> Takım 0, 1 ve 3 -> Takım 1
            return (playerIndex % 2 == 0) ? 0 : 1;
        }

        /// <summary>
        /// Aynı takımda olup olmadıklarını kontrol eder.
        /// </summary>
        public bool AreTeammates(int playerIndex1, int playerIndex2)
        {
            if (_settings.MatchMode != MatchMode.Coop) return false;
            return GetTeamId(playerIndex1) == GetTeamId(playerIndex2);
        }

        /// <summary>
        /// Oyuncunun ortağının el açıp açmadığını döner.
        /// </summary>
        public bool IsTeammateHandOpened(int playerIndex)
        {
            int teammate = GetTeammateIndex(playerIndex);
            if (teammate == -1 || !_playerHands.ContainsKey(teammate)) return false;

            return _playerHands[teammate].HasOpenedHand;
        }

        /// <summary>
        /// Oyuncunun masadaki perlere taş işleme yetkisi olup olmadığını kurallara göre denetler.
        /// </summary>
        public bool CanPlayerProcessTiles(int playerIndex)
        {
            if (!_playerHands.ContainsKey(playerIndex)) return false;

            var hand = _playerHands[playerIndex];

            // 1. Oyuncu kendi elini açtıysa kesinlikle işleme yapabilir
            if (hand.HasOpenedHand) return true;

            // 2. Eşli modda ve ayar izin veriyorsa, ortağı açtıysa işleme yapabilir
            if (_settings.MatchMode == MatchMode.Coop && _settings.AllowTeammateProcessingWithoutOpening)
            {
                if (IsTeammateHandOpened(playerIndex))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takım skorunu günceller.
        /// </summary>
        public void AddTeamPenalty(int teamId, int penaltyPoints)
        {
            if (teamId >= 0 && teamId < _teamScores.Length)
            {
                _teamScores[teamId] += penaltyPoints;
            }
        }

        /// <summary>
        /// Takımın toplam skorunu döner.
        /// </summary>
        public int GetTeamScore(int teamId)
        {
            if (teamId >= 0 && teamId < _teamScores.Length)
            {
                return _teamScores[teamId];
            }
            return 0;
        }

        public void ResetScores()
        {
            Array.Clear(_teamScores, 0, _teamScores.Length);
        }
    }
}
