#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MNQProbabilisticBot : Strategy
    {
        #region Parameters

        // --- General ---
        [NinjaScriptProperty]
        [Display(Name = "Bar Period (minutes)", GroupName = "1. General", Order = 1)]
        public int BarPeriod { get; set; }

        // --- Schedule ---
        [NinjaScriptProperty]
        [Display(Name = "Start Time (NY)", GroupName = "2. Schedule", Order = 1)]
        public int StartTimeHour { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Start Time Minutes", GroupName = "2. Schedule", Order = 2)]
        public int StartTimeMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Last Trade Hour", GroupName = "2. Schedule", Order = 3)]
        public int LastTradeHour { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Last Trade Minutes", GroupName = "2. Schedule", Order = 4)]
        public int LastTradeMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Flatten Hour", GroupName = "2. Schedule", Order = 5)]
        public int FlattenHour { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Flatten Minutes", GroupName = "2. Schedule", Order = 6)]
        public int FlattenMinute { get; set; }

        // --- Risk Management ---
        [NinjaScriptProperty]
        [Display(Name = "Max Daily Loss ($)", GroupName = "3. Risk Management", Order = 1)]
        public double MaxDailyLoss { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Profit Target ($)", GroupName = "3. Risk Management", Order = 2)]
        public double DailyProfitTarget { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades Per Day", GroupName = "3. Risk Management", Order = 3)]
        public int MaxTradesPerDay { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade Fraction", GroupName = "3. Risk Management", Order = 4)]
        public double RiskPerTradeFraction { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Kelly Fraction", GroupName = "3. Risk Management", Order = 5)]
        public double KellyFraction { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reward:Risk Ratio", GroupName = "3. Risk Management", Order = 6)]
        public double RewardRiskRatio { get; set; }

        // --- Indicators ---
        [NinjaScriptProperty]
        [Display(Name = "SMA Fast Period", GroupName = "4. Indicators", Order = 1)]
        public int SmaPeriodFast { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SMA Slow Period", GroupName = "4. Indicators", Order = 2)]
        public int SmaPeriodSlow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Period", GroupName = "4. Indicators", Order = 3)]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RSI Period", GroupName = "4. Indicators", Order = 4)]
        public int RsiPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "StdDev Period", GroupName = "4. Indicators", Order = 5)]
        public int StdDevPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume SMA Period", GroupName = "4. Indicators", Order = 6)]
        public int VolumeSMAPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Candle Lookback", GroupName = "4. Indicators", Order = 7)]
        public int CandleLookback { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Slope Lookback", GroupName = "4. Indicators", Order = 8)]
        public int SlopeLookback { get; set; }

        // --- ATR and Stops ---
        [NinjaScriptProperty]
        [Display(Name = "ATR Multiplier (Trend)", GroupName = "5. ATR and Stops", Order = 1)]
        public double AtrMultiplierTrend { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Multiplier (Counter)", GroupName = "5. ATR and Stops", Order = 2)]
        public double AtrMultiplierCounter { get; set; }

        // --- Entry Score ---
        [NinjaScriptProperty]
        [Display(Name = "Min Score Trend", GroupName = "6. Entry Score", Order = 1)]
        public int MinScoreTrend { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Score Counter", GroupName = "6. Entry Score", Order = 2)]
        public int MinScoreCounter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight Z-Score", GroupName = "6. Entry Score", Order = 3)]
        public double WeightZscore { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight RSI", GroupName = "6. Entry Score", Order = 4)]
        public double WeightRsi { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight Slope", GroupName = "6. Entry Score", Order = 5)]
        public double WeightSlope { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight ATR", GroupName = "6. Entry Score", Order = 6)]
        public double WeightAtr { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight Volume", GroupName = "6. Entry Score", Order = 7)]
        public double WeightVolume { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight Pullback", GroupName = "6. Entry Score", Order = 8)]
        public double WeightPullback { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Weight Candle", GroupName = "6. Entry Score", Order = 9)]
        public double WeightCandle { get; set; }

        // --- Regime Detection ---
        [NinjaScriptProperty]
        [Display(Name = "Spread Threshold Low (ATR)", GroupName = "7. Regime", Order = 1)]
        public double SpreadThresholdLow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Spread Threshold High (ATR)", GroupName = "7. Regime", Order = 2)]
        public double SpreadThresholdHigh { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Slope Threshold Low", GroupName = "7. Regime", Order = 3)]
        public double SlopeThresholdLow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Slope Threshold High", GroupName = "7. Regime", Order = 4)]
        public double SlopeThresholdHigh { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Range Max Slope SMA20", GroupName = "7. Regime", Order = 5)]
        public double RangeMaxSlope20 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Range Max Slope SMA200", GroupName = "7. Regime", Order = 6)]
        public double RangeMaxSlope200 { get; set; }

        // --- Position Sizing ---
        [NinjaScriptProperty]
        [Display(Name = "Counter-Trend Size %", GroupName = "8. Position Sizing", Order = 1)]
        public double CounterTrendSizePct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Moderate Score Size %", GroupName = "8. Position Sizing", Order = 2)]
        public double ModerateScoreSizePct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Anti-Martingale", GroupName = "8. Position Sizing", Order = 3)]
        public double MaxAntiMartingale { get; set; }

        // --- Statistics ---
        [NinjaScriptProperty]
        [Display(Name = "Initial Win Rate", GroupName = "9. Statistics", Order = 1)]
        public double InitialWinRate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Initial R:R", GroupName = "9. Statistics", Order = 2)]
        public double InitialRR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Rolling Window Size", GroupName = "9. Statistics", Order = 3)]
        public int RollingWindowSize { get; set; }

        // --- Visual ---
        [NinjaScriptProperty]
        [Display(Name = "Show Panel", GroupName = "10. Visual", Order = 1)]
        public bool ShowPanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Pullback Zone", GroupName = "10. Visual", Order = 2)]
        public bool ShowPullbackZone { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Counter Zone", GroupName = "10. Visual", Order = 3)]
        public bool ShowCounterZone { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Entry Markers", GroupName = "10. Visual", Order = 4)]
        public bool ShowEntryMarkers { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Stop/TP Lines", GroupName = "10. Visual", Order = 5)]
        public bool ShowStopTPLines { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Sound Alerts", GroupName = "10. Visual", Order = 6)]
        public bool EnableSoundAlerts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SMA Fast Color", GroupName = "10. Visual", Order = 7)]
        public System.Windows.Media.Brush SmaFastBrush { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SMA Slow Color", GroupName = "10. Visual", Order = 8)]
        public System.Windows.Media.Brush SmaSlowBrush { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Panel Opacity %", GroupName = "10. Visual", Order = 9)]
        public int PanelOpacity { get; set; }

        #endregion

        #region Variables and State

        private enum MarketRegime
        {
            Bullish,
            Bearish,
            CounterTrendBullish,
            CounterTrendBearish,
            Range
        }

        private SMA smaFast;
        private SMA smaSlow;
        private ATR atr;
        private RSI rsi;
        private StdDev stdDev;
        private SMA volumeSma;

        private MarketRegime currentRegime;
        private double currentScore;
        private int currentContracts;
        private double currentStopDistance;
        private double currentTargetDistance;

        private double dailyPnL;
        private int tradesToday;
        private bool dayDone;
        private double entryPrice;
        private double stopPrice;
        private double targetPrice;

        private double rollingWinRate;
        private double rollingRR;
        private List<double> tradeResults;
        private List<double> tradeWins;

        private string statsFilePath;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ Probabilistic Bot — 7-factor entry scoring with dynamic position sizing";
                Name = "MNQProbabilisticBot";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 2;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 200;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                BarPeriod = 2;
                StartTimeHour = 9; StartTimeMinute = 40;
                LastTradeHour = 15; LastTradeMinute = 25;
                FlattenHour = 15; FlattenMinute = 40;

                MaxDailyLoss = 270;
                DailyProfitTarget = 270;
                MaxTradesPerDay = 5;
                RiskPerTradeFraction = 0.33;
                KellyFraction = 0.50;
                RewardRiskRatio = 1.5;

                SmaPeriodFast = 20;
                SmaPeriodSlow = 200;
                AtrPeriod = 14;
                RsiPeriod = 7;
                StdDevPeriod = 20;
                VolumeSMAPeriod = 20;
                CandleLookback = 10;
                SlopeLookback = 5;

                AtrMultiplierTrend = 1.5;
                AtrMultiplierCounter = 2.0;

                MinScoreTrend = 65;
                MinScoreCounter = 75;
                WeightZscore = 0.20;
                WeightRsi = 0.15;
                WeightSlope = 0.20;
                WeightAtr = 0.10;
                WeightVolume = 0.10;
                WeightPullback = 0.15;
                WeightCandle = 0.10;

                SpreadThresholdLow = 1.0;
                SpreadThresholdHigh = 4.0;
                SlopeThresholdLow = 0.05;
                SlopeThresholdHigh = 0.30;
                RangeMaxSlope20 = 0.05;
                RangeMaxSlope200 = 0.02;

                CounterTrendSizePct = 0.25;
                ModerateScoreSizePct = 0.75;
                MaxAntiMartingale = 1.25;

                InitialWinRate = 0.55;
                InitialRR = 1.5;
                RollingWindowSize = 50;

                ShowPanel = true;
                ShowPullbackZone = true;
                ShowCounterZone = true;
                ShowEntryMarkers = true;
                ShowStopTPLines = true;
                EnableSoundAlerts = false;
                SmaFastBrush = System.Windows.Media.Brushes.DodgerBlue;
                SmaSlowBrush = System.Windows.Media.Brushes.Red;
                PanelOpacity = 80;
            }
            else if (State == State.Configure)
            {
            }
            else if (State == State.DataLoaded)
            {
                smaFast = SMA(SmaPeriodFast);
                smaSlow = SMA(SmaPeriodSlow);
                atr = ATR(AtrPeriod);
                rsi = RSI(RsiPeriod, 3);
                stdDev = StdDev(StdDevPeriod);
                volumeSma = SMA(Volume, VolumeSMAPeriod);

                smaFast.Plots[0].Brush = SmaFastBrush;
                smaFast.Plots[0].Width = 2;
                smaSlow.Plots[0].Brush = SmaSlowBrush;
                smaSlow.Plots[0].Width = 2;

                AddChartIndicator(smaFast);
                AddChartIndicator(smaSlow);

                tradeResults = new List<double>();
                tradeWins = new List<double>();
                rollingWinRate = InitialWinRate;
                rollingRR = InitialRR;

                statsFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "MNQBot_Stats.csv");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;
        }

        #endregion

        #region Helper Methods

        private double Sigmoid(double x)
        {
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        private double GaussianKernel(double x)
        {
            return Math.Exp(-x * x / 2.0);
        }

        private double GaussianKernelCustom(double x, double center, double width)
        {
            double z = (x - center) / width;
            return Math.Exp(-0.5 * z * z);
        }

        private double ClampValue(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private double LinearInterp(double value, double inLow, double inHigh, double outLow, double outHigh)
        {
            if (value <= inLow) return outLow;
            if (value >= inHigh) return outHigh;
            return outLow + (outHigh - outLow) * (value - inLow) / (inHigh - inLow);
        }

        private double GetAvgRange(int lookback)
        {
            double sum = 0;
            for (int i = 0; i < lookback; i++)
                sum += High[i] - Low[i];
            return sum / lookback;
        }

        private double GetSmaSlope(SMA sma, int lookback)
        {
            if (CurrentBar < lookback) return 0;
            return (sma[0] - sma[lookback]) / lookback;
        }

        private double GetNormalizedSlope(SMA sma, int lookback)
        {
            if (atr[0] <= 0) return 0;
            return GetSmaSlope(sma, lookback) / atr[0];
        }

        private TimeSpan ToNYTime(DateTime barTime)
        {
            TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime nyTime = TimeZoneInfo.ConvertTime(barTime, eastern);
            return nyTime.TimeOfDay;
        }

        private bool IsInTradingWindow()
        {
            TimeSpan nyTime = ToNYTime(Time[0]);
            TimeSpan start = new TimeSpan(StartTimeHour, StartTimeMinute, 0);
            TimeSpan lastTrade = new TimeSpan(LastTradeHour, LastTradeMinute, 0);
            return nyTime >= start && nyTime <= lastTrade;
        }

        private bool IsFlattenTime()
        {
            TimeSpan nyTime = ToNYTime(Time[0]);
            TimeSpan flatten = new TimeSpan(FlattenHour, FlattenMinute, 0);
            return nyTime >= flatten;
        }

        private double GetCTime()
        {
            TimeSpan nyTime = ToNYTime(Time[0]);
            double hours = nyTime.TotalHours;

            if (hours < 10.0) return 0.75;
            if (hours < 11.5) return 1.00;
            if (hours < 13.5) return 0.80;
            if (hours < 15.0) return 1.00;
            return 0.60;
        }

        #endregion
    }
}
