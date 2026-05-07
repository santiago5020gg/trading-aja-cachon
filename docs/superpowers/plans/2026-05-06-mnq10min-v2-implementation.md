# MNQ10min-v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a NinjaTrader 8 strategy that trades breakouts of the 09:30-09:40 range on MNQ with fixed 1:1 TP/SL and stop market entries.

**Architecture:** Single-file NinjaScript strategy with 4 states (EsperandoRango → OrdenesPuestas → EnTrade → DiaTerminado). Uses stop market orders for intrabar entry, SetStopLoss/SetProfitTarget for exit management, and CSV logging.

**Tech Stack:** C# / NinjaTrader 8 NinjaScript API

---

## File Structure

- **Create:** `MNQ10minV2.cs` — the complete strategy (single file, ~300 lines)

---

### Task 1: Scaffold — Parameters, State, and Lifecycle

**Files:**
- Create: `MNQ10minV2.cs`

- [ ] **Step 1: Create the file with using declarations, namespace, class, parameters, and state variables**

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
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MNQ10minV2 : Strategy
    {
        #region Parameters

        [NinjaScriptProperty]
        [Display(Name = "Colchon Stop (pts)", GroupName = "1. Stop", Order = 1)]
        public int ColchonStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades/Dia", GroupName = "2. Sesion", Order = 1)]
        public int MaxTrades { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Hora Cierre (HH:mm)", GroupName = "2. Sesion", Order = 2)]
        public string HoraCierre { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "3. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        #endregion

        #region Variables

        public enum LogMode { Off, Day, Month }

        private enum BotState
        {
            EsperandoRango,
            OrdenesPuestas,
            EnTrade,
            DiaTerminado
        }

        private BotState estado;
        private double rangoHigh;
        private double rangoLow;
        private double rangoPuntos;

        private int tradeDirection;       // 1 = long, -1 = short
        private double entryPrice;
        private double stopDistance;
        private int tradesToday;
        private double dailyPnL;
        private double totalPnL;
        private bool longUsado;
        private bool shortUsado;

        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;
        private int horaCierreH;
        private int horaCierreM;

        private string lastDecision;
        private string lastAction;
        private List<string> tradeLog;

        private string telemetryPath = @"C:\temp\mnq_10minv2_status.json";
        private string botHistoryDir;
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ10min-v2 — Ruptura rango 10min con TP/SL 1:1";
                Name = "MNQ10minV2";
                Calculate = Calculate.OnEachTick;
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
                BarsRequiredToTrade = 20;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                ColchonStop = 10;
                MaxTrades = 2;
                HoraCierre = "15:50";
                ModoLog = LogMode.Month;
            }
            else if (State == State.DataLoaded)
            {
                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastResetDate = DateTime.MinValue;
                tradeLog = new List<string>();
                lastAction = "";
                lastDecision = "WAITING";

                string[] hcParts = HoraCierre.Split(':');
                horaCierreH = int.Parse(hcParts[0]);
                horaCierreM = int.Parse(hcParts[1]);

                try { Directory.CreateDirectory(@"C:\temp"); } catch {}

                botHistoryDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"perficient\AI path lean\trading 7\bot\history");
                if (ModoLog != LogMode.Off)
                {
                    try { Directory.CreateDirectory(botHistoryDir); } catch {}
                    csvLogPath = Path.Combine(botHistoryDir, "mnq10minv2_trades_log.csv");
                    csvDailyPath = Path.Combine(botHistoryDir, "mnq10minv2_daily_log.csv");
                    csvBarLogPath = Path.Combine(botHistoryDir, "mnq10minv2_bar_log.csv");
                    InitCsvLogs();
                }
            }
        }

        #endregion
    }
}
```

- [ ] **Step 2: Commit scaffold**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): scaffold with parameters, state vars, lifecycle"
```

---

### Task 2: OnBarUpdate — Daily Reset, Range Accumulation, and Hour Check

