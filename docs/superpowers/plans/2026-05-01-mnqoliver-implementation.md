# MNQOliver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement MNQOliver.cs — a NinjaTrader 8 NinjaScript strategy that replaces MNQEnCalienteBot.cs with a Velez price-action + ATR-normalized approach using 11 parameters instead of 29.

**Architecture:** Single-file NinjaScript strategy (MNQOliver.cs) with a sequential 8-step filter pipeline (CheckDayLimits → CheckSOH → GetDirection → GetMovementPhase → CheckDayPhase → CheckDetonante → EnterTrade → ManageTrade). ATR(14) normalizes all thresholds. EMA(20) replaces SMA(20) for faster direction detection.

**Tech Stack:** C# / NinjaTrader 8 NinjaScript API. Indicators: EMA(20), SMA(200), ATR(14), SMA(ATR,50). CSV logging. JSON telemetry to C:\temp\mnq_bot_status.json.

**Spec:** `docs/superpowers/specs/2026-05-01-mnqoliver-design.md`

**Reference implementation:** `MNQEnCalienteBot.cs` — use for NinjaScript patterns (OnStateChange, OnBarUpdate, OnExecutionUpdate, SetStopLoss, EnterLong/Short, ExitLong/Short, Draw, etc.)

---

### Task 1: Scaffold — Parameters, Indicators, Variables

**Files:**
- Create: `MNQOliver.cs`

This task creates the skeleton file with all NinjaScriptProperty parameters, indicator declarations, state variables, and OnStateChange initialization. No logic yet.

- [ ] **Step 1: Create MNQOliver.cs with using declarations, namespace, class, and parameters**

```csharp
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

        #endregion

        #region Variables

        private EMA ema20;
        private SMA sma200;
        private ATR atr14;
        private SMA atrSma50;

        private enum TradeState { Flat, Breathe, Trailing }

        private TradeState tradeState;
        private int tradeDirection;       // 1=long, -1=short, 0=flat
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
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;
        private DateTime entryTimeNY;

        #endregion
    }
}
```

- [ ] **Step 2: Add OnStateChange with SetDefaults and DataLoaded**

Add inside the class, after the Variables region:

```csharp
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

        // Fixed — never optimize
        MaxDailyLoss = 270;
        DailyProfitTarget = 270;
        MaxTradesPerDay = 3;

        // Walk-forward
        SOH_AtrRatio = 0.80;
        SOH_EmaSlopeMin = 15;
        FaseMadura_SpreadATR = 3.0;
        Detonante_BodyPct = 0.65;
        Detonante_RangoATR = 1.0;
        Stop_AtrMult = 1.5;
        Trail_AtrBuffer = 0.3;
        Breakeven_AtrMult = 1.5;
    }
    else if (State == State.DataLoaded)
    {
        ema20 = EMA(20);
        sma200 = SMA(200);
        atr14 = ATR(14);
        atrSma50 = SMA(atr14, 50);

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
        try { Directory.CreateDirectory(botHistoryDir); } catch {}

        csvLogPath = Path.Combine(botHistoryDir, "oliver_trades_log.csv");
        csvDailyPath = Path.Combine(botHistoryDir, "oliver_daily_log.csv");
        csvBarLogPath = Path.Combine(botHistoryDir, "oliver_bar_log.csv");
        InitCsvLogs();
    }
    else if (State == State.Realtime)
    {
        pnlTrackingStarted = true;
    }
}

#endregion
```

- [ ] **Step 3: Add ResetDaily and InitCsvLogs stubs**

```csharp
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
    try
    {
        if (!File.Exists(csvLogPath))
            File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryType,EntryPrice,ExitPrice,StopPrice,EMA20,SMA200,Spread,ATR,AtrRatio,EmaSlope,BodyPct,RangoATR,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday,DayPhase,MovPhase\n");
        if (!File.Exists(csvDailyPath))
            File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,DayDoneReason\n");
        if (!File.Exists(csvBarLogPath))
            File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,EMA20,SMA200,Spread,ATR,AtrRatio,EmaSlope,PriceVsEMA20,PriceVsSMA200,Position,TradeState,DailyPnL,TotalPnL,TradesToday,SOH,Decision\n");
    }
    catch {}
}

#endregion
```

