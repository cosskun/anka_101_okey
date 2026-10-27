using System;
using System.Collections.Generic;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// Masada geçerli olan açma barajlarını, açık perleri ve katlamalı mod takibini yönetir.
    /// Katlamalı modda her yeni açılan el barajı artırır (+1).
    /// </summary>
    public class TableScoreTracker
    {
        private readonly GameSettings _settings;

        public int CurrentRequiredScore { get; private set; }
        public int CurrentRequiredPairs { get; private set; }

        public int HighestOpenedScore { get; private set; }
        public int HighestOpenedPairs { get; private set; }

        // Masadaki tüm açık perler
        public List<PerGroup> OpenedSeriesOnTable { get; private set; }
        public List<PerGroup> OpenedPairsOnTable { get; private set; }

        // Baraj güncellendiğinde UI'ı bilgilendirmek için event
        public event Action<int, int> OnThresholdChanged;
        public event Action<PerGroup> OnPerAddedToTable;

        public TableScoreTracker(GameSettings settings)
        {
            _settings = settings ?? new GameSettings();
            OpenedSeriesOnTable = new List<PerGroup>();
            OpenedPairsOnTable = new List<PerGroup>();
            ResetTracker();
        }

        public void ResetTracker()
        {
            OpenedSeriesOnTable.Clear();
            OpenedPairsOnTable.Clear();

            HighestOpenedScore = 0;
            HighestOpenedPairs = 0;

            CurrentRequiredScore = _settings.BaseMinScoreToOpen;
            CurrentRequiredPairs = _settings.BaseMinPairsToOpen;

            OnThresholdChanged?.Invoke(CurrentRequiredScore, CurrentRequiredPairs);
        }

        /// <summary>
        /// Masaya yeni bir seri grubu açıldığında çağrılır.
        /// Katlamalı modda barajı dinamik olarak artırır (en yüksek el + 1).
        /// </summary>
        public void RegisterOpenedSeries(List<PerGroup> perList, int playerIndex, int totalScore)
        {
            if (perList != null)
            {
                foreach (var per in perList)
                {
                    per.OwnerPlayerIndex = playerIndex;
                    OpenedSeriesOnTable.Add(per);
                    OnPerAddedToTable?.Invoke(per);
                }
            }

            if (totalScore > HighestOpenedScore)
            {
                HighestOpenedScore = totalScore;
            }

            if (_settings.ScoringRule == ScoringRule.Folded)
            {
                // Katlamalı modda yeni baraj en yüksek açılan elin 1 fazlasıdır
                CurrentRequiredScore = HighestOpenedScore + 1;
            }
            else
            {
                CurrentRequiredScore = _settings.BaseMinScoreToOpen;
            }

            OnThresholdChanged?.Invoke(CurrentRequiredScore, CurrentRequiredPairs);
        }

        /// <summary>
        /// Masaya çift açıldığında çağrılır.
        /// Katlamalı modda gereken çift sayısını artırır (en yüksek çift + 1).
        /// </summary>
        public void RegisterOpenedPairs(List<PerGroup> pairList, int playerIndex, int totalPairs)
        {
            if (pairList != null)
            {
                foreach (var pair in pairList)
                {
                    pair.OwnerPlayerIndex = playerIndex;
                    OpenedPairsOnTable.Add(pair);
                    OnPerAddedToTable?.Invoke(pair);
                }
            }

            if (totalPairs > HighestOpenedPairs)
            {
                HighestOpenedPairs = totalPairs;
            }

            if (_settings.ScoringRule == ScoringRule.Folded)
            {
                // Katlamalı modda yeni çift barajı en yüksek çift sayısının 1 fazlasıdır
                CurrentRequiredPairs = HighestOpenedPairs + 1;
            }
            else
            {
                CurrentRequiredPairs = _settings.BaseMinPairsToOpen;
            }

            OnThresholdChanged?.Invoke(CurrentRequiredScore, CurrentRequiredPairs);
        }

        /// <summary>
        /// Masadaki mevcut bir pere taş işlendiğinde listeyi ve Okey temsillerini günceller.
        /// </summary>
        public void AppendTileToExistingPer(PerGroup targetPer, TileData tile, bool insertAtStart)
        {
            if (targetPer == null || tile == null) return;

            if (insertAtStart)
            {
                targetPer.Tiles.Insert(0, tile);
            }
            else
            {
                targetPer.Tiles.Add(tile);
            }

            // Okey (Joker) işlendiyse temsil ettiği renk ve sayıyı anında güncelle
            if (targetPer.Type == PerType.RunSameColor)
            {
                TileValidator.ValidateRunPer(targetPer.Tiles);
            }
            else if (targetPer.Type == PerType.GroupDiffColor)
            {
                TileValidator.ValidateGroupPer(targetPer.Tiles);
            }
        }
    }
}
