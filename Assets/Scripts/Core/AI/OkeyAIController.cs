using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;

namespace Okey101.Core.AI
{
    /// <summary>
    /// 101 Okey bot yapay zekası.
    /// Katlamalı modda baraj stratejisi, eşli modda ortağa göre oynama,
    /// otomatik per bulma, el açma, taş işleme ve en uygun taşı atma kararlarını yönetir.
    /// </summary>
    public class OkeyAIController
    {
        public int BotPlayerIndex { get; }
        private readonly TurnManager _turnManager;
        private readonly Random _random;

        public OkeyAIController(int playerIndex, TurnManager turnManager, int? seed = null)
        {
            BotPlayerIndex = playerIndex;
            _turnManager = turnManager;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Botun tüm sırasını (Taş Çekme -> Açma/İşleme -> Taş Atma) oynamasını tetikler.
        /// </summary>
        public void ExecuteTurn()
        {
            if (_turnManager.IsRoundOver || _turnManager.CurrentPlayerIndex != BotPlayerIndex)
            {
                return;
            }

            // 0. AŞAMA: Gösterge Kontrolü (Elde gösterge taşı varsa -101 avantajı al)
            var hand = _turnManager.PlayerHands[BotPlayerIndex];
            if (!hand.HasShownIndicator && !hand.HasOpenedHand)
            {
                _turnManager.TryShowIndicator(BotPlayerIndex, out _);
            }

            // 1. AŞAMA: Taş Çekme (Draw Phase - Akıllı Yandan Alma veya Desteden Çekme)
            if (_turnManager.CurrentPhase == TurnPhase.Draw)
            {
                DecideAndDrawTile();
            }

            // 2. AŞAMA: El Açma, Okey Çalma veya Taş İşleme (Action Phase)
            if (_turnManager.CurrentPhase == TurnPhase.Action)
            {
                DecideAndPerformActions();
            }

            // 3. AŞAMA: Taş Atma (Discard Phase)
            if (!_turnManager.IsRoundOver && 
                (_turnManager.CurrentPhase == TurnPhase.Action || _turnManager.CurrentPhase == TurnPhase.Discard))
            {
                DecideAndDiscardTile();
            }
        }

        private void DecideAndDrawTile()
        {
            int prevPlayer = (BotPlayerIndex + 3) % 4;
            var sidePiles = _turnManager.DiscardPiles[prevPlayer];

            if (sidePiles.Count > 0)
            {
                var sideTile = sidePiles.Last();
                if (CanTileHelpOpenOrProcess(sideTile))
                {
                    if (_turnManager.DrawFromSideDiscard(BotPlayerIndex, out _, out _))
                    {
                        return;
                    }
                }
            }

            // Aksi halde desteden çek
            _turnManager.DrawFromDeck(BotPlayerIndex, out _, out _);
        }

        private bool CanTileHelpOpenOrProcess(TileData tile)
        {
            return _turnManager.CanTakeSideDiscard(BotPlayerIndex, tile, out _);
        }

        private void DecideAndPerformActions()
        {
            var hand = _turnManager.PlayerHands[BotPlayerIndex];

            // 1. Eğer henüz el açmamışsa önce Elden Bitiş (Hand-Out), sonra normal Seri/Çift açmayı dene
            if (!hand.HasOpenedHand)
            {
                if (TryFinishHandOut(hand))
                {
                    return;
                }
                TryOpenHand(hand);
            }

            // 2. Eğer el açmışsa (veya eşli modda ortağı açtığı için yetkisi varsa):
            //    Önce masadaki Okey'leri çal, sonra ek perleri/çiftleri aç, sonra tekil taşları işle
            if (_turnManager.TeamManager.CanPlayerProcessTiles(BotPlayerIndex))
            {
                if (!hand.HasOpenedPairs)
                {
                    TryStealOkeysFromTable(hand);
                    if (hand.HasOpenedSeries)
                    {
                        TryOpenAdditionalMelds(hand);
                    }
                }
                else
                {
                    TryOpenAdditionalPairs(hand);
                }
                TryProcessTiles(hand);
            }
        }

        private bool TryFinishHandOut(PlayerHand hand)
        {
            if (hand.Tiles.Count < 14) return false;

            // Okey taşını son atılacak taş olarak önceliklendir (HandOutOkeyFinish için)
            var candidates = hand.Tiles.OrderByDescending(t => t.IsOkey).ToList();
            foreach (var discardCandidate in candidates)
            {
                if (_turnManager.FinishHandOut(BotPlayerIndex, discardCandidate, out _, out _))
                {
                    return true;
                }
            }
            return false;
        }

        private void TryOpenHand(PlayerHand hand)
        {
            // Seri perleri bul (Okey ve 11-12-13-1 desteği ile)
            var foundMelds = FindAllPossibleMelds(hand.Tiles);
            if (foundMelds.Count > 0)
            {
                int totalMeltTiles = foundMelds.Sum(m => m.Count);
                // En az 1 taş yere atmak için elde kalmalı
                if (hand.Tiles.Count - totalMeltTiles >= 1)
                {
                    int totalPoints = foundMelds.Sum(m => new PerGroup(TileValidator.IdentifyPerType(m), m).CalculateScore());
                    int reqScore = _turnManager.TableScoreTracker.CurrentRequiredScore;

                    if (totalPoints >= reqScore)
                    {
                        if (_turnManager.OpenSeries(BotPlayerIndex, foundMelds, out _))
                        {
                            return;
                        }
                    }
                }
            }

            // Çiftleri bul
            var foundPairs = FindAllPossiblePairs(hand.Tiles);
            if (foundPairs.Count >= _turnManager.TableScoreTracker.CurrentRequiredPairs &&
                hand.Tiles.Count - (foundPairs.Count * 2) >= 1)
            {
                _turnManager.OpenPairs(BotPlayerIndex, foundPairs, out _);
            }
        }

        private void TryStealOkeysFromTable(PlayerHand hand)
        {
            bool swappedAny = true;
            while (swappedAny)
            {
                swappedAny = false;
                var tableSeries = _turnManager.TableScoreTracker.OpenedSeriesOnTable.ToList();

                foreach (var tile in hand.Tiles.Where(t => !t.IsOkey).ToList())
                {
                    foreach (var per in tableSeries)
                    {
                        if (_turnManager.SwapOkeyFromTablePer(BotPlayerIndex, tile, per, out _, out _))
                        {
                            swappedAny = true;
                            break;
                        }
                    }
                    if (swappedAny) break;
                }
            }
        }

        private void TryOpenAdditionalMelds(PlayerHand hand)
        {
            if (hand.Tiles.Count <= 3) return;
            var additionalMelds = FindAllPossibleMelds(hand.Tiles);
            if (additionalMelds.Count == 0) return;

            // En az 1 taş yere atmak için elde kalacak şekilde aç
            var validToOpen = new List<List<TileData>>();
            int remainingCount = hand.Tiles.Count;
            foreach (var meld in additionalMelds)
            {
                if (remainingCount - meld.Count >= 1)
                {
                    validToOpen.Add(meld);
                    remainingCount -= meld.Count;
                }
            }

            if (validToOpen.Count > 0)
            {
                _turnManager.OpenAdditionalSeries(BotPlayerIndex, validToOpen, out _);
            }
        }

        private void TryOpenAdditionalPairs(PlayerHand hand)
        {
            if (hand.Tiles.Count <= 2) return;
            var additionalPairs = FindAllPossiblePairs(hand.Tiles);
            if (additionalPairs.Count == 0) return;

            var validToOpen = new List<List<TileData>>();
            int remainingCount = hand.Tiles.Count;
            foreach (var pair in additionalPairs)
            {
                if (remainingCount - 2 >= 1)
                {
                    validToOpen.Add(pair);
                    remainingCount -= 2;
                }
            }

            if (validToOpen.Count > 0)
            {
                _turnManager.OpenAdditionalPairs(BotPlayerIndex, validToOpen, out _);
            }
        }

        private void TryProcessTiles(PlayerHand hand)
        {
            bool processedAny = true;
            while (processedAny && hand.Tiles.Count > 1) // En az 1 taş yere atmak için bırakılır
            {
                processedAny = false;
                var tableSeries = _turnManager.TableScoreTracker.OpenedSeriesOnTable.ToList();

                // Eşli modda ortağın açtığı serilere öncelik ver
                int teammate = _turnManager.TeamManager.GetTeammateIndex(BotPlayerIndex);
                if (teammate != -1)
                {
                    tableSeries = tableSeries.OrderByDescending(p => p.OwnerPlayerIndex == teammate).ToList();
                }

                foreach (var tile in hand.Tiles.ToList())
                {
                    // Okey taşını sonuna kadar elde sakla (Okey ile bitiş için)
                    if (tile.IsOkey && hand.Tiles.Count > 2) continue;

                    foreach (var per in tableSeries)
                    {
                        if (_turnManager.ProcessTile(BotPlayerIndex, tile, per, out _))
                        {
                            processedAny = true;
                            break;
                        }
                    }

                    if (processedAny) break;
                }

                // İşleme sonrası masadaki perlerden yeni bir Okey çalma fırsatı doğmuş olabilir
                if (processedAny && !hand.HasOpenedPairs)
                {
                    TryStealOkeysFromTable(hand);
                }
            }
        }

        private void DecideAndDiscardTile()
        {
            var hand = _turnManager.PlayerHands[BotPlayerIndex];
            if (hand.Tiles.Count == 0) return;

            // Eğer yandan alınan taş bir şekilde kullanılamadıysa, yan taşı geri bırakıp desteden çek
            if (_turnManager.MustUseSideTileId.HasValue &&
                hand.Tiles.Any(t => t.Id == _turnManager.MustUseSideTileId.Value))
            {
                _turnManager.CancelSideDiscardAndDrawFromDeck(BotPlayerIndex, out _, out _);
                DecideAndPerformActions();
                if (_turnManager.IsRoundOver || hand.Tiles.Count == 0) return;
            }

            TileData tileToDiscard = ChooseBestDiscardTile(hand);
            _turnManager.DiscardTile(BotPlayerIndex, tileToDiscard, out _, out _, out _);
        }

        private TileData ChooseBestDiscardTile(PlayerHand hand)
        {
            // Son taş kalmışsa doğrudan onu at
            if (hand.Tiles.Count == 1)
            {
                return hand.Tiles[0];
            }

            // Okey olmayan taşları aday yap (Okey asla ara turda yere atılmaz)
            var candidates = hand.Tiles.Where(t => !t.IsOkey).ToList();
            if (candidates.Count == 0)
            {
                return hand.Tiles.First();
            }

            // Masaya işlenebilen veya masadaki Okey'i alabilen taşları (+101 ceza yememek için) filtrele
            var safeCandidates = candidates.Where(t => !IsPlayableOnTable(t)).ToList();
            if (safeCandidates.Count > 0)
            {
                candidates = safeCandidates;
            }

            // Halihazırda elde geçerli per oluşturan taşları koru
            var currentMelds = hand.FindBestMelds(hand.Tiles);
            var meldTileIds = currentMelds.SelectMany(m => m.Select(t => t.Id)).ToHashSet();

            var countsByColorNumber = hand.Tiles.Where(t => !t.IsOkey)
                .GroupBy(t => new { t.Color, t.Number })
                .ToDictionary(g => g.Key, g => g.Count());

            var countsByNumber = hand.Tiles.Where(t => !t.IsOkey)
                .GroupBy(t => t.Number)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Color).Distinct().Count());

