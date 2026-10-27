using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Okey101.Core.AI;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;
using Okey101.UI;
using Okey101.View;

namespace Okey101.Core
{
    /// <summary>
    /// 101 Okey oyununun Unity ana yöneticisi (Game Controller / Coordinator).
    /// Core mantık katmanı, AI botları, 3D masa görünümü ve UI katmanını koordine eder.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Oyun Modu & Kural Ayarları")]
        [SerializeField] private MatchMode matchMode = MatchMode.Solo;
        [SerializeField] private ScoringRule scoringRule = ScoringRule.Folded;
        [SerializeField] private bool allowTeammateProcessingWithoutOpening = false;

        [Header("Görünüm ve UI Referansları")]
        [SerializeField] private TableViewController tableViewController;
        [SerializeField] private RackViewController rackViewController;
        [SerializeField] private TableInfoUI tableInfoUI;
        [SerializeField] private RackActionBarUI rackActionBarUI;

        [Header("AI Ayarları")]
        [SerializeField] private float botTurnDelaySeconds = 1.2f;

        // Core Sistemler
        public GameSettings Settings { get; private set; }
        public DeckManager DeckManager { get; private set; }
        public TableScoreTracker TableTracker { get; private set; }
        public TeamManager TeamManager { get; private set; }
        public ScoreManager ScoreManager { get; private set; }
        public TurnManager TurnManager { get; private set; }

        private OkeyAIController[] _botControllers;
        private Coroutine _botCoroutine;

        private void Start()
        {
            InitializeGame();
            StartNewRound();
        }

        public void InitializeGame()
        {
            // 1. Konfigürasyonu oluştur
            Settings = new GameSettings(matchMode, scoringRule)
            {
                AllowTeammateProcessingWithoutOpening = this.allowTeammateProcessingWithoutOpening
            };

            // 2. Çekirdek motorları oluştur
            DeckManager = new DeckManager(Settings);
            TableTracker = new TableScoreTracker(Settings);
            TeamManager = new TeamManager(Settings);
            ScoreManager = new ScoreManager(Settings, TeamManager);
            TurnManager = new TurnManager(Settings, DeckManager, TableTracker, TeamManager, ScoreManager);

            // 3. Bot Yapay Zekalarını oluştur (Oyuncu 1, 2, 3)
            _botControllers = new OkeyAIController[4];
            for (int i = 1; i <= 3; i++)
            {
                _botControllers[i] = new OkeyAIController(i, TurnManager);
            }

            // 4. UI ve Görünüm Bağlantıları
            if (tableInfoUI != null)
            {
                tableInfoUI.Initialize(Settings, TableTracker);
            }

            if (rackActionBarUI != null)
            {
                rackActionBarUI.Initialize(Settings, TableTracker);
                rackActionBarUI.OnOpenSeriesRequested += HandleOpenSeriesRequested;
                rackActionBarUI.OnOpenPairsRequested += HandleOpenPairsRequested;
                rackActionBarUI.OnProcessTileRequested += HandleProcessTileRequested;
                rackActionBarUI.OnSwapOkeyRequested += HandleSwapOkeyRequested;
                rackActionBarUI.OnShowIndicatorRequested += HandleShowIndicatorRequested;
                rackActionBarUI.OnDiscardRequested += HandleDiscardRequested;
                rackActionBarUI.OnSortRunsRequested += () => rackViewController?.OnSortRunsClicked();
                rackActionBarUI.OnSortPairsRequested += () => rackViewController?.OnSortPairsClicked();
            }

            if (rackViewController != null)
            {
                rackViewController.OnSelectionChanged += HandleSelectionChanged;
            }

            if (tableViewController != null)
            {
                tableViewController.OnDrawDeckClicked += HandleDrawDeckClicked;
                tableViewController.OnDiscardPileClicked += HandleDrawSideDiscardClicked;
            }

            // Sürükle-bırak (Drag & Drop) Event Abonelikleri
            TileDragHandler.OnTileDroppedOnDiscardZone += HandleTileDroppedOnDiscardZone;
            TileDragHandler.OnTileDroppedOnTable += HandleTileDroppedOnTable;

            // 5. TurnManager Event Abonelikleri
            TurnManager.OnTurnChanged += HandleTurnChanged;
            TurnManager.OnTileDrawn += HandleTileDrawn;
            TurnManager.OnTileDiscarded += HandleTileDiscarded;
            TurnManager.OnHandOpened += HandleHandOpened;
            TurnManager.OnTablePerUpdated += HandleTablePerUpdated;
            TurnManager.OnRoundEnded += HandleRoundEnded;
        }

