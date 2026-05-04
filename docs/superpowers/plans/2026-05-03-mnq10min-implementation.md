# MNQ10min Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a NinjaTrader 8 strategy that trades breakouts of the 09:30-09:40 range on MNQ 2-minute bars, with automatic contract sizing, breakeven + add-on, and bar-by-bar trailing.

**Architecture:** Single-file NinjaScript strategy (`MNQ10min.cs`) following the same pattern as `MNQOliver.cs`. State machine with 5 states (ESPERANDO_RANGO, ESPERANDO_RUPTURA, EN_TRADE, TRAILING, DIA_TERMINADO). All logic in `OnBarUpdate` (Calculate.OnBarClose). Uses `EnterLong`/`EnterShort` with quantity, `SetStopLoss`, and manual exit via `ExitLong`/`ExitShort`.

**Tech Stack:** C# / NinjaTrader 8 NinjaScript API / .NET Framework 4.8

**Spec:** `docs/superpowers/specs/2026-05-03-mnq10min-design.md`

**Reference file:** `MNQOliver.cs` — use the same patterns for: `OnStateChange` lifecycle, `OnExecutionUpdate` PnL tracking, `FlattenPosition`, CSV logging, telemetry JSON, region structure, parameter attributes.

---

### Task 1: Scaffold — File structure, parameters, variables, OnStateChange

**Files:**
- Create: `MNQ10min.cs`

