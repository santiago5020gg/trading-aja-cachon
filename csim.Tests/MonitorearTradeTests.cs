using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class MonitorearTradeTests
    {
        [Fact]
        public void Breakeven_TriggersAtBreakevenPct()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);

            // BreakevenPct=60%, so need unrealPts >= 45*0.6 = 27
            // Price needs to be entry + 27 = 20047
            h.OrderEngine.Reset();
            h.SetClose(20047);
            var t = new DateTime(2026, 1, 15, 9, 45, 0);
            h.Tick(t, 20047);

            // Should set stop at entry + ColchonBreakeven = 20020 + 5 = 20025
            Assert.Contains(h.OrderEngine.Stops, kvp => Math.Abs(kvp.Value - 20025) < 0.01);
        }

        [Fact]
        public void Breakeven_ShortTrade()
        {
            var h = SetupInTradeShort(entryPrice: 19980, stopDist: 45);

            // Need unrealPts >= 45*0.6 = 27 (entry - close >= 27 -> close <= 19953)
            h.OrderEngine.Reset();
            h.SetClose(19953);
            var t = new DateTime(2026, 1, 15, 9, 45, 0);
            h.Tick(t, 19953);

            // Stop at entry - ColchonBreakeven = 19980 - 5 = 19975
            Assert.Contains(h.OrderEngine.Stops, kvp => Math.Abs(kvp.Value - 19975) < 0.01);
        }

        [Fact]
        public void TrailingTP1_Escalon1_ActivatesAt75Pct()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);

            // TP1Act1=75%, need unrealPts >= 45*0.75 = 33.75 -> price >= 20053.75
            h.OrderEngine.Reset();
            h.SetClose(20054);
            var t = new DateTime(2026, 1, 15, 9, 46, 0);
            h.Tick(t, 20054);

            // TP1Stp1=45%, stop at entry + 45*0.45 = entry + 20.25 = 20040.25
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP1") && Math.Abs(kvp.Value - 20040.25) < 0.01);
        }

        [Fact]
        public void TrailingTP1_Escalon2_ActivatesAt85Pct()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP1Escalon1(h, 20020, 45);

            // TP1Act2=85%, need unrealPts >= 45*0.85 = 38.25 -> price >= 20058.25
            h.OrderEngine.Reset();
            h.SetClose(20059);
            var t = new DateTime(2026, 1, 15, 9, 47, 0);
            h.Tick(t, 20059);

            // TP1Stp2=60%, stop at entry + 45*0.60 = entry + 27 = 20047
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP1") && Math.Abs(kvp.Value - 20047) < 0.01);
        }

        [Fact]
        public void TrailingTP1_Escalon3_ActivatesAt95Pct()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP1Escalon1(h, 20020, 45);
            TriggerTP1Escalon2(h, 20020, 45);

            // TP1Act3=95%, need unrealPts >= 45*0.95 = 42.75 -> price >= 20062.75
            h.OrderEngine.Reset();
            h.SetClose(20063);
            var t = new DateTime(2026, 1, 15, 9, 48, 0);
            h.Tick(t, 20063);

            // TP1Stp3=80%, stop at entry + 45*0.80 = entry + 36 = 20056
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP1") && Math.Abs(kvp.Value - 20056) < 0.01);
        }

        [Fact]
        public void TrailingTP1_Escalon4_ActivatesAt99Pct()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP1Escalon1(h, 20020, 45);
            TriggerTP1Escalon2(h, 20020, 45);
            TriggerTP1Escalon3(h, 20020, 45);

            // TP1Act4=99%, need unrealPts >= 45*0.99 = 44.55 -> price >= 20064.55
            h.OrderEngine.Reset();
            h.SetClose(20065);
            var t = new DateTime(2026, 1, 15, 9, 49, 0);
            h.Tick(t, 20065);

            // TP1Stp4=95%, stop at entry + 45*0.95 = entry + 42.75 = 20062.75
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP1") && Math.Abs(kvp.Value - 20062.75) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon1_ActivatesAt50Pct()
        {
            // Use qtyTP1=0 scenario so TP1 doesn't interfere with TP2 evaluation
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);

            // TP2 target = stopDist*2 = 90
            // TP2Act1=50%, need unrealPts >= 90*0.50 = 45 -> price >= 20065
            h.OrderEngine.Reset();
            h.SetClose(20065);
            var t = new DateTime(2026, 1, 15, 9, 46, 0);
            h.Tick(t, 20065);

            // TP2Stp1=18%, stop at entry + 90*0.18 = entry + 16.2 = 20036.2
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20036.2) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon2_ActivatesAt70Pct()
        {
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP2Escalon1(h, 20020, 45);

            // TP2Act2=70%, need unrealPts >= 90*0.70 = 63 -> price >= 20083
            h.OrderEngine.Reset();
            h.SetClose(20083);
            var t = new DateTime(2026, 1, 15, 9, 47, 0);
            h.Tick(t, 20083);

            // TP2Stp2=50%, stop at entry + 90*0.50 = entry + 45 = 20065
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20065) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon3_ActivatesAt85Pct()
        {
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP2Escalon1(h, 20020, 45);
            TriggerTP2Escalon2(h, 20020, 45);

            // TP2Act3=85%, need unrealPts >= 90*0.85 = 76.5 -> price >= 20096.5
            h.OrderEngine.Reset();
            h.SetClose(20097);
            var t = new DateTime(2026, 1, 15, 9, 48, 0);
            h.Tick(t, 20097);

            // TP2Stp3=60%, stop at entry + 90*0.60 = entry + 54 = 20074
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20074) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon4_ActivatesAt90Pct()
        {
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP2Escalon1(h, 20020, 45);
            TriggerTP2Escalon2(h, 20020, 45);
            TriggerTP2Escalon3(h, 20020, 45);

            // TP2Act4=90%, need unrealPts >= 90*0.90 = 81 -> price >= 20101
            h.OrderEngine.Reset();
            h.SetClose(20101);
            var t = new DateTime(2026, 1, 15, 9, 49, 0);
            h.Tick(t, 20101);

            // TP2Stp4=70%, stop at entry + 90*0.70 = entry + 63 = 20083
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20083) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon5_ActivatesAt95Pct()
        {
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP2Escalon1(h, 20020, 45);
            TriggerTP2Escalon2(h, 20020, 45);
            TriggerTP2Escalon3(h, 20020, 45);
            TriggerTP2Escalon4(h, 20020, 45);

            // TP2Act5=95%, need unrealPts >= 90*0.95 = 85.5 -> price >= 20105.5
            h.OrderEngine.Reset();
            h.SetClose(20106);
            var t = new DateTime(2026, 1, 15, 9, 50, 0);
            h.Tick(t, 20106);

            // TP2Stp5=84%, stop at entry + 90*0.84 = entry + 75.6 = 20095.6
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20095.6) < 0.01);
        }

        [Fact]
        public void TrailingTP2_Escalon6_ActivatesAt98Pct()
        {
            var h = SetupInTradeLongTP2Only(entryPrice: 20020, stopDist: 45);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP2Escalon1(h, 20020, 45);
            TriggerTP2Escalon2(h, 20020, 45);
            TriggerTP2Escalon3(h, 20020, 45);
            TriggerTP2Escalon4(h, 20020, 45);
            TriggerTP2Escalon5(h, 20020, 45);

            // TP2Act6=98%, need unrealPts >= 90*0.98 = 88.2 -> price >= 20108.2
            h.OrderEngine.Reset();
            h.SetClose(20109);
            var t = new DateTime(2026, 1, 15, 9, 51, 0);
            h.Tick(t, 20109);

            // TP2Stp6=94%, stop at entry + 90*0.94 = entry + 84.6 = 20104.6
            Assert.Contains(h.OrderEngine.Stops, kvp =>
                kvp.Key.Contains("TP2") && Math.Abs(kvp.Value - 20104.6) < 0.01);
        }

        [Fact]
        public void FlatPosition_TriggersTradeEnded()
        {
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45);
            h.SetPosition(MarketPosition.Flat);

            var t = new DateTime(2026, 1, 15, 9, 45, 0);
            h.Tick(t, 20000);

            // After going flat, next bar should process tradeEnded
            // (No assertion on internal state, but no crash)
        }

        [Fact]
        public void CantTrailTP1_RespectsLimit()
        {
            // Set CantTrailTP1=2 -> only 2 escalones
            var h = SetupInTradeLong(entryPrice: 20020, stopDist: 45, configure: s => s.CantTrailTP1 = 2);
            TriggerBreakeven(h, 20020, 45);
            TriggerTP1Escalon1(h, 20020, 45);
            TriggerTP1Escalon2(h, 20020, 45);

            // Try to reach escalon 3 (95%) - should NOT activate since CantTrailTP1=2
            h.OrderEngine.Reset();
            h.SetClose(20063); // 95% of 45
            var t = new DateTime(2026, 1, 15, 9, 48, 0);
            h.Tick(t, 20063);

            // Should still be at escalon 2 stop level (60% = 20047), not escalon 3
            bool hasEsc3Stop = false;
            foreach (var kvp in h.OrderEngine.Stops)
            {
                if (kvp.Key.Contains("TP1") && Math.Abs(kvp.Value - 20056) < 0.01) // 80% = esc3
                    hasEsc3Stop = true;
            }
            Assert.False(hasEsc3Stop);
        }

        #region Helpers

        private StrategyTestHarness SetupInTradeLongTP2Only(double entryPrice, double stopDist)
        {
            // Uses PerdidaMaxDiaria that gives 1 contract -> qtyTP1=0, qtyTP2=1
            double rangoPuntos = stopDist - 5; // stopDist = rangoPuntos + ColchonStop
            double rangoHigh = entryPrice;
            double rangoLow = entryPrice - rangoPuntos;
            double mid = (rangoHigh + rangoLow) / 2.0;
            // riesgo1Micro = stopDist * 2 = 90 (PointValue=2 for MNQ)
            // Need presupuestoIdeal < riesgo BUT riesgo <= PerdidaMaxDiaria
            // With PerdidaMaxDiaria=150, MaxTrades=2: presup=75 < 90, but 90<=150 -> contratos=1, qtyTP1=0, qtyTP2=1

            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 150;
                s.MaxTrades = 2;
            });
            BuildRango(h, mid, rangoHigh, rangoLow);

            var t1 = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t1, mid, mid + 5, mid - 5, mid);

            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, entryPrice, entryPrice + 5, entryPrice - 1, entryPrice + 1);

            h.SetPosition(MarketPosition.Long, 1, entryPrice);
            h.SimulateExecution("TP2Long", entryPrice, 1, MarketPosition.Long, t2);

            return h;
        }

        private StrategyTestHarness SetupInTradeLong(double entryPrice, double stopDist, Action<MNQ10minV2> configure = null)
        {
            double rangoHigh = entryPrice;
            double rangoLow = entryPrice - (stopDist - 5); // rangoPuntos = stopDist - ColchonStop
            double mid = (rangoHigh + rangoLow) / 2.0;

            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                configure?.Invoke(s);
            });
            BuildRango(h, mid, rangoHigh, rangoLow);

            // Transition to OrdenesPuestas
            var t1 = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t1, mid, mid + 5, mid - 5, mid);

            // Trigger entry long
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, entryPrice, entryPrice + 5, entryPrice - 1, entryPrice + 1);

            // Simulate fill
            h.SetPosition(MarketPosition.Long, 2, entryPrice);
            h.SimulateExecution("TP1Long", entryPrice, 1, MarketPosition.Long, t2);
            h.SimulateExecution("TP2Long", entryPrice, 1, MarketPosition.Long, t2);

            return h;
        }

        private StrategyTestHarness SetupInTradeShort(double entryPrice, double stopDist, Action<MNQ10minV2> configure = null)
        {
            double rangoLow = entryPrice;
            double rangoHigh = entryPrice + (stopDist - 5);
            double mid = (rangoHigh + rangoLow) / 2.0;

            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 1000;
                configure?.Invoke(s);
            });
            BuildRango(h, mid, rangoHigh, rangoLow);

            var t1 = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t1, mid, mid + 5, mid - 5, mid);

            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, entryPrice, entryPrice + 1, entryPrice - 5, entryPrice - 1);

            h.SetPosition(MarketPosition.Short, 2, entryPrice);
            h.SimulateExecution("TP1Short", entryPrice, 1, MarketPosition.Short, t2);
            h.SimulateExecution("TP2Short", entryPrice, 1, MarketPosition.Short, t2);

            return h;
        }

        private void TriggerBreakeven(StrategyTestHarness h, double entry, double stopDist)
        {
            // 60% of stopDist
            double bePrice = entry + (stopDist * 0.60);
            h.OrderEngine.Reset();
            h.SetClose(bePrice);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 0), bePrice);
        }

        private void TriggerTP1Escalon1(StrategyTestHarness h, double entry, double stopDist)
        {
            double price = entry + (stopDist * 0.75) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 10), price);
        }

        private void TriggerTP1Escalon2(StrategyTestHarness h, double entry, double stopDist)
        {
            double price = entry + (stopDist * 0.85) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 20), price);
        }

        private void TriggerTP1Escalon3(StrategyTestHarness h, double entry, double stopDist)
        {
            double price = entry + (stopDist * 0.95) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 30), price);
        }

        private void TriggerTP2Escalon1(StrategyTestHarness h, double entry, double stopDist)
        {
            double tp2Target = stopDist * 2;
            double price = entry + (tp2Target * 0.50) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 40), price);
        }

        private void TriggerTP2Escalon2(StrategyTestHarness h, double entry, double stopDist)
        {
            double tp2Target = stopDist * 2;
            double price = entry + (tp2Target * 0.70) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 44, 50), price);
        }

        private void TriggerTP2Escalon3(StrategyTestHarness h, double entry, double stopDist)
        {
            double tp2Target = stopDist * 2;
            double price = entry + (tp2Target * 0.85) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 45, 0), price);
        }

        private void TriggerTP2Escalon4(StrategyTestHarness h, double entry, double stopDist)
        {
            double tp2Target = stopDist * 2;
            double price = entry + (tp2Target * 0.90) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 45, 10), price);
        }

        private void TriggerTP2Escalon5(StrategyTestHarness h, double entry, double stopDist)
        {
            double tp2Target = stopDist * 2;
            double price = entry + (tp2Target * 0.95) + 0.5;
            h.OrderEngine.Reset();
            h.SetClose(price);
            h.Tick(new DateTime(2026, 1, 15, 9, 45, 20), price);
        }

        private void BuildRango(StrategyTestHarness h, double mid, double high, double low)
        {
            for (int min = 32; min <= 40; min++)
            {
                var t = new DateTime(2026, 1, 15, 9, min, 0);
                h.NewBar(t, mid, high, low, mid);
            }
        }

        #endregion
    }
}
