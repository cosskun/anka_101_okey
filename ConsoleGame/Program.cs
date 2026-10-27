using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Okey101.Core.AI;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;

namespace Okey101.ConsoleGame
{
    internal class Program
    {
        private static TurnManager _turnManager;
        private static TableScoreTracker _tracker;
        private static TeamManager _teamManager;
        private static ScoreManager _scoreManager;
        private static DeckManager _deckManager;
        private static GameSettings _settings;
        private static OkeyAIController[] _bots;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "101 Okey - Terminal Oyunu";

            PrintWelcomeHeader();
            ConfigureGameSettings();
            InitializeGameEngine();
            GameLoop();
        }

        private static void PrintWelcomeHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                 🀄 101 OKEY KONSOL OYUNU 🀄                       ║");
            Console.WriteLine("║   (Tekli / Eşli & Katlamalı / Standart Modları Destekler)        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void ConfigureGameSettings()
        {
            Console.WriteLine("Lütfen Oyun Modunu Seçiniz:");
            Console.WriteLine("1) Tekli (Solo) - Katlamalı (Folded) [TAVSİYE EDİLEN]");
            Console.WriteLine("2) Tekli (Solo) - Standart (Katlamasız)");
            Console.WriteLine("3) Eşli (2v2) - Katlamalı (Folded)");
            Console.WriteLine("4) Eşli (2v2) - Standart (Katlamasız)");
            Console.Write("\nSeçiminiz (1-4) [Varsayılan: 1]: ");

            string input = Console.ReadLine()?.Trim();
            MatchMode matchMode = (input == "3" || input == "4") ? MatchMode.Coop : MatchMode.Solo;
            ScoringRule scoringRule = (input == "2" || input == "4") ? ScoringRule.Standard : ScoringRule.Folded;

            _settings = new GameSettings(matchMode, scoringRule)
            {
                AllowTeammateProcessingWithoutOpening = true
            };

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Ayarlandı: {(matchMode == MatchMode.Coop ? "Eşli (2v2)" : "Tekli (FFA)")} | {(scoringRule == ScoringRule.Folded ? "Katlamalı (+1 Baraj)" : "Standart (101 Baraj)")}");
            Console.ResetColor();
            Console.WriteLine("\nOyunu başlatmak için bir tuşa basın...");
            Console.ReadKey();
        }

        private static void InitializeGameEngine()
        {
            _deckManager = new DeckManager(_settings);
            _tracker = new TableScoreTracker(_settings);
            _teamManager = new TeamManager(_settings);
            _scoreManager = new ScoreManager(_settings, _teamManager);
            _turnManager = new TurnManager(_settings, _deckManager, _tracker, _teamManager, _scoreManager);

            _bots = new OkeyAIController[4];
            for (int i = 1; i <= 3; i++)
            {
                _bots[i] = new OkeyAIController(i, _turnManager);
            }

            _turnManager.StartNewRound(startingPlayerIndex: 0);
        }

        private static void GameLoop()
        {
            while (!_turnManager.IsRoundOver)
            {
                if (_turnManager.CurrentPlayerIndex == 0)
                {
                    // İnsan Oyuncu (Siz)
                    RenderScreen();
                    ProcessPlayerCommand();
                }
                else
                {
                    // Bot Sırası
                    RenderScreen();
                    int botIndex = _turnManager.CurrentPlayerIndex;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n⏳ [Oyuncu {botIndex} (Bot)] düşünüyor...");
                    Console.ResetColor();
                    Thread.Sleep(900);

                    _bots[botIndex].ExecuteTurn();
                }
            }

            // Oyun Bitti
            RenderScreen();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n🎉🎉🎉 EL TAMAMLANDI! 🎉🎉🎉");
            Console.ResetColor();
            Console.WriteLine("\nÇıkmak için ENTER tuşuna basınız...");
            Console.ReadLine();
        }

        private static void RenderScreen()
        {
            Console.Clear();
            PrintWelcomeHeader();

            // 1. Masa Bilgi Paneli
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("MOD: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{(_settings.MatchMode == MatchMode.Coop ? "Eşli (2v2: Siz & Oyuncu 2)" : "Tekli (FFA)")} | {(_settings.ScoringRule == ScoringRule.Folded ? "Katlamalı" : "Standart")}");
            
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  |  AÇILIŞ BARAJI: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{_tracker.CurrentRequiredScore} Puan / {_tracker.CurrentRequiredPairs} Çift");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  |  KALAN DESTE: ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"{_deckManager.RemainingDrawCount} Taş");
            Console.ResetColor();

            // Gösterge ve Okey Bilgisi
            Console.Write("GÖSTERGE: ");
            PrintTile(_deckManager.IndicatorTile);
            Console.Write("  -> OKEY TAŞI: ");
            PrintTile(_deckManager.OkeyTileIdentity);
            Console.WriteLine("\n" + new string('─', 70));

            // 2. Masadaki Oyuncuların Son Attığı Taşlar
            Console.WriteLine("MASADAKİ SON ATILAN TAŞLAR:");
            for (int i = 0; i < 4; i++)
            {
                string name = (i == 0) ? "Siz (Oyuncu 0)" : $"Oyuncu {i} (Bot)";
                Console.Write($"  {name}: ");
                if (_turnManager.DiscardPiles[i].Count > 0)
                {
                    PrintTile(_turnManager.DiscardPiles[i].Last());
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("[Boş]");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
            Console.WriteLine(new string('─', 70));

            // 3. Masada Açılan Perler
            Console.WriteLine("MASADAKİ AÇIK PERLER:");
            if (_tracker.OpenedSeriesOnTable.Count == 0 && _tracker.OpenedPairsOnTable.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  (Henüz masaya per veya çift açılmadı)");
                Console.ResetColor();
            }
            else
            {
                int perIdx = 1;
                foreach (var per in _tracker.OpenedSeriesOnTable)
                {
                    Console.Write($"  [{perIdx++}] (Oyuncu {per.OwnerPlayerIndex} Seri - {per.CalculateScore()} Puan): ");
                    foreach (var t in per.Tiles)
                    {
                        PrintTile(t);
                        Console.Write(" ");
                    }
                    Console.WriteLine();
                }

                foreach (var pair in _tracker.OpenedPairsOnTable)
                {
                    Console.Write($"  [{perIdx++}] (Oyuncu {pair.OwnerPlayerIndex} Çift): ");
                    foreach (var t in pair.Tiles)
                    {
                        PrintTile(t);
                        Console.Write(" ");
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine(new string('═', 70));

            // 4. Sizin Istakanız (Oyuncu 0)
            var hand = _turnManager.PlayerHands[0];
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🀄 SİZİN ISTAKANIZ ({hand.Tiles.Count} Taş) - [Durum: {(hand.HasOpenedHand ? "AÇIK" : "KAPALI")}]:");
            Console.ResetColor();

            PrintPlayerRack(hand);

            Console.WriteLine(new string('═', 70));

            // Sıra ve Aşama
            if (_turnManager.CurrentPlayerIndex == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"▶ SIRA SİZDE! Aşama: {_turnManager.CurrentPhase}");
                Console.ResetColor();
            }
        }

        private static void PrintPlayerRack(PlayerHand hand)
        {
            var tiles = hand.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{i + 1,2}:");
                Console.ResetColor();
                PrintTile(tiles[i]);
                Console.Write("  ");

                if ((i + 1) % 11 == 0) Console.WriteLine(); // 2 satırda göstermek için
            }
            Console.WriteLine();
        }

        private static void PrintTile(TileData tile)
        {
            if (tile == null)
            {
                Console.Write("[--]");
                return;
            }

            ConsoleColor color = tile.Color switch
            {
                TileColor.Red => ConsoleColor.Red,
                TileColor.Yellow => ConsoleColor.Yellow,
                TileColor.Blue => ConsoleColor.Cyan,
                TileColor.Black => ConsoleColor.White,
                _ => ConsoleColor.Gray
            };

            Console.ForegroundColor = color;
            if (tile.IsOkey)
            {
                Console.Write($"[★OKEY★]");
            }
            else if (tile.IsFakeOkey)
            {
                Console.Write($"[Sahte-{tile.Number}]");
            }
            else
            {
                Console.Write($"[{tile.Color.ToString().Substring(0, 1)}-{tile.Number}]");
            }
            Console.ResetColor();
        }

        private static void ProcessPlayerCommand()
        {
            Console.WriteLine("\nKOMUTLAR:");
            if (_turnManager.CurrentPhase == TurnPhase.Draw)
            {
                Console.WriteLine("  d / cek       -> Desteden kapalı taş çek");
                Console.WriteLine("  y / yan       -> Solunuzdaki oyuncunun attığı taşı çek");
                Console.WriteLine("  gosterge      -> Gösterge taşınız varsa göster (-101 puan)");
            }
            else
            {
                Console.WriteLine("  at <no>       -> Istakadaki numaralı taşı yere at (Örn: at 3)");
                Console.WriteLine("  ac <no,no..>  -> Seçili taşlarla seri per aç (Örn: ac 1,2,3 - 5,6,7)");
                Console.WriteLine("  otoac         -> Eldeki tüm geçerli perleri otomatik bul ve aç");
                Console.WriteLine("  cift <no,no..>-> Seçili taşlarla çift aç (Örn: cift 1,2 - 3,4 - 5,6 - 7,8 - 9,10)");
                Console.WriteLine("  isle <taş> <per> -> Masadaki pere taş işle (Örn: isle 4 1)");
                Console.WriteLine("  okeyal <taş> <per> -> Masadaki perden Okey çal / takas et (Örn: okeyal 4 1)");
                Console.WriteLine("  eldenbit <no> -> Seçilen taşı atarak elden bit (Örn: eldenbit 1)");
                if (_turnManager.MustUseSideTileId.HasValue)
                {
                    Console.WriteLine("  iptal         -> Yandan alınan taşı geri bırak ve desteden çek");
                }
            }
            Console.WriteLine("  diz           -> Istakayı serilere göre otomatik diz");
            Console.WriteLine("  ciftdiz       -> Istakayı çiftlere göre otomatik diz");
            Console.Write("\nKomut giriniz: ");

            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return;

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            try
            {
                if (cmd == "diz")
                {
                    _turnManager.PlayerHands[0].SortByColorAndNumber();
                    return;
                }
                if (cmd == "ciftdiz")
                {
                    _turnManager.PlayerHands[0].SortByPairs();
                    return;
                }

                if (cmd == "d" || cmd == "cek")
                {
                    if (!_turnManager.DrawFromDeck(0, out _, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "y" || cmd == "yan")
                {
                    if (!_turnManager.DrawFromSideDiscard(0, out _, out string err, validateCanUse: true))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "gosterge")
                {
                    if (!_turnManager.TryShowIndicator(0, out string msg))
                    {
                        PrintError(msg);
                    }
                    return;
                }

                if (cmd == "iptal")
                {
                    if (!_turnManager.CancelSideDiscardAndDrawFromDeck(0, out _, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "otoac")
                {
                    if (!_turnManager.AutoOpenValidMelds(0, out string msg, out _))
                    {
                        PrintError(msg);
                    }
                    return;
                }

                if (cmd == "okeyal" && parts.Length >= 3)
                {
                    int tileIdx = int.Parse(parts[1]) - 1;
                    int perIdx = int.Parse(parts[2]) - 1;
                    var tile = _turnManager.PlayerHands[0].Tiles[tileIdx];
                    var targetPer = _tracker.OpenedSeriesOnTable[perIdx];

                    if (!_turnManager.SwapOkeyFromTablePer(0, tile, targetPer, out _, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "eldenbit" && parts.Length >= 2)
                {
                    int tileIdx = int.Parse(parts[1]) - 1;
                    var tile = _turnManager.PlayerHands[0].Tiles[tileIdx];
                    if (!_turnManager.FinishHandOut(0, tile, out _, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "at" && parts.Length >= 2)
                {
                    if (int.TryParse(parts[1], out int index) && index >= 1 && index <= _turnManager.PlayerHands[0].Tiles.Count)
                    {
                        var tile = _turnManager.PlayerHands[0].Tiles[index - 1];
                        if (!_turnManager.DiscardTile(0, tile, out _, out _, out string err))
                        {
                            PrintError(err);
                        }
                    }
                    else
                    {
                        PrintError("Geçersiz taş numarası!");
                    }
                    return;
                }

                if (cmd == "ac" && parts.Length >= 2)
                {
                    // Format: ac 1,2,3  veya ac 1,2,3-4,5,6
                    string allArgs = string.Join(" ", parts.Skip(1));
                    var groupStrs = allArgs.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var melds = new List<List<TileData>>();

                    foreach (var grp in groupStrs)
                    {
                        var idxs = grp.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(s => int.Parse(s.Trim()) - 1).ToList();

                        var list = idxs.Select(i => _turnManager.PlayerHands[0].Tiles[i]).ToList();
                        melds.Add(list);
                    }

                    if (!_turnManager.OpenSeries(0, melds, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                if (cmd == "isle" && parts.Length >= 3)
                {
                    int tileIdx = int.Parse(parts[1]) - 1;
                    int perIdx = int.Parse(parts[2]) - 1;

                    var tile = _turnManager.PlayerHands[0].Tiles[tileIdx];
                    var targetPer = _tracker.OpenedSeriesOnTable[perIdx];

                    if (!_turnManager.ProcessTile(0, tile, targetPer, out string err))
                    {
                        PrintError(err);
                    }
                    return;
                }

                PrintError("Bilinmeyen komut!");
            }
            catch (Exception ex)
            {
                PrintError($"Hata: {ex.Message}");
            }
        }

        private static void PrintError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n⚠ {msg}");
            Console.ResetColor();
            Console.WriteLine("Devam etmek için bir tuşa basın...");
            Console.ReadKey();
        }
    }
}
