using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Enums;

namespace Okey101.Core.Data
{
    /// <summary>
    /// Bir oyuncunun elindeki taşları, ıstaka yerleşimini ve el açma durumunu yöneten veri sınıfı.
    /// Çift sıralı ıstaka (2 sıra x 13 sütun = 26 slot) modelini destekler.
    /// </summary>
    [Serializable]
    public class PlayerHand
    {
        public const int RACK_ROWS = 2;
        public const int RACK_COLS = 13;

        public int PlayerIndex { get; set; }
        public List<TileData> Tiles { get; private set; }

        // Çift sıralı ıstaka slotları: [satır, sütun] -> null veya TileData
        public TileData[,] RackGrid { get; private set; }

        // El Açma Durumları
        public bool HasOpenedSeries { get; set; }
        public bool HasOpenedPairs { get; set; }
        public bool HasShownIndicator { get; set; }
        public int OpenedScore { get; set; }
        public int OpenedPairsCount { get; set; }

        public bool HasOpenedHand => HasOpenedSeries || HasOpenedPairs;

        public PlayerHand(int playerIndex)
        {
            PlayerIndex = playerIndex;
            Tiles = new List<TileData>();
            RackGrid = new TileData[RACK_ROWS, RACK_COLS];
            ResetHand();
        }

        public void ResetHand()
        {
            Tiles.Clear();
            HasOpenedSeries = false;
            HasOpenedPairs = false;
            HasShownIndicator = false;
            OpenedScore = 0;
            OpenedPairsCount = 0;

            for (int r = 0; r < RACK_ROWS; r++)
            {
                for (int c = 0; c < RACK_COLS; c++)
                {
                    RackGrid[r, c] = null;
                }
            }
        }

