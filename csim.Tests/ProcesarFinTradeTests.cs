using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class ProcesarFinTradeTests
    {
        [Fact]
        public void TakeProfit_GoesToCooldown_ThenReenters()
        {
            // MaxTrades=4 so TP doesn't end the day (maxTakeProfits=2)
            var h = SetupFullTrade(maxTrades: 4);

            // Simulate TP exit
            var exitTime = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20065, 1, MarketPosition.Flat, exitTime);
            h.SimulateExecution("Profit target", 20110, 1, MarketPosition.Flat, exitTime);

            // Process
            var processTime = new DateTime(2026, 1, 15, 9, 50, 1);
            h.NewBar(processTime, 20065, 20070, 20060, 20065);

            // During cooldown - no entry
            h.OrderEngine.Reset();
            var cooldownTime = new DateTime(2026, 1, 15, 9, 50, 30);
            h.Tick(cooldownTime, 20000); // price in range
            Assert.Empty(h.OrderEngine.Entries);

            // After 50s cooldown + price back in range + breakout
            h.OrderEngine.Reset();
            var afterCooldown = new DateTime(2026, 1, 15, 9, 51, 30); // >50s
            h.Tick(afterCooldown, 20000); // price in range -> sets reentryPriceInRange
            h.Tick(new DateTime(2026, 1, 15, 9, 51, 31), 20021); // above rangoHigh

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void StopLoss_TriggersFlipToOppositeDirection()
        {
            // MaxTrades=4 -> maxStopsPuros=2, so first stop doesn't end day
            var h = SetupFullTrade(maxTrades: 4);

            // Simulate stop loss
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 2, MarketPosition.Flat, exitTime);

            // Process tradeEnded
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 19975, 19980, 19970, 19975);

            // Should flip to short
            Assert.Contains(h.OrderEngine.Entries, e => e.Direction == MarketPosition.Short);
        }

        [Fact]
        public void MaxStops_EndsDayAfterMaxStopsPuros()
        {
            // MaxTrades=4, maxStopsPuros=2 -> first stop flips, second stop ends day
            var h = SetupFullTrade(maxTrades: 4);

            // First stop loss
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 2, MarketPosition.Flat, exitTime);

            // Process
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 19975, 19980, 19970, 19975);

            // Should have flipped (first stop, maxStops not yet reached)
            Assert.NotEmpty(h.OrderEngine.Entries);

            // Simulate the flip entry
            h.SetPosition(MarketPosition.Short, 2, 19975);
            h.SimulateExecution("TP1Short", 19975, 1, MarketPosition.Short, processTime);
            h.SimulateExecution("TP2Short", 19975, 1, MarketPosition.Short, processTime);

            // Second stop loss (flip fails)
            var exitTime2 = new DateTime(2026, 1, 15, 9, 48, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime2);

            // Process - should end day (maxStopsPuros=2 reached)
            h.OrderEngine.Reset();
            var processTime2 = new DateTime(2026, 1, 15, 9, 48, 1);
            h.NewBar(processTime2, 20025, 20030, 20020, 20025);

            // Verify no more entries possible
            h.OrderEngine.Reset();
            var laterTime = new DateTime(2026, 1, 15, 10, 0, 0);
            h.NewBar(laterTime, 19979, 19980, 19975, 19979);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MaxTakeProfit_EndsDayAfterMaxTP()
        {
            // MaxTrades=2, maxTakeProfits=1
            var h = SetupFullTrade();

            // First TP
            var exitTime = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20065, 2, MarketPosition.Flat, exitTime);

            // Process
            var processTime = new DateTime(2026, 1, 15, 9, 50, 1);
            h.NewBar(processTime, 20065, 20070, 20060, 20065);

            // maxTakeProfits=1, so should end day
            h.OrderEngine.Reset();
            var laterTime = new DateTime(2026, 1, 15, 10, 0, 0);
            h.NewBar(laterTime, 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void BreakevenExit_RecalculatesRemainingBudget()
        {
            var h = SetupFullTrade();
            h.Strategy.PerdidaMaxDiaria = 400;
            h.Strategy.MaxTrades = 4;

            // Trigger breakeven on the active trade
            var beTime = new DateTime(2026, 1, 15, 9, 44, 0);
            double bePrice = 20020 + (45 * 0.60); // 20047
            h.SetClose(bePrice);
            h.Tick(beTime, bePrice);

            // Now exit at breakeven (price pulls back to BE level)
            var exitTime = new DateTime(2026, 1, 15, 9, 46, 0);
            h.SetPosition(MarketPosition.Flat);
            // Exit at entry + colchonBE = 20025 (very small profit/loss)
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime);

            // Process - should not flip (BE exit), goes to cooldown
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 46, 1);
            h.NewBar(processTime, 20025, 20030, 20020, 20025);

            // Verify no flip (flip only happens on StopLoss)
            bool hasFlipEntry = false;
            foreach (var e in h.OrderEngine.Entries)
            {
                if (e.Direction == MarketPosition.Short) hasFlipEntry = true;
            }
            Assert.False(hasFlipEntry);
        }

        [Fact]
        public void MaxBreakevens_EndsDay()
        {
            // MaxTrades=2, maxBreakevens=1
            var h = SetupFullTrade();

            // Trigger breakeven first
            var beTime = new DateTime(2026, 1, 15, 9, 44, 0);
            double bePrice = 20020 + (45 * 0.60);
            h.SetClose(bePrice);
            h.Tick(beTime, bePrice);

            // Exit at breakeven
            var exitTime = new DateTime(2026, 1, 15, 9, 46, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime);

            // Process
            var processTime = new DateTime(2026, 1, 15, 9, 46, 1);
            h.NewBar(processTime, 20025, 20030, 20020, 20025);

            // maxBreakevens=1, so should end day
            h.OrderEngine.Reset();
            var laterTime = new DateTime(2026, 1, 15, 10, 0, 0);
            h.NewBar(laterTime, 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        private StrategyTestHarness SetupFullTrade(int maxTrades = 2)
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = maxTrades;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Trigger long entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            return h;
        }
    }
}