- [ ] **Step 4: Verify file compiles conceptually — check all declarations are consistent**

Review: all NinjaScriptProperty types match (double for doubles, int for int), all variable names used in ResetDaily() are declared in Variables region, indicator names match NinjaTrader API (EMA, SMA, ATR).

- [ ] **Step 5: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): scaffold — parameters, indicators, variables, lifecycle"
```

---

### Task 2: OnBarUpdate — Main Loop + Paso 0 (CheckDayLimits)

**Files:**
- Modify: `MNQOliver.cs`

This task adds the OnBarUpdate main loop with day reset, session hours enforcement, and daily limits check. Follows the same structure as MNQEnCalienteBot.cs OnBarUpdate but simplified.

- [ ] **Step 1: Add OnBarUpdate with day reset and session enforcement**

Add after the Lifecycle region:

```csharp
#region OnBarUpdate

protected override void OnBarUpdate()
{
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
```

- [ ] **Step 2: Add CheckDayLimits method**

Add in a new region after OnBarUpdate:

```csharp
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
```

- [ ] **Step 3: Add FlattenPosition stub**

Add in Helpers region:

```csharp
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
```

- [ ] **Step 4: Add empty stubs for methods called by OnBarUpdate**

These will be implemented in subsequent tasks. Add in the Helpers region:

```csharp
private void EvaluateEntry(DateTime nyNow)
{
    lastDecision = "EVAL_NOT_IMPLEMENTED";
}

private void ManageTrade(DateTime nyNow)
{
    lastDecision = "MANAGE_NOT_IMPLEMENTED";
}

private void LogBarCsv(DateTime nyNow) {}
private void LogDailyCsv(DateTime nyDate, string reason) {}
private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason) {}
private void WriteTelemetry(DateTime nyNow) {}
```

- [ ] **Step 5: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): OnBarUpdate main loop + Paso 0 day limits"
```

---

### Task 3: EvaluateEntry — Pasos 1 through 6

**Files:**
- Modify: `MNQOliver.cs`

This is the core logic. Replace the `EvaluateEntry` stub with the full sequential filter pipeline: SOH → Direction → Movement Phase → Day Phase → Detonante/RBI → Enter.

- [ ] **Step 1: Replace EvaluateEntry stub with full implementation**

Replace the `EvaluateEntry` method:

```csharp
#region Pasos 1-6 — Entry Evaluation

private void EvaluateEntry(DateTime nyNow)
{
    // Paso 1: SOH filter
    if (!CheckSOH())
        return;

    // Paso 2: Direction
    int direction = GetDirection();
    if (direction == 0)
    {
        lastDecision = "NO_DIRECTION";
        return;
    }

    // Paso 3: Movement phase
    string movPhase = GetMovementPhase();
    if (movPhase == "MADURA")
    {
        lastDecision = string.Format("FASE_MADURA spread/atr={0:F2}", Math.Abs(ema20[0] - sma200[0]) / atr14[0]);
        return;
    }

    // Paso 4: Day phase
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

    // Paso 6: Enter trade
    double currentATR = atr14[0];
    double initialStop;
    if (direction == 1)
        initialStop = Close[0] - (Stop_AtrMult * currentATR);
    else
        initialStop = Close[0] + (Stop_AtrMult * currentATR);

    // Risk check: would this trade push us past max daily loss?
    double potentialLoss = Math.Abs(Close[0] - initialStop) * 2; // MNQ $2/point
    if (dailyPnL - potentialLoss < -MaxDailyLoss)
    {
        lastDecision = string.Format("BLOCKED_RISK potLoss={0:F2}", potentialLoss);
        return;
    }

    string signal = direction == 1 ? "OliverL" : "OliverS";
    EnterNewTrade(direction, initialStop, entryType, signal, dayPhase);

    lastDecision = string.Format("ENTER {0} {1} {2} phase={3} movPhase={4}",
        entryType, direction == 1 ? "LONG" : "SHORT",
        movPhase, dayPhase, movPhase);
}

#endregion
```

