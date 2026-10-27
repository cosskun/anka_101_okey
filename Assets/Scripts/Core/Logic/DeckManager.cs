using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.Core.Logic
{
    /// <summary>
    /// 106 adet taşın oluşturulması, gösterge ve Okey taşının belirlenmesi,
    /// destenin karıştırılması ve 4 oyuncuya (22, 21, 21, 21) dağıtılmasını yöneten sınıf.
    /// </summary>
    public class DeckManager
    {
        private readonly GameSettings _settings;
        private readonly Random _random;

        public List<TileData> FullDeck { get; private set; }
        public Stack<TileData> DrawPile { get; private set; } // Masadaki çekilecek deste
        public TileData IndicatorTile { get; private set; }   // Gösterge Taşı
        public TileData OkeyTileIdentity { get; private set; } // Okey olan taşın renk ve sayısı

        public int RemainingDrawCount => DrawPile != null ? DrawPile.Count : 0;

        public DeckManager(GameSettings settings = null, int? seed = null)
        {
            _settings = settings ?? new GameSettings();
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            FullDeck = new List<TileData>();
            DrawPile = new Stack<TileData>();
        }

        /// <summary>
        /// 106 taşlık desteyi sıfırdan oluşturur, göstergeyi ve okeyi belirler, karıştırır.
        /// </summary>
        public void PrepareNewDeck()
        {
            FullDeck.Clear();
            DrawPile.Clear();

            int idCounter = 0;
            var colors = new[] { TileColor.Red, TileColor.Yellow, TileColor.Blue, TileColor.Black };

            // 1. 104 Standart Taş (4 Renk x 1..13 x 2 Kopya)
            for (int set = 0; set < 2; set++)
            {
                foreach (var color in colors)
                {
                    for (int num = 1; num <= 13; num++)
                    {
                        FullDeck.Add(new TileData(idCounter++, color, num, isFakeOkey: false, isOkey: false));
                    }
                }
            }

            // 2. 2 Adet Sahte Okey Taşı
            FullDeck.Add(new TileData(idCounter++, TileColor.None, 0, isFakeOkey: true, isOkey: false));
            FullDeck.Add(new TileData(idCounter++, TileColor.None, 0, isFakeOkey: true, isOkey: false));

            // 3. Gösterge Taşını Seç (Sahte okey olmayan 104 taştan biri rastgele seçilir)
            int indicatorIndex = _random.Next(0, 104);
            var selectedIndicator = FullDeck[indicatorIndex];
            IndicatorTile = selectedIndicator.Clone();

            // 4. Okey Taşının Renk ve Sayısını Belirle
            // Okey, göstergenin aynı rengi ve 1 fazlasıdır (13 ise 1'e döner).
            TileColor okeyColor = IndicatorTile.Color;
            int okeyNumber = (IndicatorTile.Number % 13) + 1;

            OkeyTileIdentity = new TileData(-1, okeyColor, okeyNumber, isFakeOkey: false, isOkey: true);

            // 5. Desteyi Okey ve Sahte Okey kurallarına göre güncelle:
            foreach (var tile in FullDeck)
            {
                if (tile.IsFakeOkey)
                {
                    // Sahte okey, asıl okey olan taşın rolünü üstlenir
                    tile.Color = okeyColor;
                    tile.Number = okeyNumber;
                    tile.RepresentedColor = okeyColor;
                    tile.RepresentedNumber = okeyNumber;
                }
                else if (tile.Color == okeyColor && tile.Number == okeyNumber)
                {
                    // Gerçek Okey taşı (Joker)
                    tile.IsOkey = true;
                }
            }

            // 6. Desteyi Karıştır (Fisher-Yates)
            Shuffle(FullDeck);

            // 7. Gösterge taşını desteden ayırıp masaya koyalım (Geriye 105 taş kalır)
            // Not: Pratikte gösterge taşının kopyası gösterilir veya bir taş çekilip açık bırakılır.
            // Burada açık gösterge olarak IndicatorTile tutulur.
        }

        private void Shuffle(List<TileData> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// 4 oyuncunun eline taşları dağıtır.
        /// Başlangıç oyuncusuna 22 taş, diğer 3 oyuncuya 21 taş verilir.
        /// Kalan taşlar çekme destesine (DrawPile) yerleştirilir.
        /// </summary>
        public void DealTilesToPlayers(List<PlayerHand> playerHands, int startingPlayerIndex = 0)
        {
            if (playerHands == null || playerHands.Count != 4)
            {
                throw new ArgumentException("101 Okey için tam olarak 4 oyuncu eli gereklidir.");
            }

            int currentDeckIndex = 0;

            for (int i = 0; i < 4; i++)
            {
                int playerIndex = (startingPlayerIndex + i) % 4;
                var hand = playerHands[playerIndex];
                hand.ResetHand();

                // Başlangıç oyuncusuna 22, diğerlerine 21 taş
                int countToDeal = (playerIndex == startingPlayerIndex)
                    ? _settings.StartingPlayerTileCount
                    : _settings.OtherPlayersTileCount;

                for (int t = 0; t < countToDeal; t++)
                {
                    if (currentDeckIndex < FullDeck.Count)
                    {
                        hand.AddTile(FullDeck[currentDeckIndex++]);
                    }
                }
            }

            // Kalan taşları masadaki çekme destesine aktar
            DrawPile.Clear();
            for (int i = FullDeck.Count - 1; i >= currentDeckIndex; i--)
            {
                DrawPile.Push(FullDeck[i]);
            }
        }

        /// <summary>
        /// Desteden bir sonraki taşı çeker.
        /// </summary>
        public TileData DrawTile()
        {
            if (DrawPile.Count > 0)
            {
                return DrawPile.Pop();
            }
            return null; // Deste bitti
        }
    }
}
