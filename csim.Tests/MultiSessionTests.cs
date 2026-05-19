using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class MultiSessionTests
    {
        [Fact]
        public void TradingDay_StartsAt18ET_SameDateWhenAfter18()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = false;
                s.PerdidaMaxDiaria = 1000;
            });

            // 18:02 on Jan 14 -> tradingDay = Jan 14
            var t1 = new DateTime(2026, 1, 14, 18, 2, 0);
            h.NewBar(t1, 20000, 20005, 19995, 20000);

            // 09:30 on Jan 15 -> tradingDay = Jan 14 (same trading day)
            var t2 = new DateTime(2026, 1, 15, 9, 30, 0);
            h.NewBar(t2, 20000, 20005, 19995, 20000);

            // No crash, state progresses normally
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void TradingDay_ResetAt18ET_NewDay()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 1000;
            });

            // Day 1: start at 18:00 Jan 14
            var t1 = new DateTime(2026, 1, 14, 18, 2, 0);
            h.NewBar(t1, 20000, 20005, 19995, 20000);

            // Day 2: 18:00 Jan 15 -> new trading day
            var t2 = new DateTime(2026, 1, 15, 18, 2, 0);
            h.NewBar(t2, 20100, 20105, 20095, 20100);

            // Should have reset (no crash, waiting for range)
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Asia_RangoFormedAt1802_1810()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = false;
                s.PerdidaMaxDiaria = 1000;
            });

            // Build range during Asia window (18:02-18:10)
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20010, 19990, 20000);

            // After range
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20005, 19995, 20000);

            // Breakout
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20010, 20015, 20009, 20011);

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Europa_RangoFormedAt0202_0210()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = false;
                s.OperarEuropa = true;
                s.OperarAmerica = false;
                s.PerdidaMaxDiaria = 1000;
            });

            // Build range during Europa window (02:02-02:10)
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 15, 2, min, 0), 20000, 20010, 19990, 20000);

            // After range
            h.NewBar(new DateTime(2026, 1, 15, 2, 11, 0), 20000, 20005, 19995, 20000);

            // Breakout
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 2, 12, 0), 20010, 20015, 20009, 20011);

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void SharedBudget_LossInAsia_ReducesBudgetForAmerica()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 300;
                s.MaxTrades = 4;
            });

            // Asia range (18:02-18:10) — small range
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20010, 19990, 20000);

            // After range
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20005, 19995, 20000);

            // Breakout long in Asia
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20010, 20015, 20009, 20011);
            h.SetPosition(MarketPosition.Long, 2, 20010);
            h.SimulateExecution("TP1Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));
            h.SimulateExecution("TP2Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));

            // Stop loss in Asia — lose money
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19985, 2, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 20, 0));

            // Process tradeEnded
            h.NewBar(new DateTime(2026, 1, 14, 18, 20, 1), 19985, 19990, 19980, 19985);

            // tradesToday should be 1, dailyPnL negative
            // Now transition to America (09:30 next day)
            h.NewBar(new DateTime(2026, 1, 15, 9, 30, 0), 20000, 20005, 19995, 20000);

            // America range (09:32-09:40)
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), 20000, 20050, 19950, 20000);

            // After range — rango=100, riesgo=(100+5)*5=525 > remaining budget (~$300-loss)
            // Should be FUERA_PRESUPUESTO
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20050, 20055, 20049, 20051);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void DayHardStop_MaxTrades_PreventsRevivalInNextSession()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 1;
            });

            // Asia range
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20010, 19990, 20000);
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20005, 19995, 20000);

            // Breakout and fill
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20010, 20015, 20009, 20011);
            h.SetPosition(MarketPosition.Long, 2, 20010);
            h.SimulateExecution("TP1Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));
            h.SimulateExecution("TP2Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));

            // TP exit — tradesToday=1 >= MaxTrades=1
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20025, 2, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 30, 0));
            h.NewBar(new DateTime(2026, 1, 14, 18, 30, 1), 20025, 20030, 20020, 20025);

            // Now try America — should NOT revive (dayHardStop)
            h.NewBar(new DateTime(2026, 1, 15, 9, 30, 0), 20000, 20005, 19995, 20000);
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), 20000, 20020, 19980, 20000);
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void SessionLimit_MaxStops_AllowsRevivalInNextSession()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 6;
            });

            // Asia range (small so budget allows many trades)
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20003, 19997, 20000);

            // Entry
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20005, 20008, 20004, 20006);
            h.SetPosition(MarketPosition.Long, 2, 20005);
            h.SimulateExecution("TP1Long", 20005, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));
            h.SimulateExecution("TP2Long", 20005, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));

            // Stop #1
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19990, 2, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 15, 0));
            h.NewBar(new DateTime(2026, 1, 14, 18, 15, 1), 19990, 19995, 19985, 19990);

            // Flip fills
            h.SetPosition(MarketPosition.Short, 2, 19990);
            h.SimulateExecution("TP1Short", 19990, 1, MarketPosition.Short, new DateTime(2026, 1, 14, 18, 15, 1));
            h.SimulateExecution("TP2Short", 19990, 1, MarketPosition.Short, new DateTime(2026, 1, 14, 18, 15, 1));

            // Stop #2
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20010, 2, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 20, 0));
            h.NewBar(new DateTime(2026, 1, 14, 18, 20, 1), 20010, 20015, 20005, 20010);

            // Stop #3 = maxStopsPuros (ceil(6/2)=3) — DiaTerminado for this session
            h.SetPosition(MarketPosition.Long, 2, 20010);
            h.SimulateExecution("TP1Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 20, 1));
            h.SimulateExecution("TP2Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 20, 1));
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19990, 2, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 25, 0));
            h.NewBar(new DateTime(2026, 1, 14, 18, 25, 1), 19990, 19995, 19985, 19990);

            // Now America starts — should revive (maxStops is session limit, not dayHardStop)
            h.NewBar(new DateTime(2026, 1, 15, 9, 30, 0), 20000, 20005, 19995, 20000);
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), 20000, 20010, 19990, 20000);
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);
            // Price must visit inside range first (reentry condition: tradesToday>0)
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 30), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20010, 20015, 20009, 20011);

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Asia_ClosesAt0200_FlattensPosition()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = false;
                s.PerdidaMaxDiaria = 1000;
            });

            // Asia range
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20010, 19990, 20000);
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20005, 19995, 20000);

            // Entry
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20010, 20015, 20009, 20011);
            h.SetPosition(MarketPosition.Long, 2, 20010);
            h.SimulateExecution("TP1Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));
            h.SimulateExecution("TP2Long", 20010, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));

            // Asia closes at 02:00 next day — should flatten
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 2, 0, 0), 20020, 20025, 20015, 20020);

            Assert.NotEmpty(h.OrderEngine.Exits);
        }

        [Fact]
        public void GetSesionParaHora_AllSessionsCorrect()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarEuropa = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 1000;
            });

            // 18:30 -> Asia
            h.NewBar(new DateTime(2026, 1, 14, 18, 30, 0), 20000, 20005, 19995, 20000);
            // 01:00 -> Asia (after midnight)
            h.NewBar(new DateTime(2026, 1, 15, 1, 0, 0), 20000, 20005, 19995, 20000);
            // 03:00 -> Europa
            h.NewBar(new DateTime(2026, 1, 15, 3, 0, 0), 20000, 20005, 19995, 20000);
            // 09:35 -> America
            h.NewBar(new DateTime(2026, 1, 15, 9, 35, 0), 20000, 20005, 19995, 20000);

            // No crashes through all session transitions
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Drawdown_DayHardStop_NoRevival()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.OperarAsia = true;
                s.OperarAmerica = true;
                s.PerdidaMaxDiaria = 100;
                s.MaxTrades = 6;
            });

            // Asia range
            for (int min = 2; min <= 10; min++)
                h.NewBar(new DateTime(2026, 1, 14, 18, min, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 14, 18, 11, 0), 20000, 20003, 19997, 20000);

            // Entry
            h.NewBar(new DateTime(2026, 1, 14, 18, 12, 0), 20005, 20008, 20004, 20006);
            h.SetPosition(MarketPosition.Long, 1, 20005);
            h.SimulateExecution("TP2Long", 20005, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 12, 0));

            // TP hit — peak goes up
            h.SimulateExecution("Profit target", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 15, 0));
            // PnL = (20020-20005)*5 = 75, peak=75

            // Process
            h.SetPosition(MarketPosition.Flat);
            h.NewBar(new DateTime(2026, 1, 14, 18, 15, 1), 20020, 20025, 20015, 20020);

            // Re-entry after cooldown
            h.NewBar(new DateTime(2026, 1, 14, 18, 16, 30), 20000, 20005, 19995, 20000); // in range
            h.NewBar(new DateTime(2026, 1, 14, 18, 16, 31), 20005, 20008, 20004, 20006); // breakout
            h.SetPosition(MarketPosition.Long, 1, 20005);
            h.SimulateExecution("TP2Long", 20005, 1, MarketPosition.Long, new DateTime(2026, 1, 14, 18, 16, 31));

            // Big loss — drawdown triggers
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19985, 1, MarketPosition.Flat, new DateTime(2026, 1, 14, 18, 20, 0));
            // PnL += (19985-20005)*5 = -100. dailyPnL = 75-100=-25. peak=75. dd=75-(-25)=100 >= 100 -> DRAWDOWN

            // Process
            h.NewBar(new DateTime(2026, 1, 14, 18, 20, 1), 19985, 19990, 19980, 19985);

            // America should NOT revive
            h.NewBar(new DateTime(2026, 1, 15, 9, 30, 0), 20000, 20005, 19995, 20000);
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), 20000, 20010, 19990, 20000);
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20010, 20015, 20009, 20011);

            Assert.Empty(h.OrderEngine.Entries);
        }
    }
}