- [ ] **Step 1: Create MNQ10min.cs with using declarations, namespace, class, and parameters**

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
    public class MNQ10min : Strategy
    {
        #region Parameters

        [NinjaScriptProperty]
        [Display(Name = "Max Trades/Dia", GroupName = "1. Riesgo", Order = 1)]
        public int MaxTrades { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Riesgo Max Dia ($)", GroupName = "1. Riesgo", Order = 2)]
        public double RiesgoMaxDia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Riesgo Max Primer Trade ($)", GroupName = "1. Riesgo", Order = 3)]
        public double RiesgoMaxPrimerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Colchon Stop (pts)", GroupName = "2. Stop", Order = 1)]
        public int ColchonStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (pts)", GroupName = "2. Stop", Order = 2)]
        public int MaxStopPuntos { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Hora Cierre (HH:mm)", GroupName = "3. Sesion", Order = 1)]
        public string HoraCierre { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "% Cuerpo Ruptura", GroupName = "4. Entrada", Order = 1)]
        public int PctCuerpoRuptura { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "% Breakeven", GroupName = "5. Gestion", Order = 1)]
        public int PctBreakeven { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "% Trailing Activacion", GroupName = "5. Gestion", Order = 2)]
        public int PctTrailing { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "% Cuerpo Trailing", GroupName = "5. Gestion", Order = 3)]
        public int PctCuerpoTrailing { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "6. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        #endregion

        #region Variables

        public enum LogMode { Off, Day, Month }

        private enum BotState
        {
            EsperandoRango,
            EsperandoRuptura,
            EnTrade,
            Trailing,
            DiaTerminado
        }

        private BotState estado;
        private int tradeDirection;       // 1 = long, -1 = short
        private double entryPrice;
        private double stopPrice;
        private double stopPuntos;
        private int contratosActuales;
        private bool breakevenHit;
        private bool contratoAgregado;

        private double rangoHigh;
        private double rangoLow;
        private double rangoPuntos;
        private bool rangoCalculado;

        private double dailyPnL;
        private double totalPnL;
        private int tradesToday;
        private bool dayDone;
        private double metaDia;           // 1:1 del primer trade en USD
        private double stopPuntosPrimerTrade;
        private int contratosPrimerTrade;
        private double perdidaAcumulada;

        private double trailingNivel;

        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;
        private int horaCierreH;
        private int horaCierreM;

        private double realEntryPrice;
        private int lastClosedDirection;
        private string lastClosedReason;
        private bool pnlTrackingStarted;
        private string activeEntrySignal;

        private string lastDecision;
        private string lastAction;
        private List<string> tradeLog;

        private string telemetryPath = @"C:\temp\mnq_10min_status.json";
        private string botHistoryDir;
        private string botDayDir;
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;

        #endregion
    }
}
```

- [ ] **Step 2: Add OnStateChange with SetDefaults, Configure, DataLoaded, Realtime**

Add inside the class, after the Variables region:

```csharp
        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ10min — Ruptura del rango de 10 minutos (09:30-09:40)";
                Name = "MNQ10min";
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
                BarsRequiredToTrade = 20;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                MaxTrades = 2;
                RiesgoMaxDia = 600;
                RiesgoMaxPrimerTrade = 400;
                ColchonStop = 5;
                MaxStopPuntos = 200;
                HoraCierre = "15:50";
                PctCuerpoRuptura = 60;
                PctBreakeven = 50;
                PctTrailing = 80;
                PctCuerpoTrailing = 50;
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
                    string logDir = botHistoryDir;
                    if (ModoLog == LogMode.Day)
                    {
                        botDayDir = Path.Combine(botHistoryDir, "history-day");
                        logDir = botDayDir;
                    }
                    try { Directory.CreateDirectory(logDir); } catch {}

                    csvLogPath = Path.Combine(logDir, "mnq10min_trades_log.csv");
                    csvDailyPath = Path.Combine(logDir, "mnq10min_daily_log.csv");
                    csvBarLogPath = Path.Combine(logDir, "mnq10min_bar_log.csv");
                    InitCsvLogs();
                }
            }
            else if (State == State.Realtime)
            {
                pnlTrackingStarted = true;
            }
        }

        #endregion
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): scaffold — parameters, variables, OnStateChange"
```

---

### Task 2: OnBarUpdate — Day reset, session control, state routing

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add OnBarUpdate with day reset, session boundary checks, and state routing**

Add after the Lifecycle region:

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
                {
                    string reason = dayDone
                        ? (dailyPnL <= -RiesgoMaxDia ? "MaxLoss" : "MaxTrades")
                        : "SessionEnd";
                    if (dailyPnL >= metaDia && metaDia > 0) reason = "MetaDia";
                    LogDailyCsv(lastResetDate, reason);
                }
                ResetDaily();
                lastResetDate = nyDate;
                if (ModoLog == LogMode.Day)
                    InitCsvLogs();
            }

            if (estado == BotState.DiaTerminado)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("DayDone");
                lastDecision = "DIA_TERMINADO";
                WriteTelemetry(nyNow);
                return;
            }

            bool afterClose = nyNow.Hour > horaCierreH || (nyNow.Hour == horaCierreH && nyNow.Minute >= horaCierreM);
            if (afterClose)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                dayDone = true;
                lastDecision = "CIERRE_FORZADO";
                WriteTelemetry(nyNow);
                return;
            }

            switch (estado)
            {
                case BotState.EsperandoRango:
                    ProcesarRango(nyNow);
                    break;
                case BotState.EsperandoRuptura:
                    BuscarRuptura(nyNow);
                    break;
                case BotState.EnTrade:
                    GestionarTrade(nyNow);
                    break;
                case BotState.Trailing:
                    GestionarTrailing(nyNow);
                    break;
            }

            WriteTelemetry(nyNow);
        }

        #endregion
```

- [ ] **Step 2: Add ResetDaily helper**

Add inside a Helpers region:

```csharp
        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            totalPnL = totalPnL; // keep running total
            tradesToday = 0;
            dayDone = false;
            estado = BotState.EsperandoRango;
            tradeDirection = 0;
            breakevenHit = false;
            contratoAgregado = false;
            rangoHigh = double.MinValue;
            rangoLow = double.MaxValue;
            rangoCalculado = false;
            metaDia = 0;
            stopPuntosPrimerTrade = 0;
            contratosPrimerTrade = 0;
            perdidaAcumulada = 0;
            trailingNivel = 0;
            contratosActuales = 0;
            lastDecision = "NEW_DAY";
            lastAction = "";
        }

        #endregion
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): OnBarUpdate — day reset, session control, state routing"
```

---

### Task 3: ESPERANDO_RANGO — Accumulate 09:30-09:38 bars

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add ProcesarRango method**

Add after the OnBarUpdate region:

```csharp
        #region Estado: EsperandoRango

        private void ProcesarRango(DateTime nyNow)
        {
            bool enVentana = (nyNow.Hour == 9 && nyNow.Minute >= 30 && nyNow.Minute < 40);
            bool antesVentana = nyNow.Hour < 9 || (nyNow.Hour == 9 && nyNow.Minute < 30);

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

            // nyNow.Minute >= 40 (or hour > 9): rango listo
            if (rangoHigh == double.MinValue || rangoLow == double.MaxValue)
            {
                lastDecision = "RANGO_INVALIDO";
                estado = BotState.DiaTerminado;
                dayDone = true;
                return;
            }

            rangoPuntos = rangoHigh - rangoLow;
            rangoCalculado = true;
            estado = BotState.EsperandoRuptura;

            Draw.Rectangle(this, "Rango" + nyNow.ToString("yyyyMMdd"), false,
                5, rangoHigh, 0, rangoLow, Brushes.Transparent, Brushes.DodgerBlue, 30);

            lastDecision = string.Format("RANGO_LISTO H={0:F2} L={1:F2} pts={2:F2}", rangoHigh, rangoLow, rangoPuntos);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): ESPERANDO_RANGO — accumulate 09:30-09:38 bars"
```

---

### Task 4: ESPERANDO_RUPTURA — Breakout detection and entry

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add BuscarRuptura method**

```csharp
        #region Estado: EsperandoRuptura

        private void BuscarRuptura(DateTime nyNow)
        {
            if (CheckDayLimits())
                return;

            double cuerpo = Math.Abs(Close[0] - Open[0]);
            double rangoVela = High[0] - Low[0];

            if (rangoVela <= 0)
            {
                lastDecision = "ESPERANDO_RUPTURA (doji)";
                return;
            }

            double pctCuerpo = cuerpo / rangoVela * 100.0;
            bool cuerpoOk = pctCuerpo >= PctCuerpoRuptura;

            bool rupturaLong = Close[0] > rangoHigh && cuerpoOk;
            bool rupturaShort = Close[0] < rangoLow && cuerpoOk;

            if (!rupturaLong && !rupturaShort)
            {
                lastDecision = string.Format("ESPERANDO_RUPTURA close={0:F2} body%={1:F0}", Close[0], pctCuerpo);
                return;
            }

            int direction = rupturaLong ? 1 : -1;
            Entrar(direction, cuerpo, nyNow);
        }

        private bool CheckDayLimits()
        {
            if (perdidaAcumulada >= RiesgoMaxDia)
            {
                estado = BotState.DiaTerminado;
                dayDone = true;
                lastDecision = "STOP_MAX_LOSS";
                return true;
            }
            if (metaDia > 0 && dailyPnL >= metaDia)
            {
                estado = BotState.DiaTerminado;
                dayDone = true;
                lastDecision = "META_DIA_ALCANZADA";
                return true;
            }
            if (tradesToday >= MaxTrades)
            {
                estado = BotState.DiaTerminado;
                dayDone = true;
                lastDecision = "STOP_MAX_TRADES";
                return true;
            }
            return false;
        }

        #endregion
```

- [ ] **Step 2: Add Entrar method — calculates stop, contracts, and places order**