        /// <summary>
        /// Oyuncuya yeni taş ekler ve ıstakada ilk boş slota yerleştirir.
        /// </summary>
        public void AddTile(TileData tile)
        {
            if (tile == null) return;
            Tiles.Add(tile);

            // Istakada boş slot bul
            bool placed = false;
            for (int r = 0; r < RACK_ROWS && !placed; r++)
            {
                for (int c = 0; c < RACK_COLS; c++)
                {
                    if (RackGrid[r, c] == null)
                    {
                        RackGrid[r, c] = tile;
                        placed = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Oyuncunun elinden taş çıkarır (Taş atıldığında veya masaya açıldığında).
        /// </summary>
        public bool RemoveTile(TileData tile)
        {
            if (tile == null) return false;
            bool removed = Tiles.Remove(tile);

            // Istaka gridinden de temizle
            for (int r = 0; r < RACK_ROWS; r++)
            {
                for (int c = 0; c < RACK_COLS; c++)
                {
                    if (RackGrid[r, c] != null && RackGrid[r, c].Id == tile.Id)
                    {
                        RackGrid[r, c] = null;
                        return true;
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// 101 Okey Akıllı Seri Dizilimi:
        /// Elinizdeki geçerli perleri (Seri ve Grupları) tespit eder, aralarına 1'er boşluk bırakarak
        /// ıstakaya yerleştirir. Per oluşturmayan tekil taşları ıstakanın sonuna/alt sıraya ayırır.
        /// </summary>
        public void SortByColorAndNumber()
        {
            // Grid'i tamamen sıfırla
            for (int r = 0; r < RACK_ROWS; r++)
                for (int c = 0; c < RACK_COLS; c++)
                    RackGrid[r, c] = null;

            var unassigned = new List<TileData>(Tiles);
            var melds = FindBestMelds(unassigned);

            // Perlerde kullanılan taşları unassigned listesinden çıkar
            foreach (var m in melds)
            {
                foreach (var t in m)
                {
                    unassigned.Remove(t);
                }
            }

            int row = 0;
            int col = 0;

            // 1. Perleri aralarında 1 boşluk olacak şekilde yerleştir
            foreach (var meld in melds)
            {
                // Eğer mevcut satırda bu pere yetecek kadar yer yoksa alt satıra geç
                if (col + meld.Count > RACK_COLS)
                {
                    if (row == 0)
                    {
                        row = 1;
                        col = 0;
                    }
                }

                foreach (var tile in meld)
                {
                    if (col < RACK_COLS && row < RACK_ROWS)
                    {
                        RackGrid[row, col++] = tile;
                    }
                    else if (row == 0)
                    {
                        row = 1;
                        col = 0;
                        if (col < RACK_COLS) RackGrid[row, col++] = tile;
                    }
                }

                // Per bittiğinde 1 boşluk bırak (aralık)
                if (col < RACK_COLS)
                {
                    col++; // 1 slot boşluk
                }
                else if (row == 0)
                {
                    row = 1;
                    col = 0;
                }
            }

            // 2. Kalan per dışı tekil taşları renge/sayıya göre sıralayıp kalan boşluklara koy
            var sortedLeftovers = unassigned.OrderBy(t => t.Color).ThenBy(t => t.Number).ToList();
            foreach (var tile in sortedLeftovers)
            {
                // İlk boş slota yerleştir
                bool placed = false;
                while (row < RACK_ROWS && !placed)
                {
                    if (col < RACK_COLS)
                    {
                        if (RackGrid[row, col] == null)
                        {
                            RackGrid[row, col] = tile;
                            placed = true;
                        }
                        col++;
                    }
                    else
                    {
                        row++;
                        col = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Akıllı Çift Dizilimi:
        /// Elinizdeki çiftleri tespit edip aralarında 1'er boşluk bırakarak yerleştirir.
        /// </summary>
        public void SortByPairs()
        {
            for (int r = 0; r < RACK_ROWS; r++)
                for (int c = 0; c < RACK_COLS; c++)
                    RackGrid[r, c] = null;

            var unassigned = new List<TileData>(Tiles);
            var pairs = new List<List<TileData>>();

            var grouped = unassigned.GroupBy(t => new { t.Color, t.Number }).ToList();
            foreach (var grp in grouped)
            {
                var list = grp.ToList();
                while (list.Count >= 2)
                {
                    var p = new List<TileData> { list[0], list[1] };
                    pairs.Add(p);
                    unassigned.Remove(list[0]);
                    unassigned.Remove(list[1]);
                    list.RemoveAt(1);
                    list.RemoveAt(0);
                }
            }

            int row = 0;
            int col = 0;

            foreach (var pair in pairs)
            {
                if (col + 2 > RACK_COLS)
                {
                    if (row == 0) { row = 1; col = 0; }
                }

                if (col < RACK_COLS && row < RACK_ROWS) RackGrid[row, col++] = pair[0];
                if (col < RACK_COLS && row < RACK_ROWS) RackGrid[row, col++] = pair[1];

                if (col < RACK_COLS) col++; // 1 boşluk
                else if (row == 0) { row = 1; col = 0; }
            }

            // Kalan tekil taşlar
            var sortedLeftovers = unassigned.OrderBy(t => t.Color).ThenBy(t => t.Number).ToList();
            foreach (var tile in sortedLeftovers)
            {
                bool placed = false;
                while (row < RACK_ROWS && !placed)
                {
                    if (col < RACK_COLS)
                    {
                        if (RackGrid[row, col] == null)
                        {
                            RackGrid[row, col] = tile;
                            placed = true;
                        }
                        col++;
                    }
                    else
                    {
                        row++;
                        col = 0;
                    }
                }
            }
        }

        public List<List<TileData>> FindBestMelds(List<TileData> tiles = null)
        {
            var sourceTiles = tiles ?? Tiles;
            var melds = new List<List<TileData>>();
            var used = new HashSet<int>();

            // 1. Aynı Renk Ardışık Seriler (Örn: Mavi 7-8-9-10 ve 11-12-13-1)
            var colorGroups = sourceTiles.Where(t => !t.IsOkey).GroupBy(t => t.Color);
            foreach (var cg in colorGroups)
            {
                var sorted = cg.OrderBy(t => t.Number).ToList();
                var cur = new List<TileData>();

                for (int i = 0; i < sorted.Count; i++)
                {
                    if (cur.Count == 0)
                    {
                        cur.Add(sorted[i]);
                    }
                    else
                    {
                        if (sorted[i].Number == cur.Last().Number + 1)
                        {
                            cur.Add(sorted[i]);
                        }
                        else if (sorted[i].Number == cur.Last().Number)
                        {
                            // Aynı sayı kopya taş, seriye dahil etme
                            continue;
                        }
                        else
                        {
                            // 13'ten sonra 1 bağlama kontrolü (11-12-13-1 veya 12-13-1)
                            if (cur.Count >= 2 && cur.Last().Number == 13)
                            {
                                var oneTile = sorted.FirstOrDefault(t => t.Number == 1 && !used.Contains(t.Id) && !cur.Any(c => c.Id == t.Id));
                                if (oneTile != null)
                                {
                                    cur.Add(oneTile);
                                }
                            }

                            if (cur.Count >= 3)
                            {
                                melds.Add(new List<TileData>(cur));
                                foreach (var t in cur) used.Add(t.Id);
                            }
                            cur.Clear();
                            if (!used.Contains(sorted[i].Id))
                            {
                                cur.Add(sorted[i]);
                            }
                        }
                    }
                }

                if (cur.Count >= 2 && cur.Last().Number == 13)
                {
                    var oneTile = sorted.FirstOrDefault(t => t.Number == 1 && !used.Contains(t.Id) && !cur.Any(c => c.Id == t.Id));
                    if (oneTile != null)
                    {
                        cur.Add(oneTile);
                    }
                }

                if (cur.Count >= 3)
                {
                    melds.Add(new List<TileData>(cur));
                    foreach (var t in cur) used.Add(t.Id);
                }
            }

            // 2. Kalan taşlarla Aynı Sayı Farklı Renk Grupları (Örn: Kırmızı 5, Mavi 5, Sarı 5)
            var remaining = sourceTiles.Where(t => !used.Contains(t.Id) && !t.IsOkey).ToList();
            var numberGroups = remaining.GroupBy(t => t.Number).OrderByDescending(g => g.Key);

            foreach (var ng in numberGroups)
            {
                var distinctColors = ng.GroupBy(t => t.Color).Select(g => g.First()).ToList();
                if (distinctColors.Count >= 3)
                {
                    melds.Add(distinctColors);
                    foreach (var t in distinctColors) used.Add(t.Id);
                }
            }

            // 3. Elde Okey (Joker) varsa, kalan 2'li adayları 3'lü pere tamamla veya mevcut yüksek perlere ekle
            var availableOkeys = sourceTiles.Where(t => t.IsOkey && !used.Contains(t.Id)).ToList();
            foreach (var okey in availableOkeys)
            {
                var pool = sourceTiles.Where(t => !used.Contains(t.Id) && !t.IsOkey).ToList();
                List<TileData> bestCandidate = null;
                int bestScore = 0;

                // 3a. Aynı sayıdan 2 farklı renk varsa + Okey ile 3'lü grup (Örn: iki adet 12 + Okey = 36 puan)
                foreach (var ng in pool.GroupBy(t => t.Number))
                {
                    var dc = ng.GroupBy(t => t.Color).Select(g => g.First()).ToList();
                    if (dc.Count == 2)
                    {
                        int score = ng.Key * 3;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            var okeyClone = okey;
                            okeyClone.RepresentedNumber = ng.Key;
                            var allColors = new[] { TileColor.Red, TileColor.Yellow, TileColor.Blue, TileColor.Black };
                            okeyClone.RepresentedColor = allColors.First(c => !dc.Any(x => x.Color == c));
                            bestCandidate = new List<TileData> { dc[0], dc[1], okeyClone };
                        }
                    }
                }

                // 3b. Aynı renkten ardışık veya 1 boşluklu 2 taş + Okey ile 3'lü seri
                foreach (var cg in pool.GroupBy(t => t.Color))
                {
                    var sorted = cg.OrderBy(t => t.Number).ToList();
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        var t1 = sorted[i];
                        var t2 = sorted[i + 1];
                        if (t2.Number == t1.Number + 1)
                        {
                            // Sona veya başa Okey ekle
                            if (t2.Number < 13)
                            {
                                int score = t1.Number + t2.Number + (t2.Number + 1);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    okey.RepresentedColor = t1.Color;
                                    okey.RepresentedNumber = t2.Number + 1;
                                    bestCandidate = new List<TileData> { t1, t2, okey };
                                }
                            }
                            else if (t1.Number > 1)
                            {
                                int score = (t1.Number - 1) + t1.Number + t2.Number;
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    okey.RepresentedColor = t1.Color;
                                    okey.RepresentedNumber = t1.Number - 1;
                                    bestCandidate = new List<TileData> { okey, t1, t2 };
                                }
                            }
                        }
                        else if (t2.Number == t1.Number + 2)
                        {
                            // Araya Okey ekle (Örn: 10 - OKEY - 12)
                            int mid = t1.Number + 1;
                            int score = t1.Number + mid + t2.Number;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                okey.RepresentedColor = t1.Color;
                                okey.RepresentedNumber = mid;
                                bestCandidate = new List<TileData> { t1, okey, t2 };
                            }
                        }
                    }
                }

                if (bestCandidate != null)
                {
                    melds.Add(bestCandidate);
                    foreach (var t in bestCandidate) used.Add(t.Id);
                }
            }

            return melds;
        }

        /// <summary>
        /// Oyuncunun elindeki geçerli çiftleri (Okey desteği dahil) bulur.
        /// </summary>
        public List<List<TileData>> FindBestPairs(List<TileData> tiles = null)
        {
            var sourceTiles = tiles ?? Tiles;
            var pairs = new List<List<TileData>>();
            var used = new HashSet<int>();

            var nonOkeyGroups = sourceTiles.Where(t => !t.IsOkey)
                .GroupBy(t => new { t.Color, t.Number });

            foreach (var grp in nonOkeyGroups)
            {
                var list = grp.ToList();
                while (list.Count >= 2)
                {
                    pairs.Add(new List<TileData> { list[0], list[1] });
                    used.Add(list[0].Id);
                    used.Add(list[1].Id);
                    list.RemoveAt(1);
                    list.RemoveAt(0);
                }
            }

            // Kalan Okey taşlarını en yüksek sayılı tekil taşlarla çift yap
            var okeys = sourceTiles.Where(t => t.IsOkey && !used.Contains(t.Id)).ToList();
            var singles = sourceTiles.Where(t => !t.IsOkey && !used.Contains(t.Id))
                .OrderByDescending(t => t.Number)
                .ToList();

            foreach (var okey in okeys)
            {
                if (singles.Count > 0)
                {
                    var partner = singles[0];
                    singles.RemoveAt(0);
                    okey.RepresentedColor = partner.Color;
                    okey.RepresentedNumber = partner.Number;
                    pairs.Add(new List<TileData> { partner, okey });
                    used.Add(partner.Id);
                    used.Add(okey.Id);
                }
            }

            return pairs;
        }

        /// <summary>
        /// Oyuncunun elindeki geçerli perlerin toplam puanını hesaplar.
        /// </summary>
        public int CalculateTotalPerScore()
        {
            var melds = FindBestMelds(Tiles);
            return melds.Sum(m => m.Sum(t => t.IsOkey && t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number));
        }

        /// <summary>
        /// Oyuncunun elindeki çiftlerin sayısını hesaplar.
        /// </summary>
        public int CalculatePairsCount()
        {
            return FindBestPairs(Tiles).Count;
        }

        /// <summary>
        /// Elde kalan ceza puanını hesaplar.
        /// 101 Okey kuralına göre el açmış bir oyuncunun elinde Okey (Joker) kalırsa 101 ceza puanı sayılır.
        /// </summary>
        public int CalculateRemainingHandScore(int okeyPenaltyValue = 101)
        {
            return Tiles.Sum(t => t.IsOkey ? okeyPenaltyValue : t.Number);
        }
    }
}