        private void OnDestroy()
        {
            TileDragHandler.OnTileDroppedOnDiscardZone -= HandleTileDroppedOnDiscardZone;
            TileDragHandler.OnTileDroppedOnTable -= HandleTileDroppedOnTable;
        }

        public void StartNewRound()
        {
            if (tableViewController != null)
            {
                tableViewController.ClearTable();
            }

            TurnManager.StartNewRound(startingPlayerIndex: 0);

            if (tableViewController != null)
            {
                tableViewController.SetIndicator(DeckManager.IndicatorTile, DeckManager.RemainingDrawCount);
            }

            UpdateLocalPlayerView();
        }

        private void UpdateLocalPlayerView()
        {
            if (rackViewController != null)
            {
                rackViewController.BindHand(TurnManager.PlayerHands[0]);
            }

            if (tableInfoUI != null)
            {
                tableInfoUI.UpdateScores(ScoreManager, TeamManager);
            }

            if (tableViewController != null)
            {
                tableViewController.HighlightTurn(TurnManager.CurrentPlayerIndex);
                tableViewController.UpdateDeckCount(DeckManager.RemainingDrawCount);
            }
        }

        private void HandleSelectionChanged(List<TileData> selectedTiles)
        {
            if (rackActionBarUI != null)
            {
                rackActionBarUI.UpdateSelectionInfo(selectedTiles);
            }
        }

        #region Oyuncu Aksiyonları (Human Actions)

        private void HandleDrawDeckClicked()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            // Eğer daha önce yandan taş alındıysa ama kullanılmadıysa, yan taşı geri bırakıp desteden çek
            if (TurnManager.MustUseSideTileId.HasValue)
            {
                if (!TurnManager.CancelSideDiscardAndDrawFromDeck(0, out _, out string cancelErr))
                {
                    rackActionBarUI?.ShowNotification(cancelErr, true);
                }
                else
                {
                    rackActionBarUI?.ShowNotification("Yandan alınan taş geri bırakıldı, desteden taş çekildi.", false);
                }
                return;
            }

