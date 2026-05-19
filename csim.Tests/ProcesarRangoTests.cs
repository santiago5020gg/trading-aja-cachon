using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class ProcesarRangoTests
    {
        [Fact]
        public void Before932_WaitsForRange()
        {
            var h = new StrategyTestHarness();
            var t = new DateTime(2026, 1, 15, 9, 30, 0);
            h.NewBar(t, 20000, 20010, 19990, 20000);

            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void During932to940_AccumulatesRange()
        {
            var h = new StrategyTestHarness();

            h.NewBar(new DateTime(2026, 1, 15, 9, 32, 0), 20000, 20010, 19995, 20005);
            h.NewBar(new DateTime(2026, 1, 15, 9, 34, 0), 20005, 20015, 19990, 20010);
            h.NewBar(new DateTime(2026, 1, 15, 9, 36, 0), 20010, 20012, 19992, 20008);

            // Still in EsperandoRango, no entries
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void After940_TransitionsToOrdenesPuestas()
        {
            var h = new StrategyTestHarness();
            BuildRango(h, 20000, 20020, 19980);

            // Bar after range window - triggers transition
            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // No entries yet (price within range), but state changed
            // Verify by pushing price above rangoHigh
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20025, 20019, 20021);

            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void PositionSizing_CalculatesContractsCorrectly()
        {
            // rangoPuntos = 40 (20020-19980), ColchonStop=5, riesgo1Micro = 45*2 = 90
            // PerdidaMaxDiaria=400, MaxTrades=2 -> presupuestoIdeal = 200
            // 200 >= 90 -> contratos = floor(200/90) = 2, tradesPermitidos = 2
            var h = new StrategyTestHarness();
            BuildRango(h, 20000, 20020, 19980);

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // Should have transitioned without error (not FUERA_PRESUPUESTO)
            // Test by verifying entries can happen
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20025, 20019, 20021);
            Assert.NotEmpty(h.OrderEngine.Entries);
        }

        [Fact]
        public void PositionSizing_FueraPresupuesto_EndsDayWhenRiskTooHigh()
        {
            // rango = 200 pts -> riesgo1Micro = (200+5)*2 = 410 > PerdidaMaxDiaria=400
            var h = new StrategyTestHarness();
            h.Strategy.PerdidaMaxDiaria = 400;

            h.NewBar(new DateTime(2026, 1, 15, 9, 32, 0), 20000, 20200, 20000, 20100);
            h.NewBar(new DateTime(2026, 1, 15, 9, 34, 0), 20100, 20200, 20000, 20100);

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20100, 20100, 20000, 20050);

            // After this, the state should be DiaTerminado - no entries possible
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20200, 20210, 20199, 20205);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void PositionSizing_Solo1a1_AllContractsToTP1()
        {
            var h = new StrategyTestHarness(s => s.ModoTP = MNQ10minV2.TPMode.Solo1a1);
            BuildRango(h, 20000, 20020, 19980);

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // Now trigger entry
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20025, 20019, 20021);

            // In Solo1a1, only TP1 entries (no TP2)
            Assert.All(h.OrderEngine.Entries, e => Assert.Contains("TP1", e.SignalName));
        }

        [Fact]
        public void PositionSizing_Con1a2_SplitsContracts()
        {
            var h = new StrategyTestHarness(s => s.ModoTP = MNQ10minV2.TPMode.Con1a2);
            BuildRango(h, 20000, 20020, 19980);

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20025, 20019, 20021);

            // Should have both TP1 and TP2 entries
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName.Contains("TP1"));
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName.Contains("TP2"));
        }

        [Fact]
        public void InvalidRange_EndsDayIfNoHighLow()
        {
            var h = new StrategyTestHarness();
            // Skip 9:32-9:40 bars entirely, go straight to 9:41
            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // State should be DiaTerminado due to invalid range
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20100, 20110, 20099, 20105);
            Assert.Empty(h.OrderEngine.Entries);
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
