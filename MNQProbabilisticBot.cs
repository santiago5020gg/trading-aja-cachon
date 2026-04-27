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

        private string statsFilePath;
        private string lastTradeDirection;
        private double lastExecutionPrice;
        private TimeZoneInfo easternZone;

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

                rollingWinRate = InitialWinRate;
                rollingRR = InitialRR;

                statsFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "MNQBot_Stats.csv");

                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastTradeDirection = "";

                LoadHistoricalStats();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            if (Bars.IsFirstBarOfSession)
                ResetDaily();

            ManageExit();

            currentRegime = DetectRegime();
            bool isLong = IsLongRegime(currentRegime);
            currentScore = (currentRegime != MarketRegime.Range)
                ? CalculateEntryScore(isLong)
                : 0;

            EvaluateEntry();
            DrawChartElements();
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
            DateTime nyTime = TimeZoneInfo.ConvertTime(barTime, easternZone);
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

        #region Regime Detection

        private MarketRegime DetectRegime()
        {
            if (atr[0] <= 0) return MarketRegime.Range;

            double smaSpread = Math.Abs(smaFast[0] - smaSlow[0]) / atr[0];
            double slopeFast = GetNormalizedSlope(smaFast, SlopeLookback);
            double slopeSlow = GetNormalizedSlope(smaSlow, SlopeLookback);

            bool isRange = smaSpread < SpreadThresholdLow
                && Math.Abs(slopeFast) < RangeMaxSlope20
                && Math.Abs(slopeSlow) < RangeMaxSlope200;

            if (isRange)
                return MarketRegime.Range;

            double zScoreVsSma200 = stdDev[0] > 0
                ? (Close[0] - smaSlow[0]) / stdDev[0]
                : 0;

            double distToSma200 = Math.Abs(Close[0] - smaSlow[0]) / atr[0];

            if (smaFast[0] < smaSlow[0] && smaSpread >= SpreadThresholdLow && slopeFast < 0)
            {
                if (distToSma200 < 1.5 && rsi[0] < 30 && zScoreVsSma200 < -2.0)
                    return MarketRegime.CounterTrendBullish;
                return MarketRegime.Bearish;
            }

            if (smaFast[0] > smaSlow[0] && smaSpread >= SpreadThresholdLow && slopeFast > 0)
            {
                if (distToSma200 < 1.5 && rsi[0] > 70 && zScoreVsSma200 > 2.0)
                    return MarketRegime.CounterTrendBearish;
                return MarketRegime.Bullish;
            }

            if (smaFast[0] < smaSlow[0])
            {
                if (distToSma200 < 1.5 && rsi[0] < 30 && zScoreVsSma200 < -2.0)
                    return MarketRegime.CounterTrendBullish;
                return MarketRegime.Bearish;
            }

            if (smaFast[0] > smaSlow[0])
            {
                if (distToSma200 < 1.5 && rsi[0] > 70 && zScoreVsSma200 > 2.0)
                    return MarketRegime.CounterTrendBearish;
                return MarketRegime.Bullish;
            }

            return MarketRegime.Range;
        }

        private bool IsCounterTrend(MarketRegime regime)
        {
            return regime == MarketRegime.CounterTrendBullish || regime == MarketRegime.CounterTrendBearish;
        }

        private bool IsLongRegime(MarketRegime regime)
        {
            return regime == MarketRegime.Bullish || regime == MarketRegime.CounterTrendBullish;
        }

        #endregion

        #region Entry Score Calculation

        private double CalculateEntryScore(bool isLong)
        {
            double fZscore = CalcFZscore();
            double fRsi = CalcFRsi(isLong);
            double fSlope = CalcFSlope(isLong);
            double fAtr = CalcFAtr();
            double fVolume = CalcFVolume();
            double fPullback = CalcFPullback(isLong);
            double fCandle = CalcFCandle(isLong);

            double sRaw = WeightZscore * fZscore
                + WeightRsi * fRsi
                + WeightSlope * fSlope
                + WeightAtr * fAtr
                + WeightVolume * fVolume
                + WeightPullback * fPullback
                + WeightCandle * fCandle;

            double score = 100.0 / (1.0 + Math.Exp(-12.0 * (sRaw - 0.5)));
            return score;
        }

        private double CalcFZscore()
        {
            if (stdDev[0] <= 0) return 0.5;
            double z = (Close[0] - smaFast[0]) / stdDev[0];
            return GaussianKernel(z);
        }

        private double CalcFRsi(bool isLong)
        {
            if (isLong)
                return Sigmoid(-0.15 * (rsi[0] - 40.0));
            else
                return Sigmoid(0.15 * (rsi[0] - 60.0));
        }

        private double CalcFSlope(bool isLong)
        {
            double slope = GetNormalizedSlope(smaFast, SlopeLookback);
            if (isLong)
                return Sigmoid(10.0 * slope);
            else
                return Sigmoid(-10.0 * slope);
        }

        private double CalcFAtr()
        {
            if (atr[0] <= 0) return 0.5;
            double avgRange = GetAvgRange(CandleLookback);
            double volRatio = avgRange / atr[0];

            if (volRatio < 0.50)
                return 0.70;
            if (volRatio < 0.70)
                return 0.70 + 0.30 * (volRatio - 0.50) / 0.20;
            if (volRatio <= 1.30)
                return 1.00;
            if (volRatio <= 2.00)
                return 1.00 - 0.50 * (volRatio - 1.30) / 0.70;
            return 0.50;
        }

        private double CalcFVolume()
        {
            if (volumeSma[0] <= 0) return 0.5;
            double volRatio = Volume[0] / volumeSma[0];
            return Sigmoid(5.0 * (volRatio - 1.2));
        }

        private double CalcFPullback(bool isLong)
        {
            if (atr[0] <= 0) return 0.5;
            double d = (Close[0] - smaFast[0]) / atr[0];
            if (isLong)
                return GaussianKernelCustom(d, -0.5, 0.7);
            else
                return GaussianKernelCustom(d, 0.5, 0.7);
        }

        private double CalcFCandle(bool isLong)
        {
            double body = Math.Abs(Close[0] - Open[0]);
            double range = High[0] - Low[0];
            double lowerWick = Math.Min(Open[0], Close[0]) - Low[0];
            double upperWick = High[0] - Math.Max(Open[0], Close[0]);

            if (range <= 0) return 0;

            bool isBullishBar = Close[0] > Open[0];
            bool isBearishBar = Close[0] < Open[0];

            if (isLong)
            {
                bool engulfing = isBullishBar && CurrentBar > 0
                    && Close[0] > Open[1] && Open[0] < Close[1]
                    && Open[1] > Close[1];
                if (engulfing) return 1.00;

                bool hammer = isBullishBar && body > 0
                    && lowerWick > 2.0 * body && upperWick < 0.3 * body;
                if (hammer) return 0.85;

                if (isBullishBar && body > 0.6 * range)
                    return 0.60;

                if (isBullishBar)
                    return 0.30;

                return 0.00;
            }
            else
            {
                bool engulfing = isBearishBar && CurrentBar > 0
                    && Close[0] < Open[1] && Open[0] > Close[1]
                    && Open[1] < Close[1];
                if (engulfing) return 1.00;

                bool shootingStar = isBearishBar && body > 0
                    && upperWick > 2.0 * body && lowerWick < 0.3 * body;
                if (shootingStar) return 0.85;

                if (isBearishBar && body > 0.6 * range)
                    return 0.60;

                if (isBearishBar)
                    return 0.30;

                return 0.00;
            }
        }

        #endregion

        #region Position Sizing

        private int CalculatePositionSize(MarketRegime regime, double score)
        {
            bool isCounter = IsCounterTrend(regime);
            bool isLong = IsLongRegime(regime);

            double atrMultiplier = isCounter ? AtrMultiplierCounter : AtrMultiplierTrend;
            double slPoints = atrMultiplier * atr[0];
            double slDollars = slPoints * 2.0;

            currentStopDistance = slPoints;
            currentTargetDistance = slPoints * RewardRiskRatio;

            if (slDollars <= 0) return 0;

            double nFixed = (MaxDailyLoss * RiskPerTradeFraction) / slDollars;

            double kellyStar = rollingWinRate - (1.0 - rollingWinRate) / rollingRR;
            double kellyAdj = kellyStar * KellyFraction;
            double nKelly = kellyAdj > 0
                ? (MaxDailyLoss * kellyAdj) / slDollars
                : 0;

            double nBase = kellyStar > 0 ? Math.Min(nFixed, nKelly) : nFixed;

            double cSpread = CalcCSpread(isCounter);
            double cSlope = CalcCSlope(isLong);
            double cVol = CalcCVol();
            double dailyBudget = CalcDailyBudget();
            double cTime = GetCTime();

            double nRaw = nBase * cSpread * cSlope * cVol * dailyBudget * cTime;

            if (isCounter)
                nRaw *= CounterTrendSizePct;

            if (score >= MinScoreTrend && score < 75)
                nRaw *= ModerateScoreSizePct;

            int nFinal = (int)Math.Floor(nRaw);

            int nAbsMax = (int)Math.Floor(MaxDailyLoss / slDollars);
            nFinal = Math.Max(1, Math.Min(nFinal, nAbsMax));

            currentContracts = nFinal;
            return nFinal;
        }

        private double CalcCSpread(bool isCounter)
        {
            if (isCounter) return 0.25;
            double spread = Math.Abs(smaFast[0] - smaSlow[0]) / atr[0];
            return LinearInterp(spread, SpreadThresholdLow, SpreadThresholdHigh, 0.50, 1.00);
        }

        private double CalcCSlope(bool isLong)
        {
            double normSlope = Math.Abs(GetNormalizedSlope(smaFast, SlopeLookback));
            double base_ = LinearInterp(normSlope, SlopeThresholdLow, SlopeThresholdHigh, 0.50, 1.00);

            double slopeFast = GetNormalizedSlope(smaFast, SlopeLookback);
            double slopeSlow = GetNormalizedSlope(smaSlow, SlopeLookback);

            bool fastAgrees = isLong ? slopeFast > 0 : slopeFast < 0;
            bool slowAgrees = isLong ? slopeSlow > 0 : slopeSlow < 0;

            double agreement;
            if (fastAgrees && slowAgrees) agreement = 1.00;
            else if (fastAgrees) agreement = 0.75;
            else agreement = 0.50;

            return base_ * agreement;
        }

        private double CalcCVol()
        {
            if (atr[0] <= 0) return 1.0;
            double volRatio = GetAvgRange(CandleLookback) / atr[0];

            if (volRatio < 0.70) return 1.20;
            if (volRatio <= 1.30) return 1.00;
            return Math.Max(0.40, 1.0 / volRatio);
        }

        private double CalcDailyBudget()
        {
            if (dailyPnL <= 0)
            {
                double remaining = MaxDailyLoss - Math.Abs(dailyPnL);
                if (remaining <= 0) return 0;
                return remaining / MaxDailyLoss;
            }
            return Math.Min(MaxAntiMartingale, 1.0 + (dailyPnL / MaxDailyLoss) * 0.25);
        }

        #endregion

        #region Daily Control

        private bool IsDayDone()
        {
            if (dailyPnL <= -MaxDailyLoss) return true;
            if (dailyPnL >= DailyProfitTarget) return true;
            if (tradesToday >= MaxTradesPerDay) return true;
            if (!IsInTradingWindow()) return true;
            return false;
        }

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            dayDone = false;
            entryPrice = 0;
            stopPrice = 0;
            targetPrice = 0;
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId,
            double price, int quantity, MarketPosition marketPosition,
            string orderId, DateTime time)
        {
            if (Position.MarketPosition == MarketPosition.Flat && entryPrice != 0)
            {
                double pnl = 0;
                if (Performance.AllTrades.Count > 0)
                    pnl = Performance.AllTrades[Performance.AllTrades.Count - 1].ProfitCurrency;

                dailyPnL += pnl;
                tradesToday++;
                lastExecutionPrice = price;

                RecordTrade(pnl);

                if (EnableSoundAlerts)
                {
                    if (pnl > 0) PlaySound(@"C:\Windows\Media\tada.wav");
                    else if (pnl < 0) PlaySound(@"C:\Windows\Media\chord.wav");
                }

                entryPrice = 0;
                stopPrice = 0;
                targetPrice = 0;

                if (IsDayDone())
                {
                    dayDone = true;
                    if (EnableSoundAlerts)
                        PlaySound(@"C:\Windows\Media\notify.wav");
                }
            }
        }

        #endregion

        #region Exit Management

        private void ManageExit()
        {
            if (IsFlattenTime() && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("Flatten", "LongEntry");
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("Flatten", "ShortEntry");
                return;
            }
        }

        #endregion

        #region Entry Management

        private void EvaluateEntry()
        {
            if (Position.MarketPosition != MarketPosition.Flat)
                return;

            if (dayDone || IsDayDone())
            {
                dayDone = true;
                return;
            }

            if (currentRegime == MarketRegime.Range)
                return;

            bool isLong = IsLongRegime(currentRegime);
            bool isCounter = IsCounterTrend(currentRegime);

            int minScore = isCounter ? MinScoreCounter : MinScoreTrend;
            if (currentScore < minScore)
                return;

            if (!PassesRegimeFilters(isLong, isCounter))
                return;

            int contracts = CalculatePositionSize(currentRegime, currentScore);
            if (contracts <= 0)
                return;

            if (isLong)
            {
                double stopLevel = isCounter
                    ? smaSlow[0] - AtrMultiplierCounter * atr[0]
                    : smaFast[0] - AtrMultiplierTrend * atr[0];
                double riskPoints = Close[0] - stopLevel;
                if (riskPoints <= 0) return;

                double targetLevel = Close[0] + riskPoints * RewardRiskRatio;

                stopPrice = stopLevel;
                targetPrice = targetLevel;
                entryPrice = Close[0];
                lastTradeDirection = "LONG";

                SetStopLoss("LongEntry", CalculationMode.Price, stopLevel, false);
                SetProfitTarget("LongEntry", CalculationMode.Price, targetLevel);
                EnterLong(contracts, "LongEntry");

                if (ShowEntryMarkers)
                    Draw.ArrowUp(this, "Entry" + CurrentBar, 0, Low[0] - 2 * TickSize,
                        System.Windows.Media.Brushes.LimeGreen);

                if (EnableSoundAlerts)
                    PlaySound(@"C:\Windows\Media\Windows Background.wav");
            }
            else
            {
                double stopLevel = isCounter
                    ? smaSlow[0] + AtrMultiplierCounter * atr[0]
                    : smaFast[0] + AtrMultiplierTrend * atr[0];
                double riskPoints = stopLevel - Close[0];
                if (riskPoints <= 0) return;

                double targetLevel = Close[0] - riskPoints * RewardRiskRatio;

                stopPrice = stopLevel;
                targetPrice = targetLevel;
                entryPrice = Close[0];
                lastTradeDirection = "SHORT";

                SetStopLoss("ShortEntry", CalculationMode.Price, stopLevel, false);
                SetProfitTarget("ShortEntry", CalculationMode.Price, targetLevel);
                EnterShort(contracts, "ShortEntry");

                if (ShowEntryMarkers)
                    Draw.ArrowDown(this, "Entry" + CurrentBar, 0, High[0] + 2 * TickSize,
                        System.Windows.Media.Brushes.Red);

                if (EnableSoundAlerts)
                    PlaySound(@"C:\Windows\Media\Windows Background.wav");
            }
        }

        private bool PassesRegimeFilters(bool isLong, bool isCounter)
        {
            if (isCounter)
            {
                if (isLong && Close[0] > smaSlow[0]) return false;
                if (!isLong && Close[0] < smaSlow[0]) return false;

                double distToSma200 = Math.Abs(Close[0] - smaSlow[0]) / atr[0];
                if (distToSma200 >= 1.5) return false;

                if (isLong && rsi[0] >= 30) return false;
                if (!isLong && rsi[0] <= 70) return false;

                if (volumeSma[0] > 0 && Volume[0] < 1.5 * volumeSma[0])
                    return false;

                double fCandle = CalcFCandle(isLong);
                if (fCandle < 0.60) return false;
            }
            else
            {
                double dist = Math.Abs(Close[0] - smaFast[0]) / atr[0];
                if (dist > 0.5) return false;

                if (isLong && rsi[0] >= 45) return false;
                if (!isLong && rsi[0] <= 55) return false;
            }

            return true;
        }

        #endregion

        #region Statistics Persistence

        private void RecordTrade(double pnl)
        {
            try
            {
                bool fileExists = File.Exists(statsFilePath);

                using (StreamWriter sw = new StreamWriter(statsFilePath, true))
                {
                    if (!fileExists)
                        sw.WriteLine("Date,Time,Direction,Contracts,EntryPrice,ExitPrice,PnL,Score,Regime");

                    sw.WriteLine(string.Format("{0},{1},{2},{3},{4:F2},{5:F2},{6:F2},{7:F1},{8}",
                        DateTime.Now.ToString("yyyy-MM-dd"),
                        DateTime.Now.ToString("HH:mm:ss"),
                        lastTradeDirection,
                        currentContracts,
                        entryPrice,
                        lastExecutionPrice,
                        pnl,
                        currentScore,
                        currentRegime));
                }

                tradeResults.Add(pnl);
                if (tradeResults.Count > RollingWindowSize)
                    tradeResults.RemoveAt(0);

                UpdateRollingStats();
            }
            catch (Exception ex)
            {
                Print("MNQBot Stats Error: " + ex.Message);
            }
        }

        private void UpdateRollingStats()
        {
            if (tradeResults.Count < 5)
            {
                rollingWinRate = InitialWinRate;
                rollingRR = InitialRR;
                return;
            }

            int wins = 0;
            double totalWin = 0;
            double totalLoss = 0;
            int lossCount = 0;

            foreach (double pnl in tradeResults)
            {
                if (pnl > 0)
                {
                    wins++;
                    totalWin += pnl;
                }
                else if (pnl < 0)
                {
                    lossCount++;
                    totalLoss += Math.Abs(pnl);
                }
            }

            rollingWinRate = (double)wins / tradeResults.Count;

            if (lossCount > 0 && wins > 0)
            {
                double avgWin = totalWin / wins;
                double avgLoss = totalLoss / lossCount;
                rollingRR = avgLoss > 0 ? avgWin / avgLoss : InitialRR;
            }
            else
            {
                rollingRR = InitialRR;
            }
        }

        private void LoadHistoricalStats()
        {
            try
            {
                if (!File.Exists(statsFilePath)) return;

                string[] lines = File.ReadAllLines(statsFilePath);
                int start = Math.Max(1, lines.Length - RollingWindowSize);

                for (int i = start; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(',');
                    if (parts.Length >= 7)
                    {
                        double pnl;
                        if (double.TryParse(parts[6], out pnl))
                            tradeResults.Add(pnl);
                    }
                }

                UpdateRollingStats();
            }
            catch (Exception ex)
            {
                Print("MNQBot Load Stats Error: " + ex.Message);
            }
        }

        #endregion

        #region Chart Panel Drawing

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!ShowPanel) return;
            if (chartControl == null || RenderTarget == null) return;

            float panelWidth = 310;
            float panelHeight = 290;
            float panelX = (float)chartControl.ActualWidth - panelWidth - 10;
            float panelY = 10;

            byte alpha = (byte)(PanelOpacity * 255 / 100);

            var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(30, 30, 30, alpha));
            var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(100, 100, 100, 255));
            var textWhite = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(240, 240, 240, 255));
            var textGreen = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(0, 200, 0, 255));
            var textRed = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(220, 50, 50, 255));
            var textOrange = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(255, 165, 0, 255));
            var textGray = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(150, 150, 150, 255));
            var textBlue = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(100, 149, 237, 255));
            var textYellow = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(255, 255, 0, 255));
            var barBg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new SharpDX.Color(60, 60, 60, 255));

            var bgRect = new SharpDX.RectangleF(panelX, panelY, panelWidth, panelHeight);
            RenderTarget.FillRectangle(bgRect, bgBrush);
            RenderTarget.DrawRectangle(bgRect, borderBrush, 1.0f);

            var factory = new SharpDX.DirectWrite.Factory();
            var titleFormat = new SharpDX.DirectWrite.TextFormat(factory, "Consolas", 12f);
            var textFormat = new SharpDX.DirectWrite.TextFormat(factory, "Consolas", 10f);

            float y = panelY + 5;
            float x = panelX + 8;
            float lineH = 16;
            float col2 = panelX + 160;

            DrawPanelText("MNQ PROBABILISTIC BOT v1.0", x, y, panelWidth - 16, titleFormat, textWhite);
            y += lineH + 2;

            DrawPanelLine(panelX + 5, y, panelX + panelWidth - 5, y, borderBrush);
            y += 5;

            SharpDX.Direct2D1.SolidColorBrush regimeBrush;
            string regimeText;
            switch (currentRegime)
            {
                case MarketRegime.Bullish:
                    regimeText = "BULLISH"; regimeBrush = textGreen; break;
                case MarketRegime.Bearish:
                    regimeText = "BEARISH"; regimeBrush = textRed; break;
                case MarketRegime.CounterTrendBullish:
                    regimeText = "COUNTER BULL"; regimeBrush = textOrange; break;
                case MarketRegime.CounterTrendBearish:
                    regimeText = "COUNTER BEAR"; regimeBrush = textOrange; break;
                default:
                    regimeText = dayDone ? "DAY DONE" : "RANGE";
                    regimeBrush = dayDone ? textBlue : textGray; break;
            }
            DrawPanelText("Estado:", x, y, 80, textFormat, textWhite);
            DrawPanelText(regimeText, col2, y, 140, textFormat, regimeBrush);
            y += lineH;

            DrawPanelText("Score:", x, y, 80, textFormat, textWhite);
            SharpDX.Direct2D1.SolidColorBrush scoreBrush = currentScore >= 75 ? textGreen
                : currentScore >= 65 ? textYellow : textGray;
            DrawPanelText(string.Format("{0:F0}/100", currentScore), col2, y, 60, textFormat, scoreBrush);

            float barX = col2 + 65;
            float barW = 80;
            float barH = 10;
            float barY2 = y + 3;
            RenderTarget.FillRectangle(new SharpDX.RectangleF(barX, barY2, barW, barH), barBg);
            float fillW = (float)(barW * Math.Min(currentScore, 100) / 100.0);
            RenderTarget.FillRectangle(new SharpDX.RectangleF(barX, barY2, fillW, barH), scoreBrush);
            y += lineH;

            DrawPanelLine(panelX + 5, y + 2, panelX + panelWidth - 5, y + 2, borderBrush);
            y += 7;

            DrawPanelText("Contratos:", x, y, 100, textFormat, textWhite);
            DrawPanelText(currentContracts.ToString(), col2, y, 60, textFormat, textWhite);
            y += lineH;

            DrawPanelText("Stop Loss:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F1} pts (${1:F0})", currentStopDistance, currentStopDistance * 2),
                col2, y, 140, textFormat, textRed);
            y += lineH;

            DrawPanelText("Take Profit:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F1} pts (${1:F0})", currentTargetDistance, currentTargetDistance * 2),
                col2, y, 140, textFormat, textGreen);
            y += lineH;

            DrawPanelLine(panelX + 5, y + 2, panelX + panelWidth - 5, y + 2, borderBrush);
            y += 7;

            var pnlBrush = dailyPnL >= 0 ? textGreen : textRed;
            DrawPanelText("PnL Dia:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("${0:F2}", dailyPnL), col2, y, 100, textFormat, pnlBrush);
            y += lineH;

            DrawPanelText("Trades:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0}/{1}", tradesToday, MaxTradesPerDay), col2, y, 60, textFormat, textWhite);
            y += lineH;

            double budget = CalcDailyBudget();
            DrawPanelText("Presupuesto:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F0}%", budget * 100), col2, y, 60, textFormat, textWhite);
            y += lineH;

            DrawPanelText("WinRate:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F0}% ({1}t)", rollingWinRate * 100, tradeResults.Count),
                col2, y, 100, textFormat, textWhite);
            y += lineH;

            DrawPanelLine(panelX + 5, y + 2, panelX + panelWidth - 5, y + 2, borderBrush);
            y += 7;

            TimeSpan nyTime = ToNYTime(Time[0]);
            DrawPanelText("Hora NY:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:hh\\:mm}", nyTime), col2, y, 60, textFormat, textWhite);
            y += lineH;

            DrawPanelText("ATR(14):", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F1}", atr[0]), col2, y, 60, textFormat, textWhite);

            DrawPanelText("RSI(7):", col2 + 65, y, 60, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F0}", rsi[0]), col2 + 120, y, 40, textFormat, textWhite);
            y += lineH;

            double smaSpread = atr[0] > 0 ? Math.Abs(smaFast[0] - smaSlow[0]) / atr[0] : 0;
            DrawPanelText("Spread:", x, y, 100, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F1} ATR", smaSpread), col2, y, 60, textFormat, textWhite);

            double zScore = stdDev[0] > 0 ? (Close[0] - smaFast[0]) / stdDev[0] : 0;
            DrawPanelText("Z:", col2 + 65, y, 30, textFormat, textWhite);
            DrawPanelText(string.Format("{0:F2}", zScore), col2 + 90, y, 50, textFormat, textWhite);

            bgBrush.Dispose();
            borderBrush.Dispose();
            textWhite.Dispose();
            textGreen.Dispose();
            textRed.Dispose();
            textOrange.Dispose();
            textGray.Dispose();
            textBlue.Dispose();
            textYellow.Dispose();
            barBg.Dispose();
            titleFormat.Dispose();
            textFormat.Dispose();
            factory.Dispose();
        }

        private void DrawPanelText(string text, float x, float y, float width,
            SharpDX.DirectWrite.TextFormat format, SharpDX.Direct2D1.SolidColorBrush brush)
        {
            var rect = new SharpDX.RectangleF(x, y, width, 20);
            RenderTarget.DrawText(text, format, rect, brush);
        }

        private void DrawPanelLine(float x1, float y1, float x2, float y2,
            SharpDX.Direct2D1.SolidColorBrush brush)
        {
            RenderTarget.DrawLine(
                new SharpDX.Vector2(x1, y1),
                new SharpDX.Vector2(x2, y2),
                brush, 0.5f);
        }

        #endregion

        #region Chart Drawings

        private void DrawChartElements()
        {
            if (ShowPullbackZone && atr[0] > 0 && currentRegime != MarketRegime.Range)
            {
                double upper = smaFast[0] + 0.5 * atr[0];
                double lower = smaFast[0] - 0.5 * atr[0];

                System.Windows.Media.Brush zoneBrush = IsLongRegime(currentRegime)
                    ? System.Windows.Media.Brushes.LimeGreen
                    : System.Windows.Media.Brushes.Red;

                Draw.HorizontalLine(this, "PB_Upper", upper, zoneBrush, DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "PB_Lower", lower, zoneBrush, DashStyleHelper.Dot, 1);
            }
            else
            {
                RemoveDrawObject("PB_Upper");
                RemoveDrawObject("PB_Lower");
            }

            if (ShowCounterZone && atr[0] > 0 && IsCounterTrend(currentRegime))
            {
                Draw.HorizontalLine(this, "CT_Upper",
                    smaSlow[0] + 1.5 * atr[0], System.Windows.Media.Brushes.Orange,
                    DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "CT_Lower",
                    smaSlow[0] - 1.5 * atr[0], System.Windows.Media.Brushes.Orange,
                    DashStyleHelper.Dot, 1);
            }
            else
            {
                RemoveDrawObject("CT_Upper");
                RemoveDrawObject("CT_Lower");
            }

            if (ShowStopTPLines && Position.MarketPosition != MarketPosition.Flat)
            {
                if (stopPrice > 0)
                    Draw.HorizontalLine(this, "StopLine", stopPrice,
                        System.Windows.Media.Brushes.Red, DashStyleHelper.Dash, 2);
                if (targetPrice > 0)
                    Draw.HorizontalLine(this, "TargetLine", targetPrice,
                        System.Windows.Media.Brushes.LimeGreen, DashStyleHelper.Dash, 2);
            }
            else
            {
                RemoveDrawObject("StopLine");
                RemoveDrawObject("TargetLine");
            }
        }

        #endregion
    }
}
