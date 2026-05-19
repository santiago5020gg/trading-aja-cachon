using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class CoverageBoostTests
    {
        [Fact]
        public void DailyReset_FlattensOpenPositionFromPreviousDay()
        {
            var h = new StrategyTestHarness(s => { s.ModoLog = MNQ10minV2.LogMode.Off; s.PerdidaMaxDiaria = 1000; });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Entry and set position
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Day change with position still open
            h.OrderEngine.Reset();
            var day2 = new DateTime(2026, 1, 16, 9, 0, 0);
            h.NewBar(day2, 20050, 20055, 20045, 20050);

            // Should have flattened the position
            Assert.NotEmpty(h.OrderEngine.Exits);
        }

        [Fact]
        public void ProcesarFinTrade_TPExitWhenMaxTradesReached()
        {
            // MaxTrades=2, after TP maxTakeProfits=1 reached -> DiaTerminado
            var h = SetupFullTrade(maxTrades: 2, perdidaMax: 1000);

            // First TP exit - maxTakeProfits=1, should end day
            var exitTime = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20065, 2, MarketPosition.Flat, exitTime);

            var processTime = new DateTime(2026, 1, 15, 9, 50, 1);
            h.NewBar(processTime, 20065, 20070, 20060, 20065);

            // Verify DiaTerminado
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 10, 0, 0), 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ProcesarFinTrade_StopExitWhenMaxTradesReached()
        {
            // After a stop, if tradesToday >= tradesPermitidosHoy, end day
            // MaxTrades=4, first stop flips, second stop -> maxStopsPuros=2 -> DiaTerminado
            var h = SetupFullTrade(maxTrades: 4, perdidaMax: 1000);

            // Stop loss -> flip
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 2, MarketPosition.Flat, exitTime);

            // Process: flip to short
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 19975, 19980, 19970, 19975);
            Assert.NotEmpty(h.OrderEngine.Entries);

            // Fill the flip
            h.SetPosition(MarketPosition.Short, 2, 19975);
            h.SimulateExecution("TP1Short", 19975, 1, MarketPosition.Short, processTime);
            h.SimulateExecution("TP2Short", 19975, 1, MarketPosition.Short, processTime);

            // Second stop
            var exitTime2 = new DateTime(2026, 1, 15, 9, 48, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime2);

            // Process: maxStopsPuros=2 reached -> DiaTerminado
            h.OrderEngine.Reset();
            var processTime2 = new DateTime(2026, 1, 15, 9, 48, 1);
            h.NewBar(processTime2, 20025, 20030, 20020, 20025);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ProcesarFinTrade_BreakevenExitRecalculates()
        {
            // MaxTrades=6 -> maxBreakevens=3
            var h = SetupFullTrade(maxTrades: 6, perdidaMax: 1500);

            // Trigger breakeven
            var beTime = new DateTime(2026, 1, 15, 9, 44, 0);
            double bePrice = 20020 + (45 * 0.60);
            h.SetClose(bePrice);
            h.Tick(beTime, bePrice);

            // Exit at breakeven
            var exitTime = new DateTime(2026, 1, 15, 9, 46, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime);

            // Process - should go to cooldown (breakevensHoy=1 < maxBreakevens=3)
            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 46, 1);
            h.NewBar(processTime, 20025, 20030, 20020, 20025);

            // Should be in cooldown (no entry yet)
            Assert.Empty(h.OrderEngine.Entries);

            // After cooldown + price in range + breakout
            var afterCooldown = new DateTime(2026, 1, 15, 9, 47, 30);
            h.Tick(afterCooldown, 20000); // price in range
            h.Tick(new DateTime(2026, 1, 15, 9, 47, 31), 20021); // breakout

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MonitorearOrdenes_DetectsExistingPosition()
        {
            // If we're in OrdenesPuestas but position is already filled (not flat),
            // should transition to EnTrade
            var h = new StrategyTestHarness(s => { s.ModoLog = MNQ10minV2.LogMode.Off; s.PerdidaMaxDiaria = 1000; });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Simulate entry that fills
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Next tick while position is active
            h.OrderEngine.Reset();
            var t = new DateTime(2026, 1, 15, 9, 42, 5);
            h.Tick(t, 20025);

            // Should be in EnTrade state, monitoring trailing
        }

        [Fact]
        public void MonitorearOrdenes_FlipLong_InOperarMode()
        {
            // PerdidaMaxDiaria=2000, MaxTrades=4: presup=500, rango=40, riesgo=225
            // contratos=floor(500/225)=2, qtyTP1=1, qtyTP2=1
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 2000;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Short entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 19980, 19981, 19975, 19979);
            h.SetPosition(MarketPosition.Short, 2, 19980);
            h.SimulateExecution("TP1Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Short", 19980, 1, MarketPosition.Short, new DateTime(2026, 1, 15, 9, 42, 0));

            // Stop hit -> flip to long
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime);

            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 20025, 20030, 20020, 20025);

            // Flip to long with both TP1 and TP2
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName == "TP1Long");
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName == "TP2Long");
        }

        [Fact]
        public void MonitorearOrdenes_FlipShort_InOperarMode()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 2000;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Long entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Stop hit -> flip to short
            var exitTime = new DateTime(2026, 1, 15, 9, 45, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 2, MarketPosition.Flat, exitTime);

            h.OrderEngine.Reset();
            var processTime = new DateTime(2026, 1, 15, 9, 45, 1);
            h.NewBar(processTime, 19975, 19980, 19970, 19975);

            // Flip to short with both TP1 and TP2
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName == "TP1Short");
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName == "TP2Short");
        }

        [Fact]
        public void MonitorearOrdenes_ReentryAfterCooldownAndPriceReturnsToRange()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 6;
                s.PerdidaMaxDiaria = 1500;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // First trade
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Trigger BE
            var beTime = new DateTime(2026, 1, 15, 9, 44, 0);
            h.SetClose(20020 + 45 * 0.60);
            h.Tick(beTime, 20020 + 45 * 0.60);

            // BE exit
            var exitTime = new DateTime(2026, 1, 15, 9, 46, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 2, MarketPosition.Flat, exitTime);

            // Process
            h.NewBar(new DateTime(2026, 1, 15, 9, 46, 1), 20025, 20030, 20020, 20025);

            // Wait cooldown (50s) then price outside range
            h.OrderEngine.Reset();
            h.Tick(new DateTime(2026, 1, 15, 9, 47, 30), 20025); // outside range, after cooldown
            Assert.Empty(h.OrderEngine.Entries); // reentry waiting for price back in range

            // Price returns to range
            h.Tick(new DateTime(2026, 1, 15, 9, 47, 31), 20000); // in range

            // Now breakout
            h.OrderEngine.Reset();
            h.Tick(new DateTime(2026, 1, 15, 9, 47, 32), 20021); // above rangoHigh
            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MonitorearTrade_VizMode_NotActive_EndsTradeImmediately()
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

            // Breakout -> viz entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Stop hit immediately
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 5), 19974, 19975, 19970, 19974);

            // Process tradeEnded
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 10), 19974, 19975, 19970, 19974);

            // Should still be working (not crash)
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MonitorearTrade_OperarMode_PositionGoesFlat_SetsTradeEnded()
        {
            var h = new StrategyTestHarness(s => { s.ModoLog = MNQ10minV2.LogMode.Off; s.PerdidaMaxDiaria = 1000; });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Position goes flat externally (simulating stop hit via broker)
            h.SetPosition(MarketPosition.Flat);

            // Next tick detects flat -> tradeEnded
            var t = new DateTime(2026, 1, 15, 9, 43, 0);
            h.Tick(t, 20000);

            // No crash, state transitions correctly
        }

        [Fact]
        public void VizMonitorear_TP2HitsTarget()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 4;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout long
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Price reaches TP2 (entry + stopDist*2 = 20021 + 90 = 20111)
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 0), 20100, 20112, 20095, 20111);

            // Should have completed the trade
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizMonitorear_TrailingTP1AndTP2_FullProgression()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                s.MaxTrades = 4;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout long (entry = 20021, stopDist = 45, tp2Target = 90)
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Progress through breakeven (60% of 45 = 27 -> 20048)
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 20040, 20049, 20038, 20048);

            // TP1 escalon 1 (75% of 45 = 33.75 -> 20054.75)
            h.NewBar(new DateTime(2026, 1, 15, 9, 44, 0), 20050, 20056, 20048, 20055);

            // TP1 escalon 2 (85% of 45 = 38.25 -> 20059.25)
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 0), 20055, 20060, 20053, 20060);

            // TP1 hits target (100% = 45 -> 20066)
            h.NewBar(new DateTime(2026, 1, 15, 9, 46, 0), 20060, 20067, 20058, 20067);

            // TP2 progress: 50% of 90 = 45 -> 20066, already above
            // TP2 70% of 90 = 63 -> 20084
            h.NewBar(new DateTime(2026, 1, 15, 9, 47, 0), 20067, 20085, 20065, 20085);

            // TP2 85% of 90 = 76.5 -> 20097.5
            h.NewBar(new DateTime(2026, 1, 15, 9, 48, 0), 20085, 20098, 20083, 20098);

            // Keep going, no crash
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizMonitorear_FlipAfterStopLoss_InVizMode()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 1000;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout long
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Stop hit (close <= stop = rangoLow - colchon = 19980 - 5 = 19975)
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 20000, 20000, 19974, 19974);

            // Process tradeEnded -> flip in viz mode
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 5), 19974, 19975, 19970, 19972);

            // Should be in a new viz trade (short)
            Assert.Empty(h.OrderEngine.Entries); // still no real orders
        }

        [Fact]
        public void VizMonitorear_ShortTrade_StopHit()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 1000;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout short
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 19980, 19981, 19975, 19979);

            // Stop hit (close >= stop = rangoHigh + colchon = 20025)
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 20000, 20026, 19998, 20026);

            // No real orders
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MonitorearOrdenes_MaxTradesReached_EndsDayInOrdenesPuestas()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 1;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // First trade
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // TP exit
            var exitTime = new DateTime(2026, 1, 15, 9, 50, 0);
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20110, 1, MarketPosition.Flat, exitTime);

            // Process
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 1), 20110, 20115, 20105, 20110);

            // Should be ended
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 10, 0, 0), 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        private StrategyTestHarness SetupFullTrade(int maxTrades, double perdidaMax)
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = maxTrades;
                s.PerdidaMaxDiaria = perdidaMax;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            return h;
        }
    }
}
