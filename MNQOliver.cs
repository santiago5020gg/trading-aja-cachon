#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    public class MNQOliver : Strategy
    {
        #region Parameters — Fixed (never optimize)

        [NinjaScriptProperty]
        [Display(Name = "Max Daily Loss ($)", GroupName = "1. Riesgo Fijo", Order = 1)]
        public double MaxDailyLoss { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Profit Target ($)", GroupName = "1. Riesgo Fijo", Order = 2)]
        public double DailyProfitTarget { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades/Dia", GroupName = "1. Riesgo Fijo", Order = 3)]
        public int MaxTradesPerDay { get; set; }

        #endregion

        #region Parameters — Walk-Forward

        [NinjaScriptProperty]
        [Display(Name = "SOH ATR Ratio", GroupName = "2. Filtros", Order = 1)]
        public double SOH_AtrRatio { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SOH EMA Slope Min (pts)", GroupName = "2. Filtros", Order = 2)]
        public double SOH_EmaSlopeMin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Fase Madura Spread/ATR", GroupName = "2. Filtros", Order = 3)]
        public double FaseMadura_SpreadATR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Confidence %", GroupName = "2. Filtros", Order = 4)]
        public double MinConfidence { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Detonante Body%", GroupName = "3. Entrada", Order = 1)]
        public double Detonante_BodyPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Detonante Rango/ATR", GroupName = "3. Entrada", Order = 2)]
        public double Detonante_RangoATR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop ATR Mult", GroupName = "4. Gestion", Order = 1)]
        public double Stop_AtrMult { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail ATR Buffer", GroupName = "4. Gestion", Order = 2)]
        public double Trail_AtrBuffer { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breakeven ATR Mult", GroupName = "4. Gestion", Order = 3)]
        public double Breakeven_AtrMult { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "5. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        #endregion

        #region Variables

        private EMA ema20;
        private SMA sma200;
        private ATR atr14;
        private SMA atrSma50;

        private EMA ema20_1h;
        private SMA sma200_1h;
        private EMA ema20_2h;
        private SMA sma200_2h;
        private EMA ema20_d;
        private SMA sma200_d;
        private SMA volSma20;

        private double lastConfidence;

        public enum LogMode { Off, Day, Month }
        private enum TradeState { Flat, Breathe, Trailing }

        private TradeState tradeState;
        private int tradeDirection;
        private double entryPrice;
        private double stopPrice;
        private int breatheCount;
        private bool breakEvenHit;

        private double dailyPnL;
        private double totalPnL;
        private int tradesToday;
        private int tradesPhase1;
        private int tradesPhase3;
        private bool dayDone;
        private bool sessionStarted;
        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;

        private double realEntryPrice;
        private int lastClosedDirection;
        private string lastClosedReason;
        private bool pnlTrackingStarted;
        private string activeEntrySignal;
        private bool isSOH;

        private string lastDecision;
        private string lastAction;
        private List<string> tradeLog;

        private string telemetryPath = @"C:\temp\mnq_bot_status.json";
        private string botHistoryDir;
        private string botDayDir;
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;
        private DateTime entryTimeNY;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQOliver — Velez price-action + ATR, walk-forward validated";
                Name = "MNQOliver";
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

                MaxDailyLoss = 270;
                DailyProfitTarget = 270;
                MaxTradesPerDay = 3;

                SOH_AtrRatio = 0.80;
                SOH_EmaSlopeMin = 15;
                FaseMadura_SpreadATR = 3.0;
                Detonante_BodyPct = 0.65;
                Detonante_RangoATR = 1.0;
                MinConfidence = 65;
                Stop_AtrMult = 1.5;
                Trail_AtrBuffer = 0.3;
                Breakeven_AtrMult = 1.5;

                ModoLog = LogMode.Month;
            }
            else if (State == State.DataLoaded)
            {
                AddDataSeries(BarsPeriodType.Minute, 60);
                AddDataSeries(BarsPeriodType.Minute, 120);
                AddDataSeries(BarsPeriodType.Day, 1);

                ema20 = EMA(20);
                sma200 = SMA(200);
                atr14 = ATR(14);
                atrSma50 = SMA(atr14, 50);
                volSma20 = SMA(Volume, 20);

                ema20_1h = EMA(BarsArray[1], 20);
                sma200_1h = SMA(BarsArray[1], 200);
                ema20_2h = EMA(BarsArray[2], 20);
                sma200_2h = SMA(BarsArray[2], 200);
                ema20_d = EMA(BarsArray[3], 20);
                sma200_d = SMA(BarsArray[3], 200);

                ema20.Plots[0].Brush = Brushes.DodgerBlue;
                ema20.Plots[0].Width = 2;
                sma200.Plots[0].Brush = Brushes.Red;
                sma200.Plots[0].Width = 2;

                AddChartIndicator(ema20);
                AddChartIndicator(sma200);

                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastResetDate = DateTime.MinValue;
                tradeLog = new List<string>();
                lastAction = "";
                lastDecision = "WAITING";

                try { Directory.CreateDirectory(@"C:\temp"); } catch {}

                botHistoryDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"perficient\AI path lean\trading 7\bot\history");
                if (ModoLog != LogMode.Off)
                {
                    string logDir = botHistoryDir;
                    if (ModoLog == LogMode.Day)
                    {
                        botDayDir = Path.Combine(botHistoryDir, "history-day");
                        logDir = botDayDir;
                    }
                    try { Directory.CreateDirectory(logDir); } catch {}

                    csvLogPath = Path.Combine(logDir, "oliver_trades_log.csv");
                    csvDailyPath = Path.Combine(logDir, "oliver_daily_log.csv");
                    csvBarLogPath = Path.Combine(logDir, "oliver_bar_log.csv");
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
            if (BarsInProgress != 0) return;
            if (CurrentBar < BarsRequiredToTrade)
                return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);
            DateTime nyDate = nyNow.Date;

            LogBarCsv(nyNow);

            // Day reset
            if (nyDate != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                {
                    string reason = dayDone
                        ? (dailyPnL <= -MaxDailyLoss ? "MaxLoss" : dailyPnL >= DailyProfitTarget ? "ProfitTarget" : "MaxTrades")
                        : "SessionEnd";
                    LogDailyCsv(lastResetDate, reason);
                }
                ResetDaily();
                lastResetDate = nyDate;
                if (ModoLog == LogMode.Day)
                    InitCsvLogs();
            }

            // Day done — flatten and stop
            if (dayDone)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenPosition("DayDone");
                lastDecision = "DAY_DONE";
                WriteTelemetry(nyNow);
                return;
            }

            // Session hours: 09:32 - 15:50 NY
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

            // Wait for 09:32 bar
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

            // Paso 0: Day limits
            if (!CheckDayLimits(nyNow))
            {
                WriteTelemetry(nyNow);
                return;
            }

            // If in a trade, manage it (Paso 7)
            if (tradeState != TradeState.Flat)
            {
                ManageTrade(nyNow);
                WriteTelemetry(nyNow);
                return;
            }

            // Evaluate new entry (Pasos 1-6)
            EvaluateEntry(nyNow);
            WriteTelemetry(nyNow);
        }

        #endregion

        #region Paso 0 — Day Limits

        private bool CheckDayLimits(DateTime nyNow)
        {
            if (dailyPnL <= -MaxDailyLoss)
            {
                dayDone = true;
                lastDecision = "STOP_MAX_LOSS";
                return false;
            }
            if (dailyPnL >= DailyProfitTarget)
            {
                dayDone = true;
                lastDecision = "STOP_PROFIT_TARGET";
                return false;
            }
            if (tradesToday >= MaxTradesPerDay)
            {
                dayDone = true;
                lastDecision = "STOP_MAX_TRADES";
                return false;
            }

            // After 15:30 — only manage open trades, no new entries
            bool lateSession = nyNow.Hour == 15 && nyNow.Minute >= 30;
            if (lateSession && tradeState == TradeState.Flat)
            {
                lastDecision = "LATE_SESSION_NO_NEW";
                return false;
            }

            return true;
        }

        #endregion

        #region Pasos 1-6 — Entry Evaluation

        private void EvaluateEntry(DateTime nyNow)
        {
            // Paso 1: SOH filter (hard block — no confidence can override)
            if (!CheckSOH())
                return;

            // Paso 2: Direction
            int direction = GetDirection();
            if (direction == 0)
            {
                lastDecision = "NO_DIRECTION";
                return;
            }

            // Paso 3: Movement phase (now soft — feeds into confidence)
            string movPhase = GetMovementPhase();

            // Paso 4: Day phase (hard blocks remain)
            int dayPhase = GetDayPhase(nyNow);
            if (dayPhase == 2)
            {
                lastDecision = "FASE2_SOH";
                return;
            }
            if (dayPhase == 1 && tradesPhase1 >= 2)
            {
                lastDecision = "FASE1_MAX_2";
                return;
            }
            if (dayPhase == 3 && tradesPhase3 >= 1)
            {
                lastDecision = "FASE3_MAX_1";
                return;
            }

            // Paso 5: Detonante or RBI/GBI
            string entryType = CheckDetonante(direction);
            if (entryType == "NONE")
            {
                lastDecision = string.Format("NO_DETONANTE dir={0} bodyPct={1:F2} rangoATR={2:F2}",
                    direction == 1 ? "LONG" : "SHORT",
                    GetBodyPct(), GetRangoATR());
                return;
            }

            // Paso 5.5: Confidence score
            double confidence = CalcConfidence(direction, movPhase);
            lastConfidence = confidence;

            if (confidence < MinConfidence)
            {
                lastDecision = string.Format("LOW_CONF {0:F0}% < {1}% dir={2} movPhase={3}",
                    confidence, MinConfidence,
                    direction == 1 ? "LONG" : "SHORT", movPhase);
                return;
            }

            // Paso 6: Enter trade
            double currentATR = atr14[0];
            double initialStop;
            if (direction == 1)
                initialStop = Close[0] - (Stop_AtrMult * currentATR);
            else
                initialStop = Close[0] + (Stop_AtrMult * currentATR);

            double potentialLoss = Math.Abs(Close[0] - initialStop) * 2;
            if (dailyPnL - potentialLoss < -MaxDailyLoss)
            {
                lastDecision = string.Format("BLOCKED_RISK potLoss={0:F2}", potentialLoss);
                return;
            }

            string signal = direction == 1 ? "OliverL" : "OliverS";
            EnterNewTrade(direction, initialStop, entryType, signal, dayPhase);

            lastDecision = string.Format("ENTER {0} {1} conf={2:F0}% phase={3} mov={4}",
                entryType, direction == 1 ? "LONG" : "SHORT",
                confidence, dayPhase, movPhase);
        }

        #endregion

        #region Paso 1 — SOH

        private bool CheckSOH()
        {
            if (atrSma50[0] <= 0) return true;

            double atrRatio = atr14[0] / atrSma50[0];
            double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);

            bool lowATR = atrRatio < SOH_AtrRatio;
            bool flatEMA = emaSlope < SOH_EmaSlopeMin;

            if (lowATR && flatEMA)
            {
                if (CheckBreakoutFromSOH(atrRatio))
                {
                    isSOH = false;
                    return true;
                }

                isSOH = true;
                lastDecision = string.Format("SOH atrR={0:F2} slope={1:F1}", atrRatio, emaSlope);
                return false;
            }

            isSOH = false;
            return true;
        }

        private bool CheckBreakoutFromSOH(double atrRatio)
        {
            if (atrRatio < 1.0) return false;

            int lookback = Math.Min(20, CurrentBar);
            double recentHigh = double.MinValue;
            double recentLow = double.MaxValue;
            for (int i = 1; i <= lookback; i++)
            {
                if (High[i] > recentHigh) recentHigh = High[i];
                if (Low[i] < recentLow) recentLow = Low[i];
            }

            return Close[0] > recentHigh || Close[0] < recentLow;
        }

        #endregion

        #region Paso 2 — Direction

        private int GetDirection()
        {
            double price = Close[0];
            double ema = ema20[0];

            if (price > ema) return 1;
            if (price < ema) return -1;
            return 0;
        }

        #endregion

        #region Paso 3 — Movement Phase

        private string GetMovementPhase()
        {
            if (atr14[0] <= 0) return "TEMPRANA";

            double spread = Math.Abs(ema20[0] - sma200[0]);
            double spreadNorm = spread / atr14[0];

            if (spreadNorm > FaseMadura_SpreadATR) return "MADURA";
            if (spreadNorm > 1.5) return "MEDIA";
            return "TEMPRANA";
        }

        #endregion

        #region Paso 4 — Day Phase

        private int GetDayPhase(DateTime nyNow)
        {
            int hour = nyNow.Hour;
            int min = nyNow.Minute;
            double timeDecimal = hour + min / 60.0;

            if (timeDecimal < 11.25) return 1;
            if (timeDecimal < 14.25) return 2;
            return 3;
        }

        #endregion

        #region Paso 5 — Detonante

        private string CheckDetonante(int direction)
        {
            double bodyPct = GetBodyPct();
            double rangoATR = GetRangoATR();

            bool isDetonante = bodyPct >= Detonante_BodyPct && rangoATR >= Detonante_RangoATR;

            bool candleMatchesDir = (direction == 1 && Close[0] > Open[0])
                                 || (direction == -1 && Close[0] < Open[0]);

            if (isDetonante && candleMatchesDir)
                return "DETONANTE";

            if (CurrentBar < 2) return "NONE";

            if (direction == 1 && Close[0] > High[1] && Close[1] < Open[1])
                return "RBI";

            if (direction == -1 && Close[0] < Low[1] && Close[1] > Open[1])
                return "GBI";

            return "NONE";
        }

        private double GetBodyPct()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            return Math.Abs(Close[0] - Open[0]) / range;
        }

        private double GetRangoATR()
        {
            if (atr14[0] <= 0) return 0;
            return (High[0] - Low[0]) / atr14[0];
        }

        #endregion

        #region Paso 5.5 — Confidence Score

        private double CalcConfidence(int direction, string movPhase)
        {
            double conf = 40;

            // +10% EMA20 1H confirms direction
            if (BarsArray[1] != null && BarsArray[1].Count > 1)
            {
                bool h1Confirm = (direction == 1 && Closes[1][0] > ema20_1h[0])
                              || (direction == -1 && Closes[1][0] < ema20_1h[0]);
                if (h1Confirm) conf += 10;
            }

            // +12% EMA20 2H confirms direction
            if (BarsArray[2] != null && BarsArray[2].Count > 1)
            {
                bool h2Confirm = (direction == 1 && Closes[2][0] > ema20_2h[0])
                              || (direction == -1 && Closes[2][0] < ema20_2h[0]);
                if (h2Confirm) conf += 12;
            }

            // +15% EMA20 Daily confirms direction
            if (BarsArray[3] != null && BarsArray[3].Count > 1)
            {
                bool dConfirm = (direction == 1 && Closes[3][0] > ema20_d[0])
                             || (direction == -1 && Closes[3][0] < ema20_d[0]);
                if (dConfirm) conf += 15;
            }

            // +8% Volume > 2x average (institutional participation)
            if (volSma20[0] > 0 && Volume[0] > 2 * volSma20[0])
                conf += 8;

            // +8% Movement phase TEMPRANA (early move)
            if (movPhase == "TEMPRANA") conf += 8;

            // +5% Body% > 80% (strong conviction candle)
            if (GetBodyPct() > 0.80) conf += 5;

            // +5% Range/ATR > 1.5 (elephant bar)
            if (GetRangoATR() > 1.5) conf += 5;

            // +7% Bonus: all 3 higher TFs aligned
            bool allAligned = false;
            if (BarsArray[1] != null && BarsArray[1].Count > 1
                && BarsArray[2] != null && BarsArray[2].Count > 1
                && BarsArray[3] != null && BarsArray[3].Count > 1)
            {
                bool h1 = (direction == 1 && Closes[1][0] > ema20_1h[0])
                        || (direction == -1 && Closes[1][0] < ema20_1h[0]);
                bool h2 = (direction == 1 && Closes[2][0] > ema20_2h[0])
                        || (direction == -1 && Closes[2][0] < ema20_2h[0]);
                bool d  = (direction == 1 && Closes[3][0] > ema20_d[0])
                        || (direction == -1 && Closes[3][0] < ema20_d[0]);
                if (h1 && h2 && d) { conf += 7; allAligned = true; }
            }

            // -15% FASE_MADURA without multi-TF support
            if (movPhase == "MADURA" && !allAligned) conf -= 15;

            return Math.Min(conf, 95);
        }

        #endregion

        #region Paso 6 — Enter Trade

        private void EnterNewTrade(int direction, double initialStop, string entryType, string signal, int dayPhase)
        {
            entryPrice = Close[0];
            stopPrice = initialStop;
            tradeDirection = direction;
            tradeState = TradeState.Breathe;
            breatheCount = 0;
            breakEvenHit = false;
            tradesToday++;
            activeEntrySignal = signal;

            if (dayPhase == 1) tradesPhase1++;
            if (dayPhase == 3) tradesPhase3++;

            SetStopLoss(signal, CalculationMode.Price, initialStop, false);

            if (direction == 1)
                EnterLong(1, signal);
            else
                EnterShort(1, signal);

            Draw.ArrowUp(this, "Entry" + CurrentBar, true, 0,
                direction == 1 ? Low[0] - 5 : High[0] + 5,
                direction == 1 ? Brushes.Lime : Brushes.Red);
        }

        #endregion

        #region Paso 7 — Trade Management

        private void ManageTrade(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                tradeState = TradeState.Flat;
                tradeDirection = 0;
                return;
            }

            if (tradeState == TradeState.Breathe)
            {
                ManageBreathe();
                return;
            }

            if (tradeState == TradeState.Trailing)
            {
                ManageTrailing();
                return;
            }
        }

        private void ManageBreathe()
        {
            breatheCount++;

            bool whipsaw = false;
            if (tradeDirection == 1 && Low[0] <= stopPrice) whipsaw = true;
            if (tradeDirection == -1 && High[0] >= stopPrice) whipsaw = true;

            if (whipsaw)
            {
                FlattenPosition("BreatheWhipsaw");
                lastDecision = string.Format("WHIPSAW breathe bar={0}", breatheCount);
                return;
            }

            if (breatheCount >= 2)
            {
                tradeState = TradeState.Trailing;
                lastDecision = "BREATHE_DONE -> TRAILING";
                return;
            }

            lastDecision = string.Format("BREATHE {0}/2", breatheCount);
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
            double buffer = Trail_AtrBuffer * currentATR;

            if (tradeDirection == 1)
            {
                double low2bar = Math.Min(Low[0], Low[1]);
                double newTrail = low2bar - buffer;
                if (newTrail > stopPrice)
                    stopPrice = newTrail;
            }
            else
            {
                double high2bar = Math.Max(High[0], High[1]);
                double newTrail = high2bar + buffer;
                if (newTrail < stopPrice)
                    stopPrice = newTrail;
            }

            if (!breakEvenHit)
            {
                double unrealizedPts = tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0];
                double beThreshold = Breakeven_AtrMult * currentATR;

                if (unrealizedPts >= beThreshold)
                {
                    double beBuffer = 0.3 * currentATR;
                    double beLevel = tradeDirection == 1 ? entryPrice + beBuffer : entryPrice - beBuffer;

                    if ((tradeDirection == 1 && beLevel > stopPrice) || (tradeDirection == -1 && beLevel < stopPrice))
                    {
                        stopPrice = beLevel;
                        breakEvenHit = true;
                    }
                }
            }

            double unrealizedPnL = (tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0]) * 2;
            if (dailyPnL + unrealizedPnL >= DailyProfitTarget)
            {
                FlattenPosition("ProfitTarget");
                dayDone = true;
                lastDecision = "EXIT ProfitTarget";
                return;
            }

            lastDecision = string.Format("TRAILING stop={0:F2} unrealized={1:F1}pts",
                stopPrice, tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0]);
        }

        #endregion

        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;
            if (!pnlTrackingStarted) return;

            string orderName = execution.Order.Name;
            bool isEntry = orderName == "OliverL" || orderName == "OliverS";
            bool isExit = orderName.StartsWith("X_") || orderName == "Stop loss";

            if (isExit)
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
                totalPnL += realPnL;

                tradeLog.Add(string.Format("EXIT {0} | reason={1} | entry={2:F2} exit={3:F2} pnl=${4:F2} | dailyPnL=${5:F2}",
                    dir, lastClosedReason, realEntryPrice, realExitPrice, realPnL, dailyPnL));
                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}", dir, lastClosedReason, realExitPrice, realPnL);

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", realEntryPrice, realExitPrice, realPnL, lastClosedReason);

                if (dailyPnL <= -MaxDailyLoss || dailyPnL >= DailyProfitTarget)
                    dayDone = true;
            }
            else if (isEntry)
            {
                realEntryPrice = price;
                entryPrice = price;
                entryTimeNY = TimeZoneInfo.ConvertTime(time, easternZone);
                string dir = marketPosition == MarketPosition.Long ? "LONG" : "SHORT";
                lastAction = string.Format("FILL {0} @{1:F2} stop={2:F2}", dir, price, stopPrice);
                tradeLog.Add(string.Format("FILL {0} | price={1:F2} stop={2:F2} | ema20={3:F2} sma200={4:F2} atr={5:F2}",
                    dir, price, stopPrice, ema20[0], sma200[0], atr14[0]));

                LogTradeCsv(entryTimeNY, "ENTRY", price, 0, 0, "");
            }
        }

        #endregion

        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            tradesPhase1 = 0;
            tradesPhase3 = 0;
            dayDone = false;
            sessionStarted = false;
            tradeState = TradeState.Flat;
            tradeDirection = 0;
            lastDecision = "NEW_DAY";
            isSOH = false;
            breakEvenHit = false;
        }

        private void InitCsvLogs()
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                bool overwrite = ModoLog == LogMode.Day;
                if (overwrite || !File.Exists(csvLogPath))
                    File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryType,EntryPrice,ExitPrice,StopPrice,EMA20,SMA200,Spread,ATR,AtrRatio,EmaSlope,BodyPct,RangoATR,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday,DayPhase,MovPhase,Confidence\n");
                if (overwrite || !File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,DayDoneReason\n");
                if (overwrite || !File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,EMA20,SMA200,Spread,ATR,AtrRatio,EmaSlope,PriceVsEMA20,PriceVsSMA200,Position,TradeState,DailyPnL,TotalPnL,TradesToday,SOH,Decision,Confidence\n");
            }
            catch {}
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

        private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string dir = tradeDirection != 0
                    ? (tradeDirection == 1 ? "LONG" : "SHORT")
                    : (lastClosedDirection == 1 ? "LONG" : "SHORT");
                double atrRatio = atrSma50[0] > 0 ? atr14[0] / atrSma50[0] : 0;
                double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1},{2},{3},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12:F1},{13:F2},{14:F2},{15:F2},{16:F2},{17:F2},{18},{19},{20},{21},{22:F0}\n",
                    nyNow, action, dir, "", fillPrice, exitPrice, stopPrice,
                    ema20[0], sma200[0], Math.Abs(ema20[0] - sma200[0]), atr14[0],
                    atrRatio, emaSlope, GetBodyPct(), GetRangoATR(),
                    pnl, dailyPnL, totalPnL, exitReason, tradesToday,
                    GetDayPhase(nyNow), GetMovementPhase(), lastConfidence);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime nyDate, string reason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4}\n",
                    nyDate, dailyPnL, totalPnL, tradesToday, reason);
                File.AppendAllText(csvDailyPath, line);
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
                string vsEma = Close[0] >= ema20[0] ? "ENCIMA" : "DEBAJO";
                string vsSma = Close[0] >= sma200[0] ? "ENCIMA" : "DEBAJO";
                double atrRatio = atrSma50[0] > 0 ? atr14[0] / atrSma50[0] : 0;
                double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F1},{12},{13},{14},{15},{16:F2},{17:F2},{18},{19},{20},{21:F0}\n",
                    nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
                    ema20[0], sma200[0], Math.Abs(ema20[0] - sma200[0]), atr14[0],
                    atrRatio, emaSlope, vsEma, vsSma, pos, state,
                    dailyPnL, totalPnL, tradesToday, isSOH ? "YES" : "NO", lastDecision, lastConfidence);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        private void WriteTelemetry(DateTime nyNow)
        {
            try
            {
                double spread = Math.Abs(ema20[0] - sma200[0]);
                string pos = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : "FLAT";
                double unrealizedPts = 0;
                if (tradeDirection == 1) unrealizedPts = Close[0] - entryPrice;
                else if (tradeDirection == -1) unrealizedPts = entryPrice - Close[0];
                double atrRatio = atrSma50[0] > 0 ? atr14[0] / atrSma50[0] : 0;
                double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);

                string recentTrades = "";
                int start = Math.Max(0, tradeLog.Count - 10);
                for (int i = start; i < tradeLog.Count; i++)
                    recentTrades += (i > start ? "," : "") + "\"" + tradeLog[i].Replace("\"", "'") + "\"";

                string json = string.Format(
@"{{
  ""timestamp"": ""{0:yyyy-MM-dd HH:mm}"",
  ""strategy"": ""MNQOliver"",
  ""price"": {1:F2},
  ""ema20"": {2:F2},
  ""sma200"": {3:F2},
  ""spread"": {4:F2},
  ""atr"": {5:F2},
  ""atrRatio"": {6:F2},
  ""emaSlope"": {7:F1},
  ""position"": ""{8}"",
  ""tradeState"": ""{9}"",
  ""decision"": ""{10}"",
  ""entryPrice"": {11:F2},
  ""stopPrice"": {12:F2},
  ""unrealizedPts"": {13:F2},
  ""unrealizedPnL"": {14:F2},
  ""dailyPnL"": {15:F2},
  ""totalPnL"": {16:F2},
  ""tradesToday"": {17},
  ""dayPhase"": {18},
  ""movementPhase"": ""{19}"",
  ""isSOH"": {20},
  ""dayDone"": {21},
  ""confidence"": {22:F0},
  ""lastAction"": ""{23}"",
  ""recentTrades"": [{24}]
}}",
                    nyNow, Close[0], ema20[0], sma200[0], spread, atr14[0],
                    atrRatio, emaSlope,
                    pos, tradeState, lastDecision,
                    entryPrice, stopPrice, unrealizedPts, unrealizedPts * 2,
                    dailyPnL, totalPnL, tradesToday,
                    GetDayPhase(nyNow), GetMovementPhase(),
                    isSOH ? "true" : "false", dayDone ? "true" : "false",
                    lastConfidence,
                    lastAction.Replace("\"", "'"), recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }

        #endregion
    }
}
