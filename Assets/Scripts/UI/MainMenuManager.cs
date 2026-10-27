using System;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Enums;
using Okey101.Core;

namespace Okey101.UI
{
    /// <summary>
    /// Unity Ana Menü ve Lobi Yöneticisi (Resort VIP Lobi Arayüzü).
    /// Oyuncunun oyun modunu (Eşli/Tekli & Katlamalı/Standart) seçip masaya oturmasını,
    /// profil, sıralama, etkinlikler ve mağaza etkileşimlerini yönetir.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Ana Ekran ve Lobi Panelleri")]
        [SerializeField] private GameObject lobbyScreenPanel;
        [SerializeField] private GameObject gameBoard3DPanel;
        [SerializeField] private GameObject matchMakingModal;
        [SerializeField] private GameObject playerProfileModal;
        [SerializeField] private GameObject storeModal;

        [Header("Kullanıcı Bilgileri & Para Birimleri")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text playerLevelText;
        [SerializeField] private Text coinsAmountText;
        [SerializeField] private Text diamondsAmountText;
        [SerializeField] private Image playerAvatarImage;
        [SerializeField] private Button vipButton;

        [Header("Ana Oyun Modu Butonları")]
        [SerializeField] private Button quickMatchButton;       // 101 Okey Otomatik Eşleşme (Büyük Altın Kart)
        [SerializeField] private Button tournamentButton;       // Yarışma / Turnuva
        [SerializeField] private Button createTableButton;      // Masa Aç
        [SerializeField] private Button tokenTournamentButton;  // Jeton Turnuvası
        [SerializeField] private Button clanButton;             // Klan
        [SerializeField] private Button voiceChatRoomButton;    // Sohbet Odası
        [SerializeField] private Button postsFeedButton;        // Gönderiler

        [Header("Hızlı Eşleşme / Masa Kurma Modal Butonları")]
        [SerializeField] private Button selectSoloModeButton;
        [SerializeField] private Button selectCoopModeButton;
        [SerializeField] private Button selectFoldedRuleButton;
        [SerializeField] private Button selectStandardRuleButton;
        [SerializeField] private Button confirmStartGameButton;
        [SerializeField] private Button closeModalButton;

        [Header("Alt Menü Butonları")]
        [SerializeField] private Button inventoryButton;   // Çanta
        [SerializeField] private Button freeGiftButton;     // Hediye Çip
        [SerializeField] private Button questsButton;       // Görevler
        [SerializeField] private Button friendsButton;      // Arkadaşlarım
        [SerializeField] private Button storeButton;        // Mağaza

        [Header("Oyun İçi Lobiye Dön Butonu")]
        [SerializeField] private Button returnToLobbyButton;

        [Header("Game Controller")]
        [SerializeField] private GameController gameController;

        // Seçili Oyun Ayarları
        private MatchMode currentMatchMode = MatchMode.Coop;
        private ScoringRule currentScoringRule = ScoringRule.Folded;
        private int currentTableStake = 25000;

        private void Start()
        {
            ShowLobby();
            InitUserData();
            BindButtons();
        }

        private void InitUserData()
        {
            if (playerNameText != null) playerNameText.text = "COİN SULTANI";
            if (playerLevelText != null) playerLevelText.text = "5";
            if (coinsAmountText != null) coinsAmountText.text = "965.881";
            if (diamondsAmountText != null) diamondsAmountText.text = "1711,9";
        }

        private void BindButtons()
        {
            // Hızlı Eşleşme & Masa Aç Modalını Tetikleme
            quickMatchButton?.onClick.AddListener(() => OpenMatchmakingModal());
            createTableButton?.onClick.AddListener(() => OpenMatchmakingModal());

            // Modal İçi Seçimler
            selectSoloModeButton?.onClick.AddListener(() => SetMatchMode(MatchMode.Solo));
            selectCoopModeButton?.onClick.AddListener(() => SetMatchMode(MatchMode.Coop));
            selectFoldedRuleButton?.onClick.AddListener(() => SetScoringRule(ScoringRule.Folded));
            selectStandardRuleButton?.onClick.AddListener(() => SetScoringRule(ScoringRule.Standard));
            confirmStartGameButton?.onClick.AddListener(StartMatchWithCurrentConfig);
            closeModalButton?.onClick.AddListener(CloseModals);

            // Alt Bar
            storeButton?.onClick.AddListener(() => { if (storeModal != null) storeModal.SetActive(true); });

            // Lobiye Geri Dön
            returnToLobbyButton?.onClick.AddListener(ShowLobby);
        }

        public void OpenMatchmakingModal()
        {
            if (matchMakingModal != null) matchMakingModal.SetActive(true);
        }

        public void CloseModals()
        {
            if (matchMakingModal != null) matchMakingModal.SetActive(false);
            if (playerProfileModal != null) playerProfileModal.SetActive(false);
            if (storeModal != null) storeModal.SetActive(false);
        }

        public void SetMatchMode(MatchMode mode)
        {
            currentMatchMode = mode;
            Debug.Log($"[MainMenu] Seçilen Oyun Modu: {mode}");
        }

        public void SetScoringRule(ScoringRule rule)
        {
            currentScoringRule = rule;
            Debug.Log($"[MainMenu] Seçilen Baraj Kuralı: {rule}");
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
