using System;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Enums;
using Okey101.Core;

namespace Okey101.UI
{
    /// <summary>
    /// Unity Çevrimdışı (Yapay Zeka Botlara Karşı) Ana Menü Yöneticisi.
    /// Oyuncunun Hızlı Başla, Klasik 101 Okey (Katlamasız) veya Katlamalı 101 Okey
    /// modlarından birini seçerek botlara karşı masaya oturmasını ve yerel istatistiklerini yönetir.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Ana Ekran ve Masa Panelleri")]
        [SerializeField] private GameObject lobbyScreenPanel;
        [SerializeField] private GameObject gameBoard3DPanel;
        [SerializeField] private GameObject tableSetupModal;
        [SerializeField] private GameObject playerProfileModal;
        [SerializeField] private GameObject rulesModal;

        [Header("Yerel Oyuncu Bilgileri & İstatistikler")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text coinsAmountText;
        [SerializeField] private Text gamesPlayedText;
        [SerializeField] private Text gamesWonText;
        [SerializeField] private Text winRateText;
        [SerializeField] private Image playerAvatarImage;

        [Header("Çevrimdışı Oyun Modu Kartları (Botlara Karşı)")]
        [SerializeField] private Button quickPlayBotMatchButton;     // Hemen Oyna (Tek Tıkla Botlarla Başla)
        [SerializeField] private Button classicModeSetupButton;      // Klasik 101 Okey (Katlamasız)
        [SerializeField] private Button foldedModeSetupButton;       // Katlamalı 101 Okey

        [Header("Masa Kurulum Modal Butonları")]
        [SerializeField] private Button selectSoloModeButton;
        [SerializeField] private Button selectCoopModeButton;
        [SerializeField] private Button selectFoldedRuleButton;
        [SerializeField] private Button selectStandardRuleButton;
        [SerializeField] private Button confirmStartGameButton;
        [SerializeField] private Button closeModalButton;

        [Header("Alt Bilgi & Ödül Butonları")]
        [SerializeField] private Button dailyRewardButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button returnToLobbyButton;

        [Header("Game Controller")]
        [SerializeField] private GameController gameController;

        // Seçili Oyun Ayarları (Çevrimdışı Bot Masası)
        private MatchMode currentMatchMode = MatchMode.Solo;
        private ScoringRule currentScoringRule = ScoringRule.Standard;
        private int currentTableStake = 1000;

        private void Start()
        {
            ShowLobby();
            InitUserData();
            BindButtons();
        }

        private void InitUserData()
        {
            if (playerNameText != null) playerNameText.text = "OYUNCU";
            if (coinsAmountText != null) coinsAmountText.text = "20.000";
            if (gamesPlayedText != null) gamesPlayedText.text = "0";
            if (gamesWonText != null) gamesWonText.text = "0";
            if (winRateText != null) winRateText.text = "%0";
        }

        private void BindButtons()
        {
            // Hızlı Başla (Doğrudan 3 Botla Masaya Otur)
            quickPlayBotMatchButton?.onClick.AddListener(QuickStartBotMatch);

            // Klasik veya Katlamalı Masa Kurulumu
            classicModeSetupButton?.onClick.AddListener(() => OpenSetupForRule(ScoringRule.Standard));
            foldedModeSetupButton?.onClick.AddListener(() => OpenSetupForRule(ScoringRule.Folded));

            // Modal İçi Seçimler
            selectSoloModeButton?.onClick.AddListener(() => SetMatchMode(MatchMode.Solo));
            selectCoopModeButton?.onClick.AddListener(() => SetMatchMode(MatchMode.Coop));
            selectFoldedRuleButton?.onClick.AddListener(() => SetScoringRule(ScoringRule.Folded));
            selectStandardRuleButton?.onClick.AddListener(() => SetScoringRule(ScoringRule.Standard));
            confirmStartGameButton?.onClick.AddListener(StartMatchWithCurrentConfig);
            closeModalButton?.onClick.AddListener(CloseModals);

            // Kurallar & Lobiye Dön
            rulesButton?.onClick.AddListener(() => { if (rulesModal != null) rulesModal.SetActive(true); });
            returnToLobbyButton?.onClick.AddListener(ShowLobby);
        }

        public void QuickStartBotMatch()
        {
            currentMatchMode = MatchMode.Solo;
            currentScoringRule = ScoringRule.Standard;
            StartMatchWithCurrentConfig();
        }

        public void OpenSetupForRule(ScoringRule rule)
        {
            currentScoringRule = rule;
            if (tableSetupModal != null) tableSetupModal.SetActive(true);
        }

        public void CloseModals()
        {
            if (tableSetupModal != null) tableSetupModal.SetActive(false);
            if (playerProfileModal != null) playerProfileModal.SetActive(false);
            if (rulesModal != null) rulesModal.SetActive(false);
        }

        public void SetMatchMode(MatchMode mode)
        {
            currentMatchMode = mode;
        }

        public void SetScoringRule(ScoringRule rule)
        {
            currentScoringRule = rule;
        }

        public void StartMatchWithCurrentConfig()
        {
            CloseModals();
            StartGame(currentMatchMode, currentScoringRule);
        }

        public void StartGame(MatchMode matchMode, ScoringRule scoringRule)
        {
            if (lobbyScreenPanel != null) lobbyScreenPanel.SetActive(false);
            if (gameBoard3DPanel != null) gameBoard3DPanel.SetActive(true);

            if (gameController != null)
            {
                gameController.InitializeGame();
                gameController.StartNewRound();
            }
        }

        public void ShowLobby()
        {
            if (lobbyScreenPanel != null) lobbyScreenPanel.SetActive(true);
            if (gameBoard3DPanel != null) gameBoard3DPanel.SetActive(false);
            CloseModals();
        }
    }
}