```csharp
        #region Entrada

        private void Entrar(int direction, double cuerpoVelaRuptura, DateTime nyNow)
        {
            double stopNivel;
            double stopPts;

            if (direction == 1)
            {
                stopNivel = rangoLow - cuerpoVelaRuptura - ColchonStop;
                stopPts = Close[0] - stopNivel;
            }
            else
            {
                stopNivel = rangoHigh + cuerpoVelaRuptura + ColchonStop;
                stopPts = stopNivel - Close[0];
            }

            if (stopPts > MaxStopPuntos)
            {
                stopPts = MaxStopPuntos;
                stopNivel = direction == 1
                    ? Close[0] - MaxStopPuntos
                    : Close[0] + MaxStopPuntos;
            }

            if (stopPts <= 0)
            {
                lastDecision = "STOP_INVALIDO";
                return;
            }

            double riesgoMax;
            if (tradesToday == 0)
                riesgoMax = RiesgoMaxPrimerTrade;
            else
                riesgoMax = perdidaAcumulada > 0
                    ? RiesgoMaxDia - perdidaAcumulada
                    : RiesgoMaxPrimerTrade;

            int contratos = (int)Math.Floor(riesgoMax / (stopPts * 0.50));
            if (contratos <= 0)
            {
                estado = BotState.DiaTerminado;
                dayDone = true;
                lastDecision = "SIN_CONTRATOS_DISPONIBLES";
                return;
            }

            entryPrice = Close[0];
            stopPrice = stopNivel;
            stopPuntos = stopPts;
            tradeDirection = direction;
            contratosActuales = contratos;
            breakevenHit = false;
            contratoAgregado = false;
            trailingNivel = 0;
            tradesToday++;

            if (tradesToday == 1)
            {
                stopPuntosPrimerTrade = stopPts;
                contratosPrimerTrade = contratos;
                metaDia = stopPts * 0.50 * contratos;
            }

            string signal = direction == 1 ? "Rango10L" : "Rango10S";
            activeEntrySignal = signal;

            SetStopLoss(signal, CalculationMode.Price, stopNivel, false);

            if (direction == 1)
                EnterLong(contratos, signal);
            else
                EnterShort(contratos, signal);

            estado = BotState.EnTrade;

            Draw.ArrowUp(this, "Entry" + CurrentBar, true, 0,
                direction == 1 ? Low[0] - 5 : High[0] + 5,
                direction == 1 ? Brushes.Lime : Brushes.Red);

            lastDecision = string.Format("ENTRY {0} qty={1} stop={2:F2} stopPts={3:F0} meta=${4:F0}",
                direction == 1 ? "LONG" : "SHORT", contratos, stopNivel, stopPts, metaDia);
        }

        #endregion
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): ESPERANDO_RUPTURA — breakout detection and entry"
```

---

### Task 5: EN_TRADE — Stop monitoring, breakeven, add-on, trailing activation

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add GestionarTrade method**

```csharp
        #region Estado: EnTrade

        private void GestionarTrade(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                estado = BotState.EsperandoRuptura;
                tradeDirection = 0;
                return;
            }

            double movimiento = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];

            double pctMov = stopPuntos > 0 ? (movimiento / stopPuntos) * 100.0 : 0;

            // Check trailing activation first (80%)
            if (pctMov >= PctTrailing)
            {
                ActivarTrailing();
                lastDecision = string.Format("TRAILING_ACTIVADO mov={0:F0}pts ({1:F0}%)", movimiento, pctMov);
                return;
            }

            // Check breakeven (50%)
            if (!breakevenHit && pctMov >= PctBreakeven)
            {
                stopPrice = entryPrice;
                breakevenHit = true;

                string signal = activeEntrySignal;
                SetStopLoss(signal, CalculationMode.Price, entryPrice, false);

                if (!contratoAgregado)
                {
                    string addSignal = tradeDirection == 1 ? "AddL" : "AddS";
                    if (tradeDirection == 1)
                        EnterLong(1, addSignal);
                    else
                        EnterShort(1, addSignal);
                    contratoAgregado = true;
                    contratosActuales += 1;
                }

                lastDecision = string.Format("BREAKEVEN stop={0:F2} qty={1}", stopPrice, contratosActuales);
                return;
            }

            lastDecision = string.Format("EN_TRADE {0} mov={1:F0}pts ({2:F0}%) stop={3:F2}",
                tradeDirection == 1 ? "LONG" : "SHORT", movimiento, pctMov, stopPrice);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): EN_TRADE — breakeven, add-on contract, trailing activation"
```

---

### Task 6: TRAILING — Bar-by-bar trailing with body filter

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add ActivarTrailing and GestionarTrailing methods**

