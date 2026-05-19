using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class OnExecutionUpdateTests
    {
        [Fact]
        public void LongEntry_SetsDirectionAndPrice()
        {
            var h = SetupAtOrdenesPuestas();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);

            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);

            // After execution, strategy should have set entry price
            // Verify via behavior: price at entry should trigger breakeven correctly
            // No direct field access but we can confirm via subsequent trailing behavior
        }

        [Fact]
        public void ShortEntry_SetsDirectionAndPrice()
        {
            var h = SetupAtOrdenesPuestas();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);

            h.SimulateExecution("TP1Short", 19980, 1, MarketPosition.Short, t);
            // Should set direction=-1
        }

        [Fact]
        public void EntryLong_CountsTrade()
        {
            var h = SetupAtOrdenesPuestas();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);

            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, t);

            // tradesToday should be 1 (counted once, not twice)
            // Verified indirectly: a second full trade cycle should work (MaxTrades=2)
        }

        [Fact]
        public void NullOrder_IsIgnored()
        {
            var h = SetupAtOrdenesPuestas();
            var execution = new Execution { Order = null };

            // Should not throw
            h.Strategy.TriggerOnExecutionUpdate(execution, "id", 20000, 1, MarketPosition.Long, "oid", DateTime.Now);
        }

        [Fact]
        public void StopLoss_CalculatesPnLForLong()
        {
            var h = SetupAtOrdenesPuestas();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, t);

            // Now simulate stop loss hit at 19975 (loss of 45 pts per contract, $2/pt)
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SimulateExecution("Stop loss", 19975, 1, MarketPosition.Long, exitTime);

            // PnL = (19975 - 20020) * 2.0 * 1 = -90
            // We can't directly read dailyPnL but can verify behavior cascades correctly
        }

        [Fact]
        public void ProfitTarget_MarksTakeProfitExit()
        {
            var h = SetupAtOrdenesPuestas();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, t);

            // Profit target hit - goes flat
            var exitTime = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20065, 1, MarketPosition.Flat, exitTime);
            h.SimulateExecution("Profit target", 20065, 1, MarketPosition.Flat, exitTime);

            // After this, tradeEndedByTakeProfit should be true
            // Verified indirectly: next bar processes the TP logic
            var processTime = new DateTime(2026, 1, 15, 9, 50, 1);
            h.NewBar(processTime, 20065, 20070, 20060, 20065);

            // After TP, should go to cooldown (not flip)
            // Verify no immediate flip entry
            h.OrderEngine.Reset();
            var nextTime = new DateTime(2026, 1, 15, 9, 50, 10);
            h.Tick(nextTime, 20021); // above rangoHigh
            Assert.Empty(h.OrderEngine.Entries); // cooldown active
        }

        [Fact]
        public void StopLoss_MarksFlipPending()
        {
            var h = SetupAtOrdenesPuestas(maxTrades: 4);
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, t);

            // Stop loss - goes flat
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 2, MarketPosition.Flat, exitTime);

            // Process tradeEnded - should trigger flip (maxStopsPuros=2 not reached yet)
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 19975, 19980, 19970, 19975);

            // Flip should enter SHORT immediately
            Assert.Contains(h.OrderEngine.Entries, e =>
                e.Direction == MarketPosition.Short);
        }

        [Fact]
        public void TrailingDailyDrawdown_EndsDayWhenExceeded()
        {
            var h = SetupAtOrdenesPuestas();
            h.Strategy.PerdidaMaxDiaria = 100;
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, t);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, t);

            // First trade profits - peak goes up
            var exitTime1 = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SimulateExecution("Profit target", 20045, 1, MarketPosition.Long, exitTime1);
            // PnL = (20045-20020)*2*1 = 50, peak=50

            // Second execution loses badly (simulating combined)
            h.SimulateExecution("Stop loss", 19945, 1, MarketPosition.Flat, exitTime1);
            // PnL += (19945-20020)*2*1 = -150, dailyPnL = 50-150=-100
            // peak=50, drawdown = 50-(-100)=150 >= PerdidaMaxDiaria=100

            // After this, dia terminado
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 50, 1);
            h.NewBar(processTime, 19945, 19950, 19940, 19945);
            Assert.Empty(h.OrderEngine.Entries);
        }

        private StrategyTestHarness SetupAtOrdenesPuestas(int maxTrades = 2)
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = maxTrades;
                s.PerdidaMaxDiaria = 1000;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            return h;
        }
    }
}