- [ ] **Step 2: Add Paso 1 — CheckSOH**

```csharp
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
        // Exception: breakout from range
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
```

- [ ] **Step 3: Add Paso 2 — GetDirection**

```csharp
#region Paso 2 — Direction

private int GetDirection()
{
    double price = Close[0];
    double ema = ema20[0];

    if (price > ema) return 1;   // LONG
    if (price < ema) return -1;  // SHORT
    return 0;
}

#endregion
```

- [ ] **Step 4: Add Paso 3 — GetMovementPhase**

```csharp
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
```

- [ ] **Step 5: Add Paso 4 — GetDayPhase**

```csharp
#region Paso 4 — Day Phase

private int GetDayPhase(DateTime nyNow)
{
    int hour = nyNow.Hour;
    int min = nyNow.Minute;
    double timeDecimal = hour + min / 60.0;

    if (timeDecimal < 11.25) return 1;   // 09:32 - 11:15 = Fase 1
    if (timeDecimal < 14.25) return 2;   // 11:15 - 14:15 = Fase 2 (SOH)
    return 3;                             // 14:15 - 15:30 = Fase 3
}

#endregion
```

- [ ] **Step 6: Add Paso 5 — CheckDetonante + RBI/GBI**

```csharp
#region Paso 5 — Detonante

private string CheckDetonante(int direction)
{
    double bodyPct = GetBodyPct();
    double rangoATR = GetRangoATR();

    // Detonante: body% high + range > ATR
    bool isDetonante = bodyPct >= Detonante_BodyPct && rangoATR >= Detonante_RangoATR;

    // Direction must match candle color
    bool candleMatchesDir = (direction == 1 && Close[0] > Open[0])
                         || (direction == -1 && Close[0] < Open[0]);

    if (isDetonante && candleMatchesDir)
        return "DETONANTE";

    // RBI/GBI fallback
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
```

- [ ] **Step 7: Add Paso 6 — EnterNewTrade**

```csharp
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

    // Hard stop via NinjaTrader order
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
```

- [ ] **Step 8: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): Pasos 1-6 — SOH, direction, movement phase, day phase, detonante, entry"
```

---

### Task 4: ManageTrade — Paso 7 (Breathe + Trailing + Breakeven)

**Files:**
- Modify: `MNQOliver.cs`

Replace the `ManageTrade` stub with the full trade management logic: breathe period (2 bars), trailing bar-a-bar with 2-bar buffer + ATR offset, breakeven.

- [ ] **Step 1: Replace ManageTrade stub**

```csharp
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

    // Check if stop was hit during breathe
    bool whipsaw = false;
    if (tradeDirection == 1 && Low[0] <= stopPrice) whipsaw = true;
    if (tradeDirection == -1 && High[0] >= stopPrice) whipsaw = true;

    if (whipsaw)
    {
        FlattenPosition("BreatheWhipsaw");
        lastDecision = string.Format("WHIPSAW breathe bar={0}", breatheCount);
        return;
    }

    // After 2 bars, transition to trailing
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
    // Check if trail stop hit
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

    // Trail: use MIN/MAX of last 2 bars + ATR buffer
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

    // Breakeven
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

    // Check daily profit target with unrealized
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
```

- [ ] **Step 2: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): Paso 7 — breathe, trailing 2-bar+ATR buffer, breakeven"
```

---

### Task 5: OnExecutionUpdate — P&L Tracking

**Files:**
- Modify: `MNQOliver.cs`

Tracks real fill prices and calculates P&L on exits. Follows same pattern as MNQEnCalienteBot.cs but simplified (no score tracking).