```csharp
        #region Estado: Trailing

        private void ActivarTrailing()
        {
            trailingNivel = BuscarNivelTrailing();
            estado = BotState.Trailing;
        }

        private double BuscarNivelTrailing()
        {
            // Search backwards for the most recent completed bar with body >= PctCuerpoTrailing%
            for (int i = 1; i <= CurrentBar; i++)
            {
                double cuerpo = Math.Abs(Close[i] - Open[i]);
                double rango = High[i] - Low[i];
                if (rango <= 0) continue;

                double pct = cuerpo / rango * 100.0;
                if (pct >= PctCuerpoTrailing)
                {
                    return tradeDirection == 1 ? Low[i] : High[i];
                }
            }
            // Fallback: use current bar
            return tradeDirection == 1 ? Low[0] : High[0];
        }

        private void GestionarTrailing(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                estado = BotState.EsperandoRuptura;
                tradeDirection = 0;
                return;
            }

            // Check if trailing hit
            bool trailingHit = false;
            if (tradeDirection == 1 && Close[0] < trailingNivel) trailingHit = true;
            if (tradeDirection == -1 && Close[0] > trailingNivel) trailingHit = true;

            if (trailingHit)
            {
                FlattenAll("TrailingStop");
                lastDecision = string.Format("EXIT_TRAILING nivel={0:F2}", trailingNivel);
                return;
            }

            // Update trailing with most recent completed bar (bar[1] = last completed since OnBarClose)
            double cuerpo1 = Math.Abs(Close[1] - Open[1]);
            double rango1 = High[1] - Low[1];

            if (rango1 > 0)
            {
                double pct1 = cuerpo1 / rango1 * 100.0;
                if (pct1 >= PctCuerpoTrailing)
                {
                    double nuevoNivel = tradeDirection == 1 ? Low[1] : High[1];
                    bool mejora = tradeDirection == 1
                        ? nuevoNivel > trailingNivel
                        : nuevoNivel < trailingNivel;

                    if (mejora)
                        trailingNivel = nuevoNivel;
                }
            }

            lastDecision = string.Format("TRAILING {0} nivel={1:F2}",
                tradeDirection == 1 ? "LONG" : "SHORT", trailingNivel);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): TRAILING — bar-by-bar trailing with body% filter"
```

---

### Task 7: OnExecutionUpdate — PnL tracking and state transitions on fill/exit

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add OnExecutionUpdate**

```csharp
        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;
            if (!pnlTrackingStarted) return;

            string orderName = execution.Order.Name;
            bool isEntry = orderName == "Rango10L" || orderName == "Rango10S"
                        || orderName == "AddL" || orderName == "AddS";
            bool isExit = orderName.StartsWith("X_") || orderName == "Stop loss";

            if (isExit)
            {
                if (orderName == "Stop loss")
                {
                    lastClosedDirection = tradeDirection;
                    lastClosedReason = "HardStop";
                }

                double realPnL = 0;
                string dir = lastClosedDirection == 1 ? "LONG" : "SHORT";

                if (lastClosedDirection == 1)
                    realPnL = (price - realEntryPrice) * 0.50 * quantity;
                else if (lastClosedDirection == -1)
                    realPnL = (realEntryPrice - price) * 0.50 * quantity;

                dailyPnL += realPnL;
                totalPnL += realPnL;

                if (realPnL < 0)
                    perdidaAcumulada += Math.Abs(realPnL);

                tradeLog.Add(string.Format("EXIT {0} | reason={1} | entry={2:F2} exit={3:F2} qty={4} pnl=${5:F2} | dailyPnL=${6:F2}",
                    dir, lastClosedReason, realEntryPrice, price, quantity, realPnL, dailyPnL));
                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}", dir, lastClosedReason, price, realPnL);

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", realEntryPrice, price, realPnL, lastClosedReason);

                if (marketPosition == MarketPosition.Flat)
                {
                    tradeDirection = 0;
                    activeEntrySignal = null;
                    contratosActuales = 0;

                    if (perdidaAcumulada >= RiesgoMaxDia || (metaDia > 0 && dailyPnL >= metaDia) || tradesToday >= MaxTrades)
                    {
                        estado = BotState.DiaTerminado;
                        dayDone = true;
                    }
                    else
                    {
                        estado = BotState.EsperandoRuptura;
                    }
                }
            }
            else if (isEntry)
            {
                if (orderName == "Rango10L" || orderName == "Rango10S")
                {
                    realEntryPrice = price;
                    entryPrice = price;
                }

                string dir = marketPosition == MarketPosition.Long ? "LONG" : "SHORT";
                lastAction = string.Format("FILL {0} @{1:F2} qty={2} stop={3:F2}", dir, price, quantity, stopPrice);
                tradeLog.Add(string.Format("FILL {0} | price={1:F2} qty={2} stop={3:F2}",
                    dir, price, quantity, stopPrice));

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", price, 0, 0, "");
            }
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): OnExecutionUpdate — PnL tracking and state transitions"
```

