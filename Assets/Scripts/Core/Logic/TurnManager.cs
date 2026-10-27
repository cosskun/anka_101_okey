using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// 101 Okey oyununun sıra, tur aşamaları, taş çekme/atma/açma ve tur tamamlama döngüsünü yönetir.
    /// </summary>
    public class TurnManager
    {
        public GameSettings Settings { get; }
        public DeckManager DeckManager { get; }
        public TableScoreTracker TableScoreTracker { get; }
        public TeamManager TeamManager { get; }
        public ScoreManager ScoreManager { get; }

        public List<PlayerHand> PlayerHands { get; }
        public List<TileData>[] DiscardPiles { get; } // 4 Oyuncunun yere attığı taş kuyrukları

        public int CurrentPlayerIndex { get; private set; }
        public TurnPhase CurrentPhase { get; private set; }
        public int TurnCount { get; private set; }
        public bool IsRoundOver { get; private set; }

        public TileData LastDiscardedTile { get; private set; }
        public int LastDiscardedPlayerIndex { get; private set; } = -1;

        /// <summary>
        /// Yandan taş alındığında, o taşın bu tur içinde açılışta veya işlemede kullanılmasını zorunlu kılan taş kimliği.
        /// </summary>
        public int? MustUseSideTileId { get; private set; }

        /// <summary>
        /// Yandan alınan taşın kullanılmadan taş atılmasını engelleyen kural kilidi.
        /// </summary>
        public bool EnforceStrictSideDiscardLock { get; set; } = true;

        // Yere işler taş atma cezaları takip sözlüğü (Oyuncu -> Adet)
        public Dictionary<int, int> WrongDiscardPenalties { get; }

        // Eventler (View ve UI dinleyicileri için)
        public event Action<int, TurnPhase> OnTurnChanged;
        public event Action<int, TileData, bool> OnTileDrawn; // (player, tile, isFromSide)
        public event Action<int, TileData> OnTileDiscarded;
        public event Action<int, bool, int> OnHandOpened; // (player, isPairs, scoreOrCount)
        public event Action<int, PerGroup> OnTablePerUpdated;
        public event Action<ScoreManager.RoundScoreResult> OnRoundEnded;

        public TurnManager(
            GameSettings settings,
            DeckManager deckManager,
            TableScoreTracker tableScoreTracker,
            TeamManager teamManager,
            ScoreManager scoreManager)
        {
            Settings = settings ?? new GameSettings();
            DeckManager = deckManager ?? new DeckManager(Settings);
            TableScoreTracker = tableScoreTracker ?? new TableScoreTracker(Settings);
            TeamManager = teamManager ?? new TeamManager(Settings);
            ScoreManager = scoreManager ?? new ScoreManager(Settings, TeamManager);

            PlayerHands = new List<PlayerHand>
            {
                new PlayerHand(0),
                new PlayerHand(1),
                new PlayerHand(2),
                new PlayerHand(3)
            };

            DiscardPiles = new List<TileData>[4];
            for (int i = 0; i < 4; i++)
            {
                DiscardPiles[i] = new List<TileData>();
                TeamManager.RegisterPlayerHand(i, PlayerHands[i]);
            }

            WrongDiscardPenalties = new Dictionary<int, int>();
        }

        /// <summary>
        /// Yeni bir el/tur başlatır: desteyi hazırlar, taşları dağıtır.
        /// </summary>
        public void StartNewRound(int startingPlayerIndex = 0)
        {
            IsRoundOver = false;
            TurnCount = 1;
            CurrentPlayerIndex = startingPlayerIndex;
            LastDiscardedTile = null;
            LastDiscardedPlayerIndex = -1;
            MustUseSideTileId = null;
            WrongDiscardPenalties.Clear();

            for (int i = 0; i < 4; i++)
            {
                DiscardPiles[i].Clear();
            }

            TableScoreTracker.ResetTracker();
            DeckManager.PrepareNewDeck();
            DeckManager.DealTilesToPlayers(PlayerHands, startingPlayerIndex);

            // Başlangıç oyuncusu 22 taşla başlar, dolayısıyla ilk turda taş çekmeden doğrudan Action/Discard aşamasına geçer.
            CurrentPhase = TurnPhase.Action;
            OnTurnChanged?.Invoke(CurrentPlayerIndex, CurrentPhase);
        }

        /// <summary>
        /// Masadaki kapalı desteden taş çeker.
        /// </summary>
        public bool DrawFromDeck(int playerIndex, out TileData drawnTile, out string error)
        {
            drawnTile = null;
            error = string.Empty;

            if (IsRoundOver) { error = "Oyun zaten bitti."; return false; }
            if (playerIndex != CurrentPlayerIndex) { error = "Sıra sizde değil."; return false; }
            if (CurrentPhase != TurnPhase.Draw) { error = "Taş çekme aşamasında değilsiniz."; return false; }

            drawnTile = DeckManager.DrawTile();
            if (drawnTile == null)
            {
                // Deste bitti, el kimse bitmeden sona erer
                EndRound(EndRoundType.DeckDepleted, -1);
                return false;
            }

            PlayerHands[playerIndex].AddTile(drawnTile);
            CurrentPhase = TurnPhase.Action;

            OnTileDrawn?.Invoke(playerIndex, drawnTile, false);
            OnTurnChanged?.Invoke(CurrentPlayerIndex, CurrentPhase);
            return true;
        }

        /// <summary>
        /// Yandan (sol oyuncudan) atılan taşın kurallara göre alınıp alınamayacağını kontrol eder.
        /// </summary>
        public bool CanTakeSideDiscard(int playerIndex, TileData sideTile, out string reason)
        {
            reason = string.Empty;
            if (sideTile == null)
            {
                reason = "Yanda alınacak taş yok.";
                return false;
            }

            var hand = PlayerHands[playerIndex];

            // 1. Oyuncu kendi elini seri olarak açmışsa
            if (hand.HasOpenedSeries)
            {
                // Masadaki bir pere işlenebiliyor mu veya Okey çalabiliyor mu?
                foreach (var per in TableScoreTracker.OpenedSeriesOnTable)
                {
                    if (TileValidator.CanProcessTileIntoPer(per, sideTile, out _) ||
                        TileValidator.CanSwapOkeyFromPer(per, sideTile, out _))
                    {
                        return true;
                    }
                }

                // Eldeki taşlarla yeni bir 3+ ek per oluşturabiliyor mu?
                var simTiles = hand.Tiles.Select(t => t.Clone()).ToList();
                var sideClone = sideTile.Clone();
                simTiles.Add(sideClone);
                var melds = hand.FindBestMelds(simTiles);
                if (melds.Any(m => m.Any(t => t.Id == sideClone.Id)))
                {
                    return true;
                }
            }

            if (hand.HasOpenedPairs)
            {
                // Çift açmış oyuncu yandan aldığı taşla yeni bir çift yapabilmeli veya masadaki bir seri pere işleyebilmeli
                if (hand.Tiles.Any(t => t.MatchesPairWith(sideTile)) ||
                    TableScoreTracker.OpenedSeriesOnTable.Any(per => TileValidator.CanProcessTileIntoPer(per, sideTile, out _)))
                {
                    return true;
                }
                reason = "Çift açmış oyuncu yandan aldığı taşla çift oluşturmalı veya masadaki bir pere işlemelidir.";
                return false;
            }

            // 2. Oyuncu henüz el açmamışsa: bu taşla barajı geçerek kendi elini açabiliyor mu?
            if (!hand.HasOpenedHand)
            {
                var simTiles = hand.Tiles.Select(t => t.Clone()).ToList();
                var sideClone = sideTile.Clone();
                simTiles.Add(sideClone);

                var melds = hand.FindBestMelds(simTiles);
                int totalScore = melds.Sum(m => m.Sum(t => t.IsOkey && t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number));
                bool usedInMelds = melds.Any(m => m.Any(t => t.Id == sideClone.Id));

                if (usedInMelds && totalScore >= TableScoreTracker.CurrentRequiredScore)
                {
                    return true;
                }

                var pairs = hand.FindBestPairs(simTiles);
                bool usedInPairs = pairs.Any(p => p.Any(t => t.Id == sideClone.Id));
                if (usedInPairs && pairs.Count >= TableScoreTracker.CurrentRequiredPairs)
                {
                    return true;
                }

                reason = $"Yandan taş almak için o taşı kullanarak en az {TableScoreTracker.CurrentRequiredScore} puan (veya {TableScoreTracker.CurrentRequiredPairs} çift) açabilmelisiniz.";
                return false;
            }

            reason = "Yandan alınan taş masaya işlenemiyor veya yeni bir per oluşturmuyor.";
            return false;
        }

        /// <summary>
        /// Solundaki (önceki) oyuncunun attığı son taşı çeker (Yandan taş alma).
        /// 101 Okey kuralı: Yandan taş alan oyuncu o el mutlaka elini açmak veya o taşı işlemek zorundadır.
        /// </summary>
        public bool DrawFromSideDiscard(int playerIndex, out TileData drawnTile, out string error, bool validateCanUse = false)
        {
            drawnTile = null;
            error = string.Empty;

            if (IsRoundOver) { error = "Oyun zaten bitti."; return false; }
            if (playerIndex != CurrentPlayerIndex) { error = "Sıra sizde değil."; return false; }
            if (CurrentPhase != TurnPhase.Draw) { error = "Taş çekme aşamasında değilsiniz."; return false; }

            int prevPlayer = (playerIndex + 3) % 4;
            if (DiscardPiles[prevPlayer].Count == 0)
            {
                error = "Yan oyuncunun attığı taş bulunmuyor.";
                return false;
            }

            var lastTile = DiscardPiles[prevPlayer].Last();
            if (validateCanUse && !CanTakeSideDiscard(playerIndex, lastTile, out error))
            {
                return false;
            }

            DiscardPiles[prevPlayer].RemoveAt(DiscardPiles[prevPlayer].Count - 1);
            PlayerHands[playerIndex].AddTile(lastTile);

            drawnTile = lastTile;
            MustUseSideTileId = lastTile.Id;
            CurrentPhase = TurnPhase.Action;

            OnTileDrawn?.Invoke(playerIndex, drawnTile, true);
            OnTurnChanged?.Invoke(CurrentPlayerIndex, CurrentPhase);
            return true;
        }

        /// <summary>
        /// Yandan alınan taş kullanılamadığında yan taşı geri bırakıp desteden taş çekmeyi sağlar.
        /// </summary>
        public bool CancelSideDiscardAndDrawFromDeck(int playerIndex, out TileData drawnTile, out string error)
        {
            drawnTile = null;
            error = string.Empty;

            if (!MustUseSideTileId.HasValue || playerIndex != CurrentPlayerIndex)
            {
                error = "İptal edilecek yan taş bulunmuyor.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            var sideTile = hand.Tiles.FirstOrDefault(t => t.Id == MustUseSideTileId.Value);
            if (sideTile != null)
            {
                int prevPlayer = (playerIndex + 3) % 4;
                hand.RemoveTile(sideTile);
                DiscardPiles[prevPlayer].Add(sideTile);
            }

            MustUseSideTileId = null;
            CurrentPhase = TurnPhase.Draw;
            return DrawFromDeck(playerIndex, out drawnTile, out error);
        }

        /// <summary>
        /// Oyuncu seri perlerini masaya açar.
        /// </summary>
        public bool OpenSeries(int playerIndex, List<List<TileData>> proposedPerLists, out string error)
        {
            error = string.Empty;
            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda el açamazsınız.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.HasOpenedPairs)
            {
                error = "Daha önce çift açtığınız için seri per açamazsınız.";
                return false;
            }

            // Eğer oyuncu zaten daha önce seri açmışsa, baraj şartı aranmaksızın ek per açmaya yönlendir
            if (hand.HasOpenedSeries)
            {
                return OpenAdditionalSeries(playerIndex, proposedPerLists, out error);
            }

            if (!TileValidator.ValidateOpenSeriesProposal(
                proposedPerLists,
                Settings,
                TableScoreTracker,
                out var validatedPerGroups,
                out int totalScore,
                out error))
            {
                return false;
            }

            int totalTilesToOpen = validatedPerGroups.Sum(g => g.Tiles.Count);
            if (hand.Tiles.Count - totalTilesToOpen < 1)
            {
                error = "Son taşı yere atmak için elinizde en az 1 taş bırakmalısınız (veya Elden Bitiş yapmalısınız).";
                return false;
            }

            // Taşları oyuncunun elinden çıkar
            foreach (var per in validatedPerGroups)
            {
                foreach (var tile in per.Tiles)
                {
                    if (MustUseSideTileId.HasValue && tile.Id == MustUseSideTileId.Value)
                    {
                        MustUseSideTileId = null;
                    }
                    hand.RemoveTile(tile);
                }
            }

            hand.HasOpenedSeries = true;
            hand.OpenedScore += totalScore;

            TableScoreTracker.RegisterOpenedSeries(validatedPerGroups, playerIndex, totalScore);
            OnHandOpened?.Invoke(playerIndex, false, totalScore);
            return true;
        }

        /// <summary>
        /// Oyuncu zaten elini açmışsa, elindeki yeni 3+ taşlı perleri baraj şartı aranmaksızın masaya açar.
        /// </summary>
        public bool OpenAdditionalSeries(int playerIndex, List<List<TileData>> proposedPerLists, out string error)
        {
            error = string.Empty;
            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda ek per açamazsınız.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (!hand.HasOpenedSeries)
            {
                error = "Ek per açabilmek için önce elinizi açmış olmalısınız.";
                return false;
            }

            if (proposedPerLists == null || proposedPerLists.Count == 0)
            {
                error = "Açılacak per seçilmedi.";
                return false;
            }

            var validatedGroups = new List<PerGroup>();
            foreach (var list in proposedPerLists)
            {
                PerType type = TileValidator.IdentifyPerType(list);
                if (type != PerType.RunSameColor && type != PerType.GroupDiffColor)
                {
                    error = "Geçersiz ek per grubu.";
                    return false;
                }
                validatedGroups.Add(new PerGroup(type, list) { OwnerPlayerIndex = playerIndex });
            }

            // En az 1 taş yere atmak için elde kalmalı
            int totalTilesToOpen = validatedGroups.Sum(g => g.Tiles.Count);
            if (hand.Tiles.Count - totalTilesToOpen < 1)
            {
                error = "Son taşı yere atmak için elinizde en az 1 taş bırakmalısınız.";
                return false;
            }

            foreach (var per in validatedGroups)
            {
                foreach (var t in per.Tiles)
                {
                    if (MustUseSideTileId.HasValue && t.Id == MustUseSideTileId.Value)
                    {
                        MustUseSideTileId = null;
                    }
                    hand.RemoveTile(t);
                }
                TableScoreTracker.OpenedSeriesOnTable.Add(per);
            }

            return true;
        }

        public void ApplyPenalty(int playerIndex)
        {
            if (!WrongDiscardPenalties.ContainsKey(playerIndex))
                WrongDiscardPenalties[playerIndex] = 0;
            WrongDiscardPenalties[playerIndex]++;
        }

        /// <summary>
        /// Oyuncunun elindeki perleri otomatik tespit eder ve masaya açar.
        /// Eğer puan 101 barajının altındaysa +101 Hatalı Açılış Cezası verir.
        /// </summary>
        public bool AutoOpenValidMelds(int playerIndex, out string message, out bool isPenalty)
        {
            message = string.Empty;
            isPenalty = false;

            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                message = "Şu anda taş açamazsınız (Sıra sizde değil veya taş çekmediniz).";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            int totalScore = hand.CalculateTotalPerScore();
            int requiredScore = TableScoreTracker.CurrentRequiredScore;

            if (totalScore < requiredScore)
            {
                isPenalty = true;
                ApplyPenalty(playerIndex);
                message = $"Hatalı Açılış! Elinizdeki perlerin toplamı ({totalScore} Puan) {requiredScore} barajına yetersiz. +101 Ceza Puanı uygulandı!";
                return false;
            }

            // Uygun perleri al ve masaya aç
            var proposedMelds = hand.FindBestMelds(hand.Tiles);
            return OpenSeries(playerIndex, proposedMelds, out message);
        }

        /// <summary>
        /// Gösterge Kuralı: Oyuncunun elinde masadaki gösterge taşının eşi varsa,
        /// gösterge yaparak -101 puan avantajı (veya rakiplere ceza) kazanır.
        /// </summary>
        public bool TryShowIndicator(int playerIndex, out string message)
        {
            message = string.Empty;
            if (IsRoundOver)
            {
                message = "Oyun bitti.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.HasShownIndicator)
            {
                message = "Bu elde zaten gösterge yaptınız.";
                return false;
            }

            if (hand.HasOpenedHand || TurnCount > 4)
            {
                message = "Gösterge yalnızca turun başında (ilk sıranızda ve el açılmadan önce) yapılabilir.";
                return false;
            }

            var indicator = DeckManager.IndicatorTile;
            if (indicator == null)
            {
                message = "Gösterge taşı tanımlı değil.";
                return false;
            }

            var matchingTile = hand.Tiles.FirstOrDefault(t =>
                !t.IsFakeOkey && !t.IsOkey &&
                t.Color == indicator.Color && t.Number == indicator.Number);

            if (matchingTile == null)
            {
                message = $"Elinizde gösterge taşı ({indicator.Color} {indicator.Number}) bulunmuyor.";
                return false;
            }

            hand.HasShownIndicator = true;
            if (!WrongDiscardPenalties.ContainsKey(playerIndex))
                WrongDiscardPenalties[playerIndex] = 0;

            // -101 puan ödülü (WrongDiscardPenalty = 101 olduğu için -1 birim = -101 puan)
            WrongDiscardPenalties[playerIndex] -= 1;
            message = $"Gösterge yapıldı! ({indicator.Color} {indicator.Number}) -> -101 Puan Avantajı!";
            return true;
        }

        /// <summary>
        /// Oyuncu çiftlerini masaya açar.
        /// </summary>
        public bool OpenPairs(int playerIndex, List<List<TileData>> proposedPairs, out string error)
        {
            error = string.Empty;
            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda çift açamazsınız.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.HasOpenedSeries)
            {
                error = "Daha önce seri açtığınız için çift açamazsınız.";
                return false;
            }

            // Eğer oyuncu zaten daha önce çift açmışsa, baraj şartı aranmaksızın ek çiftlerini masaya açabilir
            if (hand.HasOpenedPairs)
            {
                return OpenAdditionalPairs(playerIndex, proposedPairs, out error);
            }

            if (!TileValidator.ValidateOpenPairsProposal(
                proposedPairs,
                Settings,
                TableScoreTracker,
                out var validatedPairs,
                out int totalPairs,
                out error))
            {
                return false;
            }

            int totalTilesToOpen = validatedPairs.Sum(p => p.Tiles.Count);
            if (hand.Tiles.Count - totalTilesToOpen < 1)
            {
                error = "Son taşı yere atmak için elinizde en az 1 taş bırakmalısınız (veya Elden Bitiş yapmalısınız).";
                return false;
            }

            // Taşları oyuncunun elinden çıkar
            foreach (var pair in validatedPairs)
            {
                foreach (var tile in pair.Tiles)
                {
                    if (MustUseSideTileId.HasValue && tile.Id == MustUseSideTileId.Value)
                    {
                        MustUseSideTileId = null;
                    }
                    hand.RemoveTile(tile);
                }
            }

            hand.HasOpenedPairs = true;
            hand.OpenedPairsCount += totalPairs;

            TableScoreTracker.RegisterOpenedPairs(validatedPairs, playerIndex, totalPairs);
            OnHandOpened?.Invoke(playerIndex, true, totalPairs);
            return true;
        }

        /// <summary>
        /// Daha önce çift açmış bir oyuncunun sonraki turlarda elinde oluşan yeni çiftleri masaya açmasını sağlar.
        /// </summary>
        public bool OpenAdditionalPairs(int playerIndex, List<List<TileData>> proposedPairs, out string error)
        {
            error = string.Empty;
            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda ek çift açamazsınız.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (!hand.HasOpenedPairs)
            {
                error = "Ek çift açabilmek için önce çiftten el açmış olmalısınız.";
                return false;
            }

            if (proposedPairs == null || proposedPairs.Count == 0)
            {
                error = "Açılacak çift seçilmedi.";
                return false;
            }

            var validatedPairs = new List<PerGroup>();
            foreach (var pairList in proposedPairs)
            {
                if (!TileValidator.ValidatePairPer(pairList))
                {
                    error = "Seçilen taşlar geçerli bir çift oluşturmuyor.";
                    return false;
                }
                validatedPairs.Add(new PerGroup(PerType.Pair, pairList) { OwnerPlayerIndex = playerIndex });
            }

            int totalTilesToOpen = validatedPairs.Sum(p => p.Tiles.Count);
            if (hand.Tiles.Count - totalTilesToOpen < 1)
            {
                error = "Son taşı yere atmak için elinizde en az 1 taş bırakmalısınız.";
                return false;
            }

            foreach (var pair in validatedPairs)
            {
                foreach (var t in pair.Tiles)
                {
                    if (MustUseSideTileId.HasValue && t.Id == MustUseSideTileId.Value)
                    {
                        MustUseSideTileId = null;
                    }
                    hand.RemoveTile(t);
                }
                TableScoreTracker.OpenedPairsOnTable.Add(pair);
            }

            hand.OpenedPairsCount += validatedPairs.Count;
            return true;
        }

        /// <summary>
        /// Masadaki açık bir pere elden taş işler.
        /// Son taş masaya işlenemez (yere atılarak bitilmelidir).
        /// </summary>
        public bool ProcessTile(int playerIndex, TileData tile, PerGroup targetPer, out string error)
        {
            error = string.Empty;
            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda taş işleyemezsiniz.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.Tiles.Count <= 1)
            {
                error = "Son taş masaya işlenemez! Eli bitirmek için son taşı yere atmalısınız.";
                return false;
            }

            if (!TeamManager.CanPlayerProcessTiles(playerIndex))
            {
                error = "Taş işleyebilmek için önce elinizi açmış olmalısınız (veya eşli modda ortağınız açmış olmalıdır).";
                return false;
            }

            if (!TileValidator.CanProcessTileIntoPer(targetPer, tile, out bool insertAtStart))
            {
                error = "Bu taş seçilen pere işlenemez.";
                return false;
            }

            if (MustUseSideTileId.HasValue && tile.Id == MustUseSideTileId.Value)
            {
                MustUseSideTileId = null;
            }

            hand.RemoveTile(tile);
            TableScoreTracker.AppendTileToExistingPer(targetPer, tile, insertAtStart);
            OnTablePerUpdated?.Invoke(playerIndex, targetPer);

            return true;
        }

        /// <summary>
        /// Masadaki açık bir perin içindeki Okey (Joker) taşını, eldeki gerçek taşla değiştirir (Okey Çalma / Takas).
        /// </summary>
        public bool SwapOkeyFromTablePer(int playerIndex, TileData replacementTile, PerGroup targetPer, out TileData stolenOkey, out string error)
        {
            stolenOkey = null;
            error = string.Empty;

            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda Okey takası yapamazsınız.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.HasOpenedPairs || !TeamManager.CanPlayerProcessTiles(playerIndex))
            {
                error = "Masadaki Okey'i alabilmek için seri el açmış olmalısınız.";
                return false;
            }

            if (!hand.Tiles.Any(t => t.Id == replacementTile.Id))
            {
                error = "Takas edilecek taş elinizde bulunmuyor.";
                return false;
            }

            if (!TileValidator.CanSwapOkeyFromPer(targetPer, replacementTile, out int okeyIndex))
            {
                error = "Bu taş perdeki Okey'in temsil ettiği taşla eşleşmiyor.";
                return false;
            }

            stolenOkey = targetPer.Tiles[okeyIndex];
            targetPer.Tiles[okeyIndex] = replacementTile;

            if (MustUseSideTileId.HasValue && replacementTile.Id == MustUseSideTileId.Value)
            {
                MustUseSideTileId = null;
            }

            hand.RemoveTile(replacementTile);

            // Alınan Okey'i tekrar joker olarak kullanılmak üzere sıfırla ve ele ekle
            stolenOkey.RepresentedColor = stolenOkey.Color;
            stolenOkey.RepresentedNumber = stolenOkey.Number;
            hand.AddTile(stolenOkey);

            OnTablePerUpdated?.Invoke(playerIndex, targetPer);
            return true;
        }

        /// <summary>
        /// Elden Bitiş (Hand-Out Finish): Oyuncu daha önce hiç el açmadan,
        /// 1 atılacak taş hariç elindeki tüm taşları geçerli per/çift olarak tek seferde açıp biter.
        /// </summary>
        public bool FinishHandOut(int playerIndex, TileData discardTile, out EndRoundType finishType, out string error)
        {
            finishType = EndRoundType.HandOutFinish;
            error = string.Empty;

            if (IsRoundOver || playerIndex != CurrentPlayerIndex || CurrentPhase != TurnPhase.Action)
            {
                error = "Şu anda elden bitiş yapılamaz.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (hand.HasOpenedHand || discardTile == null || !hand.Tiles.Any(t => t.Id == discardTile.Id))
            {
                error = "Elden bitiş koşulları sağlanmıyor.";
                return false;
            }

            var remaining = hand.Tiles.Where(t => t.Id != discardTile.Id).ToList();
            var melds = hand.FindBestMelds(remaining);
            int meldTilesCount = melds.Sum(m => m.Count);
            int totalScore = melds.Sum(m => m.Sum(t => t.IsOkey && t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number));

            if (meldTilesCount == remaining.Count && totalScore >= TableScoreTracker.CurrentRequiredScore)
            {
                OpenSeries(playerIndex, melds, out _);
                hand.RemoveTile(discardTile);
                DiscardPiles[playerIndex].Add(discardTile);
                LastDiscardedTile = discardTile;
                LastDiscardedPlayerIndex = playerIndex;
                MustUseSideTileId = null;

                OnTileDiscarded?.Invoke(playerIndex, discardTile);
                finishType = discardTile.IsOkey ? EndRoundType.HandOutOkeyFinish : EndRoundType.HandOutFinish;
                EndRound(finishType, playerIndex);
                return true;
            }

            var pairs = hand.FindBestPairs(remaining);
            if (pairs.Count * 2 == remaining.Count && pairs.Count >= TableScoreTracker.CurrentRequiredPairs)
            {
                OpenPairs(playerIndex, pairs, out _);
                hand.RemoveTile(discardTile);
                DiscardPiles[playerIndex].Add(discardTile);
                LastDiscardedTile = discardTile;
                LastDiscardedPlayerIndex = playerIndex;
                MustUseSideTileId = null;

                OnTileDiscarded?.Invoke(playerIndex, discardTile);
                finishType = EndRoundType.HandOutPairsFinish;
                EndRound(finishType, playerIndex);
                return true;
            }

            error = "Elinizdeki tüm taşlar eksiksiz per veya çift oluşturmuyor.";
            return false;
        }

        /// <summary>
        /// Yere bir taş atarak sırayı sonraki oyuncuya devreder veya oyunu bitirir.
        /// </summary>
        public bool DiscardTile(int playerIndex, TileData tileToDiscard, out bool isFinish, out EndRoundType finishType, out string error)
        {
            isFinish = false;
            finishType = EndRoundType.StandardFinish;
            error = string.Empty;

            if (IsRoundOver) { error = "Oyun bitti."; return false; }
            if (playerIndex != CurrentPlayerIndex) { error = "Sıra sizde değil."; return false; }
            if (CurrentPhase != TurnPhase.Action && CurrentPhase != TurnPhase.Discard)
            {
                error = "Taş atma aşamasında değilsiniz.";
                return false;
            }

            var hand = PlayerHands[playerIndex];
            if (!hand.Tiles.Any(t => t.Id == tileToDiscard.Id))
            {
                error = "Atılmak istenen taş oyuncunun elinde bulunmuyor.";
                return false;
            }

            // Yandan alınan taş kullanılmadıysa taş atılmasına izin verme
            if (EnforceStrictSideDiscardLock && MustUseSideTileId.HasValue &&
                hand.Tiles.Any(t => t.Id == MustUseSideTileId.Value))
            {
                error = "Yandan aldığınız taşı açılışta veya işlemede kullanmadan taş atamazsınız!";
                return false;
            }

            // Bitiş taşı değilse, yere işler taş veya Okey atma kontrolü (+101 Ceza)
            if (hand.Tiles.Count > 1)
            {
                CheckWrongDiscard(playerIndex, tileToDiscard);
            }

            // Taşı elden çıkar ve oyuncunun discard kuyruğuna ekle
            hand.RemoveTile(tileToDiscard);
            DiscardPiles[playerIndex].Add(tileToDiscard);
            LastDiscardedTile = tileToDiscard;
            LastDiscardedPlayerIndex = playerIndex;
            MustUseSideTileId = null;

            OnTileDiscarded?.Invoke(playerIndex, tileToDiscard);

            // Bitiş Kontrolü: Oyuncunun elinde hiç taş kalmadıysa (son taşını yere attıysa)
            if (hand.Tiles.Count == 0)
            {
                isFinish = true;
                if (tileToDiscard.IsOkey)
                {
                    finishType = EndRoundType.OkeyFinish;
                }
                else if (hand.HasOpenedPairs)
                {
                    finishType = EndRoundType.PairsFinish;
                }
                else
                {
                    finishType = EndRoundType.StandardFinish;
                }

                EndRound(finishType, playerIndex);
                return true;
            }

            // Sırayı sonraki oyuncuya geçir
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % 4;
            CurrentPhase = TurnPhase.Draw;
            TurnCount++;

            OnTurnChanged?.Invoke(CurrentPlayerIndex, CurrentPhase);
            return true;
        }

        private void CheckWrongDiscard(int playerIndex, TileData tile)
        {
            if (tile == null) return;

            // 1. Oyun bitmiyorsa yere Okey (Joker) atmak +101 cezadır
            if (tile.IsOkey)
            {
                ApplyPenalty(playerIndex);
                return;
            }

            // 2. Masada açık perler varsa ve atılan taş bir pere işlenebiliyorsa veya perdeki Okey'i alabiliyorsa +101 ceza alır
            foreach (var per in TableScoreTracker.OpenedSeriesOnTable)
            {
                if (TileValidator.CanProcessTileIntoPer(per, tile, out _) ||
                    TileValidator.CanSwapOkeyFromPer(per, tile, out _))
                {
                    ApplyPenalty(playerIndex);
                    break;
                }
            }
        }

        private void EndRound(EndRoundType finishType, int winnerIndex)
        {
            IsRoundOver = true;
            var result = ScoreManager.CalculateRoundScores(winnerIndex, finishType, PlayerHands, WrongDiscardPenalties);
            OnRoundEnded?.Invoke(result);
        }
    }
}
