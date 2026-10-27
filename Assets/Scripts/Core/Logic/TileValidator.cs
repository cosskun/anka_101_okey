using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// 101 Okey oyununun tüm taş, per, el açma ve işleme kurallarını doğrulayan kural motoru.
    /// </summary>
    public static class TileValidator
    {
        /// <summary>
        /// Verilen taş grubunun geçerli bir seri per (aynı renk ardışık), grup per (aynı sayı farklı renk)
        /// veya çift olup olmadığını denetler.
        /// </summary>
        public static PerType IdentifyPerType(List<TileData> tiles)
        {
            if (tiles == null || tiles.Count < 2) return PerType.Invalid;

            if (tiles.Count == 2 && ValidatePairPer(tiles))
            {
                return PerType.Pair;
            }

            if (tiles.Count >= 3)
            {
                if (ValidateRunPer(tiles))
                {
                    return PerType.RunSameColor;
                }

                if (ValidateGroupPer(tiles))
                {
                    return PerType.GroupDiffColor;
                }
            }

            return PerType.Invalid;
        }

        /// <summary>
        /// Aynı renk ardışık seri per kontrolü (Örn: Mavi 7-8-9 veya Siyah 11-12-13-1).
        /// Okey taşlarının joker ikamesini otomatik çözer.
        /// </summary>
        public static bool ValidateRunPer(List<TileData> tiles)
        {
            if (tiles == null || tiles.Count < 3 || tiles.Count > 13) return false;

            // Okey olmayan referans taşları bul
            var nonOkeyTiles = tiles.Where(t => !t.IsOkey).ToList();
            if (nonOkeyTiles.Count == 0)
            {
                // Tümü okey ise (nadir durum), geçerli sayılır
                return true;
            }

            // Tüm okey olmayan taşların rengi aynı olmalıdır
            TileColor expectedColor = nonOkeyTiles[0].Color;
            if (nonOkeyTiles.Any(t => t.Color != expectedColor))
            {
                return false;
            }

            // Okey taşlarına renk ata
            foreach (var t in tiles.Where(x => x.IsOkey))
            {
                t.RepresentedColor = expectedColor;
            }

            // Ardışıklık kontrolü (Normal 1..13 veya 13'ten sonra 1 bağlama: 11-12-13-1)
            // Taşların diziliş sırasını kontrol edelim; eğer sırasız seçilmişse (ve Okey yoksa) sıralayıp tekrar deneyelim:
            if (CheckSequentialRun(tiles))
            {
                return true;
            }

            if (nonOkeyTiles.Count == tiles.Count)
            {
                // Okey içermeyen sırasız seçimleri (örn: 8, 6, 7 veya 1, 12, 13) doğru sıraya dizmeyi dene
                var sortedAsc = tiles.OrderBy(t => t.Number).ToList();
                if (CheckSequentialRun(sortedAsc))
                {
                    tiles.Clear();
                    tiles.AddRange(sortedAsc);
                    return true;
                }

                // 12-13-1 wrap durumu (1 sona gelecek şekilde)
                if (tiles.Any(t => t.Number == 1) && tiles.Any(t => t.Number == 13))
                {
                    var sortedWrap = tiles.Where(t => t.Number != 1).OrderBy(t => t.Number)
                        .Concat(tiles.Where(t => t.Number == 1)).ToList();
                    if (CheckSequentialRun(sortedWrap))
                    {
                        tiles.Clear();
                        tiles.AddRange(sortedWrap);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckSequentialRun(List<TileData> tiles)
        {
            // Örnek: [11, 12, OKEY, 1] -> Okey 13 olmalı
            // Sıradaki her eleman bir öncekinin 1 fazlası olmalı. 13'ten sonra sadece 1 gelebilir.
            int? previousVal = null;

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];

                if (tile.IsOkey)
                {
                    if (previousVal.HasValue)
                    {
                        int nextExpected = previousVal.Value == 13 ? 1 : previousVal.Value + 1;
                        tile.RepresentedNumber = nextExpected;
                        previousVal = nextExpected;
                    }
                    else
                    {
                        // İlk taş Okey ise, bir sonraki taşa göre değer belirle
                        int nextNonOkeyIndex = -1;
                        for (int j = i + 1; j < tiles.Count; j++)
                        {
                            if (!tiles[j].IsOkey)
                            {
                                nextNonOkeyIndex = j;
                                break;
                            }
                        }

                        if (nextNonOkeyIndex != -1)
                        {
                            int diff = nextNonOkeyIndex - i;
                            int derivedVal = tiles[nextNonOkeyIndex].Number - diff;
                            if (derivedVal < 1) return false; // 1'den küçük olamaz
                            tile.RepresentedNumber = derivedVal;
                            previousVal = derivedVal;
                        }
                        else
                        {
                            tile.RepresentedNumber = 1;
                            previousVal = 1;
                        }
                    }
                }
                else
                {
                    int currentVal = tile.Number;
                    if (previousVal.HasValue)
                    {
                        bool isStandardNext = (currentVal == previousVal.Value + 1);
                        bool is13to1Wrap = (previousVal.Value == 13 && currentVal == 1);

                        // 13-1-2 geçersizdir (1'den sonra 2 gelemez eğer 13'ten bağlanmışsa)
                        if (!isStandardNext && !is13to1Wrap)
                        {
                            return false;
                        }

                        // Eğer 13'ten sonra 1 geldiyse bu serinin son taşı olmak zorundadır!
                        if (is13to1Wrap && i != tiles.Count - 1)
                        {
                            return false;
                        }
                    }
                    previousVal = currentVal;
                }
            }

            return true;
        }

        /// <summary>
        /// Aynı sayı farklı renkler grup per kontrolü (Örn: Kırmızı 7, Mavi 7, Siyah 7).
        /// En az 3, en fazla 4 taş olabilir. Renkler benzersiz olmalıdır.
        /// </summary>
        public static bool ValidateGroupPer(List<TileData> tiles)
        {
            if (tiles == null || tiles.Count < 3 || tiles.Count > 4) return false;

            var nonOkeyTiles = tiles.Where(t => !t.IsOkey).ToList();
            if (nonOkeyTiles.Count == 0) return true;

            int expectedNumber = nonOkeyTiles[0].Number;
            if (nonOkeyTiles.Any(t => t.Number != expectedNumber))
            {
                return false;
            }

            // Renklerin benzersizliği kontrolü
            var usedColors = new HashSet<TileColor>();
            foreach (var tile in nonOkeyTiles)
            {
                if (usedColors.Contains(tile.Color))
                {
                    return false; // Aynı renkten iki adet olamaz!
                }
                usedColors.Add(tile.Color);
            }

            // Okey taşları için boşta kalan renkleri ata
            var allColors = new[] { TileColor.Red, TileColor.Yellow, TileColor.Blue, TileColor.Black };
            var availableColors = allColors.Where(c => !usedColors.Contains(c)).ToList();

            foreach (var okey in tiles.Where(t => t.IsOkey))
            {
                okey.RepresentedNumber = expectedNumber;
                if (availableColors.Count > 0)
                {
                    okey.RepresentedColor = availableColors[0];
                    availableColors.RemoveAt(0);
                }
            }

            return true;
        }

        /// <summary>
        /// Çift kontrolü (Aynı renk ve aynı sayı 2 taş).
        /// </summary>
        public static bool ValidatePairPer(List<TileData> tiles)
        {
            if (tiles == null || tiles.Count != 2) return false;

            var t1 = tiles[0];
            var t2 = tiles[1];

            if (t1.IsOkey && t2.IsOkey) return true;

            if (t1.IsOkey)
            {
                t1.RepresentedColor = t2.Color;
                t1.RepresentedNumber = t2.Number;
                return true;
            }

            if (t2.IsOkey)
            {
                t2.RepresentedColor = t1.Color;
                t2.RepresentedNumber = t1.Number;
                return true;
            }

            return t1.Color == t2.Color && t1.Number == t2.Number;
        }

        /// <summary>
        /// Oyuncunun seri perler açma teklifini ve baraj uygunluğunu doğrular.
        /// </summary>
        public static bool ValidateOpenSeriesProposal(
            List<List<TileData>> proposedPerLists,
            GameSettings settings,
            TableScoreTracker tracker,
            out List<PerGroup> validatedPerGroups,
            out int totalScore,
            out string errorMessage)
        {
            validatedPerGroups = new List<PerGroup>();
            totalScore = 0;
            errorMessage = string.Empty;

            if (proposedPerLists == null || proposedPerLists.Count == 0)
            {
                errorMessage = "Açılacak hiç per seçilmedi.";
                return false;
            }

            foreach (var tileList in proposedPerLists)
            {
                PerType type = IdentifyPerType(tileList);
                if (type != PerType.RunSameColor && type != PerType.GroupDiffColor)
                {
                    errorMessage = $"Geçersiz per grubu tespit edildi: {string.Join(", ", tileList)}";
                    return false;
                }

                var perGroup = new PerGroup(type, tileList);
                totalScore += perGroup.CalculateScore();
                validatedPerGroups.Add(perGroup);
            }

            int requiredScore = tracker.CurrentRequiredScore;
            if (totalScore < requiredScore)
            {
                errorMessage = settings.ScoringRule == ScoringRule.Folded
                    ? $"Katlamalı modda el açmak için en az {requiredScore} puan gerekir (Eliniz: {totalScore})."
                    : $"Standart modda el açmak için en az {settings.BaseMinScoreToOpen} puan gerekir (Eliniz: {totalScore}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Oyuncunun çift açma teklifini ve çift barajı uygunluğunu doğrular.
        /// </summary>
        public static bool ValidateOpenPairsProposal(
            List<List<TileData>> proposedPairLists,
            GameSettings settings,
            TableScoreTracker tracker,
            out List<PerGroup> validatedPairs,
            out int totalPairsCount,
            out string errorMessage)
        {
            validatedPairs = new List<PerGroup>();
            totalPairsCount = 0;
            errorMessage = string.Empty;

            if (proposedPairLists == null || proposedPairLists.Count == 0)
            {
                errorMessage = "Açılacak hiç çift seçilmedi.";
                return false;
            }

            foreach (var tileList in proposedPairLists)
            {
                if (!ValidatePairPer(tileList))
                {
                    errorMessage = $"Geçersiz çift grubu tespit edildi: {string.Join(", ", tileList)}";
                    return false;
                }

                validatedPairs.Add(new PerGroup(PerType.Pair, tileList));
            }

            totalPairsCount = validatedPairs.Count;
            int requiredPairs = tracker.CurrentRequiredPairs;

            if (totalPairsCount < requiredPairs)
            {
                errorMessage = settings.ScoringRule == ScoringRule.Folded
                    ? $"Katlamalı modda çift açmak için en az {requiredPairs} çift gerekir (Eliniz: {totalPairsCount})."
                    : $"Standart modda çift açmak için en az {settings.BaseMinPairsToOpen} çift gerekir (Eliniz: {totalPairsCount}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Masadaki mevcut açık bir pere tek bir taşın işlenip işlenemeyeceğini kontrol eder.
        /// 11-12-13-1 serilerinin sonuna 2 eklenmesini engeller ve Okey sınırlarını doğrular.
        /// </summary>
        public static bool CanProcessTileIntoPer(PerGroup targetPer, TileData tile, out bool insertAtStart)
        {
            insertAtStart = false;
            if (targetPer == null || tile == null || targetPer.Type == PerType.Pair) return false;
            if (targetPer.Tiles == null || targetPer.Tiles.Count == 0) return false;

            if (targetPer.Type == PerType.RunSameColor)
            {
                if (targetPer.Tiles.Count >= 13) return false;

                // Mevcut perin Okey temsillerini güncelle
                ValidateRunPer(targetPer.Tiles);

                var nonOkey = targetPer.Tiles.FirstOrDefault(t => !t.IsOkey);
                TileColor perColor = nonOkey != null ? nonOkey.Color : targetPer.Tiles[0].RepresentedColor;

                if (!tile.IsOkey && tile.Color != perColor)
                {
                    return false;
                }

                var firstTile = targetPer.Tiles.First();
                var lastTile = targetPer.Tiles.Last();

                int firstVal = firstTile.IsOkey && firstTile.RepresentedNumber > 0 ? firstTile.RepresentedNumber : firstTile.Number;
                int lastVal = lastTile.IsOkey && lastTile.RepresentedNumber > 0 ? lastTile.RepresentedNumber : lastTile.Number;

                // Perin sonunda 13-1 bağlantısı (wrap) var mı? Varsa sona hiçbir taş eklenemez!
                bool has13To1WrapAtEnd = false;
                if (targetPer.Tiles.Count >= 2 && lastVal == 1)
                {
                    var prevTile = targetPer.Tiles[targetPer.Tiles.Count - 2];
                    int prevVal = prevTile.IsOkey && prevTile.RepresentedNumber > 0 ? prevTile.RepresentedNumber : prevTile.Number;
                    if (prevVal == 13)
                    {
                        has13To1WrapAtEnd = true;
                    }
                }

                // 1. Sona eklenebilir mi? (Örn: 7-8-9 perinin sonuna 10 veya 11-12-13 perinin sonuna 1)
                if (!has13To1WrapAtEnd)
                {
                    if (lastVal < 13 && (tile.IsOkey || tile.Number == lastVal + 1))
                    {
                        var testEnd = targetPer.Tiles.Select(t => t.Clone()).ToList();
                        testEnd.Add(tile.Clone());
                        if (ValidateRunPer(testEnd))
                        {
                            insertAtStart = false;
                            return true;
                        }
                    }

                    // 13'ten sonra 1 bağlama (ancak serinin başında zaten 1 yoksa)
                    if (lastVal == 13 && firstVal != 1 && (tile.IsOkey || tile.Number == 1))
                    {
                        var testWrap = targetPer.Tiles.Select(t => t.Clone()).ToList();
                        testWrap.Add(tile.Clone());
                        if (ValidateRunPer(testWrap))
                        {
                            insertAtStart = false;
                            return true;
                        }
                    }
                }

                // 2. Başa eklenebilir mi? (Örn: 7-8-9 perinin başına 6)
                if (firstVal > 1 && (tile.IsOkey || tile.Number == firstVal - 1))
                {
                    // Eğer sonda 13-1 wrap varsa ve başa 1 eklenmeye çalışılıyorsa (1..13..1) engelle
                    if (!(has13To1WrapAtEnd && (firstVal - 1) == 1))
                    {
                        var testStart = targetPer.Tiles.Select(t => t.Clone()).ToList();
                        testStart.Insert(0, tile.Clone());
                        if (ValidateRunPer(testStart))
                        {
                            insertAtStart = true;
                            return true;
                        }
                    }
                }
            }
            else if (targetPer.Type == PerType.GroupDiffColor)
            {
                // 3 taşlı bir gruba 4. renk eklenebilir (4 taşlı gruba hiçbir taş/Okey eklenemez)
                if (targetPer.Tiles.Count >= 4) return false;

                ValidateGroupPer(targetPer.Tiles);

                var nonOkey = targetPer.Tiles.FirstOrDefault(t => !t.IsOkey);
                int targetNum = nonOkey != null ? nonOkey.Number : targetPer.Tiles[0].RepresentedNumber;
                if (!tile.IsOkey && tile.Number != targetNum) return false;

                var existingColors = targetPer.Tiles
                    .Where(t => !t.IsOkey)
                    .Select(t => t.Color)
                    .ToHashSet();

                if (!tile.IsOkey && existingColors.Contains(tile.Color)) return false;

                insertAtStart = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Masadaki mevcut açık bir pere tek bir taşın işlenip işlenemeyeceğini kontrol eder (Basit aşırı yükleme).
        /// </summary>
        public static bool CanProcessTileIntoPer(PerGroup targetPer, TileData tile)
        {
            return CanProcessTileIntoPer(targetPer, tile, out _);
        }

        /// <summary>
        /// Masadaki açık bir seri veya grup perinin içinde Okey (Joker) varsa,
        /// oyuncunun elindeki gerçek taşla bu Okey'in yer değiştirip değiştiremeyeceğini (Okey Çalma) kontrol eder.
        /// Çift perlerinden Okey alınamaz.
        /// </summary>
        public static bool CanSwapOkeyFromPer(PerGroup targetPer, TileData replacementTile, out int okeyIndex)
        {
            okeyIndex = -1;
            if (targetPer == null || replacementTile == null || targetPer.Type == PerType.Pair)
                return false;

            if (replacementTile.IsOkey)
                return false;

            // Önce perin mevcut Okey temsillerinin güncel olduğundan emin ol
            if (targetPer.Type == PerType.RunSameColor)
            {
                if (!ValidateRunPer(targetPer.Tiles)) return false;
            }
            else if (targetPer.Type == PerType.GroupDiffColor)
            {
                if (!ValidateGroupPer(targetPer.Tiles)) return false;
            }
            else
            {
                return false;
            }

            for (int i = 0; i < targetPer.Tiles.Count; i++)
            {
                var t = targetPer.Tiles[i];
                if (!t.IsOkey) continue;

                bool matchesRepresented = (t.RepresentedColor == replacementTile.Color &&
                                           t.RepresentedNumber == replacementTile.Number);

                // Grup perinde birden fazla eksik renk varsa (ör. 3 taşlı grupta 1 Okey varsa),
                // aynı sayıda ve grupta henüz bulunmayan herhangi bir renkle de değiştirilebilir
                bool matchesGroupAlternative = false;
                if (targetPer.Type == PerType.GroupDiffColor &&
                    t.RepresentedNumber == replacementTile.Number)
                {
                    bool colorAlreadyInGroup = targetPer.Tiles
                        .Where((tile, idx) => idx != i && !tile.IsOkey)
                        .Any(tile => tile.Color == replacementTile.Color);
                    if (!colorAlreadyInGroup)
                    {
                        matchesGroupAlternative = true;
                    }
                }

                if (matchesRepresented || matchesGroupAlternative)
                {
                    var testList = targetPer.Tiles.Select(x => x.Clone()).ToList();
                    testList[i] = replacementTile.Clone();

                    bool validAfterSwap = targetPer.Type == PerType.RunSameColor
                        ? ValidateRunPer(testList)
                        : ValidateGroupPer(testList);

                    if (validAfterSwap)
                    {
                        okeyIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