---

### Task 8: FlattenAll and drawing helpers

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add FlattenAll method inside the Helpers region**

```csharp
        private void FlattenAll(string reason)
        {
            lastClosedDirection = tradeDirection;
            lastClosedReason = reason;

            string fromSignal = activeEntrySignal ?? "";
            if (Position.MarketPosition == MarketPosition.Long)
                ExitLong("X_" + reason, fromSignal);
            else if (Position.MarketPosition == MarketPosition.Short)
                ExitShort("X_" + reason, fromSignal);

            // Also close add-on if exists
            if (contratoAgregado)
            {
                string addSignal = tradeDirection == 1 ? "AddL" : "AddS";
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("X_" + reason, addSignal);
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("X_" + reason, addSignal);
            }

            tradeDirection = 0;
            activeEntrySignal = null;
            contratosActuales = 0;
            contratoAgregado = false;
            breakevenHit = false;

            Draw.Diamond(this, "Exit" + CurrentBar, true, 0, Close[0], Brushes.Yellow);
        }
```

- [ ] **Step 2: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): FlattenAll — close all positions with reason tracking"
```

---

### Task 9: CSV logging and telemetry

**Files:**
- Modify: `MNQ10min.cs`

- [ ] **Step 1: Add InitCsvLogs, LogTradeCsv, LogDailyCsv, LogBarCsv**

Add inside the Helpers region:

```csharp
        private void InitCsvLogs()
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                bool overwrite = ModoLog == LogMode.Day;
                if (overwrite || !File.Exists(csvLogPath))
                    File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryPrice,ExitPrice,StopPrice,StopPts,Contratos,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday,MetaDia\n");
                if (overwrite || !File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,DayDoneReason,RangoPts,MetaDia\n");
                if (overwrite || !File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,Estado,Position,StopPrice,TrailingNivel,DailyPnL,TotalPnL,TradesToday,Decision\n");
            }
            catch {}
        }

        private void LogTradeCsv(DateTime nyNow, string action, double fillPrice, double exitPrice, double pnl, string exitReason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string dir = tradeDirection != 0
                    ? (tradeDirection == 1 ? "LONG" : "SHORT")
                    : (lastClosedDirection == 1 ? "LONG" : "SHORT");
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1},{2},{3:F2},{4:F2},{5:F2},{6:F0},{7},{8:F2},{9:F2},{10:F2},{11:F2},{12:F2},{13:F2},{14},{15},{16:F0}\n",
                    nyNow, action, dir, fillPrice, exitPrice, stopPrice, stopPuntos, contratosActuales,
                    rangoHigh, rangoLow, rangoPuntos,
                    pnl, dailyPnL, totalPnL, exitReason, tradesToday, metaDia);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime nyDate, string reason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4},{5:F2},{6:F0}\n",
                    nyDate, dailyPnL, totalPnL, tradesToday, reason, rangoPuntos, metaDia);
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
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6},{7},{8:F2},{9:F2},{10:F2},{11:F2},{12},{13}\n",
                    nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
                    estado, pos, stopPrice, trailingNivel,
                    dailyPnL, totalPnL, tradesToday, lastDecision);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }
```

- [ ] **Step 2: Add WriteTelemetry**

```csharp
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
  ""timestamp"": ""{0:yyyy-MM-dd HH:mm}"",
  ""strategy"": ""MNQ10min"",
  ""price"": {1:F2},
  ""rangoHigh"": {2:F2},
  ""rangoLow"": {3:F2},
  ""rangoPuntos"": {4:F2},
  ""position"": ""{5}"",
  ""estado"": ""{6}"",
  ""decision"": ""{7}"",
  ""entryPrice"": {8:F2},
  ""stopPrice"": {9:F2},
  ""stopPuntos"": {10:F0},
  ""trailingNivel"": {11:F2},
  ""contratosActuales"": {12},
  ""unrealizedPts"": {13:F2},
  ""unrealizedPnL"": {14:F2},
  ""dailyPnL"": {15:F2},
  ""totalPnL"": {16:F2},
  ""tradesToday"": {17},
  ""metaDia"": {18:F0},
  ""perdidaAcumulada"": {19:F2},
  ""breakevenHit"": {20},
  ""contratoAgregado"": {21},
  ""dayDone"": {22},
  ""lastAction"": ""{23}"",
  ""recentTrades"": [{24}]
}}",
                    nyNow, Close[0],
                    rangoHigh == double.MinValue ? 0 : rangoHigh,
                    rangoLow == double.MaxValue ? 0 : rangoLow,
                    rangoPuntos, pos, estado, lastDecision,
                    entryPrice, stopPrice, stopPuntos, trailingNivel,
                    contratosActuales, unrealizedPts, unrealizedPts * 0.50 * contratosActuales,
                    dailyPnL, totalPnL, tradesToday, metaDia, perdidaAcumulada,
                    breakevenHit ? "true" : "false",
                    contratoAgregado ? "true" : "false",
                    dayDone ? "true" : "false",
                    lastAction.Replace("\"", "'"), recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }
```

- [ ] **Step 3: Commit**

```bash
git add MNQ10min.cs
git commit -m "feat(mnq10min): CSV logging and telemetry JSON"
```

---

### Task 10: Skill file — mnq-10min-strategy

**Files:**
- Create: skill file for the MNQ10min strategy (same pattern as `mnq-probabilistic-strategy`)

- [ ] **Step 1: Locate existing skill directory**

Run: `find ~/.claude/plugins -name "mnq-probabilistic-strategy*" -type f 2>/dev/null` to find the pattern for custom skills.

If skills are in `.claude/commands/` instead, check there.

- [ ] **Step 2: Create the skill file**

Create the skill following the same structure as `mnq-probabilistic-strategy`, but adapted for the 10-minute range breakout strategy. Content should reference:
- The spec at `docs/superpowers/specs/2026-05-03-mnq10min-design.md`
- The 5 states: ESPERANDO_RANGO, ESPERANDO_RUPTURA, EN_TRADE, TRAILING, DIA_TERMINADO
- Entry rules: range breakout with body >= 60%
- Stop calculation: range + body + cushion
- Breakeven at 50%, add-on at breakeven, trailing at 80%
- Contract auto-sizing based on risk/stop
- Day goal = 1:1 of first trade's stop

- [ ] **Step 3: Commit**

```bash
git add <skill-file-path>
git commit -m "feat: add mnq-10min-strategy skill for range breakout analysis"
```

---

### Task 11: Copy to NinjaTrader and verify compilation

**Files:**
- Copy: `MNQ10min.cs` → `C:\Users\<USER>\OneDrive - Perficient, Inc\Documents\NinjaTrader 8\bin\Custom\Strategies\`

- [ ] **Step 1: Copy file to NinjaTrader Strategies directory**

```bash
cp MNQ10min.cs "/c/Users/santiago.burgos/OneDrive - Perficient, Inc/Documents/NinjaTrader 8/bin/Custom/Strategies/MNQ10min.cs"
```

- [ ] **Step 2: Verify file is in place**

```bash
ls -la "/c/Users/santiago.burgos/OneDrive - Perficient, Inc/Documents/NinjaTrader 8/bin/Custom/Strategies/MNQ10min.cs"
```

- [ ] **Step 3: Instruct user to compile in NinjaTrader**

Tell user: "Abre NinjaTrader → New → NinjaScript Editor → presiona F5 (compile). Verifica que MNQ10min aparece sin errores."

- [ ] **Step 4: Final commit with any compilation fixes**

```bash
git add MNQ10min.cs
git commit -m "fix(mnq10min): compilation fixes from NinjaTrader"
```
