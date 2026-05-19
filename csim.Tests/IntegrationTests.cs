using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void FullDay_LongBreakout_TP1Hit_Cooldown_Reentry()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 4;
            });

            // Build range 20000-20020 (40pts)
            BuildRango(h, 20000, 20020, 19980);

            // Transition
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);

            // Breakout long
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            Assert.NotEmpty(h.OrderEngine.Entries);

            // Fill
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Price rises to TP1 target
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20065, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 50, 0));
            h.SimulateExecution("Profit target", 20110, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 50, 0));

            // Process TP exit
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 1), 20065, 20070, 20060, 20065);

            // In cooldown - no entry
            h.OrderEngine.Reset();
            h.Tick(new DateTime(2026, 1, 15, 9, 50, 30), 20000);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void FullDay_ShortBreakout_StopHit_FlipToLong()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 4;
            });

            BuildRango(h, 20000, 20020, 19980);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);

            // Breakout short
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 19980, 19981, 19975, 19979);
            h.SetPosition(MarketPosition.Short, 2, 19980);
            h.SimulateExecution("TP1Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));

            // Stop hit (price goes above stopShort = 20025)
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 45, 0));

            // Process stop -> should flip to long
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 1), 20025, 20030, 20020, 20025);

            Assert.Contains(h.OrderEngine.Entries, e => e.Direction == MarketPosition.Long);
        }

        [Fact]
        public void FullDay_ExcessiveRiskEndsDayImmediately()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 50; // very tight budget
            });

            // Large range makes it impossible
            BuildRango(h, 20000, 20050, 19950); // 100pt range -> riesgo=(100+5)*5=525 > 50

            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);

            // Should be ended immediately
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20050, 20055, 20049, 20051);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void FullDay_MultipleBarsDuringRange_UsesMaxHighMinLow()
        {
            var h = new StrategyTestHarness(s => { s.ModoLog = MNQ10minV2.LogMode.Off; s.PerdidaMaxDiaria = 1000; });

            // Progressive range expansion
            h.NewBar(new DateTime(2026, 1, 15, 9, 32, 0), 20000, 20005, 19998, 20002);
            h.NewBar(new DateTime(2026, 1, 15, 9, 34, 0), 20002, 20010, 19995, 20008);
            h.NewBar(new DateTime(2026, 1, 15, 9, 36, 0), 20008, 20015, 19993, 20005);
            h.NewBar(new DateTime(2026, 1, 15, 9, 38, 0), 20005, 20012, 19990, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 40, 0), 20000, 20008, 19992, 20003);

            // rangoHigh should be 20015, rangoLow should be 19990
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20003, 20005, 20000, 20003);

            // Entry above 20015
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20015, 20018, 20014, 20016);
            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Visualizar_FullCycle_NoRealOrders()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
            });

            BuildRango(h, 20000, 20020, 19980);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);

            // Breakout
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Run to TP
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 0), 20060, 20070, 20055, 20068);

            // Close
            h.NewBar(new DateTime(2026, 1, 15, 15, 50, 0), 20100, 20105, 20095, 20100);

            // Never any real orders
            Assert.Empty(h.OrderEngine.Entries);
            Assert.Empty(h.OrderEngine.Exits);
        }

        [Fact]
        public void DayChange_ResetsCompletely()
        {
            var h = new StrategyTestHarness(s => { s.ModoLog = MNQ10minV2.LogMode.Off; s.PerdidaMaxDiaria = 1000; });

            // Day 1
            BuildRango(h, 20000, 20020, 19980);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), 20000, 20005, 19995, 20000);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Day 2 - completely fresh
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 16, 9, 30, 0), 20100, 20105, 20095, 20100);

            // Should be waiting for range again
            Assert.Empty(h.OrderEngine.Entries);

            // Build new range on day 2
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 16, 9, min, 0), 20100, 20120, 20080, 20100);
            h.NewBar(new DateTime(2026, 1, 16, 9, 41, 0), 20100, 20105, 20095, 20100);

            // New breakout
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 16, 9, 42, 0), 20120, 20125, 20119, 20121);
            Assert.NotEmpty(h.OrderEngine.Entries);
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
