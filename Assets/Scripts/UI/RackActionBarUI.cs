using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;

namespace Okey101.UI
{
    /// <summary>
    /// Istakanın altında yer alan aksiyon butonlarını, anlık seçilen taş puanı/çift toplamını
    /// ve durum bildirimlerini yöneten UI sınıfı.
    /// </summary>
    public class RackActionBarUI : MonoBehaviour
    {
        [Header("Bilgi & Puan Göstergeleri")]
        [SerializeField] private Text selectionSummaryText; // Örn: "Seçilen: 110 | Gereken: 108 -> AÇILABİLİR!"
        [SerializeField] private Text notificationText;     // Hata veya uyarı mesajları

        [Header("Aksiyon Butonları")]
        [SerializeField] private Button openSeriesButton;
        [SerializeField] private Button openPairsButton;
        [SerializeField] private Button processTileButton;
        [SerializeField] private Button swapOkeyButton;
        [SerializeField] private Button showIndicatorButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Button sortRunsButton;
        [SerializeField] private Button sortPairsButton;

        private GameSettings _settings;
        private TableScoreTracker _tracker;

        public event Action OnOpenSeriesRequested;
        public event Action OnOpenPairsRequested;
        public event Action OnProcessTileRequested;
        public event Action OnSwapOkeyRequested;
        public event Action OnShowIndicatorRequested;
        public event Action OnDiscardRequested;
        public event Action OnSortRunsRequested;
        public event Action OnSortPairsRequested;

        public void Initialize(GameSettings settings, TableScoreTracker tracker)
        {
            _settings = settings;
            _tracker = tracker;

            if (openSeriesButton != null) openSeriesButton.onClick.AddListener(() => OnOpenSeriesRequested?.Invoke());
            if (openPairsButton != null) openPairsButton.onClick.AddListener(() => OnOpenPairsRequested?.Invoke());
            if (processTileButton != null) processTileButton.onClick.AddListener(() => OnProcessTileRequested?.Invoke());
            if (swapOkeyButton != null) swapOkeyButton.onClick.AddListener(() => OnSwapOkeyRequested?.Invoke());
            if (showIndicatorButton != null) showIndicatorButton.onClick.AddListener(() => OnShowIndicatorRequested?.Invoke());
            if (discardButton != null) discardButton.onClick.AddListener(() => OnDiscardRequested?.Invoke());
            if (sortRunsButton != null) sortRunsButton.onClick.AddListener(() => OnSortRunsRequested?.Invoke());
            if (sortPairsButton != null) sortPairsButton.onClick.AddListener(() => OnSortPairsRequested?.Invoke());

            UpdateSelectionInfo(new List<TileData>());
        }

        /// <summary>
        /// Oyuncu ıstakadan taş seçtiğinde veya seçimi kaldırdığında puan/çift toplamını günceller.
        /// </summary>
        public void UpdateSelectionInfo(List<TileData> selectedTiles)
        {
            if (selectionSummaryText == null || _tracker == null) return;

            if (selectedTiles == null || selectedTiles.Count == 0)
            {
                selectionSummaryText.text = "Taş seçiniz veya sürükleyiniz...";
                return;
            }

            var tempHand = new PlayerHand(0);
            var validMelds = tempHand.FindBestMelds(selectedTiles);
            int validMeldScore = validMelds.Sum(m => m.Sum(t => t.IsOkey && t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number));
            int rawSum = selectedTiles.Sum(t => t.IsOkey && t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number);
            int sum = validMeldScore > 0 ? validMeldScore : rawSum;

            int reqScore = _tracker.CurrentRequiredScore;
            int reqPairs = _tracker.CurrentRequiredPairs;

            // Çift sayısı kontrolü
            int pairCount = tempHand.FindBestPairs(selectedTiles).Count;

            if (sum >= reqScore)
            {
                selectionSummaryText.text = $"Seçilen Per: <b>{sum}</b> | Gereken: {reqScore} -> <color=#00FF66><b>AÇILABİLİR!</b></color>";
            }
            else if (pairCount >= reqPairs)
            {
                selectionSummaryText.text = $"Seçilen Çift: <b>{pairCount}</b> | Gereken: {reqPairs} -> <color=#00FF66><b>ÇİFT AÇILABİLİR!</b></color>";
            }
            else
            {
                selectionSummaryText.text = $"Seçilen Toplam: <color=#FF5555>{sum}</color> | Gereken: {reqScore} ({pairCount}/{reqPairs} Çift)";
            }
        }

        /// <summary>
        /// Oyuncuya kısa süreli bilgilendirme veya kural hatası mesajı gösterir.
        /// </summary>
        public void ShowNotification(string message, bool isError = false)
        {
            if (notificationText != null)
            {
                notificationText.text = isError ? $"<color=#FF4444>{message}</color>" : $"<color=#FFFFFF>{message}</color>";
                CancelInvoke(nameof(ClearNotification));
                Invoke(nameof(ClearNotification), 3.5f);
            }
        }

        private void ClearNotification()
        {
            if (notificationText != null)
            {
                notificationText.text = string.Empty;
            }
        }
    }
}
