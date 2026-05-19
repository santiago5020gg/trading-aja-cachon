using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using Xunit;

namespace CSimulator.Tests
{
    public class OnStateChangeTests
    {
        [Fact]
        public void SetDefaults_SetsAllDefaultValues()
        {
            var h = new StrategyTestHarness();
            var s = h.Strategy;

            Assert.Equal("MNQ10minV2", s.Name);
            Assert.Equal(5, s.ColchonStop);
            Assert.Equal(5, s.ColchonBreakeven);
            Assert.Equal(60, s.BreakevenPct);
            Assert.Equal(2, s.MaxTrades);
            Assert.Equal(400.0, s.PerdidaMaxDiaria);
            Assert.Equal(MNQ10minV2.TPMode.Con1a2, s.ModoTP);
            Assert.Equal(MNQ10minV2.OperationMode.Operar, s.ModoOperacion);
            Assert.Equal("15:50", s.HoraCierre);
            Assert.Equal(MNQ10minV2.LogMode.Month, s.ModoLog);
        }

        [Fact]
        public void SetDefaults_TrailingTP1Defaults()
        {
            var h = new StrategyTestHarness();
            var s = h.Strategy;

            Assert.Equal(4, s.CantTrailTP1);
            Assert.Equal(75, s.TP1Act1); Assert.Equal(45, s.TP1Stp1);
            Assert.Equal(85, s.TP1Act2); Assert.Equal(60, s.TP1Stp2);
            Assert.Equal(95, s.TP1Act3); Assert.Equal(80, s.TP1Stp3);
            Assert.Equal(99, s.TP1Act4); Assert.Equal(95, s.TP1Stp4);
        }

        [Fact]
        public void SetDefaults_TrailingTP2Defaults()
        {
            var h = new StrategyTestHarness();
            var s = h.Strategy;

            Assert.Equal(6, s.CantTrailTP2);
            Assert.Equal(50, s.TP2Act1); Assert.Equal(18, s.TP2Stp1);
            Assert.Equal(70, s.TP2Act2); Assert.Equal(50, s.TP2Stp2);
            Assert.Equal(85, s.TP2Act3); Assert.Equal(60, s.TP2Stp3);
            Assert.Equal(90, s.TP2Act4); Assert.Equal(70, s.TP2Stp4);
            Assert.Equal(95, s.TP2Act5); Assert.Equal(84, s.TP2Stp5);
            Assert.Equal(98, s.TP2Act6); Assert.Equal(94, s.TP2Stp6);
        }

        [Fact]
        public void SetDefaults_CalculateOnEachTick()
        {
            var h = new StrategyTestHarness();
            Assert.Equal(Calculate.OnEachTick, h.Strategy.Calculate);
        }

        [Fact]
        public void SetDefaults_BarsRequiredToTrade20()
        {
            var h = new StrategyTestHarness();
            Assert.Equal(20, h.Strategy.BarsRequiredToTrade);
        }

        [Fact]
        public void Configure_SetsMaxStopsPurosFromMaxTrades()
        {
            // MaxTrades=2 -> maxStopsPuros=1 (round(2/2))
            // This is tested indirectly through the strategy behavior
            var h = new StrategyTestHarness();
            // We can verify by having 1 stop loss end the day
            // (tested in ProcesarFinTradeTests)
        }
    }
}
