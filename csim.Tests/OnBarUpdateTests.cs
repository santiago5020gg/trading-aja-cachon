using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class OnBarUpdateTests
    {
        [Fact]
        public void SkipsIfCurrentBarLessThanBarsRequired()
        {
            var h = new StrategyTestHarness();
            h.Strategy.CurrentBar = 5; // < 20
            h.Strategy.IsFirstTickOfBar = true;
            var et = new DateTime(2026, 1, 15, 9, 35, 0);
            h.SetTime(et);
            h.SetPrice(100, 101, 99, 100);
            h.Strategy.TriggerOnBarUpdate();

            // Should not have placed any orders
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ResetsDailyOnNewDate()
        {
            var h = new StrategyTestHarness();

            // Day 1 - build rango
            var day1_932 = new DateTime(2026, 1, 15, 9, 32, 0);
            h.NewBar(day1_932, 20000, 20010, 19990, 20005);

            var day1_940 = new DateTime(2026, 1, 15, 9, 40, 0);
            h.NewBar(day1_940, 20005, 20012, 19988, 20000);

            // Day 2 - should reset
            var day2_early = new DateTime(2026, 1, 16, 8, 0, 0);
            h.NewBar(day2_early, 20000, 20005, 19995, 20000);

            // No entries should be placed before 9:32
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void DiaTerminado_StopsProcessing()
        {
            var h = new StrategyTestHarness();

            // Build rango
            BuildRango(h, 20000, 20010, 19990);

            // After rango -> OrdenesPuestas
            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // Trigger close time (15:50)
            var closetime = new DateTime(2026, 1, 15, 15, 50, 0);
            h.NewBar(closetime, 20000, 20005, 19995, 20000);

            // Now any tick should be ignored
            h.OrderEngine.Reset();
            var afterClose = new DateTime(2026, 1, 15, 15, 55, 0);
            h.NewBar(afterClose, 20000, 20005, 19995, 20000);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void AfterClose_FlattensPosition()
        {
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);

            // Build rango so qtyTP1/qtyTP2 get calculated
            BuildRango(h, 20000, 20020, 19980);
            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // Entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            h.OrderEngine.Reset();
            var closetime = new DateTime(2026, 1, 15, 15, 50, 0);
            h.NewBar(closetime, 20050, 20055, 20045, 20050);

            Assert.NotEmpty(h.OrderEngine.Exits);
        }

        [Fact]
        public void BeforeOpen_CancelsPendingOrders()
        {
            var h = new StrategyTestHarness();
            var order = new Order
            {
                Name = "TestOrder",
                OrderState = OrderState.Working,
                Instrument = h.Strategy.Instrument
            };
            h.Strategy.Account.Orders.Add(order);

            var preopen = new DateTime(2026, 1, 15, 9, 0, 0);
            h.NewBar(preopen, 20000, 20005, 19995, 20000);

            Assert.Contains(order, h.OrderEngine.CancelledOrders);
        }

        private void BuildRango(StrategyTestHarness h, double mid, double high, double low)
        {
            for (int min = 32; min <= 40; min++)
            {
                var t = new DateTime(2026, 1, 15, 9, min, 0);
                h.NewBar(t, mid, high, low, mid);
            }
        }
    }
}
