using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Config;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;
using Okey101.Core.AI;

namespace Okey101.Tests
{
    internal class Program
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("101 OKEY CORE ENGINE - BİRİM VE ENTEGRASYON TESTLERİ");
            Console.WriteLine("==================================================\n");

            TestDeckCreationAndDistribution();
            TestTileValidatorRuns();
            TestTileValidatorGroups();
            TestTileValidatorPairs();
            TestTableScoreTrackerStandardAndFolded();
            TestTeamManagerSoloAndCoop();
            TestScoreManagerPenaltiesAndRewards();
            TestTurnManagerRoundFlow();
            TestAIBotExecution();
            TestOkeySwappingAndStealing();
            TestSideDiscardLockAndIndicatorAndPenalties();

            Console.WriteLine("\n==================================================");
            Console.WriteLine($"SONUÇ: {_passedCount} Başarılı, {_failedCount} Başarısız.");
            Console.WriteLine("==================================================");

            if (_failedCount > 0)
            {
                Environment.Exit(1);
            }
        }

        static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($"[GEÇTİ] {testName}");
                _passedCount++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[BAŞARISIZ] {testName}");
                Console.ResetColor();
                _failedCount++;
            }
        }

        static void TestDeckCreationAndDistribution()
        {
            Console.WriteLine("--- 1. DESTE VE DAĞITIM TESTLERİ ---");
            var settings = new GameSettings();
            var deckManager = new DeckManager(settings, seed: 42);
            deckManager.PrepareNewDeck();

            Assert(deckManager.FullDeck.Count == 106, "Destede tam 106 taş olmalı.");
            Assert(deckManager.FullDeck.Count(t => t.IsFakeOkey) == 2, "Destede tam 2 adet Sahte Okey olmalı.");
            Assert(deckManager.FullDeck.Count(t => t.IsOkey) == 2, "Destede tam 2 adet Okey (Joker) olmalı.");
            Assert(deckManager.IndicatorTile != null, "Gösterge taşı belirlenmiş olmalı.");

            // Okey göstergenin bir fazlası mı?
            int expectedOkeyNum = (deckManager.IndicatorTile.Number % 13) + 1;
            Assert(deckManager.OkeyTileIdentity.Number == expectedOkeyNum && deckManager.OkeyTileIdentity.Color == deckManager.IndicatorTile.Color,
                $"Okey ({deckManager.OkeyTileIdentity}) göstergenin ({deckManager.IndicatorTile}) aynı rengi ve 1 fazlası olmalı.");

            // 4 Oyuncuya dağıtım
            var hands = new List<PlayerHand>
            {
                new PlayerHand(0),
                new PlayerHand(1),
                new PlayerHand(2),
                new PlayerHand(3)
            };

            deckManager.DealTilesToPlayers(hands, startingPlayerIndex: 0);

            Assert(hands[0].Tiles.Count == 22, "Oyuncu 0 (Başlangıç oyuncusu) 22 taşa sahip olmalı.");
            Assert(hands[1].Tiles.Count == 21, "Oyuncu 1 21 taşa sahip olmalı.");
            Assert(hands[2].Tiles.Count == 21, "Oyuncu 2 21 taşa sahip olmalı.");
            Assert(hands[3].Tiles.Count == 21, "Oyuncu 3 21 taşa sahip olmalı.");
            Assert(deckManager.RemainingDrawCount == 106 - 85, $"Kalan çekme destesinde {106 - 85} taş olmalı. (Gerçek: {deckManager.RemainingDrawCount})");
        }

        static void TestTileValidatorRuns()
        {
            Console.WriteLine("\n--- 2. KURAL DOĞRULAYICI: SERİ PER TESTLERİ ---");

            // Kırmızı 7-8-9
            var validRun1 = new List<TileData>
            {
                new TileData(1, TileColor.Red, 7),
                new TileData(2, TileColor.Red, 8),
                new TileData(3, TileColor.Red, 9)
            };
            Assert(TileValidator.ValidateRunPer(validRun1), "Kırmızı 7-8-9 geçerli seri olmalı.");

            // Kırmızı 11-12-13-1 (13'ten sonra 1 bağlama)
            var validRunWrap = new List<TileData>
            {
                new TileData(1, TileColor.Red, 11),
                new TileData(2, TileColor.Red, 12),
                new TileData(3, TileColor.Red, 13),
                new TileData(4, TileColor.Red, 1)
            };
            Assert(TileValidator.ValidateRunPer(validRunWrap), "Kırmızı 11-12-13-1 geçerli seri olmalı.");

            // Kırmızı 13-1-2 (Geçersiz!)
            var invalidRunWrap = new List<TileData>
            {
                new TileData(1, TileColor.Red, 13),
                new TileData(2, TileColor.Red, 1),
                new TileData(3, TileColor.Red, 2)
            };
            Assert(!TileValidator.ValidateRunPer(invalidRunWrap), "Kırmızı 13-1-2 GEÇERSİZ olmalı (1'den sonra devam edemez).");

            // Okey içeren seri: Mavi 4 - [OKEY] - Mavi 6
            var okeyRun = new List<TileData>
            {
                new TileData(1, TileColor.Blue, 4),
                new TileData(2, TileColor.Yellow, 8, isFakeOkey: false, isOkey: true),
                new TileData(3, TileColor.Blue, 6)
            };
            Assert(TileValidator.ValidateRunPer(okeyRun), "Mavi 4 - [OKEY] - Mavi 6 geçerli seri olmalı.");
            Assert(okeyRun[1].RepresentedNumber == 5 && okeyRun[1].RepresentedColor == TileColor.Blue, "Okey Mavi 5 olarak atanmış olmalı.");
        }

        static void TestTileValidatorGroups()
        {
            Console.WriteLine("\n--- 3. KURAL DOĞRULAYICI: GRUP PER TESTLERİ ---");

            // Kırmızı 8, Sarı 8, Siyah 8
            var validGroup = new List<TileData>
            {
                new TileData(1, TileColor.Red, 8),
                new TileData(2, TileColor.Yellow, 8),
                new TileData(3, TileColor.Black, 8)
            };
            Assert(TileValidator.ValidateGroupPer(validGroup), "Kırmızı 8, Sarı 8, Siyah 8 geçerli grup olmalı.");

            // Aynı renkten 2 adet olan grup (Kırmızı 8, Kırmızı 8, Mavi 8) -> GEÇERSİZ
            var invalidGroup = new List<TileData>
            {
                new TileData(1, TileColor.Red, 8),
                new TileData(2, TileColor.Red, 8),
                new TileData(3, TileColor.Blue, 8)
            };
            Assert(!TileValidator.ValidateGroupPer(invalidGroup), "Aynı renkten 2 taş içeren grup GEÇERSİZ olmalı.");

            // 4 Renk Grup: Kırmızı 5, Sarı 5, Mavi 5, Siyah 5
            var fullGroup = new List<TileData>
            {
                new TileData(1, TileColor.Red, 5),
                new TileData(2, TileColor.Yellow, 5),
                new TileData(3, TileColor.Blue, 5),
                new TileData(4, TileColor.Black, 5)
            };
            Assert(TileValidator.ValidateGroupPer(fullGroup), "4 farklı renkten oluşan 5 grubu geçerli olmalı.");
        }

        static void TestTileValidatorPairs()
        {
            Console.WriteLine("\n--- 4. KURAL DOĞRULAYICI: ÇİFT PER TESTLERİ ---");

            // Mavi 10 ve Mavi 10
            var validPair = new List<TileData>
            {
                new TileData(1, TileColor.Blue, 10),
                new TileData(2, TileColor.Blue, 10)
            };
            Assert(TileValidator.ValidatePairPer(validPair), "Aynı renk ve aynı sayı 2 taş geçerli çift olmalı.");

            // Mavi 10 ve Kırmızı 10 -> GEÇERSİZ
            var invalidPair = new List<TileData>
            {
                new TileData(1, TileColor.Blue, 10),
                new TileData(2, TileColor.Red, 10)
            };
            Assert(!TileValidator.ValidatePairPer(invalidPair), "Farklı renkler çift oluşturamaz.");
        }

        static void TestTableScoreTrackerStandardAndFolded()
        {
            Console.WriteLine("\n--- 5. MASA BARAJ TAKİP TESTLERİ (STANDART vs KATLAMALI) ---");

            // Standart Mod Testi
            var stdSettings = new GameSettings(MatchMode.Solo, ScoringRule.Standard);
            var stdTracker = new TableScoreTracker(stdSettings);

            Assert(stdTracker.CurrentRequiredScore == 101, "Standart modda başlangıç barajı 101 olmalı.");
            Assert(stdTracker.CurrentRequiredPairs == 5, "Standart modda başlangıç çift barajı 5 olmalı.");

            var per1 = new PerGroup(PerType.RunSameColor, new List<TileData>
            {
                new TileData(1, TileColor.Red, 10),
                new TileData(2, TileColor.Red, 11),
                new TileData(3, TileColor.Red, 12),
                new TileData(4, TileColor.Red, 13),
                new TileData(5, TileColor.Red, 1) // 10+11+12+13+1 = 47
            });
            var per2 = new PerGroup(PerType.GroupDiffColor, new List<TileData>
            {
                new TileData(6, TileColor.Red, 13),
                new TileData(7, TileColor.Yellow, 13),
                new TileData(8, TileColor.Blue, 13),
                new TileData(9, TileColor.Black, 13) // 13*4 = 52
            });
            var per3 = new PerGroup(PerType.GroupDiffColor, new List<TileData>
            {
                new TileData(10, TileColor.Red, 5),
                new TileData(11, TileColor.Yellow, 5),
                new TileData(12, TileColor.Blue, 5) // 15 -> Toplam 47+52+15 = 114
            });

            int totalScore1 = per1.CalculateScore() + per2.CalculateScore() + per3.CalculateScore(); // 114
            stdTracker.RegisterOpenedSeries(new List<PerGroup> { per1, per2, per3 }, 0, totalScore1);

            Assert(stdTracker.CurrentRequiredScore == 101, "Standart modda el açılsa bile baraj 101 kalmalıdır.");

            // Katlamalı Mod Testi
            var foldedSettings = new GameSettings(MatchMode.Solo, ScoringRule.Folded);
            var foldedTracker = new TableScoreTracker(foldedSettings);

            Assert(foldedTracker.CurrentRequiredScore == 101, "Katlamalı modda başlangıç barajı 101 olmalı.");

            // Oyuncu 0 114 puanla açtı
            foldedTracker.RegisterOpenedSeries(new List<PerGroup> { per1, per2, per3 }, 0, totalScore1);
            Assert(foldedTracker.CurrentRequiredScore == 115, $"Katlamalı modda 114 puanla açıldıktan sonra yeni baraj 115 olmalı. (Gerçek: {foldedTracker.CurrentRequiredScore})");

            // Oyuncu 1 120 puanla açtı
            foldedTracker.RegisterOpenedSeries(new List<PerGroup>(), 1, 120);
            Assert(foldedTracker.CurrentRequiredScore == 121, $"Katlamalı modda 120 puanla açıldıktan sonra yeni baraj 121 olmalı. (Gerçek: {foldedTracker.CurrentRequiredScore})");
        }

        static void TestTeamManagerSoloAndCoop()
        {
            Console.WriteLine("\n--- 6. TAKIM VE EŞ YÖNETİMİ TESTLERİ ---");

            // Solo Mod
            var soloSettings = new GameSettings(MatchMode.Solo, ScoringRule.Standard);
            var soloTeam = new TeamManager(soloSettings);
            Assert(soloTeam.GetTeammateIndex(0) == -1, "Solo modda oyuncunun ortağı olmamalı (-1).");
            Assert(soloTeam.GetTeamId(0) != soloTeam.GetTeamId(2), "Solo modda oyuncu 0 ve 2 farklı takımlarda olmalı.");

            // Eşli Mod
            var coopSettings = new GameSettings(MatchMode.Coop, ScoringRule.Standard);
            var coopTeam = new TeamManager(coopSettings);
            Assert(coopTeam.GetTeammateIndex(0) == 2, "Eşli modda Oyuncu 0'ın ortağı 2 olmalı.");
            Assert(coopTeam.GetTeammateIndex(2) == 0, "Eşli modda Oyuncu 2'nin ortağı 0 olmalı.");
            Assert(coopTeam.GetTeammateIndex(1) == 3, "Eşli modda Oyuncu 1'in ortağı 3 olmalı.");
            Assert(coopTeam.GetTeammateIndex(3) == 1, "Eşli modda Oyuncu 3'ün ortağı 1 olmalı.");
            Assert(coopTeam.AreTeammates(0, 2), "0 ve 2 aynı takımda (Takım A) olmalı.");
            Assert(coopTeam.AreTeammates(1, 3), "1 ve 3 aynı takımda (Takım B) olmalı.");
            Assert(!coopTeam.AreTeammates(0, 1), "0 ve 1 rakip takımlarda olmalı.");
        }

        static void TestScoreManagerPenaltiesAndRewards()
        {
            Console.WriteLine("\n--- 7. SKOR VE CEZA MATRİSİ TESTLERİ ---");

            var settings = new GameSettings(MatchMode.Coop, ScoringRule.Folded);
            var teamManager = new TeamManager(settings);
            var scoreManager = new ScoreManager(settings, teamManager);

            var hands = new List<PlayerHand>
            {
                new PlayerHand(0),
                new PlayerHand(1),
                new PlayerHand(2),
                new PlayerHand(3)
            };

            // Oyuncu 0 bitti (kazandı)
            // Oyuncu 1 hiç açmadı (Katlamalı modda açamama cezası: 202)
            // Oyuncu 2 el açmış, kalan taş toplamı: 25
            // Oyuncu 3 hiç açmadı (202)
            hands[2].HasOpenedSeries = true;
            hands[2].AddTile(new TileData(1, TileColor.Red, 12));
            hands[2].AddTile(new TileData(2, TileColor.Red, 13)); // 12+13 = 25

            var result = scoreManager.CalculateRoundScores(0, EndRoundType.StandardFinish, hands);

            Assert(result.PlayerRoundPenalties[0] == -101, "Biten oyuncu 0 -101 almalı.");
            Assert(result.PlayerRoundPenalties[1] == 202, "Katlamalı modda açamayan oyuncu 1 +202 ceza almalı.");
            Assert(result.PlayerRoundPenalties[2] == 25, "Açan oyuncu 2 kalan taş kadar (+25) ceza almalı.");
            Assert(result.PlayerRoundPenalties[3] == 202, "Açamayan oyuncu 3 +202 ceza almalı.");

            // Eşli takım toplamları:
            // Takım 0 (Oyuncu 0 + 2): -101 + 25 = -76
            // Takım 1 (Oyuncu 1 + 3): 202 + 202 = 404
            Assert(result.TeamRoundPenalties[0] == -76, $"Takım 0 toplamı -76 olmalı. (Gerçek: {result.TeamRoundPenalties[0]})");
            Assert(result.TeamRoundPenalties[1] == 404, $"Takım 1 toplamı 404 olmalı. (Gerçek: {result.TeamRoundPenalties[1]})");

            // Okey ile bitiş testi (x2 ceza çarpanı ve -202 ödül)
            var okeyResult = scoreManager.CalculateRoundScores(0, EndRoundType.OkeyFinish, hands);
            Assert(okeyResult.PlayerRoundPenalties[0] == -202, "Okey ile biten oyuncu -202 ödül almalı.");
            Assert(okeyResult.PlayerRoundPenalties[1] == 404, "Okeyle bitişte açamayan oyuncu 1 x2 ceza (+404) almalı.");
            Assert(okeyResult.PlayerRoundPenalties[2] == 50, "Okeyle bitişte açan oyuncu 2 kalan taşın x2'si (+50) ceza almalı.");

            // Elden Bitiş testi (HandOutFinish: -202 ödül, açmayanlara x2 ceza)
            var handOutResult = scoreManager.CalculateRoundScores(0, EndRoundType.HandOutFinish, hands);
            Assert(handOutResult.PlayerRoundPenalties[0] == -202, "Elden biten oyuncu -202 ödül almalı.");
            Assert(handOutResult.PlayerRoundPenalties[1] == 404, "Elden bitişte açamayan oyuncu x2 ceza (+404) almalı.");

            // Elden Okey ile Bitiş testi (HandOutOkeyFinish: -404 ödül, açmayanlara x4 ceza)
            var handOutOkeyResult = scoreManager.CalculateRoundScores(0, EndRoundType.HandOutOkeyFinish, hands);
            Assert(handOutOkeyResult.PlayerRoundPenalties[0] == -404, "Elden Okeyle biten oyuncu -404 ödül almalı.");
            Assert(handOutOkeyResult.PlayerRoundPenalties[1] == 808, "Elden Okeyle bitişte açamayan oyuncu x4 ceza (+808) almalı.");
        }

        static void TestTurnManagerRoundFlow()
        {
            Console.WriteLine("\n--- 8. TUR YÖNETİMİ VE TUR AKIŞ TESTLERİ ---");

            var settings = new GameSettings(MatchMode.Solo, ScoringRule.Standard);
            var deck = new DeckManager(settings);
            var tracker = new TableScoreTracker(settings);
            var team = new TeamManager(settings);
            var score = new ScoreManager(settings, team);
            var turnManager = new TurnManager(settings, deck, tracker, team, score);

            turnManager.StartNewRound(startingPlayerIndex: 0);

            Assert(turnManager.CurrentPlayerIndex == 0, "İlk turda sıra oyuncu 0'da olmalı.");
            Assert(turnManager.CurrentPhase == TurnPhase.Action, "Başlangıç oyuncusu 22 taşla Action aşamasında başlamalı.");
            Assert(turnManager.PlayerHands[0].Tiles.Count == 22, "Başlangıç oyuncusunun 22 taşı olmalı.");

            // Oyuncu 0 bir taş atar
            var tileToDiscard = turnManager.PlayerHands[0].Tiles[0];
            bool discarded = turnManager.DiscardTile(0, tileToDiscard, out _, out _, out string err);

            Assert(discarded, $"Taş atılabilmeli. Hata: {err}");
            Assert(turnManager.CurrentPlayerIndex == 1, "Taş atıldıktan sonra sıra oyuncu 1'e geçmeli.");
            Assert(turnManager.CurrentPhase == TurnPhase.Draw, "Oyuncu 1 taş çekme (Draw) aşamasında olmalı.");

            // Oyuncu 1 desteden taş çeker
            bool drawn = turnManager.DrawFromDeck(1, out var drawnTile, out string drawErr);
            Assert(drawn && drawnTile != null, $"Oyuncu 1 desteden taş çekebilmeli. Hata: {drawErr}");
            Assert(turnManager.CurrentPhase == TurnPhase.Action, "Taş çekildikten sonra aşama Action olmalı.");
            Assert(turnManager.PlayerHands[1].Tiles.Count == 22, "Taş çekildikten sonra Oyuncu 1'in 22 taşı olmalı.");
        }

        static void TestAIBotExecution()
        {
            Console.WriteLine("\n--- 9. BOT YAPAY ZEKASI (AI) TESTLERİ ---");

            var settings = new GameSettings(MatchMode.Coop, ScoringRule.Standard);
            var deck = new DeckManager(settings);
            var tracker = new TableScoreTracker(settings);
            var team = new TeamManager(settings);
            var score = new ScoreManager(settings, team);
            var turnManager = new TurnManager(settings, deck, tracker, team, score);

            turnManager.StartNewRound(0);

            var bot1 = new OkeyAIController(1, turnManager);
            var bot2 = new OkeyAIController(2, turnManager);
            var bot3 = new OkeyAIController(3, turnManager);

            // Oyuncu 0 taş atar
            turnManager.DiscardTile(0, turnManager.PlayerHands[0].Tiles[0], out _, out _, out _);

            // Bot 1 sırasını oynasın
            bot1.ExecuteTurn();
            Assert(turnManager.CurrentPlayerIndex == 2, "Bot 1 oynadıktan sonra sıra Oyuncu 2'ye geçmeli.");

            // Bot 2 sırasını oynasın
            bot2.ExecuteTurn();
            Assert(turnManager.CurrentPlayerIndex == 3, "Bot 2 oynadıktan sonra sıra Oyuncu 3'e geçmeli.");

            // Bot 3 sırasını oynasın
            bot3.ExecuteTurn();
            Assert(turnManager.CurrentPlayerIndex == 0, "Bot 3 oynadıktan sonra sıra tekrar Oyuncu 0'a dönmeli.");
        }

        static void TestOkeySwappingAndStealing()
        {
            Console.WriteLine("\n--- 10. OKEY ÇALMA (JOKER TAKASI) TESTLERİ ---");

            var okeyTile = new TileData(99, TileColor.Yellow, 8, isFakeOkey: false, isOkey: true);
            var runPer = new PerGroup(PerType.RunSameColor, new List<TileData>
            {
                new TileData(1, TileColor.Blue, 4),
                okeyTile,
                new TileData(3, TileColor.Blue, 6)
            });

            // Seri doğrulaması ile Okey Mavi 5'i temsil eder
            TileValidator.ValidateRunPer(runPer.Tiles);
            var realBlue5 = new TileData(4, TileColor.Blue, 5);
            var wrongBlue7 = new TileData(5, TileColor.Blue, 7);

            Assert(!TileValidator.CanSwapOkeyFromPer(runPer, wrongBlue7, out _), "Mavi 7 ile Mavi 5'i temsil eden Okey değiştirilememeli.");
            Assert(TileValidator.CanSwapOkeyFromPer(runPer, realBlue5, out int okeyIdx) && okeyIdx == 1, "Gerçek Mavi 5 ile perdeki Okey takas edilebilmeli.");

            // Çift perinden Okey çalınamaz
            var pairPer = new PerGroup(PerType.Pair, new List<TileData>
            {
                new TileData(10, TileColor.Red, 9),
                new TileData(100, TileColor.Yellow, 8, isFakeOkey: false, isOkey: true)
            });
            TileValidator.ValidatePairPer(pairPer.Tiles);
            Assert(!TileValidator.CanSwapOkeyFromPer(pairPer, new TileData(11, TileColor.Red, 9), out _), "Çift perinden Okey çalınamamalı.");

            // TurnManager üzerinden Okey çalma akışı
            var settings = new GameSettings(MatchMode.Solo, ScoringRule.Standard);
            var deck = new DeckManager(settings);
            var tracker = new TableScoreTracker(settings);
            var team = new TeamManager(settings);
            var score = new ScoreManager(settings, team);
            var tm = new TurnManager(settings, deck, tracker, team, score);

            tm.StartNewRound(0);
            var hand0 = tm.PlayerHands[0];
            hand0.HasOpenedSeries = true;
            hand0.AddTile(realBlue5);
            tracker.OpenedSeriesOnTable.Add(runPer);

            bool swapped = tm.SwapOkeyFromTablePer(0, realBlue5, runPer, out var stolenOkey, out string swapErr);
            Assert(swapped && stolenOkey != null && stolenOkey.IsOkey, $"TurnManager üzerinden Okey çalınabilmeli. Hata: {swapErr}");
            Assert(hand0.Tiles.Any(t => t.Id == 99 && t.IsOkey), "Çalınan Okey oyuncunun eline geçmiş olmalı.");
            Assert(runPer.Tiles[1].Id == realBlue5.Id && !runPer.Tiles[1].IsOkey, "Masadaki perin içinde artık gerçek Mavi 5 bulunmalı.");
        }

        static void TestSideDiscardLockAndIndicatorAndPenalties()
        {
            Console.WriteLine("\n--- 11. YANDAN TAŞ KİLİDİ, GÖSTERGE, SON TAŞ VE CEZA TESTLERİ ---");

            var settings = new GameSettings(MatchMode.Solo, ScoringRule.Standard);
            var deck = new DeckManager(settings, seed: 7);
            var tracker = new TableScoreTracker(settings);
            var team = new TeamManager(settings);
            var score = new ScoreManager(settings, team);
            var tm = new TurnManager(settings, deck, tracker, team, score);

            tm.StartNewRound(0);

            // 1. Gösterge Kuralı Testi
            var indicatorClone = new TileData(500, deck.IndicatorTile.Color, deck.IndicatorTile.Number);
            tm.PlayerHands[0].AddTile(indicatorClone);
            bool indicatorShown = tm.TryShowIndicator(0, out string indMsg);
            Assert(indicatorShown, $"Gösterge taşı elde varken gösterge yapılabilmeli: {indMsg}");
            Assert(tm.WrongDiscardPenalties[0] == -1, "Gösterge yapan oyuncuya -1 ceza birimi (-101 puan bonusu) yazılmalı.");
            Assert(!tm.TryShowIndicator(0, out _), "Aynı elde ikinci kez gösterge yapılamamalı.");

            // 2. Yandan Taş Alma Zorunluluk Kilidi Testi
            var discardedBy0 = tm.PlayerHands[0].Tiles.First(t => !t.IsOkey);
            tm.DiscardTile(0, discardedBy0, out _, out _, out _);

            // Sıra Oyuncu 1'de: Yandan taşı çeker
            bool sideDrawn = tm.DrawFromSideDiscard(1, out var sideTile, out _);
            Assert(sideDrawn && tm.MustUseSideTileId == sideTile.Id, "Yandan çekilen taş MustUseSideTileId olarak kilitlenmeli.");

            // Oyuncu 1 yan taşı kullanmadan başka bir taş atmaya çalışırsa engellenmeli
            var otherTile = tm.PlayerHands[1].Tiles.First(t => t.Id != sideTile.Id);
            bool illegalDiscard = tm.DiscardTile(1, otherTile, out _, out _, out string lockErr);
            Assert(!illegalDiscard, $"Yandan alınan taş kullanılmadan taş atılamamalı. Mesaj: {lockErr}");

            // Yan taşı iptal edip desteden çekebilmeli
            bool cancelled = tm.CancelSideDiscardAndDrawFromDeck(1, out _, out string cancelErr);
            Assert(cancelled && !tm.MustUseSideTileId.HasValue, $"Yan taş iptal edilip desteden çekilebilmeli. Hata: {cancelErr}");

            // 3. Son Taş İşlenemez Kuralı Testi
            var hand1 = tm.PlayerHands[1];
            hand1.HasOpenedSeries = true;
            var tableRun = new PerGroup(PerType.RunSameColor, new List<TileData>
            {
                new TileData(601, TileColor.Red, 5),
                new TileData(602, TileColor.Red, 6),
                new TileData(603, TileColor.Red, 7)
            });
            tracker.OpenedSeriesOnTable.Add(tableRun);

            // Oyuncu 1'in elinde tek 1 taş (Kırmızı 8) bırakalım
            hand1.Tiles.Clear();
            var lastPlayableTile = new TileData(604, TileColor.Red, 8);
            hand1.AddTile(lastPlayableTile);

            bool processedLast = tm.ProcessTile(1, lastPlayableTile, tableRun, out string lastTileErr);
            Assert(!processedLast, $"Eldeki son taş masaya işlenememeli (yere atılmalı): {lastTileErr}");

            // 4. İşler Taş veya Okey Atma Cezası (+101) Testi
            hand1.AddTile(new TileData(605, TileColor.Black, 2)); // Artık elde 2 taş var
            tm.DiscardTile(1, lastPlayableTile, out bool finished, out _, out _);
            Assert(!finished && tm.WrongDiscardPenalties.ContainsKey(1) && tm.WrongDiscardPenalties[1] == 1,
                "Masadaki pere işlenebilen Kırmızı 8 yere atıldığında +101 işlek taş cezası uygulanmalı.");

            // 5. 11-12-13-1 Serisinin Sonuna 2 İşlenemez, Başına 10 İşlenebilir Testi
            var wrapPer = new PerGroup(PerType.RunSameColor, new List<TileData>
            {
                new TileData(701, TileColor.Blue, 11),
                new TileData(702, TileColor.Blue, 12),
                new TileData(703, TileColor.Blue, 13),
                new TileData(704, TileColor.Blue, 1)
            });
            Assert(!TileValidator.CanProcessTileIntoPer(wrapPer, new TileData(705, TileColor.Blue, 2), out _),
                "11-12-13-1 perinin sonuna 2 taşı ASLA işlenememeli.");
            Assert(TileValidator.CanProcessTileIntoPer(wrapPer, new TileData(706, TileColor.Blue, 10), out bool atStart) && atStart,
                "11-12-13-1 perinin başına 10 taşı işlenebilmeli.");

            // 6. Pere İşlenen Okey'in Temsil Değerini Güncellemesi ve Sonradan Çalınabilmesi Testi
            var shortRun = new PerGroup(PerType.RunSameColor, new List<TileData>
            {
                new TileData(710, TileColor.Yellow, 7),
                new TileData(711, TileColor.Yellow, 8),
                new TileData(712, TileColor.Yellow, 9)
            });
            var appendedOkey = new TileData(713, TileColor.Red, 3, isFakeOkey: false, isOkey: true);
            Assert(TileValidator.CanProcessTileIntoPer(shortRun, appendedOkey, out bool okeyAtStart), "7-8-9 perine Okey işlenebilmeli.");
            tracker.AppendTileToExistingPer(shortRun, appendedOkey, okeyAtStart);
            Assert(appendedOkey.RepresentedColor == TileColor.Yellow && appendedOkey.RepresentedNumber == 10,
                $"7-8-9 perinin sonuna işlenen Okey Sarı 10'u temsil etmeli. (Gerçek: {appendedOkey.RepresentedColor} {appendedOkey.RepresentedNumber})");
            Assert(TileValidator.CanSwapOkeyFromPer(shortRun, new TileData(714, TileColor.Yellow, 10), out _),
                "Sonradan pere işlenen Okey de gerçek Sarı 10 taşı ile çalınabilmeli.");

            // 7. Sırasız Seçilen Per Doğrulaması ([8, 6, 7] -> [6, 7, 8])
            var unorderedRun = new List<TileData>
            {
                new TileData(720, TileColor.Black, 8),
                new TileData(721, TileColor.Black, 6),
                new TileData(722, TileColor.Black, 7)
            };
            Assert(TileValidator.ValidateRunPer(unorderedRun) && unorderedRun[0].Number == 6 && unorderedRun[2].Number == 8,
                "Sırasız seçilen [8, 6, 7] taşları otomatik [6, 7, 8] olarak sıralanıp doğrulanmalı.");

            // 8. Elde Kalan Okey Cezası (101 Puan) Testi
            var handWithOkey = new PlayerHand(2) { HasOpenedSeries = true };
            handWithOkey.AddTile(new TileData(730, TileColor.Red, 5));
            handWithOkey.AddTile(new TileData(731, TileColor.Blue, 4, isFakeOkey: false, isOkey: true));
            Assert(handWithOkey.CalculateRemainingHandScore() == 106,
                $"Elde kalan Kırmızı 5 + Okey (101) toplam 106 ceza puanı olmalı. (Gerçek: {handWithOkey.CalculateRemainingHandScore()})");
        }
    }
}