- [ ] **Step 1: Add OnExecutionUpdate**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): OnExecutionUpdate — P&L tracking on fills"
```

---

### Task 6: Logging — CSV + Telemetry

**Files:**
- Modify: `MNQOliver.cs`

Replace the logging stubs with full implementations. CSV format is adapted to MNQOliver fields (no scores, add ATR ratio, EMA slope, body%, rango/ATR). Telemetry JSON updated for MCP bridge compatibility.

- [ ] **Step 1: Replace LogTradeCsv stub**

```csharp
private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason)
{
    try
    {
        string dir = tradeDirection != 0
            ? (tradeDirection == 1 ? "LONG" : "SHORT")
            : (lastClosedDirection == 1 ? "LONG" : "SHORT");
        double atrRatio = atrSma50[0] > 0 ? atr14[0] / atrSma50[0] : 0;
        double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);
        string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1},{2},{3},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12:F1},{13:F2},{14:F2},{15:F2},{16:F2},{17:F2},{18},{19},{20},{21}\n",
            nyNow, action, dir, "", fillPrice, exitPrice, stopPrice,
            ema20[0], sma200[0], Math.Abs(ema20[0] - sma200[0]), atr14[0],
            atrRatio, emaSlope, GetBodyPct(), GetRangoATR(),
            pnl, dailyPnL, totalPnL, exitReason, tradesToday,
            GetDayPhase(nyNow), GetMovementPhase());
        File.AppendAllText(csvLogPath, line);
    }
    catch {}
}
```

- [ ] **Step 2: Replace LogDailyCsv stub**

```csharp
private void LogDailyCsv(DateTime nyDate, string reason)
{
    try
    {
        string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4}\n",
            nyDate, dailyPnL, totalPnL, tradesToday, reason);
        File.AppendAllText(csvDailyPath, line);
    }
    catch {}
}
```

- [ ] **Step 3: Replace LogBarCsv stub**

```csharp
private void LogBarCsv(DateTime nyNow)
{
    try
    {
        string pos = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : "FLAT";
        string state = tradeState.ToString();
        string vsEma = Close[0] >= ema20[0] ? "ENCIMA" : "DEBAJO";
        string vsSma = Close[0] >= sma200[0] ? "ENCIMA" : "DEBAJO";
        double atrRatio = atrSma50[0] > 0 ? atr14[0] / atrSma50[0] : 0;
        double emaSlope = Math.Abs(ema20[0] - ema20[Math.Min(10, CurrentBar)]);
        string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F1},{12},{13},{14},{15},{16:F2},{17:F2},{18},{19},{20}\n",
            nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
            ema20[0], sma200[0], Math.Abs(ema20[0] - sma200[0]), atr14[0],
            atrRatio, emaSlope, vsEma, vsSma, pos, state,
            dailyPnL, totalPnL, tradesToday, isSOH ? "YES" : "NO", lastDecision);
        File.AppendAllText(csvBarLogPath, line);
    }
    catch {}
}
```

- [ ] **Step 4: Replace WriteTelemetry stub**

```csharp
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
  ""lastAction"": ""{22}"",
  ""recentTrades"": [{23}]
}}",
            nyNow, Close[0], ema20[0], sma200[0], spread, atr14[0],
            atrRatio, emaSlope,
            pos, tradeState, lastDecision,
            entryPrice, stopPrice, unrealizedPts, unrealizedPts * 2,
            dailyPnL, totalPnL, tradesToday,
            GetDayPhase(nyNow), GetMovementPhase(),
            isSOH ? "true" : "false", dayDone ? "true" : "false",
            lastAction.Replace("\"", "'"), recentTrades);

        File.WriteAllText(telemetryPath, json);
    }
    catch {}
}
```

- [ ] **Step 5: Commit**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): logging — CSV trades/daily/bar + JSON telemetry"
```

---

### Task 7: Final Review + Copy to NinjaTrader

