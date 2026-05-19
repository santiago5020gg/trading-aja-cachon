using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class HelpersTests
    {
        [Fact]
        public void FlattenAll_ExitsLongPosition()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);
            SetupInTrade(h, MarketPosition.Long);

            // Trigger flatten via close time
            var closeTime = new DateTime(2026, 1, 15, 15, 50, 0);
            h.NewBar(closeTime, 20050, 20055, 20045, 20050);

            Assert.Contains(h.OrderEngine.Exits, e =>
                e.FromEntry.Contains("Long") && e.Direction == MarketPosition.Long);
        }

        [Fact]
        public void FlattenAll_ExitsShortPosition()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);
            SetupInTrade(h, MarketPosition.Short);

            var closeTime = new DateTime(2026, 1, 15, 15, 50, 0);
            h.NewBar(closeTime, 19950, 19955, 19945, 19950);

            Assert.Contains(h.OrderEngine.Exits, e =>
                e.FromEntry.Contains("Short") && e.Direction == MarketPosition.Short);
        }

        [Fact]
        public void CancelAllPendingOrders_CancelsWorkingOrders()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);
            var workingOrder = new Order
            {
                Name = "TestStop",
                OrderState = OrderState.Working,
                Instrument = h.Strategy.Instrument
            };
            var acceptedOrder = new Order
            {
                Name = "TestTarget",
                OrderState = OrderState.Accepted,
                Instrument = h.Strategy.Instrument
            };
            var filledOrder = new Order
            {
                Name = "Filled",
                OrderState = OrderState.Filled,
                Instrument = h.Strategy.Instrument
            };
            h.Strategy.Account.Orders.Add(workingOrder);
            h.Strategy.Account.Orders.Add(acceptedOrder);
            h.Strategy.Account.Orders.Add(filledOrder);

            // Trigger before open (cancels pending)
            var preOpen = new DateTime(2026, 1, 15, 9, 0, 0);
            h.NewBar(preOpen, 20000, 20005, 19995, 20000);

            Assert.Contains(workingOrder, h.OrderEngine.CancelledOrders);
            Assert.Contains(acceptedOrder, h.OrderEngine.CancelledOrders);
            Assert.DoesNotContain(filledOrder, h.OrderEngine.CancelledOrders);
        }

        [Fact]
        public void CancelAllPendingOrders_IgnoresOtherInstruments()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);
            var otherInstrumentOrder = new Order
            {
                Name = "OtherOrder",
                OrderState = OrderState.Working,
                Instrument = new Instrument { FullName = "ES 06-26" }
            };
            h.Strategy.Account.Orders.Add(otherInstrumentOrder);

            var preOpen = new DateTime(2026, 1, 15, 9, 0, 0);
            h.NewBar(preOpen, 20000, 20005, 19995, 20000);

            Assert.DoesNotContain(otherInstrumentOrder, h.OrderEngine.CancelledOrders);
        }

        [Fact]
        public void HoraCierre_CustomValue()
        {
            // Reconfigure handles HoraCierre re-parse via DataLoaded
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.HoraCierre = "14:30";
            });

            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // At 14:30 should close
            var closeTime = new DateTime(2026, 1, 15, 14, 30, 0);
            h.NewBar(closeTime, 20050, 20055, 20045, 20050);

            // Verify dia terminado
            h.OrderEngine.Reset();
            var after = new DateTime(2026, 1, 15, 14, 35, 0);
            h.NewBar(after, 20020, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ResetDaily_ClearsAllState()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);

            // Day 1 - full trade
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Day 2 - should be fresh
            h.OrderEngine.Reset();
            var day2 = new DateTime(2026, 1, 16, 9, 32, 0);
            h.NewBar(day2, mid, high, low, mid);

            // Verify range is accumulating fresh (no entries yet)
            Assert.Empty(h.OrderEngine.Entries);
        }

        private void SetupInTrade(StrategyTestHarness h, MarketPosition position)
        {
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            if (position == MarketPosition.Long)
            {
                h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
                h.SetPosition(MarketPosition.Long, 2, 20020);
                h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
                h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            }
            else
            {
                h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 19980, 19981, 19975, 19979);
                h.SetPosition(MarketPosition.Short, 2, 19980);
                h.SimulateExecution("TP1Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));
                h.SimulateExecution("TP2Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));
            }
        }
    }
}
