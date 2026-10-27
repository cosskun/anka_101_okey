using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Enums;

namespace Okey101.Core.Data
{
    /// <summary>
    /// Masaya açılmış veya elde oluşturulmuş bir per grubunu temsil eder.
    /// (Seri, Grup veya Çift)
    /// </summary>
    [Serializable]
    public class PerGroup
    {
        public PerType Type { get; set; }
        public List<TileData> Tiles { get; private set; }
        public int OwnerPlayerIndex { get; set; }

        public PerGroup()
        {
            Type = PerType.Invalid;
            Tiles = new List<TileData>();
            OwnerPlayerIndex = -1;
        }

        public PerGroup(PerType type, IEnumerable<TileData> tiles, int ownerIndex = -1)
        {
            Type = type;
            Tiles = new List<TileData>(tiles);
            OwnerPlayerIndex = ownerIndex;
        }

        /// <summary>
        /// Perdeki toplam puanı hesaplar.
        /// (101 kurallarına göre 12-13-1 perindeki 1 taşı 1 puan sayılır; 1-2-3 perindeki 1 taşı 1 puan sayılır).
        /// Okey taşları yerine geçtiği temsil edilen taşın puanını alır.
        /// </summary>
        public int CalculateScore()
        {
            if (Type == PerType.Pair)
            {
                // Çift perlerde puan değil çift adedi esastır; yine de taşların sayı toplamı hesaplanabilir.
                return Tiles.Sum(t => t.RepresentedNumber > 0 ? t.RepresentedNumber : t.Number);
            }

            int sum = 0;
            foreach (var tile in Tiles)
            {
                int val = tile.RepresentedNumber > 0 ? tile.RepresentedNumber : tile.Number;
                sum += val;
            }
            return sum;
        }

        public override string ToString()
        {
            string tilesStr = string.Join(", ", Tiles.Select(t => t.ToString()));
            return $"{Type} (Score: {CalculateScore()}): [{tilesStr}]";
        }
    }
}