**Files:**
- Modify: `MNQ10minV2.cs`

- [ ] **Step 1: Add OnBarUpdate with daily reset and state dispatch**

Add after the `#endregion` of Lifecycle:

```csharp
        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);
            DateTime nyDate = nyNow.Date;

            LogBarCsv(nyNow);

            if (nyDate != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                    LogDailyCsv(lastResetDate);
                ResetDaily();
                lastResetDate = nyDate;
            }

            if (estado == BotState.DiaTerminado)
            {
                WriteTelemetry(nyNow);
                return;
            }

            bool afterClose = nyNow.Hour > horaCierreH || (nyNow.Hour == horaCierreH && nyNow.Minute >= horaCierreM);
            if (afterClose)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                lastDecision = "CIERRE_FORZADO";
                WriteTelemetry(nyNow);
                return;
            }

            switch (estado)
            {
                case BotState.EsperandoRango:
                    ProcesarRango(nyNow);
                    break;
                case BotState.OrdenesPuestas:
                    GestionarOrdenes(nyNow);
                    break;
                case BotState.EnTrade:
                    GestionarTrade(nyNow);
                    break;
            }

            WriteTelemetry(nyNow);
        }

        #endregion
```

- [ ] **Step 2: Add ProcesarRango method**

Add after OnBarUpdate region:

```csharp
        #region Estado: EsperandoRango

        private void ProcesarRango(DateTime nyNow)
        {
            bool antesVentana = nyNow.Hour < 9 || (nyNow.Hour == 9 && nyNow.Minute < 30);
            bool enVentana = (nyNow.Hour == 9 && nyNow.Minute >= 30 && nyNow.Minute < 40);

            if (antesVentana)
            {
                lastDecision = "ESPERANDO_0930";
                return;
            }

            if (enVentana)
            {
                if (High[0] > rangoHigh) rangoHigh = High[0];
                if (Low[0] < rangoLow) rangoLow = Low[0];
                lastDecision = string.Format("ACUMULANDO_RANGO H={0:F2} L={1:F2}", rangoHigh, rangoLow);
                return;
            }

            if (rangoHigh == double.MinValue || rangoLow == double.MaxValue || rangoHigh == rangoLow)
            {
                lastDecision = "RANGO_INVALIDO";
                estado = BotState.DiaTerminado;
                return;
            }

            rangoPuntos = rangoHigh - rangoLow;
            ColocarOrdenes();
        }

        #endregion
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): add OnBarUpdate, daily reset, range accumulation"
```

---

### Task 3: Order Placement and OCO Logic

**Files:**
- Modify: `MNQ10minV2.cs`

- [ ] **Step 1: Add ColocarOrdenes and GestionarOrdenes methods**

Add after the EsperandoRango region:

```csharp
        #region Estado: OrdenesPuestas

        private void ColocarOrdenes()
        {
            stopDistance = rangoPuntos + ColchonStop;

            if (!longUsado)
            {
                SetStopLoss("RangoLong", CalculationMode.Ticks, stopDistance / TickSize, false);
                SetProfitTarget("RangoLong", CalculationMode.Ticks, stopDistance / TickSize);
                EnterLongStopMarket(1, rangoHigh, "RangoLong");
            }

            if (!shortUsado)
            {
                SetStopLoss("RangoShort", CalculationMode.Ticks, stopDistance / TickSize, false);
                SetProfitTarget("RangoShort", CalculationMode.Ticks, stopDistance / TickSize);
                EnterShortStopMarket(1, rangoLow, "RangoShort");
            }

            estado = BotState.OrdenesPuestas;

            Draw.Rectangle(this, "Rango" + lastResetDate.ToString("yyyyMMdd"), false,
                5, rangoHigh, 0, rangoLow, Brushes.Transparent, Brushes.DodgerBlue, 30);

            lastDecision = string.Format("ORDENES_PUESTAS H={0:F2} L={1:F2} stopDist={2:F0}",
                rangoHigh, rangoLow, stopDistance);
        }

        private void GestionarOrdenes(DateTime nyNow)
        {
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                estado = BotState.EnTrade;
                return;
            }
            lastDecision = string.Format("ESPERANDO_FILL H={0:F2} L={1:F2}", rangoHigh, rangoLow);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): add stop market order placement and OCO state"
```