**Files:**
- Review: `MNQOliver.cs`
- Copy to: `C:\Users\santiago.burgos\OneDrive - Perficient, Inc\Documents\NinjaTrader 8\bin\Custom\Strategies\MNQOliver.cs`

- [ ] **Step 1: Full file review — verify all regions are present and methods connected**

Check that MNQOliver.cs has these regions in order:
1. Using declarations
2. Parameters — Fixed
3. Parameters — Walk-Forward
4. Variables
5. Lifecycle (OnStateChange)
6. OnBarUpdate
7. Paso 0 — Day Limits
8. Pasos 1-6 — Entry Evaluation (with CheckSOH, GetDirection, GetMovementPhase, GetDayPhase, CheckDetonante, EnterNewTrade)
9. Paso 7 — Trade Management (ManageBreathe, ManageTrailing)
10. OnExecutionUpdate
11. Helpers (ResetDaily, FlattenPosition, InitCsvLogs, logging methods, WriteTelemetry)

- [ ] **Step 2: Verify method name consistency across all tasks**

Check these exact method names are used consistently:
- `CheckDayLimits(DateTime)` — called in OnBarUpdate
- `EvaluateEntry(DateTime)` — called in OnBarUpdate
- `ManageTrade(DateTime)` — called in OnBarUpdate
- `CheckSOH()` — called in EvaluateEntry
- `GetDirection()` — called in EvaluateEntry
- `GetMovementPhase()` — called in EvaluateEntry, LogTradeCsv, WriteTelemetry
- `GetDayPhase(DateTime)` — called in EvaluateEntry, LogTradeCsv, WriteTelemetry
- `CheckDetonante(int)` — called in EvaluateEntry
- `GetBodyPct()` — called in CheckDetonante, LogTradeCsv
- `GetRangoATR()` — called in CheckDetonante, LogTradeCsv
- `EnterNewTrade(int, double, string, string, int)` — called in EvaluateEntry
- `ManageBreathe()` — called in ManageTrade
- `ManageTrailing()` — called in ManageTrade
- `FlattenPosition(string)` — called in multiple places
- `LogTradeCsv(DateTime, string, double, double, double, string)` — called in OnExecutionUpdate
- `LogDailyCsv(DateTime, string)` — called in OnBarUpdate
- `LogBarCsv(DateTime)` — called in OnBarUpdate
- `WriteTelemetry(DateTime)` — called in OnBarUpdate

- [ ] **Step 3: Copy to NinjaTrader strategies folder**

```bash
cp MNQOliver.cs "/c/Users/santiago.burgos/OneDrive - Perficient, Inc/Documents/NinjaTrader 8/bin/Custom/Strategies/MNQOliver.cs"
```

- [ ] **Step 4: Commit final**

```bash
git add MNQOliver.cs
git commit -m "feat(mnqoliver): complete strategy — ready for NinjaTrader compilation"
```

---

### Task 8: Update CLAUDE.md + Skill

**Files:**
- Modify: `CLAUDE.md`
- Modify: `.claude/skills/mnq-probabilistic-strategy/SKILL.md` (if applicable)

- [ ] **Step 1: Add MNQOliver section to CLAUDE.md**

Add after the existing MNQEnCalienteBot description:

```markdown
- **MNQOliver.cs** — Estrategia v4 basada en Oliver Velez price-action + ATR. Reemplaza MNQEnCalienteBot.cs. Usa EMA(20)/SMA(200)/ATR(14), flujo de 8 pasos (SOH→dirección→fase→detonante→entrada→trailing), 11 parámetros (8 walk-forward + 3 fijos). Spec: docs/superpowers/specs/2026-05-01-mnqoliver-design.md
```

- [ ] **Step 2: Update the copy instructions in CLAUDE.md**

Add MNQOliver.cs to the copy list:

```markdown
  - `MNQOliver.cs` → `Strategies/`
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add MNQOliver to CLAUDE.md architecture and copy instructions"
```