            var scoredTiles = candidates.Select(tile =>
            {
                int discardPriority = 0;

                // Tam bir perin parçasıysa atma
                if (meldTileIds.Contains(tile.Id)) discardPriority -= 120;

                // Çifti varsa koru
                if (countsByColorNumber.TryGetValue(new { tile.Color, tile.Number }, out int pairCnt) && pairCnt > 1)
                {
                    discardPriority -= hand.HasOpenedPairs ? 150 : 45;
                }

                // Aynı sayıdan farklı renkler varsa (grup potansiyeli) koru
                if (countsByNumber.TryGetValue(tile.Number, out int groupColors) && groupColors > 1)
                {
                    discardPriority -= (groupColors - 1) * 18;
                }

                // Aynı renkten bitişik (+-1) veya 1 aralıklı (+-2) taş varsa (seri potansiyeli) koru
                bool hasDirectNeighbor = hand.Tiles.Any(t => !t.IsOkey && t.Id != tile.Id && t.Color == tile.Color &&
                    (Math.Abs(t.Number - tile.Number) == 1 || (tile.Number == 13 && t.Number == 1) || (tile.Number == 1 && t.Number == 13)));
                bool hasGapNeighbor = hand.Tiles.Any(t => !t.IsOkey && t.Id != tile.Id && t.Color == tile.Color && Math.Abs(t.Number - tile.Number) == 2);

                if (hasDirectNeighbor) discardPriority -= 30;
                else if (hasGapNeighbor) discardPriority -= 12;

                // Yüksek sayılı yalnız taşları elde ceza kalmaması için öncelikli elden çıkar
                discardPriority += tile.Number * 2;

                return new { Tile = tile, Priority = discardPriority };
            }).OrderByDescending(x => x.Priority).ToList();

            return scoredTiles.First().Tile;
        }

        private bool IsPlayableOnTable(TileData tile)
        {
            if (tile == null) return false;
            foreach (var per in _turnManager.TableScoreTracker.OpenedSeriesOnTable)
            {
                if (TileValidator.CanProcessTileIntoPer(per, tile, out _) ||
                    TileValidator.CanSwapOkeyFromPer(per, tile, out _))
                {
                    return true;
                }
            }
            return false;
        }

        public List<List<TileData>> FindAllPossibleMelds(List<TileData> tiles)
        {
            var tempHand = new PlayerHand(BotPlayerIndex);
            var found = tempHand.FindBestMelds(tiles);
            // Her bir perin TileValidator tarafından da doğrulandığından emin ol
            return found.Where(m =>
            {
                var type = TileValidator.IdentifyPerType(m);
                return type == PerType.RunSameColor || type == PerType.GroupDiffColor;
            }).ToList();
        }

        public List<List<TileData>> FindAllPossiblePairs(List<TileData> tiles)
        {
            var tempHand = new PlayerHand(BotPlayerIndex);
            var found = tempHand.FindBestPairs(tiles);
            return found.Where(TileValidator.ValidatePairPer).ToList();
        }
    }
}