---

### Task 4: Trade Management and Re-entry Logic

**Files:**
- Modify: `MNQ10minV2.cs`

- [ ] **Step 1: Add GestionarTrade method**

Add after OrdenesPuestas region:

```csharp
        #region Estado: EnTrade

        private void GestionarTrade(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (tradesToday >= MaxTrades || (longUsado && shortUsado))
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = "DIA_TERMINADO_TRADES";
                    return;
                }
                ColocarOrdenes();
                return;
            }

            double movimiento = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];
            lastDecision = string.Format("EN_TRADE {0} mov={1:F0}pts entry={2:F2}",
                tradeDirection == 1 ? "LONG" : "SHORT", movimiento, entryPrice);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): add trade management and re-entry logic"
```

---

### Task 5: OnExecutionUpdate — Fill Tracking and P&L

**Files:**
- Modify: `MNQ10minV2.cs`

- [ ] **Step 1: Add OnExecutionUpdate method**

Add after EnTrade region:

```csharp
        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;

            string orderName = execution.Order.Name;
            bool isEntry = orderName == "RangoLong" || orderName == "RangoShort";
            bool isExit = orderName == "Stop loss" || orderName == "Profit target";

            if (isEntry)
            {
                entryPrice = price;
                tradesToday++;

                if (orderName == "RangoLong")
                {
                    tradeDirection = 1;
                    longUsado = true;
                }
                else
                {
                    tradeDirection = -1;
                    shortUsado = true;
                }

                estado = BotState.EnTrade;
                lastAction = string.Format("FILL {0} @{1:F2}", tradeDirection == 1 ? "LONG" : "SHORT", price);
                tradeLog.Add(lastAction);

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", price, 0, 0, "");
            }
            else if (isExit)
            {
                double pnl = 0;
                string reason = orderName == "Profit target" ? "TP" : "SL";

                if (tradeDirection == 1)
                    pnl = (price - entryPrice) * 2.0 * quantity;
                else if (tradeDirection == -1)
                    pnl = (entryPrice - price) * 2.0 * quantity;

                dailyPnL += pnl;
                totalPnL += pnl;

                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}",
                    tradeDirection == 1 ? "LONG" : "SHORT", reason, price, pnl);
                tradeLog.Add(lastAction);

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", entryPrice, price, pnl, reason);

                tradeDirection = 0;
            }
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): add OnExecutionUpdate with fill/exit tracking"
```

---

### Task 6: Helpers — ResetDaily, FlattenAll, Logging, Telemetry

**Files:**
- Modify: `MNQ10minV2.cs`

- [ ] **Step 1: Add all helper methods**

Add after OnExecutionUpdate region:

```csharp
        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            estado = BotState.EsperandoRango;
            tradeDirection = 0;
            rangoHigh = double.MinValue;
            rangoLow = double.MaxValue;
            rangoPuntos = 0;
            stopDistance = 0;
            entryPrice = 0;
            longUsado = false;
            shortUsado = false;
            lastDecision = "NEW_DAY";
            lastAction = "";
        }

        private void FlattenAll(string reason)
        {
            if (Position.MarketPosition == MarketPosition.Long)
                ExitLong("X_" + reason, "RangoLong");
            else if (Position.MarketPosition == MarketPosition.Short)
                ExitShort("X_" + reason, "RangoShort");

            tradeDirection = 0;
            Draw.Diamond(this, "Exit" + CurrentBar, true, 0, Close[0], Brushes.Yellow);
        }

        private void InitCsvLogs()
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                if (!File.Exists(csvLogPath))
                    File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryPrice,ExitPrice,StopDist,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday\n");
                if (!File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,RangoPts\n");
                if (!File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,Estado,Position,DailyPnL,TotalPnL,TradesToday,Decision\n");
            }
            catch {}
        }

        private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string dir = tradeDirection == 1 ? "LONG" : tradeDirection == -1 ? "SHORT" : "FLAT";
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm:ss},{1},{2},{3:F2},{4:F2},{5:F0},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12},{13}\n",
                    nyNow, action, dir, fillPrice, exitPrice, stopDistance,
                    rangoHigh == double.MinValue ? 0 : rangoHigh,
                    rangoLow == double.MaxValue ? 0 : rangoLow,
                    rangoPuntos, pnl, dailyPnL, totalPnL, exitReason, tradesToday);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime nyDate)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4:F2}\n",
                    nyDate, dailyPnL, totalPnL, tradesToday, rangoPuntos);
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
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm:ss},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6},{7},{8:F2},{9:F2},{10},{11}\n",
                    nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
                    estado, pos, dailyPnL, totalPnL, tradesToday, lastDecision);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        private void WriteTelemetry(DateTime nyNow)
        {
            try
            {
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
  ""timestamp"": ""{0:yyyy-MM-dd HH:mm:ss}"",
  ""strategy"": ""MNQ10minV2"",
  ""price"": {1:F2},
  ""rangoHigh"": {2:F2},
  ""rangoLow"": {3:F2},
  ""rangoPuntos"": {4:F2},
  ""position"": ""{5}"",
  ""estado"": ""{6}"",
  ""decision"": ""{7}"",
  ""entryPrice"": {8:F2},
  ""stopDistance"": {9:F0},
  ""unrealizedPts"": {10:F2},
  ""unrealizedPnL"": {11:F2},
  ""dailyPnL"": {12:F2},
  ""totalPnL"": {13:F2},
  ""tradesToday"": {14},
  ""longUsado"": {15},
  ""shortUsado"": {16},
  ""lastAction"": ""{17}"",
  ""recentTrades"": [{18}]
}}",
                    nyNow, Close[0],
                    rangoHigh == double.MinValue ? 0 : rangoHigh,
                    rangoLow == double.MaxValue ? 0 : rangoLow,
                    rangoPuntos, pos, estado, lastDecision,
                    entryPrice, stopDistance, unrealizedPts, unrealizedPts * 2.0,
                    dailyPnL, totalPnL, tradesToday,
                    longUsado ? "true" : "false",
                    shortUsado ? "true" : "false",
                    lastAction.Replace("\"", "'"), recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }

        #endregion
```

- [ ] **Step 2: Close the class and namespace braces (already exist from Task 1)**

Verify the file ends with:
```csharp
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): add helpers, logging, telemetry, reset"
```

---

### Task 7: Final Verification

**Files:**
- Read: `MNQ10minV2.cs`

- [ ] **Step 1: Verify file compiles conceptually — check for:**
- All regions properly closed
- No missing braces
- All referenced methods exist
- State transitions are coherent (EsperandoRango → OrdenesPuestas → EnTrade → DiaTerminado)
- SetStopLoss/SetProfitTarget called BEFORE EnterLongStopMarket/EnterShortStopMarket

- [ ] **Step 2: Copy to NinjaTrader for compilation test**

```bash
cp MNQ10minV2.cs "/c/Users/santiago.burgos/OneDrive - Perficient, Inc/Documents/NinjaTrader 8/bin/Custom/Strategies/MNQ10minV2.cs"
```

- [ ] **Step 3: Final commit with any fixes**

```bash
git add MNQ10minV2.cs
git commit -m "feat(mnq10minv2): complete strategy ready for NT8 compilation"
```
