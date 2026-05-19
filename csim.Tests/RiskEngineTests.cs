using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class RiskEngineTests
    {
        [Fact]
        public void SmallRange_CalculatesMultipleContracts()
        {
            // rango=10, ColchonStop=5, riesgo1Micro=(10+5)*2=30
            // PerdidaMaxDiaria=400, MaxTrades=2
            // presupuestoIdeal=200, 200>=30 -> contratos=floor(200/30)=6, trades=2
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 2;
            });
            BuildRango(h, 20000, 20005, 19995); // range=10

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20003, 19997, 20000);

            // Trigger entry
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20005, 20008, 20004, 20006);

            // Should have entries with qty > 1
            int totalQty = 0;
            foreach (var e in h.OrderEngine.Entries)
                totalQty += e.Quantity;
            Assert.True(totalQty > 1);
        }

        [Fact]
        public void LargeRange_LimitsTo1Contract_ReducesTrades()
        {
            // rango=150, ColchonStop=5, riesgo1Micro=(150+5)*2=310
            // PerdidaMaxDiaria=400, MaxTrades=2
            // presupuestoIdeal=200, 200<310 BUT 310<=400
            // -> contratos=1, tradesPermitidos=floor(400/310)=1
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 2;
            });
            BuildRango(h, 20000, 20075, 19925); // range=150

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20075, 20080, 20074, 20076);

            // Should enter with qty=1 total (since Con1a2 with 1 contract -> qtyTP1=0, qtyTP2=1)
            int totalQty = 0;
            foreach (var e in h.OrderEngine.Entries)
                totalQty += e.Quantity;
            Assert.Equal(1, totalQty);
        }

        [Fact]
        public void ExcessiveRange_EndsDayImmediately()
        {
            // rango=250, riesgo1Micro=(250+5)*2=510 > PerdidaMaxDiaria=400
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.PerdidaMaxDiaria = 400;
            });
            BuildRango(h, 20000, 20125, 19875); // range=250

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20005, 19995, 20000);

            // No entries possible
            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20125, 20130, 20124, 20126);
            Assert.Empty(h.OrderEngine.Entries);
        }

        [Fact]
        public void Solo1a1_WithMultipleContracts_AllGoToTP1()
        {
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.ModoTP = MNQ10minV2.TPMode.Solo1a1;
                s.PerdidaMaxDiaria = 600;
                s.MaxTrades = 2;
            });
            BuildRango(h, 20000, 20010, 19990); // range=20, riesgo=(20+5)*2=50, presup=300, contratos=6

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20003, 19997, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20010, 20013, 20009, 20011);

            Assert.All(h.OrderEngine.Entries, e => Assert.Contains("TP1", e.SignalName));
            Assert.DoesNotContain(h.OrderEngine.Entries, e => e.SignalName.Contains("TP2"));
        }

        [Fact]
        public void Con1a2_With3Contracts_SplitsCorrectly()
        {
            // 3 contratos -> qtyTP1=2, qtyTP2=1
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.ModoTP = MNQ10minV2.TPMode.Con1a2;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 2;
            });
            // Need presupuestoIdeal/riesgo >= 3
            // rango=20 -> riesgo=50, presupuesto=200, contratos=floor(200/50)=4 (not 3)
            // rango=30 -> riesgo=70, presupuesto=200, contratos=floor(200/70)=2
            // rango=25 -> riesgo=60, presupuesto=200, contratos=floor(200/60)=3
            BuildRango(h, 20000, 20012.5, 19987.5); // range=25

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20003, 19997, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20013, 20015, 20012, 20013);

            // Should have TP1Long qty=2 and TP2Long qty=1
            var tp1 = h.OrderEngine.Entries.Find(e => e.SignalName == "TP1Long");
            var tp2 = h.OrderEngine.Entries.Find(e => e.SignalName == "TP2Long");
            Assert.NotNull(tp1);
            Assert.NotNull(tp2);
            Assert.Equal(2, tp1.Quantity);
            Assert.Equal(1, tp2.Quantity);
        }

        [Fact]
        public void Con1a2_With2Contracts_Splits1and1()
        {
            // 2 contratos -> qtyTP1=1, qtyTP2=1
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.ModoTP = MNQ10minV2.TPMode.Con1a2;
                s.PerdidaMaxDiaria = 400;
                s.MaxTrades = 2;
            });
            // rango=40 -> riesgo=(40+5)*2=90, presupuesto=200, contratos=floor(200/90)=2
            BuildRango(h, 20000, 20020, 19980); // range=40

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20003, 19997, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20023, 20019, 20021);

            var tp1 = h.OrderEngine.Entries.Find(e => e.SignalName == "TP1Long");
            var tp2 = h.OrderEngine.Entries.Find(e => e.SignalName == "TP2Long");
            Assert.NotNull(tp1);
            Assert.NotNull(tp2);
            Assert.Equal(1, tp1.Quantity);
            Assert.Equal(1, tp2.Quantity);
        }

        [Fact]
        public void Con1a2_With1Contract_AllGoesToTP2()
        {
            // 1 contrato -> qtyTP1=0, qtyTP2=1
            var h = new StrategyTestHarness(s =>
            {
                s.ModoLog = MNQ10minV2.LogMode.Off;
                s.ModoTP = MNQ10minV2.TPMode.Con1a2;
                s.PerdidaMaxDiaria = 200;
                s.MaxTrades = 2;
            });
            // rango=40 -> riesgo=90, presupuesto=100, 100<90
            // 90<=200 -> contratos=1, tradesPermitidos=floor(200/90)=2
            BuildRango(h, 20000, 20020, 19980); // range=40

            var t = new DateTime(2026, 1, 15, 9, 41, 0);
            h.NewBar(t, 20000, 20003, 19997, 20000);

            h.OrderEngine.Reset();
            var t2 = new DateTime(2026, 1, 15, 9, 42, 0);
            h.NewBar(t2, 20020, 20023, 20019, 20021);

            Assert.DoesNotContain(h.OrderEngine.Entries, e => e.SignalName.Contains("TP1"));
            Assert.Contains(h.OrderEngine.Entries, e => e.SignalName == "TP2Long");
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
