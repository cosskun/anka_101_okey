using System;
using Okey101.Core.Enums;

namespace Okey101.Core.Data
{
    /// <summary>
    /// Masadaki veya ıstakadaki tek bir taşı temsil eden veri sınıfı.
    /// Saf C# nesnesidir (Unity MonoBehaviour içermez), böylece performanslı ve test edilebilirdir.
    /// </summary>
    [Serializable]
    public class TileData : IEquatable<TileData>, IComparable<TileData>
    {
        public int Id { get; set; }                    // Benzersiz taş kimliği (0 - 105)
        public TileColor Color { get; set; }          // Taşın rengi (Kırmızı, Sarı, Mavi, Siyah)
        public int Number { get; set; }               // Taşın sayısı (1 - 13)
        public bool IsFakeOkey { get; set; }          // Sahte okey mi?
        public bool IsOkey { get; set; }              // Bu elin Okey (Joker) taşı mı?

        // Okey taşı başka bir taş yerine geçtiğinde temsil ettiği değerler (Per doğrulama için):
        public TileColor RepresentedColor { get; set; }
        public int RepresentedNumber { get; set; }

        public TileData()
        {
            Id = -1;
            Color = TileColor.None;
            Number = 0;
            IsFakeOkey = false;
            IsOkey = false;
            RepresentedColor = TileColor.None;
            RepresentedNumber = 0;
        }

        public TileData(int id, TileColor color, int number, bool isFakeOkey = false, bool isOkey = false)
        {
            Id = id;
            Color = color;
            Number = number;
            IsFakeOkey = isFakeOkey;
            IsOkey = isOkey;
            RepresentedColor = color;
            RepresentedNumber = number;
        }

        /// <summary>
        /// Taşın kopyasını oluşturur.
        /// </summary>
        public TileData Clone()
        {
            return new TileData(Id, Color, Number, IsFakeOkey, IsOkey)
            {
                RepresentedColor = this.RepresentedColor,
                RepresentedNumber = this.RepresentedNumber
            };
        }

        /// <summary>
        /// İki taşın oyun kuralı açısından aynı değerde olup olmadığını kontrol eder.
        /// (Renk ve sayı aynı ise, kimlik farklı olsa dahi eşleşir).
        /// </summary>
        public bool HasSameValue(TileData other)
        {
            if (other == null) return false;
            return this.Color == other.Color && this.Number == other.Number;
        }

        /// <summary>
        /// Çift kontrolü: İki taş tam olarak aynı renk ve aynı sayı mı?
        /// Not: Okey taşı da çiftte joker olarak kullanılabilir.
        /// </summary>
        public bool MatchesPairWith(TileData other)
        {
            if (other == null) return false;
            if (this.IsOkey || other.IsOkey) return true;
            return this.Color == other.Color && this.Number == other.Number;
        }

        public bool Equals(TileData other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is TileData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public int CompareTo(TileData other)
        {
            if (other == null) return 1;

            // Önce renge göre sırala
            int colorComparison = Color.CompareTo(other.Color);
            if (colorComparison != 0) return colorComparison;

            // Sonra sayıya göre sırala
            return Number.CompareTo(other.Number);
        }

        public override string ToString()
        {
            if (IsFakeOkey) return $"[FakeOkey (-> {Color} {Number}) #{Id}]";
            if (IsOkey) return $"[★OKEY★ {Color} {Number} #{Id}]";
            return $"[{Color} {Number} #{Id}]";
        }
    }
}