            if (!TurnManager.DrawFromDeck(0, out var drawnTile, out string error))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
        }

        private void HandleDrawSideDiscardClicked(int discardPlayerIndex)
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            // Yan oyuncu: Oyuncu 3 (Solundaki)
            if (discardPlayerIndex != 3)
            {
                rackActionBarUI?.ShowNotification("Yalnızca solunuzdaki oyuncunun attığı son taşı alabilirsiniz.", true);
                return;
            }

            if (!TurnManager.DrawFromSideDiscard(0, out _, out string error, validateCanUse: true))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
        }

        private void HandleOpenSeriesRequested()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            var hand = TurnManager.PlayerHands[0];
            var selected = rackViewController?.GetSelectedTiles();

            // Eğer el zaten açıksa ve yeni 3+ taşlı bir per seçildiyse ek per olarak aç
            if (hand.HasOpenedSeries && selected != null && selected.Count >= 3)
            {
                var additional = hand.FindBestMelds(selected);
                if (additional.Count == 0) additional = new List<List<TileData>> { selected };
                if (TurnManager.OpenAdditionalSeries(0, additional, out string addErr))
                {
                    rackActionBarUI?.ShowNotification("Ek per masaya açıldı!", false);
                    rackViewController?.ClearSelection();
                    tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                    UpdateLocalPlayerView();
                }
                else
                {
                    rackActionBarUI?.ShowNotification(addErr, true);
                }
                return;
            }

            // Seçilen taşlar varsa önce onları perlere ayır, yoksa tüm ıstakadaki perleri otomatik bul
            List<List<TileData>> melds;
            if (selected != null && selected.Count >= 3)
            {
                melds = hand.FindBestMelds(selected);
                if (melds.Count == 0)
                {
                    melds = new List<List<TileData>> { selected };
                }
            }
            else
            {
                melds = hand.FindBestMelds(hand.Tiles);
            }

            if (!TurnManager.OpenSeries(0, melds, out string error))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
            else
            {
                rackActionBarUI?.ShowNotification("El başarıyla açıldı!", false);
                rackViewController?.ClearSelection();
                tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                UpdateLocalPlayerView();
            }
        }

        private void HandleOpenPairsRequested()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            var hand = TurnManager.PlayerHands[0];
            var selected = rackViewController?.GetSelectedTiles();
            List<List<TileData>> pairs;

            if (selected != null && selected.Count >= 2)
            {
                pairs = hand.FindBestPairs(selected);
                if (pairs.Count == 0)
                {
                    pairs = new List<List<TileData>>();
                    for (int i = 0; i < selected.Count - 1; i += 2)
                    {
                        pairs.Add(new List<TileData> { selected[i], selected[i + 1] });
                    }
                }
            }
            else
            {
                pairs = hand.FindBestPairs(hand.Tiles);
            }

            if (!TurnManager.OpenPairs(0, pairs, out string error))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
            else
            {
                rackActionBarUI?.ShowNotification("Çiftler masaya açıldı!", false);
                rackViewController?.ClearSelection();
                tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                UpdateLocalPlayerView();
            }
        }

        private void HandleProcessTileRequested()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            var selected = rackViewController?.GetSelectedTiles();
            if (selected == null || selected.Count != 1)
            {
                rackActionBarUI?.ShowNotification("İşlemek veya Okey almak için tam olarak 1 taş seçmelisiniz.", true);
                return;
            }

            TryProcessOrSwapSingleTile(selected[0]);
        }

        private void HandleSwapOkeyRequested()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            var selected = rackViewController?.GetSelectedTiles();
            if (selected == null || selected.Count != 1)
            {
                rackActionBarUI?.ShowNotification("Masadaki Okey'i almak için yerine koyacağınız 1 taşı seçmelisiniz.", true);
                return;
            }

            var tile = selected[0];
            foreach (var per in TableTracker.OpenedSeriesOnTable)
            {
                if (TurnManager.SwapOkeyFromTablePer(0, tile, per, out var stolenOkey, out _))
                {
                    rackActionBarUI?.ShowNotification($"Masadaki Okey alındı! ({tile} -> ★OKEY★)", false);
                    rackViewController?.ClearSelection();
                    tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                    UpdateLocalPlayerView();
                    return;
                }
            }

            rackActionBarUI?.ShowNotification("Seçilen taş masadaki hiçbir Okey'in temsil ettiği değerle eşleşmiyor.", true);
        }

        private void HandleShowIndicatorRequested()
        {
            if (TurnManager.TryShowIndicator(0, out string msg))
            {
                rackActionBarUI?.ShowNotification(msg, false);
                tableInfoUI?.UpdateScores(ScoreManager, TeamManager);
            }
            else
            {
                rackActionBarUI?.ShowNotification(msg, true);
            }
        }

        private void TryProcessOrSwapSingleTile(TileData tileToProcess)
        {
            if (tileToProcess == null) return;
            var openSeries = TableTracker.OpenedSeriesOnTable;

            // 1. Önce Okey takası (Okey çalma) dene
            foreach (var per in openSeries)
            {
                if (TurnManager.SwapOkeyFromTablePer(0, tileToProcess, per, out _, out _))
                {
                    rackActionBarUI?.ShowNotification($"Masadaki Okey alındı: {tileToProcess} -> ★OKEY★", false);
                    rackViewController?.ClearSelection();
                    tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                    UpdateLocalPlayerView();
                    return;
                }
            }

            // 2. Sonra normal taş işleme dene
            string lastError = "Seçilen taş masadaki hiçbir açık pere uymuyor.";
            foreach (var per in openSeries)
            {
                if (TurnManager.ProcessTile(0, tileToProcess, per, out lastError))
                {
                    rackActionBarUI?.ShowNotification($"Taş masadaki pere işlendi: {tileToProcess}", false);
                    rackViewController?.ClearSelection();
                    tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                    UpdateLocalPlayerView();
                    return;
                }
            }

            rackActionBarUI?.ShowNotification(lastError, true);
        }

        private void HandleTileDroppedOnDiscardZone(TileView droppedTileView)
        {
            if (TurnManager.CurrentPlayerIndex != 0 || droppedTileView == null || droppedTileView.Data == null) return;

            var tile = droppedTileView.Data;
            var hand = TurnManager.PlayerHands[0];

            // Eğer oyuncu henüz el açmamışsa ve bu taşı attığında elindeki diğer tüm taşlar eksiksiz açılabiliyorsa Elden Bitiş (Hand-Out)!
            if (!hand.HasOpenedHand && TurnManager.FinishHandOut(0, tile, out _, out _))
            {
                rackViewController?.ClearSelection();
                tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                UpdateLocalPlayerView();
                return;
            }

            if (!TurnManager.DiscardTile(0, tile, out _, out _, out string error))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
            else
            {
                rackViewController?.ClearSelection();
                UpdateLocalPlayerView();
            }
        }

        private void HandleTileDroppedOnTable(TileView droppedTileView, Vector2 screenPos)
        {
            if (TurnManager.CurrentPlayerIndex != 0 || droppedTileView == null || droppedTileView.Data == null) return;
            TryProcessOrSwapSingleTile(droppedTileView.Data);
        }

        private void HandleDiscardRequested()
        {
            if (TurnManager.CurrentPlayerIndex != 0) return;

            var selected = rackViewController?.GetSelectedTiles();
            if (selected == null || selected.Count != 1)
            {
                rackActionBarUI?.ShowNotification("Yere atmak için tek bir taş seçiniz.", true);
                return;
            }

            var tile = selected[0];
            var hand = TurnManager.PlayerHands[0];

            if (!hand.HasOpenedHand && TurnManager.FinishHandOut(0, tile, out _, out _))
            {
                rackViewController?.ClearSelection();
                tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
                UpdateLocalPlayerView();
                return;
            }

            if (!TurnManager.DiscardTile(0, tile, out _, out _, out string error))
            {
                rackActionBarUI?.ShowNotification(error, true);
            }
            else
            {
                rackViewController?.ClearSelection();
                UpdateLocalPlayerView();
            }
        }

        #endregion

        #region TurnManager Event Dinleyicileri

        private void HandleTurnChanged(int playerIndex, TurnPhase phase)
        {
            UpdateLocalPlayerView();

            if (playerIndex != 0 && !TurnManager.IsRoundOver)
            {
                if (_botCoroutine != null) StopCoroutine(_botCoroutine);
                _botCoroutine = StartCoroutine(BotTurnRoutine(playerIndex));
            }
        }

        private IEnumerator BotTurnRoutine(int botIndex)
        {
            yield return new WaitForSeconds(botTurnDelaySeconds);

            if (!TurnManager.IsRoundOver && TurnManager.CurrentPlayerIndex == botIndex)
            {
                _botControllers[botIndex]?.ExecuteTurn();
            }
        }

        private void HandleTileDrawn(int playerIndex, TileData tile, bool fromSide)
        {
            if (tableViewController != null)
            {
                tableViewController.UpdateDeckCount(DeckManager.RemainingDrawCount);
                if (fromSide)
                {
                    int prevPlayer = (playerIndex + 3) % 4;
                    tableViewController.RemoveLastDiscardTile(prevPlayer);
                }
            }

            if (playerIndex == 0)
            {
                rackViewController?.RefreshRack();
            }
        }

        private void HandleTablePerUpdated(int playerIndex, PerGroup updatedPer)
        {
            tableViewController?.RefreshAllTableMelds(TableTracker.OpenedSeriesOnTable, TableTracker.OpenedPairsOnTable);
            if (playerIndex == 0)
            {
                rackViewController?.RefreshRack();
            }
        }

        private void HandleTileDiscarded(int playerIndex, TileData tile)
        {
            if (tableViewController != null)
            {
                tableViewController.AddDiscardTile(playerIndex, tile);
            }

            if (playerIndex == 0)
            {
                rackViewController?.RefreshRack();
            }
        }

        private void HandleHandOpened(int playerIndex, bool isPairs, int scoreOrCount)
        {
            string log = isPairs ? $"{playerIndex}. Oyuncu {scoreOrCount} çift açtı." : $"{playerIndex}. Oyuncu {scoreOrCount} puanla seri açtı.";
            rackActionBarUI?.ShowNotification(log, false);
            UpdateLocalPlayerView();
        }

        private void HandleRoundEnded(ScoreManager.RoundScoreResult result)
        {
            string winnerMsg = result.WinnerPlayerIndex != -1 
                ? $"Kazanan: Oyuncu {result.WinnerPlayerIndex} ({result.EndType})!" 
                : "Deste bitti, kimse bitemedi!";

            rackActionBarUI?.ShowNotification(winnerMsg, false);
            tableInfoUI?.UpdateScores(ScoreManager, TeamManager);
        }

        #endregion
    }
}
