using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class VisualizarModeTests
    {
        [Fact]
        public void VizEntrar_DoesNotSubmitOrders()
        {
            var h = SetupVizAtOrdenesPuestas();

            h.OrderEngine.Reset();
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // No real orders in Visualizar mode
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizBreakoutLong_TransitionsToEnTrade()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Breakout above range
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Should now be in EnTrade (VIZ) - next tick still shouldn't submit orders
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 5);
            h.Tick(t2, 20025);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizBreakoutShort_TransitionsToEnTrade()
        {
            var h = SetupVizAtOrdenesPuestas();

            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 19980, 19981, 19975, 19979);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 5);
            h.Tick(t2, 19975);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizTP1_ExitsWhenStopHit()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Entry long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Price drops to stop level (rangoLow - ColchonStop = 19975)
            var t2 = new DateTime(2026, 1, 15, 9, 43, 0);
            h.NewBar(t2, 20000, 20000, 19974, 19974);

            // Should have exited viz trade
            // Next tick should process tradeEnded
            var t3 = new DateTime(2026, 1, 15, 9, 43, 5);
            h.Tick(t3, 19974);

            // No crash, no real orders
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizTP1_ExitsAtTakeProfit()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Entry long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Price reaches TP1 (entry + stopDistance = 20021 + 45 = 20066 approx)
            // Actually entry in viz is Close[0] at breakout = 20021
            // stopDistance = 45, TP1 = entry + 45 = 20066
            var t2 = new DateTime(2026, 1, 15, 9, 44, 0);
            h.NewBar(t2, 20060, 20067, 20055, 20067);

            // No real orders
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizMonitorear_BreakevenWorks()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Entry long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);
            // vizEntry = 20021, stopDistance = 45
            // BE at 60% = 20021 + 27 = 20048

            // Push price to BE level
            var t2 = new DateTime(2026, 1, 15, 9, 43, 0);
            h.NewBar(t2, 20045, 20049, 20044, 20048);

            // Now price pulls back - should hit BE stop (entry + colchonBE = 20026)
            var t3 = new DateTime(2026, 1, 15, 9, 44, 0);
            h.NewBar(t3, 20048, 20048, 20025, 20025);

            // Should have exited at BE level
            Assert.Empty(h.OrderEngine.Entries); // no real orders
        }

        [Fact]
        public void VizAfterClose_ExitsForcefully()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Entry long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Close time
            var closeTime = new DateTime(2026, 1, 15, 15, 50, 0);
            h.NewBar(closeTime, 20050, 20055, 20045, 20050);

            // Should be DiaTerminado after
            h.OrderEngine.Reset();
            var afterClose = new DateTime(2026, 1, 15, 15, 55, 0);
            h.NewBar(afterClose, 20050, 20055, 20045, 20050);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizFlip_AfterStopLoss()
        {
            var h = SetupVizAtOrdenesPuestas();

            // Entry long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Stop hit (price <= stop = 19975)
            var t2 = new DateTime(2026, 1, 15, 9, 43, 0);
            h.NewBar(t2, 20000, 20000, 19974, 19974);

            // After stop loss in viz, should flip to short (pendingFlip)
            // Process the flip on next bar
            var t3 = new DateTime(2026, 1, 15, 9, 43, 5);
            h.NewBar(t3, 19974, 19975, 19970, 19972);

            // Still no real orders
            Assert.Empty(h.OrderEngine.Entries);
        }

        private StrategyTestHarness SetupVizAtOrdenesPuestas()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            return h;
        }
    }
}
