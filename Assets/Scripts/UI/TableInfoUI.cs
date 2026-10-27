using System;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Config;
using Okey101.Core.Enums;
using Okey101.Core.Logic;

namespace Okey101.UI
{
    /// <summary>
    /// Masa üstü bilgi panelini yönetir.
    /// Seçili oyun modu, dinamik açılış barajı (Puan / Çift) ve canlı skor tablosunu görüntüler.
    /// </summary>
    public class TableInfoUI : MonoBehaviour
    {
        [Header("Mod ve Baraj Bilgileri")]
        [SerializeField] private Text gameModeText;          // Örn: "Mod: Eşli (2v2) | Katlamalı"
        [SerializeField] private Text thresholdInfoText;     // Örn: "Açılış Barajı: 108 Puan / 6 Çift"

        [Header("Canlı Skor Tablosu")]
        [SerializeField] private GameObject soloScoreboardPanel;
        [SerializeField] private Text[] playerPenaltyTexts;  // [0..3]

        [SerializeField] private GameObject coopScoreboardPanel;
        [SerializeField] private Text teamAScoreText;        // Oyuncu 0 & 2
        [SerializeField] private Text teamBScoreText;        // Oyuncu 1 & 3

        public void Initialize(GameSettings settings, TableScoreTracker tracker)
        {
            if (settings == null) return;

            // Mod Başlığı
            string modeStr = settings.MatchMode == MatchMode.Coop ? "Eşli (2v2)" : "Tekli (FFA)";
            string ruleStr = settings.ScoringRule == ScoringRule.Folded ? "Katlamalı" : "Standart (Katlamasız)";
            if (gameModeText != null)
            {
                gameModeText.text = $"Mod: {modeStr} | {ruleStr}";
            }

            // Panel Görünürlükleri
            bool isCoop = (settings.MatchMode == MatchMode.Coop);
            if (coopScoreboardPanel != null) coopScoreboardPanel.SetActive(isCoop);
            if (soloScoreboardPanel != null) soloScoreboardPanel.SetActive(!isCoop);

            // Baraj Takip Eventi
            if (tracker != null)
            {
                tracker.OnThresholdChanged += UpdateThresholdDisplay;
                UpdateThresholdDisplay(tracker.CurrentRequiredScore, tracker.CurrentRequiredPairs);
            }
        }

        public void UpdateThresholdDisplay(int requiredScore, int requiredPairs)
        {
            if (thresholdInfoText != null)
            {
                thresholdInfoText.text = $"Açılış Barajı: <color=#FFD700><b>{requiredScore} Puan</b></color> / <color=#00FFFF><b>{requiredPairs} Çift</b></color>";
            }
        }

        public void UpdateScores(ScoreManager scoreManager, TeamManager teamManager)
        {
            if (scoreManager == null) return;

            if (teamManager != null && teamManager.CurrentMatchMode == MatchMode.Coop)
            {
                if (teamAScoreText != null) teamAScoreText.text = $"Takım A (0 & 2): {teamManager.GetTeamScore(0)} ceza";
                if (teamBScoreText != null) teamBScoreText.text = $"Takım B (1 & 3): {teamManager.GetTeamScore(1)} ceza";
            }
            else
            {
                if (playerPenaltyTexts != null)
                {
                    for (int i = 0; i < playerPenaltyTexts.Length && i < scoreManager.PlayerTotalScores.Length; i++)
                    {
                        if (playerPenaltyTexts[i] != null)
                        {
                            playerPenaltyTexts[i].text = $"Oyuncu {i}: {scoreManager.PlayerTotalScores[i]} ceza";
                        }
                    }
                }
            }
        }
    }
}
