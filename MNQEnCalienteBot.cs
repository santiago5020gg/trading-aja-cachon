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
        #region Parameters

        [NinjaScriptProperty]
        [Display(Name = "Spread Tendencia (pts)", GroupName = "1. Estrategia", Order = 1)]
        public double SpreadTendencia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Re-entry Extension (pts)", GroupName = "1. Estrategia", Order = 6)]
        public double MaxReEntryExtension { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Spread Rango (pts)", GroupName = "1. Estrategia", Order = 2)]
        public double SpreadRango { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Body Min % Entrada", GroupName = "1. Estrategia", Order = 3)]
        public double BodyMinPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breathe Bars", GroupName = "1. Estrategia", Order = 4)]
        public int BreatheBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Breakeven (pts)", GroupName = "2. Riesgo", Order = 1)]
        public double BreakevenPts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (pts)", GroupName = "2. Riesgo", Order = 2)]
        public double MaxStopPts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Daily Loss ($)", GroupName = "2. Riesgo", Order = 3)]
        public double MaxDailyLoss { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Breathe Whipsaws/Dia", GroupName = "2. Riesgo", Order = 4)]
        public int MaxBreatheWhipsaws { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Pullback Bars", GroupName = "1. Estrategia", Order = 5)]
        public int MaxPullbackBars { get; set; }

        #endregion

        #region Variables

        private SMA sma20;
        private SMA sma200;
        private ATR atr14;

        private enum EntryType { None, Tendencia, Pullback, Ruptura }
        private enum TradeState { Flat, Breathe, Trailing }
        private enum MarketMode { Waiting, Tendencia, Rango, PullbackWatch }

        private TradeState tradeState;
        private MarketMode marketMode;
        private EntryType lastEntryType;
        private int tradeDirection; // 1=LONG, -1=SHORT, 0=flat
        private double entryPrice;
        private double stopPrice;
        private int breatheCount;
        private double runningExtreme; // HH for LONG, LL for SHORT
        private bool breakEvenHit;

        private double dailyPnL;
        private double myTotalPnL;
        private int tradesToday;
        private int breatheWhipsawsToday;
        private bool dayDone;
        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;

        // 09:32 bar tracking
        private bool evaluated0932;
        private double bar0932Open, bar0932Close, bar0932High, bar0932Low;

        // Pullback tracking
        private int barsAboveSMA20;
        private int barsBelowSMA20;
        private bool smasCrossed;
        private double spreadAtCross;

        // Ruptura tracking
        private bool rupturaAttempted;
        private int sma20CrossCount;
        private bool rupturaPending;
        private int rupturaPendingDir;
        private double rupturaPendingStop;
        private int barsSinceRupturaFail;
        private int rupturaCooldownBars = 10;
        private int rupturaAttemptsToday;
        private int maxRupturaAttempts = 3;

        // Re-entry tracking
        private bool lastExitByTrailing;
        private int pullbackBarCount;
        private int reEntriesThisDirection;
        private int maxReEntriesPerDirection = 2;
        private int barsSinceExit;
        private int lastTrailDirection;

        // Execution tracking (real fills from NinjaTrader)
        private double realEntryPrice;
        private int lastClosedDirection;
        private string lastClosedReason;
        private bool pnlTrackingStarted;
        private string activeEntrySignal;

        // Telemetry
        private string telemetryPath = @"C:\temp\mnq_bot_status.json";
        private List<string> tradeLog;
        private string lastAction;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ En Caliente — Estrategia probabilistica con SMA20/SMA200, trailing bar-a-bar";
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

                SpreadTendencia = 70;
                MaxReEntryExtension = 50;
                SpreadRango = 30;
                BodyMinPct = 60;
                BreatheBars = 3;
                BreakevenPts = 67.5;
                MaxStopPts = 67.5;
                MaxDailyLoss = 270;
                MaxBreatheWhipsaws = 2;
                MaxPullbackBars = 3;
            }
            else if (State == State.DataLoaded)
            {
                sma20 = SMA(20);
                sma200 = SMA(200);
                atr14 = ATR(14);

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

                try { Directory.CreateDirectory(@"C:\temp"); } catch {}
            }
            else if (State == State.Realtime)
            {
                // In live mode, start tracking immediately
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

            if (nyDate != lastResetDate)
            {
                ResetDaily();
                lastResetDate = nyDate;
            }

            if (dayDone)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenPosition("DayDone");
                WriteTelemetry(nyNow);
                return;
            }

            double spread = sma20[0] - sma200[0];
            double absSpread = Math.Abs(spread);
            bool priceAboveSMA20 = Close[0] > sma20[0];
            bool priceAboveSMA200 = Close[0] > sma200[0];
            bool priceBelowSMA20 = Close[0] < sma20[0];
            bool priceBelowSMA200 = Close[0] < sma200[0];

            // Outside regular session: flatten and skip
            bool beforeSession = nyNow.Hour < 9 || (nyNow.Hour == 9 && nyNow.Minute < 30);
            bool afterSession = nyNow.Hour >= 16 || (nyNow.Hour == 15 && nyNow.Minute >= 55);
            if (beforeSession || afterSession)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenPosition("OutOfSession");
                WriteTelemetry(nyNow);
                return;
            }

            // --- MANAGE OPEN POSITION ---
            if (tradeState != TradeState.Flat)
            {
                ManageOpenTrade(nyNow);
                WriteTelemetry(nyNow);
                return;
            }

            // --- NO POSITION: EVALUATE ENTRIES ---

            // FASE 1: Evaluate 09:32 bar
            if (!evaluated0932 && nyNow.Hour == 9 && nyNow.Minute == 32)
            {
                Evaluate0932(absSpread, priceAboveSMA20, priceAboveSMA200, priceBelowSMA20, priceBelowSMA200);
                WriteTelemetry(nyNow);
                return;
            }

            // Post 09:32: monitor for entries based on marketMode
            if (!evaluated0932)
            {
                WriteTelemetry(nyNow);
                return;
            }

            // Track bars since last exit (for re-entry cooldown)
            if (lastExitByTrailing)
                barsSinceExit++;

            switch (marketMode)
            {
                case MarketMode.Tendencia:
                    // Already entered at 09:32 or no more entry needed
                    break;

                case MarketMode.PullbackWatch:
                    EvaluatePullback(absSpread, priceAboveSMA20, priceBelowSMA20);
                    break;

                case MarketMode.Rango:
                    EvaluateRuptura(absSpread, priceAboveSMA20, priceAboveSMA200, priceBelowSMA20, priceBelowSMA200);
                    // Bug #3: transition to PullbackWatch when spread exits range
                    if (absSpread >= SpreadRango && smasCrossed)
                        marketMode = MarketMode.PullbackWatch;
                    // Bug #8: track SMA crosses from Rango mode
                    CheckSMACross(spread);
                    break;

                case MarketMode.Waiting:
                    // Check if conditions evolved to pullback or range
                    if (absSpread < SpreadRango)
                        marketMode = MarketMode.Rango;
                    else if (smasCrossed && absSpread > SpreadRango)
                        marketMode = MarketMode.PullbackWatch;
                    CheckSMACross(spread);
                    break;
            }

            // Always check if spread converges to range territory
            if (marketMode != MarketMode.Rango && absSpread < SpreadRango)
                marketMode = MarketMode.Rango;

            WriteTelemetry(nyNow);
        }

        #endregion

        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;

            string orderName = execution.Order.Name;
            bool isOurEntry = orderName.StartsWith("Tend") || orderName.StartsWith("Rupt") || orderName.StartsWith("Pull");
            bool isOurExit = orderName.StartsWith("X_");

            if (!pnlTrackingStarted) return;

            if (isOurExit)
            {
                double realExitPrice = price;
                double realPnL = 0;
                string dir = lastClosedDirection == 1 ? "LONG" : "SHORT";

                if (lastClosedDirection == 1)
                    realPnL = (realExitPrice - realEntryPrice) * 2;
                else if (lastClosedDirection == -1)
                    realPnL = (realEntryPrice - realExitPrice) * 2;

                dailyPnL += realPnL;
                myTotalPnL += realPnL;

                tradeLog.Add(string.Format("EXIT {0} | reason={1} | entry={2:F2} exit={3:F2} pnl=${4:F2} | dailyPnL=${5:F2} | totalPnL=${6:F2}",
                    dir, lastClosedReason, realEntryPrice, realExitPrice, realPnL, dailyPnL, myTotalPnL));
                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2} total=${4:F2}", dir, lastClosedReason, realExitPrice, realPnL, myTotalPnL);

                if (dailyPnL <= -MaxDailyLoss)
                    dayDone = true;
            }
            else if (isOurEntry)
            {
                realEntryPrice = price;
                entryPrice = price;
                string dir = marketPosition == MarketPosition.Long ? "LONG" : "SHORT";
                lastAction = string.Format("FILL {0} @{1:F2} stop={2:F2}", dir, price, stopPrice);
                tradeLog.Add(string.Format("FILL {0} | price={1:F2} stop={2:F2} | sma20={3:F2} sma200={4:F2} spread={5:F2}",
                    dir, price, stopPrice, sma20[0], sma200[0], Math.Abs(sma20[0] - sma200[0])));
            }
        }

        #endregion

        #region FASE 1: Evaluate 09:32

        private void Evaluate0932(double absSpread, bool aboveSMA20, bool aboveSMA200, bool belowSMA20, bool belowSMA200)
        {
            evaluated0932 = true;
            pnlTrackingStarted = true;

            bar0932Open = Open[0];
            bar0932Close = Close[0];
            bar0932High = High[0];
            bar0932Low = Low[0];

            double barRange = bar0932High - bar0932Low;
            if (barRange <= 0) { marketMode = MarketMode.Waiting; return; }

            double bodySize = Math.Abs(bar0932Close - bar0932Open);
            double bodyPct = (bodySize / barRange) * 100;
            bool isGreen = bar0932Close > bar0932Open;

            // Decision tree
            if (absSpread >= SpreadTendencia && bodyPct >= BodyMinPct)
            {
                // Tendencia 09:32
                if (isGreen && aboveSMA20 && aboveSMA200)
                {
                    EnterTrade(1, Close[0], Low[0], EntryType.Tendencia, "Tend0932L");
                    return;
                }
                if (!isGreen && belowSMA20 && belowSMA200)
                {
                    EnterTrade(-1, Close[0], High[0], EntryType.Tendencia, "Tend0932S");
                    return;
                }
            }

            if (absSpread < SpreadRango)
            {
                // Check for immediate ruptura
                if (bodyPct >= BodyMinPct && ((aboveSMA20 && aboveSMA200) || (belowSMA20 && belowSMA200)))
                {
                    if (aboveSMA20 && aboveSMA200)
                        EnterTrade(1, Close[0], Low[0], EntryType.Ruptura, "Rupt0932L");
                    else
                        EnterTrade(-1, Close[0], High[0], EntryType.Ruptura, "Rupt0932S");
                    return;
                }
                marketMode = MarketMode.Rango;
                return;
            }

            // No immediate entry — watch for pullback or wait
            if (absSpread >= SpreadRango)
                marketMode = MarketMode.Waiting;

            // Track SMA cross state
            smasCrossed = (sma20[0] > sma200[0]) != (sma20[1] > sma200[1]);
        }

        #endregion

        #region FASE 3A: Pullback to SMA20

        private void EvaluatePullback(double absSpread, bool aboveSMA20, bool belowSMA20)
        {
            if (breatheWhipsawsToday >= MaxBreatheWhipsaws) return;
            if (reEntriesThisDirection >= maxReEntriesPerDirection) return;

            bool bullish = sma20[0] > sma200[0];

            if (bullish)
            {
                if (belowSMA20 || (Close[0] <= sma20[0] + 5))
                    pullbackBarCount++;
                else if (pullbackBarCount == 0)
                    pullbackBarCount = 0;

                // Bounce: price was near/below SMA20 and now closes above
                if (pullbackBarCount >= 1 && pullbackBarCount <= MaxPullbackBars && aboveSMA20 && Close[0] > sma20[0])
                {
                    if (absSpread > SpreadRango)
                    {
                        EnterTrade(1, Close[0], Low[0], EntryType.Pullback, "PullL");
                        pullbackBarCount = 0;
                        if (lastTrailDirection == 1) reEntriesThisDirection++;
                    }
                }
                if (pullbackBarCount > MaxPullbackBars)
                    pullbackBarCount = 0;
            }
            else
            {
                if (aboveSMA20 || (Close[0] >= sma20[0] - 5))
                    pullbackBarCount++;
                else if (pullbackBarCount == 0)
                    pullbackBarCount = 0;

                if (pullbackBarCount >= 1 && pullbackBarCount <= MaxPullbackBars && belowSMA20 && Close[0] < sma20[0])
                {
                    if (absSpread > SpreadRango)
                    {
                        EnterTrade(-1, Close[0], High[0], EntryType.Pullback, "PullS");
                        pullbackBarCount = 0;
                        if (lastTrailDirection == -1) reEntriesThisDirection++;
                    }
                }
                if (pullbackBarCount > MaxPullbackBars)
                    pullbackBarCount = 0;
            }

            CheckSMACross(sma20[0] - sma200[0]);
        }

        #endregion

        #region FASE 3B: Range Breakout

        private void EvaluateRuptura(double absSpread, bool aboveSMA20, bool aboveSMA200, bool belowSMA20, bool belowSMA200)
        {
            if (breatheWhipsawsToday >= MaxBreatheWhipsaws) return;
            // Bug #7: limit consecutive failed rupturas
            if (rupturaAttemptsToday >= maxRupturaAttempts) return;

            barsSinceRupturaFail++;

            // Step 2: confirmation bar — check if pending ruptura confirms
            if (rupturaPending)
            {
                // Bug #6: re-verify spread is still in range territory
                bool spreadStillRange = absSpread < SpreadRango;
                bool confirmed = false;
                if (rupturaPendingDir == 1 && aboveSMA20 && aboveSMA200 && spreadStillRange)
                    confirmed = true;
                if (rupturaPendingDir == -1 && belowSMA20 && belowSMA200 && spreadStillRange)
                    confirmed = true;

                if (confirmed)
                {
                    rupturaAttemptsToday++;
                    string signal = rupturaPendingDir == 1 ? "RuptL" : "RuptS";
                    EnterTrade(rupturaPendingDir, Close[0], rupturaPendingStop, EntryType.Ruptura, signal);
                }
                rupturaPending = false;
                return;
            }

            if (absSpread >= SpreadRango) return;
            if (barsSinceRupturaFail < rupturaCooldownBars) return;

            double barRange = High[0] - Low[0];
            if (barRange <= 0) return;

            double bodySize = Math.Abs(Close[0] - Open[0]);
            double bodyPct = (bodySize / barRange) * 100;

            // Bug #2: verify actual price cross — previous bar must have been on opposite side
            bool prevAboveSMA20 = Close[1] > sma20[1];
            bool prevAboveSMA200 = Close[1] > sma200[1];
            bool prevBelowSMA20 = Close[1] < sma20[1];
            bool prevBelowSMA200 = Close[1] < sma200[1];

            // Step 1: breakout candle detected — mark pending, wait for confirmation
            if (bodyPct >= BodyMinPct)
            {
                // LONG ruptura: now above both, was below at least one
                if (aboveSMA20 && aboveSMA200 && (prevBelowSMA20 || prevBelowSMA200))
                {
                    rupturaPending = true;
                    rupturaPendingDir = 1;
                    rupturaPendingStop = Low[0];
                    return;
                }
                // SHORT ruptura: now below both, was above at least one
                if (belowSMA20 && belowSMA200 && (prevAboveSMA20 || prevAboveSMA200))
                {
                    rupturaPending = true;
                    rupturaPendingDir = -1;
                    rupturaPendingStop = High[0];
                    return;
                }
            }
        }

        #endregion

        #region Trade Management

        private void EnterTrade(int direction, double price, double initialStop, EntryType type, string signal)
        {
            // Bug #10 fix: ensure stop is on correct side
            if (direction == 1 && initialStop >= price)
                initialStop = Low[0];
            else if (direction == -1 && initialStop <= price)
                initialStop = High[0];

            double stopDist = Math.Abs(price - initialStop);
            if (stopDist > MaxStopPts)
                initialStop = direction == 1 ? price - MaxStopPts : price + MaxStopPts;

            // Bug #9 fix: filter re-entries when price is too extended from SMA20
            if (type == EntryType.Pullback && lastExitByTrailing)
            {
                double distFromSMA20 = Math.Abs(price - sma20[0]);
                if (distFromSMA20 > MaxReEntryExtension)
                    return;
            }

            // Check daily loss limit
            double potentialLoss = Math.Abs(price - initialStop) * 2;
            if (dailyPnL - potentialLoss < -MaxDailyLoss)
                return;

            entryPrice = price;
            stopPrice = initialStop;
            tradeDirection = direction;
            tradeState = TradeState.Breathe;
            breatheCount = 0;
            runningExtreme = direction == 1 ? High[0] : Low[0];
            breakEvenHit = false;
            lastEntryType = type;
            lastExitByTrailing = false;
            tradesToday++;

            // Reset re-entry counter if direction changed
            if (direction != lastTrailDirection)
                reEntriesThisDirection = 0;

            activeEntrySignal = signal;

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
            // If native stop already closed the position intra-bar, skip
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                tradeState = TradeState.Flat;
                tradeDirection = 0;
                return;
            }

            if (tradeState == TradeState.Breathe)
            {
                breatheCount++;

                // Check whipsaw during breathe
                bool whipsaw = false;
                if (tradeDirection == 1 && Low[0] <= stopPrice)
                    whipsaw = true;
                if (tradeDirection == -1 && High[0] >= stopPrice)
                    whipsaw = true;

                if (whipsaw)
                {
                    breatheWhipsawsToday++;
                    if (lastEntryType == EntryType.Ruptura)
                        barsSinceRupturaFail = 0;
                    FlattenPosition("BreatheWhipsaw");

                    if (breatheWhipsawsToday >= MaxBreatheWhipsaws)
                    {
                        dayDone = true;
                        marketMode = MarketMode.Waiting;
                    }
                    else
                    {
                        marketMode = MarketMode.Rango;
                    }
                    return;
                }

                // Track HH/LL during breathe
                if (tradeDirection == 1 && High[0] > runningExtreme)
                    runningExtreme = High[0];
                if (tradeDirection == -1 && Low[0] < runningExtreme)
                    runningExtreme = Low[0];

                if (breatheCount >= BreatheBars)
                    tradeState = TradeState.Trailing;

                return;
            }

            if (tradeState == TradeState.Trailing)
            {
                ManageTrailing();
            }
        }

        private void ManageTrailing()
        {
            // Check stop hit
            if (tradeDirection == 1 && Low[0] <= stopPrice)
            {
                lastExitByTrailing = true;
                FlattenPosition("TrailStop");
                CheckReEntryEligibility();
                return;
            }
            if (tradeDirection == -1 && High[0] >= stopPrice)
            {
                lastExitByTrailing = true;
                FlattenPosition("TrailStop");
                CheckReEntryEligibility();
                return;
            }

            // Bug #4 fix: use ATR-based minimum trailing distance for explosive moves
            double currentATR = atr14[0];
            double minTrailDist = currentATR * 1.5;
            double unrealizedNow = tradeDirection == 1 ? Close[0] - entryPrice : entryPrice - Close[0];

            // LONG trailing: new HH → move stop to that bar's low (but respect min distance)
            if (tradeDirection == 1)
            {
                if (High[0] > runningExtreme)
                {
                    runningExtreme = High[0];
                    double newStop = Low[0];
                    // In explosive moves (profit > 2x ATR), don't trail tighter than 1.5x ATR from extreme
                    if (unrealizedNow > currentATR * 2)
                        newStop = Math.Min(newStop, runningExtreme - minTrailDist);
                    if (newStop > stopPrice)
                        stopPrice = newStop;
                }
            }
            // SHORT trailing: new LL → move stop to that bar's high (but respect min distance)
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

            // Breakeven check
            if (!breakEvenHit)
            {
                double unrealizedPts = tradeDirection == 1
                    ? Close[0] - entryPrice
                    : entryPrice - Close[0];
                if (unrealizedPts >= BreakevenPts)
                {
                    if ((tradeDirection == 1 && entryPrice > stopPrice) ||
                        (tradeDirection == -1 && entryPrice < stopPrice))
                    {
                        stopPrice = entryPrice;
                        breakEvenHit = true;
                    }
                }
            }
        }

        private void CheckReEntryEligibility()
        {
            if (!lastExitByTrailing) return;

            barsSinceExit = 0;
            lastTrailDirection = lastClosedDirection;

            double absSpread = Math.Abs(sma20[0] - sma200[0]);

            // If spread still healthy → stay in pullback watch for re-entry
            if (absSpread > SpreadRango)
                marketMode = MarketMode.PullbackWatch;
            else
                marketMode = MarketMode.Rango;
        }

        #endregion

        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            breatheWhipsawsToday = 0;
            dayDone = false;
            evaluated0932 = false;
            tradeState = TradeState.Flat;
            tradeDirection = 0;
            marketMode = MarketMode.Waiting;
            smasCrossed = false;
            pullbackBarCount = 0;
            rupturaAttempted = false;
            rupturaAttemptsToday = 0;
            sma20CrossCount = 0;
            lastExitByTrailing = false;
            lastEntryType = EntryType.None;
            reEntriesThisDirection = 0;
            barsSinceExit = 0;
            lastTrailDirection = 0;
            rupturaPending = false;
            rupturaPendingDir = 0;
            barsSinceRupturaFail = rupturaCooldownBars;
        }

        private void CheckSMACross(double spread)
        {
            if (CurrentBar < 1) return;
            double prevSpread = sma20[1] - sma200[1];
            if ((spread > 0 && prevSpread <= 0) || (spread < 0 && prevSpread >= 0))
            {
                smasCrossed = true;
                spreadAtCross = Math.Abs(spread);
            }
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
  ""marketMode"": ""{7}"",
  ""entryPrice"": {8:F2},
  ""stopPrice"": {9:F2},
  ""unrealizedPts"": {10:F2},
  ""unrealizedPnL"": {11:F2},
  ""dailyPnL"": {12:F2},
  ""totalPnL"": {13:F2},
  ""tradesToday"": {14},
  ""breatheWhipsaws"": {15},
  ""breatheCount"": {16},
  ""dayDone"": {17},
  ""lastAction"": ""{18}"",
  ""recentTrades"": [{19}]
}}",
                    nyNow, Close[0], sma20[0], sma200[0], absSpread,
                    pos, tradeState, marketMode,
                    entryPrice, stopPrice, unrealizedPts, unrealizedPts * 2,
                    dailyPnL, myTotalPnL, tradesToday, breatheWhipsawsToday, breatheCount,
                    dayDone ? "true" : "false", lastAction.Replace("\"", "'"),
                    recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }

        #endregion
    }
}
