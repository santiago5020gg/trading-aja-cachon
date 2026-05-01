#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.IO;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MNQEnCalienteBot : Strategy
    {
        #region Parameters — Score Thresholds

        [NinjaScriptProperty]
        [Display(Name = "Score Min Entrada", GroupName = "1. Scoring", Order = 1)]
        public double ScoreEntryMin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Score Alta Confianza", GroupName = "1. Scoring", Order = 2)]
        public double ScoreHighConfidence { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Score Min Scaling", GroupName = "1. Scoring", Order = 3)]
        public double ScoreScalingMin { get; set; }

        #endregion

        #region Parameters — Weights Tendencia

        [NinjaScriptProperty]
        [Display(Name = "W Tend Spread", GroupName = "2. Pesos Tendencia", Order = 1)]
        public double WeightTendSpread { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Tend Body", GroupName = "2. Pesos Tendencia", Order = 2)]
        public double WeightTendBody { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Tend Align", GroupName = "2. Pesos Tendencia", Order = 3)]
        public double WeightTendAlign { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Tend Wick", GroupName = "2. Pesos Tendencia", Order = 4)]
        public double WeightTendWick { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Tend Momentum", GroupName = "2. Pesos Tendencia", Order = 5)]
        public double WeightTendMomentum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Tend ATR", GroupName = "2. Pesos Tendencia", Order = 6)]
        public double WeightTendATR { get; set; }

        #endregion

        #region Parameters — Weights Pullback

        [NinjaScriptProperty]
        [Display(Name = "W Pull Spread", GroupName = "3. Pesos Pullback", Order = 1)]
        public double WeightPullSpread { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Pull Body", GroupName = "3. Pesos Pullback", Order = 2)]
        public double WeightPullBody { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Pull Align", GroupName = "3. Pesos Pullback", Order = 3)]
        public double WeightPullAlign { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Pull Wick", GroupName = "3. Pesos Pullback", Order = 4)]
        public double WeightPullWick { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Pull Momentum", GroupName = "3. Pesos Pullback", Order = 5)]
        public double WeightPullMomentum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Pull ATR", GroupName = "3. Pesos Pullback", Order = 6)]
        public double WeightPullATR { get; set; }

        #endregion

        #region Parameters — Weights Ruptura

        [NinjaScriptProperty]
        [Display(Name = "W Rupt Spread", GroupName = "4. Pesos Ruptura", Order = 1)]
        public double WeightRuptSpread { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Rupt Body", GroupName = "4. Pesos Ruptura", Order = 2)]
        public double WeightRuptBody { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Rupt Align", GroupName = "4. Pesos Ruptura", Order = 3)]
        public double WeightRuptAlign { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Rupt Wick", GroupName = "4. Pesos Ruptura", Order = 4)]
        public double WeightRuptWick { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Rupt Momentum", GroupName = "4. Pesos Ruptura", Order = 5)]
        public double WeightRuptMomentum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "W Rupt ATR", GroupName = "4. Pesos Ruptura", Order = 6)]
        public double WeightRuptATR { get; set; }

        #endregion

        #region Parameters — Risk Management

        [NinjaScriptProperty]
        [Display(Name = "Max Daily Loss ($)", GroupName = "5. Riesgo", Order = 1)]
        public double MaxDailyLoss { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Profit Target ($)", GroupName = "5. Riesgo", Order = 2)]
        public double DailyProfitTarget { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Breathe Whipsaws/Dia", GroupName = "5. Riesgo", Order = 3)]
        public int MaxBreatheWhipsaws { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breakeven Pts (Alta Conf)", GroupName = "5. Riesgo", Order = 4)]
        public double BreakevenPtsHigh { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breakeven Pts (Normal)", GroupName = "5. Riesgo", Order = 5)]
        public double BreakevenPtsNormal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breathe Bars (Alta Conf)", GroupName = "5. Riesgo", Order = 6)]
        public int BreatheHigh { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breathe Bars (Normal)", GroupName = "5. Riesgo", Order = 7)]
        public int BreatheNormal { get; set; }

        #endregion

        #region Parameters — Logging

        public enum LogMode { Off, Day, Month }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "6. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        #endregion

        #region Variables

        private SMA sma20;
        private SMA sma200;
        private ATR atr14;
        private SMA atrSma50;

        private enum EntryType { None, Tendencia, Pullback, Ruptura }
        private enum TradeState { Flat, Breathe, Trailing }

        private TradeState tradeState;
        private EntryType lastEntryType;
        private int tradeDirection;
        private double entryPrice;
        private double stopPrice;
        private double entryScore;
        private int breatheCount;
        private int breatheBarsNeeded;
        private double runningExtreme;
        private bool breakEvenHit;
        private double breakevenTarget;

        private double dailyPnL;
        private double myTotalPnL;
        private int tradesToday;
        private int breatheWhipsawsToday;
        private bool dayDone;
        private bool sessionStarted;
        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;

        private double realEntryPrice;
        private int lastClosedDirection;
        private string lastClosedReason;
        private bool pnlTrackingStarted;
        private string activeEntrySignal;
        private bool isChoppyMode;

        private string telemetryPath = @"C:\temp\mnq_bot_status.json";
        private string botHistoryDir = @"C:\Users\santiago.burgos\OneDrive - Perficient, Inc\Documents\perficient\AI path lean\trading 7\bot\history";
        private string botDayDir;
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;
        private List<string> tradeLog;
        private string lastAction;

        private double lastScoreTend, lastScorePull, lastScoreRupt;
        private double[] lastFactors;
        private string lastDecision;

        private DateTime entryTimeNY;
        private double[] entryFactors;
        private double prevDayPnL;
        private int prevDayTrades;
        private DateTime prevDayDate;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ En Caliente — Scoring probabilistico con SMA20/SMA200, trailing bar-a-bar";
                Name = "MNQEnCalienteBot";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 1;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = true;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 200;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                ScoreEntryMin = 80;
                ScoreHighConfidence = 80;
                ScoreScalingMin = 70;

                WeightTendSpread = 25;
                WeightTendBody = 20;
                WeightTendAlign = 25;
                WeightTendWick = 10;
                WeightTendMomentum = 10;
                WeightTendATR = 10;

                WeightPullSpread = 15;
                WeightPullBody = 15;
                WeightPullAlign = 20;
                WeightPullWick = 15;
                WeightPullMomentum = 25;
                WeightPullATR = 10;

                WeightRuptSpread = 10;
                WeightRuptBody = 25;
                WeightRuptAlign = 20;
                WeightRuptWick = 15;
                WeightRuptMomentum = 10;
                WeightRuptATR = 20;

                MaxDailyLoss = 270;
                DailyProfitTarget = 270;
                MaxBreatheWhipsaws = 1;
                BreakevenPtsHigh = 50;
                BreakevenPtsNormal = 67.5;
                BreatheHigh = 2;
                BreatheNormal = 3;

                ModoLog = LogMode.Month;
            }
            else if (State == State.DataLoaded)
            {
                sma20 = SMA(20);
                sma200 = SMA(200);
                atr14 = ATR(14);
                atrSma50 = SMA(atr14, 50);

                sma20.Plots[0].Brush = Brushes.DodgerBlue;
                sma20.Plots[0].Width = 2;
                sma200.Plots[0].Brush = Brushes.Red;
                sma200.Plots[0].Width = 2;

                AddChartIndicator(sma20);
                AddChartIndicator(sma200);

                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastResetDate = DateTime.MinValue;
                tradeLog = new List<string>();
                lastAction = "";
                lastFactors = new double[6];
                entryFactors = new double[6];
                lastDecision = "WAITING";
                prevDayDate = DateTime.MinValue;

                try { Directory.CreateDirectory(@"C:\temp"); } catch {}
                if (ModoLog != LogMode.Off)
                {
                    string logDir = botHistoryDir;
                    if (ModoLog == LogMode.Day)
                    {
                        botDayDir = Path.Combine(botHistoryDir, "history-day");
                        logDir = botDayDir;
                    }
                    try { Directory.CreateDirectory(logDir); } catch {}
                    csvLogPath = Path.Combine(logDir, "mnq_trades_log.csv");
                    csvDailyPath = Path.Combine(logDir, "mnq_daily_log.csv");
                    csvBarLogPath = Path.Combine(logDir, "mnq_bar_log.csv");
                    InitCsvLogs();
                }
            }
            else if (State == State.Realtime)
            {
                pnlTrackingStarted = true;
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);
            DateTime nyDate = nyNow.Date;

            LogBarCsv(nyNow);

            if (nyDate != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                {
                    prevDayPnL = dailyPnL;
                    prevDayTrades = tradesToday;
                    string reason = dayDone ? (dailyPnL <= -MaxDailyLoss ? "MaxLoss" : dailyPnL >= DailyProfitTarget ? "ProfitTarget" : "Whipsaws") : "SessionEnd";
                    LogDailyCsv(lastResetDate, reason);
                }
                ResetDaily();
                lastResetDate = nyDate;
                if (ModoLog == LogMode.Day)
                    InitCsvLogs();
            }

            if (dayDone)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenPosition("DayDone");
                lastDecision = "DAY_DONE";
                WriteTelemetry(nyNow);
                return;
            }

            bool beforeSession = nyNow.Hour < 9 || (nyNow.Hour == 9 && nyNow.Minute < 32);
            bool afterSession = nyNow.Hour >= 16 || (nyNow.Hour == 15 && nyNow.Minute >= 50);
            if (beforeSession || afterSession)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenPosition("OutOfSession");
                lastDecision = "OUT_OF_SESSION";
                WriteTelemetry(nyNow);
                return;
            }

            // First evaluable bar is 09:32 closed
            if (!sessionStarted)
            {
                if (nyNow.Hour == 9 && nyNow.Minute == 32)
                    sessionStarted = true;
                else
                {
                    lastDecision = "WAITING_0932";
                    WriteTelemetry(nyNow);
                    return;
                }
            }

            if (!pnlTrackingStarted)
                pnlTrackingStarted = true;

            if (tradeState != TradeState.Flat)
            {
                ManageOpenTrade(nyNow);
                WriteTelemetry(nyNow);
                return;
            }

            EvaluateEntry(nyNow);
            WriteTelemetry(nyNow);
        }

        #endregion

        #region Score Engine

        private double CalcSpreadScore(double sma20Val, double sma200Val)
        {
            double absSpread = Math.Abs(sma20Val - sma200Val);
            return Interpolate(absSpread, new double[] { 0, 15, 30, 60, 80 }, new double[] { 0, 30, 50, 75, 100 });
        }

        private double CalcBodyScore(double open, double close, double high, double low)
        {
            double range = high - low;
            if (range <= 0) return 0;
            double bodyPct = (Math.Abs(close - open) / range) * 100;
            return Interpolate(bodyPct, new double[] { 20, 40, 55, 60, 80 }, new double[] { 10, 40, 65, 75, 100 });
        }

        private double CalcAlignmentScore(double price, double sma20Val, double sma200Val, int direction)
        {
            bool aboveSma20 = price > sma20Val;
            bool aboveSma200 = price > sma200Val;
            bool sma20AboveSma200 = sma20Val > sma200Val;

            if (direction == 1)
            {
                if (aboveSma20 && aboveSma200 && sma20AboveSma200) return 100;
                if (aboveSma20 && aboveSma200 && !sma20AboveSma200) return 70;
                if (aboveSma20 && !aboveSma200) return 40;
                if (!aboveSma20 && !aboveSma200) return 5;
                return 20;
            }
            else
            {
                if (!aboveSma20 && !aboveSma200 && !sma20AboveSma200) return 100;
                if (!aboveSma20 && !aboveSma200 && sma20AboveSma200) return 70;
                if (!aboveSma20 && aboveSma200) return 40;
                if (aboveSma20 && aboveSma200) return 5;
                return 20;
            }
        }

        private double CalcWickScore(double open, double close, double high, double low, int direction)
        {
            double range = high - low;
            if (range <= 0) return 50;

            double wickAgainst;
            if (direction == 1)
                wickAgainst = (Math.Min(open, close) - low) / range * 100;
            else
                wickAgainst = (high - Math.Max(open, close)) / range * 100;

            return Interpolate(wickAgainst, new double[] { 10, 25, 40, 60 }, new double[] { 100, 75, 50, 20 });
        }

        private double CalcMomentumScore(int direction)
        {
            if (CurrentBar < 3) return 50;

            int consecutive = 0;
            for (int i = 0; i < 3; i++)
            {
                bool barUp = Close[i] > Open[i];
                if ((direction == 1 && barUp) || (direction == -1 && !barUp))
                    consecutive++;
                else
                    break;
            }

            if (consecutive >= 3) return 100;
            if (consecutive == 2) return 75;
            if (consecutive == 1) return 50;
            return 25;
        }

        private double CalcATRScore()
        {
            if (atrSma50[0] <= 0) return 50;
            double ratio = atr14[0] / atrSma50[0];
            return Interpolate(ratio, new double[] { 0.7, 1.0, 1.5, 2.0 }, new double[] { 30, 50, 75, 100 });
        }

        private double CalcWeightedScore(EntryType type, int direction)
        {
            double spreadScore = CalcSpreadScore(sma20[0], sma200[0]);
            double bodyScore = CalcBodyScore(Open[0], Close[0], High[0], Low[0]);
            double alignScore = CalcAlignmentScore(Close[0], sma20[0], sma200[0], direction);
            double wickScore = CalcWickScore(Open[0], Close[0], High[0], Low[0], direction);
            double momentumScore = CalcMomentumScore(direction);
            double atrScore = CalcATRScore();

            lastFactors[0] = spreadScore;
            lastFactors[1] = bodyScore;
            lastFactors[2] = alignScore;
            lastFactors[3] = wickScore;
            lastFactors[4] = momentumScore;
            lastFactors[5] = atrScore;

            double wSpread, wBody, wAlign, wWick, wMom, wATR;

            switch (type)
            {
                case EntryType.Tendencia:
                    wSpread = WeightTendSpread; wBody = WeightTendBody; wAlign = WeightTendAlign;
                    wWick = WeightTendWick; wMom = WeightTendMomentum; wATR = WeightTendATR;
                    break;
                case EntryType.Pullback:
                    wSpread = WeightPullSpread; wBody = WeightPullBody; wAlign = WeightPullAlign;
                    wWick = WeightPullWick; wMom = WeightPullMomentum; wATR = WeightPullATR;
                    break;
                case EntryType.Ruptura:
                    wSpread = WeightRuptSpread; wBody = WeightRuptBody; wAlign = WeightRuptAlign;
                    wWick = WeightRuptWick; wMom = WeightRuptMomentum; wATR = WeightRuptATR;
                    break;
                default:
                    return 0;
            }

            double totalWeight = wSpread + wBody + wAlign + wWick + wMom + wATR;
            if (totalWeight <= 0) return 0;

            return (spreadScore * wSpread + bodyScore * wBody + alignScore * wAlign
                  + wickScore * wWick + momentumScore * wMom + atrScore * wATR) / totalWeight;
        }

        private double Interpolate(double value, double[] xs, double[] ys)
        {
            if (value <= xs[0]) return ys[0];
            if (value >= xs[xs.Length - 1]) return ys[ys.Length - 1];

            for (int i = 0; i < xs.Length - 1; i++)
            {
                if (value >= xs[i] && value <= xs[i + 1])
                {
                    double t = (value - xs[i]) / (xs[i + 1] - xs[i]);
                    return ys[i] + t * (ys[i + 1] - ys[i]);
                }
            }
            return ys[ys.Length - 1];
        }

        #endregion

        #region Entry Evaluation

        private void EvaluateEntry(DateTime nyNow)
        {
            if (tradesToday >= 6)
            {
                lastDecision = "MAX_TRADES_6";
                return;
            }

            double absSpread = Math.Abs(sma20[0] - sma200[0]);
            if (absSpread < 80)
            {
                lastDecision = string.Format("LOW_SPREAD {0:F1}", absSpread);
                return;
            }
            if (absSpread >= 170)
            {
                lastDecision = string.Format("HIGH_SPREAD {0:F1}", absSpread);
                return;
            }

            double scoreMin = ScoreEntryMin;
            if (isChoppyMode)
                scoreMin = 85;

            bool isOpening = nyNow.Hour == 9 && nyNow.Minute >= 32 && nyNow.Minute <= 36;
            if (isOpening && scoreMin < 90)
                scoreMin = 90;

            int bestDir = 0;
            double bestScore = 0;
            EntryType bestType = EntryType.None;

            foreach (int dir in new int[] { 1, -1 })
            {
                double sTend = CalcWeightedScore(EntryType.Tendencia, dir);
                double sRupt = CalcWeightedScore(EntryType.Ruptura, dir);

                if (dir == 1) { lastScoreTend = sTend; lastScorePull = 0; lastScoreRupt = sRupt; }

                if (!isChoppyMode)
                {
                    if (sTend > bestScore) { bestScore = sTend; bestDir = dir; bestType = EntryType.Tendencia; }
                }
                if (sRupt > bestScore) { bestScore = sRupt; bestDir = dir; bestType = EntryType.Ruptura; }
            }

            if (isChoppyMode && bestType == EntryType.Ruptura && bestScore >= scoreMin)
            {
                double recentHigh = double.MinValue;
                double recentLow = double.MaxValue;
                int lookback = Math.Min(10, CurrentBar);
                for (int i = 1; i <= lookback; i++)
                {
                    if (High[i] > recentHigh) recentHigh = High[i];
                    if (Low[i] < recentLow) recentLow = Low[i];
                }

                double rangeSize = recentHigh - recentLow;
                bool breakout = (bestDir == 1 && Close[0] > recentHigh) || (bestDir == -1 && Close[0] < recentLow);

                if (!breakout)
                {
                    lastDecision = string.Format("CHOPPY_WAIT range={0:F1} score={1:F1}", rangeSize, bestScore);
                    return;
                }
            }

            // Recalculate factors for best direction for telemetry
            if (bestDir != 0)
                CalcWeightedScore(bestType, bestDir);

            if (bestScore >= scoreMin)
            {
                double initialStop;
                if (bestDir == 1)
                    initialStop = Low[0];
                else
                    initialStop = High[0];

                double stopDist = Math.Abs(Close[0] - initialStop);
                double atrStop = bestScore >= ScoreHighConfidence ? atr14[0] * 0.75 : atr14[0];

                if (stopDist > atrStop)
                    initialStop = bestDir == 1 ? Close[0] - atrStop : Close[0] + atrStop;

                double potentialLoss = Math.Abs(Close[0] - initialStop) * 2;
                double maxRiskPerTrade = MaxDailyLoss / 2;
                if (potentialLoss > maxRiskPerTrade)
                    initialStop = bestDir == 1 ? Close[0] - (maxRiskPerTrade / 2) : Close[0] + (maxRiskPerTrade / 2);
                potentialLoss = Math.Abs(Close[0] - initialStop) * 2;

                if (dailyPnL - potentialLoss < -MaxDailyLoss)
                {
                    lastDecision = string.Format("BLOCKED_RISK score={0:F1}", bestScore);
                    return;
                }

                string signal;
                switch (bestType)
                {
                    case EntryType.Tendencia: signal = bestDir == 1 ? "TendL" : "TendS"; break;
                    case EntryType.Pullback: signal = bestDir == 1 ? "PullL" : "PullS"; break;
                    case EntryType.Ruptura: signal = bestDir == 1 ? "RuptL" : "RuptS"; break;
                    default: signal = "Entry"; break;
                }

                EnterTrade(bestDir, Close[0], initialStop, bestType, bestScore, signal);
                lastDecision = string.Format("ENTER {0} {1} score={2:F1}", bestType, bestDir == 1 ? "LONG" : "SHORT", bestScore);
            }
            else if (bestScore >= 40)
            {
                lastDecision = string.Format("MONITOR {0} score={1:F1}", bestType, bestScore);
            }
            else
            {
                lastDecision = string.Format("NO_SETUP best={0:F1}", bestScore);
            }
        }

        #endregion

        #region Trade Management

        private void EnterTrade(int direction, double price, double initialStop, EntryType type, double score, string signal)
        {
            if (direction == 1 && initialStop >= price)
                initialStop = price - atr14[0] * 0.75;
            else if (direction == -1 && initialStop <= price)
                initialStop = price + atr14[0] * 0.75;

            entryPrice = price;
            stopPrice = initialStop;
            tradeDirection = direction;
            tradeState = TradeState.Breathe;
            breatheCount = 0;
            entryScore = score;
            Array.Copy(lastFactors, entryFactors, 6);
            breatheBarsNeeded = score >= ScoreHighConfidence ? BreatheHigh : BreatheNormal;
            breakevenTarget = score >= ScoreHighConfidence ? BreakevenPtsHigh : BreakevenPtsNormal;
            runningExtreme = direction == 1 ? High[0] : Low[0];
            breakEvenHit = false;
            lastEntryType = type;
            tradesToday++;
            activeEntrySignal = signal;

            double hardStop = MaxDailyLoss / 2;
            double hardStopPrice = direction == 1 ? price - (hardStop / 2) : price + (hardStop / 2);
            if (direction == 1 && initialStop < hardStopPrice)
                hardStopPrice = initialStop;
            else if (direction == -1 && initialStop > hardStopPrice)
                hardStopPrice = initialStop;

            SetStopLoss(signal, CalculationMode.Price, hardStopPrice, false);

            if (direction == 1)
                EnterLong(1, signal);
            else
                EnterShort(1, signal);

            Draw.ArrowUp(this, "Entry" + CurrentBar, true, 0,
                direction == 1 ? Low[0] - 5 : High[0] + 5,
                direction == 1 ? Brushes.Lime : Brushes.Red);
        }

        private void ManageOpenTrade(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                tradeState = TradeState.Flat;
                tradeDirection = 0;
                return;
            }

            if (tradeState == TradeState.Breathe)
            {
                breatheCount++;

                bool whipsaw = false;
                if (tradeDirection == 1 && Low[0] <= stopPrice) whipsaw = true;
                if (tradeDirection == -1 && High[0] >= stopPrice) whipsaw = true;

                if (whipsaw)
                {
                    breatheWhipsawsToday++;
                    FlattenPosition("BreatheWhipsaw");

                    if (breatheWhipsawsToday >= MaxBreatheWhipsaws && !isChoppyMode)
                        isChoppyMode = true;

                    lastDecision = string.Format("WHIPSAW breathe #{0}{1}", breatheWhipsawsToday, isChoppyMode ? " CHOPPY" : "");
                    return;
                }

                if (tradeDirection == 1 && High[0] > runningExtreme) runningExtreme = High[0];
                if (tradeDirection == -1 && Low[0] < runningExtreme) runningExtreme = Low[0];

                if (breatheCount >= breatheBarsNeeded)
                {
                    tradeState = TradeState.Trailing;
                    if (isChoppyMode)
                        isChoppyMode = false;
                }

                lastDecision = string.Format("BREATHE {0}/{1}", breatheCount, breatheBarsNeeded);
                return;
            }

            if (tradeState == TradeState.Trailing)
            {
                ManageTrailing();
            }
        }

        private void ManageTrailing()
        {
            if (tradeDirection == 1 && Low[0] <= stopPrice)
            {
                FlattenPosition("TrailStop");
                lastDecision = "EXIT TrailStop";
                return;
            }
            if (tradeDirection == -1 && High[0] >= stopPrice)
            {
                FlattenPosition("TrailStop");
                lastDecision = "EXIT TrailStop";
                return;
            }

            double currentATR = atr14[0];
            double minTrailDist = currentATR * 1.5;
            double unrealizedNow = tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0];

            if (tradeDirection == 1)
            {
                if (High[0] > runningExtreme)
                {
                    runningExtreme = High[0];
                    double newStop = Low[0];
                    if (unrealizedNow > currentATR * 2)
                        newStop = Math.Min(newStop, runningExtreme - minTrailDist);
                    if (newStop > stopPrice)
                        stopPrice = newStop;
                }
            }
            else
            {
                if (Low[0] < runningExtreme)
                {
                    runningExtreme = Low[0];
                    double newStop = High[0];
                    if (unrealizedNow > currentATR * 2)
                        newStop = Math.Max(newStop, runningExtreme + minTrailDist);
                    if (newStop < stopPrice)
                        stopPrice = newStop;
                }
            }

            if (!breakEvenHit)
            {
                double unrealizedPts = tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0];
                if (unrealizedPts >= breakevenTarget)
                {
                    double beLevel = entryPrice;
                    if (entryScore < ScoreHighConfidence)
                        beLevel = tradeDirection == 1 ? entryPrice + 10 : entryPrice - 10;

                    if ((tradeDirection == 1 && beLevel > stopPrice) || (tradeDirection == -1 && beLevel < stopPrice))
                    {
                        stopPrice = beLevel;
                        breakEvenHit = true;
                    }
                }
            }

            lastDecision = string.Format("TRAILING stop={0:F2} unrealized={1:F1}pts", stopPrice,
                tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0]);
        }

        #endregion

        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;

            string orderName = execution.Order.Name;
            bool isOurEntry = orderName.StartsWith("Tend") || orderName.StartsWith("Rupt") || orderName.StartsWith("Pull");
            bool isOurExit = orderName.StartsWith("X_") || orderName == "Stop loss";

            if (!pnlTrackingStarted) return;

            if (isOurExit)
            {
                if (orderName == "Stop loss")
                {
                    lastClosedDirection = tradeDirection;
                    lastClosedReason = "HardStop";
                    tradeState = TradeState.Flat;
                    tradeDirection = 0;
                    activeEntrySignal = null;
                }

                double realExitPrice = price;
                double realPnL = 0;
                string dir = lastClosedDirection == 1 ? "LONG" : "SHORT";

                if (lastClosedDirection == 1)
                    realPnL = (realExitPrice - realEntryPrice) * 2;
                else if (lastClosedDirection == -1)
                    realPnL = (realEntryPrice - realExitPrice) * 2;

                dailyPnL += realPnL;
                myTotalPnL += realPnL;

                tradeLog.Add(string.Format("EXIT {0} | reason={1} | entry={2:F2} exit={3:F2} pnl=${4:F2} | dailyPnL=${5:F2} | totalPnL=${6:F2} | score={7:F1}",
                    dir, lastClosedReason, realEntryPrice, realExitPrice, realPnL, dailyPnL, myTotalPnL, entryScore));
                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2} total=${4:F2}", dir, lastClosedReason, realExitPrice, realPnL, myTotalPnL);

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", realEntryPrice, realExitPrice, realPnL, lastClosedReason);

                if (dailyPnL <= -MaxDailyLoss || dailyPnL >= DailyProfitTarget)
                    dayDone = true;
            }
            else if (isOurEntry)
            {
                realEntryPrice = price;
                entryPrice = price;
                entryTimeNY = TimeZoneInfo.ConvertTime(time, easternZone);
                string dir = marketPosition == MarketPosition.Long ? "LONG" : "SHORT";
                lastAction = string.Format("FILL {0} @{1:F2} stop={2:F2} score={3:F1}", dir, price, stopPrice, entryScore);
                tradeLog.Add(string.Format("FILL {0} | price={1:F2} stop={2:F2} | sma20={3:F2} sma200={4:F2} spread={5:F2} | score={6:F1} type={7}",
                    dir, price, stopPrice, sma20[0], sma200[0], Math.Abs(sma20[0] - sma200[0]), entryScore, lastEntryType));

                LogTradeCsv(entryTimeNY, "ENTRY", price, 0, 0, "");
            }
        }

        #endregion

        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            breatheWhipsawsToday = 0;
            dayDone = false;
            sessionStarted = false;
            tradeState = TradeState.Flat;
            tradeDirection = 0;
            lastEntryType = EntryType.None;
            lastDecision = "NEW_DAY";
            lastScoreTend = 0;
            lastScorePull = 0;
            lastScoreRupt = 0;
            isChoppyMode = false;
        }

        private void FlattenPosition(string reason)
        {
            lastClosedDirection = tradeDirection;
            lastClosedReason = reason;

            string fromSignal = activeEntrySignal ?? "";
            if (Position.MarketPosition == MarketPosition.Long)
                ExitLong("X_" + reason, fromSignal);
            else if (Position.MarketPosition == MarketPosition.Short)
                ExitShort("X_" + reason, fromSignal);

            tradeState = TradeState.Flat;
            tradeDirection = 0;
            activeEntrySignal = null;

            Draw.Diamond(this, "Exit" + CurrentBar, true, 0, Close[0], Brushes.Yellow);
        }

        private void InitCsvLogs()
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                bool overwrite = ModoLog == LogMode.Day;
                if (overwrite || !File.Exists(csvLogPath))
                    File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryType,Score,SpreadScore,BodyScore,AlignScore,WickScore,MomentumScore,ATRScore,EntryPrice,ExitPrice,StopPrice,SMA20,SMA200,Spread,ATR,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday,BreatheWhipsaws\n");
                if (overwrite || !File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,BreatheWhipsaws,DayDoneReason\n");
                if (overwrite || !File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,SMA20,SMA200,Spread,ATR,PriceVsSMA20,PriceVsSMA200,Position,TradeState,EntryType,Score,DailyPnL,TotalPnL,TradesToday,Whipsaws,ChoppyMode,Decision\n");
            }
            catch {}
        }

        private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                double[] factors = action == "ENTRY" ? entryFactors : lastFactors;
                string dir = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : (lastClosedDirection == 1 ? "LONG" : "SHORT");
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1},{2},{3},{4:F1},{5:F1},{6:F1},{7:F1},{8:F1},{9:F1},{10:F1},{11:F2},{12:F2},{13:F2},{14:F2},{15:F2},{16:F2},{17:F2},{18:F2},{19:F2},{20:F2},{21},{22},{23}\n",
                    nyNow, action, dir, lastEntryType, entryScore,
                    factors[0], factors[1], factors[2], factors[3], factors[4], factors[5],
                    fillPrice, exitPrice, stopPrice, sma20[0], sma200[0], Math.Abs(sma20[0] - sma200[0]), atr14[0],
                    pnl, dailyPnL, myTotalPnL, exitReason, tradesToday, breatheWhipsawsToday);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime nyDate, string reason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                if (prevDayDate == nyDate) return;
                string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4},{5}\n",
                    nyDate, prevDayPnL, myTotalPnL, prevDayTrades, breatheWhipsawsToday, reason);
                File.AppendAllText(csvDailyPath, line);
                prevDayDate = nyDate;
            }
            catch {}
        }

        private void LogBarCsv(DateTime nyNow)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string pos = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : "FLAT";
                string state = tradeState.ToString();
                string etype = lastEntryType == EntryType.None ? "" : lastEntryType.ToString();
                string vsSma20 = Close[0] >= sma20[0] ? "ENCIMA" : "DEBAJO";
                string vsSma200 = Close[0] >= sma200[0] ? "ENCIMA" : "DEBAJO";
                double score = tradeState != TradeState.Flat ? entryScore : 0;
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10},{11},{12},{13},{14},{15:F1},{16:F2},{17:F2},{18},{19},{20},{21}\n",
                    nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
                    sma20[0], sma200[0], Math.Abs(sma20[0] - sma200[0]), atr14[0],
                    vsSma20, vsSma200, pos, state, etype, score,
                    dailyPnL, myTotalPnL, tradesToday, breatheWhipsawsToday,
                    isChoppyMode ? "YES" : "NO", lastDecision);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        private void WriteTelemetry(DateTime nyNow)
        {
            try
            {
                double absSpread = Math.Abs(sma20[0] - sma200[0]);
                string pos = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : "FLAT";
                double unrealizedPts = 0;
                if (tradeDirection == 1) unrealizedPts = Close[0] - entryPrice;
                else if (tradeDirection == -1) unrealizedPts = entryPrice - Close[0];

                string recentTrades = "";
                int start = Math.Max(0, tradeLog.Count - 10);
                for (int i = start; i < tradeLog.Count; i++)
                    recentTrades += (i > start ? "," : "") + "\"" + tradeLog[i].Replace("\"", "'") + "\"";

                string json = string.Format(
@"{{
  ""timestamp"": ""{0:yyyy-MM-dd HH:mm}"",
  ""price"": {1:F2},
  ""sma20"": {2:F2},
  ""sma200"": {3:F2},
  ""spread"": {4:F2},
  ""position"": ""{5}"",
  ""tradeState"": ""{6}"",
  ""decision"": ""{7}"",
  ""entryPrice"": {8:F2},
  ""stopPrice"": {9:F2},
  ""entryScore"": {10:F1},
  ""unrealizedPts"": {11:F2},
  ""unrealizedPnL"": {12:F2},
  ""dailyPnL"": {13:F2},
  ""totalPnL"": {14:F2},
  ""tradesToday"": {15},
  ""breatheWhipsaws"": {16},
  ""dayDone"": {17},
  ""scores"": {{ ""tendencia"": {18:F1}, ""pullback"": {19:F1}, ""ruptura"": {20:F1} }},
  ""factors"": {{ ""spread"": {21:F1}, ""body"": {22:F1}, ""align"": {23:F1}, ""wick"": {24:F1}, ""momentum"": {25:F1}, ""atr"": {26:F1} }},
  ""lastAction"": ""{27}"",
  ""recentTrades"": [{28}]
}}",
                    nyNow, Close[0], sma20[0], sma200[0], absSpread,
                    pos, tradeState, lastDecision,
                    entryPrice, stopPrice, entryScore, unrealizedPts, unrealizedPts * 2,
                    dailyPnL, myTotalPnL, tradesToday, breatheWhipsawsToday,
                    dayDone ? "true" : "false",
                    lastScoreTend, lastScorePull, lastScoreRupt,
                    lastFactors[0], lastFactors[1], lastFactors[2], lastFactors[3], lastFactors[4], lastFactors[5],
                    lastAction.Replace("\"", "'"),
                    recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }

        #endregion
    }
}
