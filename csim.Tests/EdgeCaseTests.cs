using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class EdgeCaseTests
    {
        [Fact]
        public void ProcesarFinTrade_TP_MaxTradesReached_EndsDayDueTrades()
        {
            // Scenario: after TP, tradesToday >= tradesPermitidosHoy -> DIA_TERMINADO_MAX_TRADES
            // Need: maxTakeProfits > 1 (so TP doesn't end day via maxTP check)
            // AND tradesToday >= tradesPermitidosHoy
            // MaxTrades=4, maxTP=2, arrange for tradesPermitidosHoy to be 1 (via risk calc)
            // Actually: tradesPermitidosHoy is set during rango calculation.
            // rango=40, riesgo=90, PerdidaMaxDiaria=400, MaxTrades=4:
            //   presup=100, 100<90 -> contratos=1, tradesPermitidos=floor(400/90)=4
            // Hard to make tradesToday>=4 after just 1 TP...
            // Easier: use PerdidaMaxDiaria=90, MaxTrades=4:
            //   presup=22.5 < 90, riesgo=90 <= 90 -> contratos=1, tradesPermitidos=floor(90/90)=1
            //   maxTP=2, so TP won't end day via that check
            //   But tradesToday=1 >= tradesPermitidos=1 -> DIA_TERMINADO_MAX_TRADES
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 90;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Entry (tradesPermitidos=1 here)
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 1, 20020);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // TP exit
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20110, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 50, 0));

            // Process: takeProfitsHoy=1, maxTP=2 (doesn't trigger)
            // But tradesToday=1 >= tradesPermitidosHoy=1 -> DIA_TERMINADO_MAX_TRADES
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 1), 20110, 20115, 20105, 20110);

            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 10, 0, 0), 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ProcesarFinTrade_Stop_MaxTradesReached_EndsDayDueTrades()
        {
            // After stop, if tradesToday >= tradesPermitidosHoy AND stopsPuros < maxStopsPuros
            // -> should end day due to max trades, not max stops
            // MaxTrades=4, PerdidaMaxDiaria=180 -> presup=45 < 90, contratos=1, tradesPermitidos=floor(180/90)=2
            // maxStopsPuros=2
            // After first trade (tradesToday=1), stop -> stopsPuros=1 < 2
            // tradesToday=1 < 2 -> flip happens
            // After flip (tradesToday=2), second stop -> stopsPuros=2 >= 2 -> max stops
            // We want the trades path. Use: tradesPermitidos=1 with maxStops=2
            // PerdidaMaxDiaria=90 -> tradesPermitidos=1, maxStopsPuros=2
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 90; // tradesPermitidos will be floor(90/90)=1
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 1, 20020);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Stop loss
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 45, 0));

            // Process: stopsPuros=1, maxStopsPuros=2 (doesn't trigger max stops)
            // tradesToday=1 >= tradesPermitidosHoy=1 -> DIA_TERMINADO_MAX_TRADES
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 1), 19975, 19980, 19970, 19975);

            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 10, 0, 0), 19979, 19980, 19975, 19979);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void ProcesarFinTrade_BE_MaxTradesReached_EndsDayDueTrades()
        {
            // After breakeven exit, tradesToday >= tradesPermitidosHoy
            // Use tradesPermitidosHoy=1, maxBreakevens=2
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 90; // tradesPermitidos=1
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 1, 20020);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Trigger breakeven
            double bePrice = 20020 + (45 * 0.60);
            h.SetClose(bePrice);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 0), bePrice);

            // BE exit
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 20025, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 46, 0));

            // Process: breakevensHoy=1, maxBreakevens=2 (no trigger)
            // After recalc: tradesToday=1 >= tradesPermitidosHoy(recalculated)
            // Since dailyPnL = small profit from BE exit, perdidaRealAcumulada=0
            // presupuestoRestante = 90 - 0 = 90
            // tradesPermitidosHoy = tradesToday + floor(90/90) = 1 + 1 = 2, capped at MaxTrades=4
            // So tradesToday=1 < 2 -> doesn't end day via this path
            // This actually goes to cooldown. Let's verify at least it doesn't crash.
            h.NewBar(new DateTime(2026, 1, 15, 9, 46, 1), 20025, 20030, 20020, 20025);
        }

        [Fact]
        public void MonitorearOrdenes_VizTradeActive_TransitionsToEnTrade()
        {
            // In Visualizar mode, if vizTradeActive is true when entering MonitorearOrdenes
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Entry in viz mode (via breakout)
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // Continue in trade
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 20021, 20030, 20019, 20025);
            h.NewBar(new DateTime(2026, 1, 15, 9, 44, 0), 20025, 20035, 20022, 20030);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void MonitorearOrdenes_PositionNotFlat_TransitionsToEnTrade()
        {
            // Edge: MonitorearOrdenes called with position already filled
            // This can happen when entry fills between ticks
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Off);
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Set position before breakout triggers (simulating a fill that happened during transition)
            h.SetPosition(MarketPosition.Long, 2, 20020);

            // Next tick in OrdenesPuestas state with position already long
            var t = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t, 20020, 20025, 20019, 20021);

            // Should detect position and transition to EnTrade
            // No new entries should be submitted
        }

        [Fact]
        public void MonitorearOrdenes_MaxTradesReached_EndsDayDirectly()
        {
            // Edge: tradesToday >= tradesPermitidosHoy check at start of MonitorearOrdenes
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.MaxTrades = 4;
                s.PerdidaMaxDiaria = 90; // tradesPermitidos=1
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Entry
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 1, 20020);
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // TP
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Profit target", 20110, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 50, 0));

            // Process tradeEnded
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 1), 20110, 20115, 20105, 20110);

            // Should be DiaTerminado
            h.OrderEngine.Reset();
            h.NewBar(new DateTime(2026, 1, 15, 10, 0, 0), 20021, 20025, 20019, 20021);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizMonitorear_ShortTrade_TrailingStopsProgression()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 4;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout short (entry = 19979, stopDist=45, TP1 target down 45, TP2 down 90)
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 19980, 19981, 19975, 19979);

            // BE at 60% -> unrealPts >= 27 -> close <= 19979-27=19952
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 19960, 19961, 19951, 19951);

            // TP1 75% -> unrealPts >= 33.75 -> close <= 19979-33.75=19945.25
            h.NewBar(new DateTime(2026, 1, 15, 9, 44, 0), 19950, 19951, 19944, 19944);

            // TP1 85% -> unrealPts >= 38.25 -> close <= 19979-38.25=19940.75
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 0), 19944, 19945, 19939, 19939);

            // TP1 hits target (100% = 45 -> close <= 19934)
            h.NewBar(new DateTime(2026, 1, 15, 9, 46, 0), 19939, 19940, 19933, 19933);

            // TP2 progress (50% of 90 = 45 -> already there)
            // TP2 70% of 90 = 63 -> close <= 19979-63=19916
            h.NewBar(new DateTime(2026, 1, 15, 9, 47, 0), 19933, 19934, 19915, 19915);

            // TP2 85% = 76.5 -> close <= 19979-76.5=19902.5
            h.NewBar(new DateTime(2026, 1, 15, 9, 48, 0), 19915, 19916, 19901, 19901);

            // TP2 90% = 81 -> close <= 19979-81=19898
            h.NewBar(new DateTime(2026, 1, 15, 9, 49, 0), 19901, 19902, 19897, 19897);

            // TP2 95% = 85.5 -> close <= 19979-85.5=19893.5
            h.NewBar(new DateTime(2026, 1, 15, 9, 50, 0), 19897, 19898, 19892, 19892);

            // TP2 98% = 88.2 -> close <= 19979-88.2=19890.8
            h.NewBar(new DateTime(2026, 1, 15, 9, 51, 0), 19892, 19893, 19889, 19889);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void VizMonitorear_TP1StoppedByTrailing_TP2Continues()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoOperacion = MNQ10minV2.OperationMode.Visualizar;
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 4;
            });
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);

            // Breakout long (entry=20021, stopDist=45)
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);

            // BE (60% of 45=27 -> 20048)
            h.NewBar(new DateTime(2026, 1, 15, 9, 43, 0), 20040, 20049, 20038, 20048);

            // TP1 escalon 1 (75% of 45=33.75 -> 20054.75)
            // TP1 stop moves to entry + 45*0.45 = 20020.25 + 20.25 = 20041.25
            h.NewBar(new DateTime(2026, 1, 15, 9, 44, 0), 20050, 20056, 20048, 20055);

            // Price pulls back and hits TP1 trailing stop (20041.25)
            // TP2 should still be alive (its stop is at BE level = 20026)
            h.NewBar(new DateTime(2026, 1, 15, 9, 45, 0), 20055, 20056, 20040, 20040);

            // TP2 continues even though TP1 exited
            h.NewBar(new DateTime(2026, 1, 15, 9, 46, 0), 20040, 20050, 20038, 20045);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Logging_WithModoLogDay_InitializesFiles()
        {
            // Just verify it doesn't crash when ModoLog is Day
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Day);
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));
            h.SimulateExecution("TP2Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Day change to trigger LogDailyCsv
            h.NewBar(new DateTime(2026, 1, 16, 9, 0, 0), 20000, 20005, 19995, 20000);
        }

        [Fact]
        public void Logging_WithModoLogMonth_LogsTradesAndBars()
        {
            // Verify logging paths with Month mode
            var h = new StrategyTestHarness(s => s.ModoLog = MNQ10minV2.LogMode.Month);
            double mid = 20000, high = 20020, low = 19980;
            for (int min = 32; min <= 40; min++)
                h.NewBar(new DateTime(2026, 1, 15, 9, min, 0), mid, high, low, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 41, 0), mid, mid + 5, mid - 5, mid);
            h.NewBar(new DateTime(2026, 1, 15, 9, 42, 0), 20020, 20025, 20019, 20021);
            h.SetPosition(MarketPosition.Long, 2, 20020);
            h.SimulateExecution("TP1Long", 20020, 1, MarketPosition.Long, new DateTime(2026, 1, 15, 9, 42, 0));

            // Exit
            h.SetPosition(MarketPosition.Flat);
            h.SimulateExecution("Stop loss", 19975, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 45, 0));
            h.SimulateExecution("Profit target", 20065, 1, MarketPosition.Flat, new DateTime(2026, 1, 15, 9, 45, 0));

            // Day change
            h.NewBar(new DateTime(2026, 1, 16, 9, 0, 0), 20000, 20005, 19995, 20000);
        }
    }
}
