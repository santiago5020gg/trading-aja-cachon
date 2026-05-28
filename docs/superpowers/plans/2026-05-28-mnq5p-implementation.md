# MNQ5P Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create MNQ5P.cs — a NinjaTrader strategy that detects EMA 20/200 crossovers, waits for ATR compression (channel), and enters on channel breakout with the same trailing/breakeven/risk system as MNQ10minV2.

**Architecture:** Single-file NinjaScript strategy following MNQ10minV2 patterns. State machine with 6 states (EsperandoCruce → ConfirmandoTendencia → BuscandoCanal → EsperandoBreakout → EnTrade → DiaTerminado). Reuses identical trailing, breakeven, position sizing, and session management logic.

**Tech Stack:** C# / NinjaScript (NinjaTrader 8), single .cs file

---

## File Structure

- **Create:** `MNQ5P.cs` — Complete strategy (single file, ~1200 lines)

---

### Task 1: Scaffold — Namespace, Enums, Parameters

**Files:**
- Create: `MNQ5P.cs`

- [ ] **Step 1: Create the file with using declarations, namespace, class, enums, and all NinjaScript parameters**

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
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MNQ5P : Strategy
    {
        #region Parameters

        // --- Setup (5 condiciones) ---
        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Barras Confirmacion Tendencia", GroupName = "1. Setup", Order = 1)]
        public int BarrasConfirmacion { get; set; }

        [NinjaScriptProperty]
        [Range(10, 90)]
        [Display(Name = "Umbral ATR Compresion (%)", GroupName = "1. Setup", Order = 2)]
        public int UmbralATR { get; set; }

        [NinjaScriptProperty]
        [Range(3, 100)]
        [Display(Name = "Barras Minimas Canal", GroupName = "1. Setup", Order = 3)]
        public int BarrasCanal { get; set; }

        [NinjaScriptProperty]
        [Range(5, 200)]
        [Display(Name = "Max Barras Espera Breakout", GroupName = "1. Setup", Order = 4)]
        public int MaxBarrasEspera { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Colchon Stop (pts)", GroupName = "1. Setup", Order = 5)]
        public int ColchonStop { get; set; }

        // --- Risk ---
        [NinjaScriptProperty]
        [Display(Name = "Colchon Breakeven (pts)", GroupName = "2. Risk", Order = 1)]
        public int ColchonBreakeven { get; set; }

        [NinjaScriptProperty]
        [Range(1, 99)]
        [Display(Name = "Breakeven Activacion (%)", GroupName = "2. Risk", Order = 2)]
        public int BreakevenPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades/Dia", GroupName = "2. Risk", Order = 3)]
        public int MaxTrades { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pérdida Máxima Diaria ($)", GroupName = "2. Risk", Order = 4)]
        public double PerdidaMaxDiaria { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo TP (1a1 o 1a2)", GroupName = "2. Risk", Order = 5)]
        public TPMode ModoTP { get; set; }

        // --- Session ---
        [NinjaScriptProperty]
        [Display(Name = "Hora Cierre (HH:mm)", GroupName = "3. Sesion", Order = 1)]
        public string HoraCierre { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar Asia (18:00-02:00 ET)", GroupName = "3. Sesion", Order = 2)]
        public bool OperarAsia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar Europa (02:00-09:30 ET)", GroupName = "3. Sesion", Order = 3)]
        public bool OperarEuropa { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar America (09:30-HoraCierre ET)", GroupName = "3. Sesion", Order = 4)]
        public bool OperarAmerica { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "4. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        // --- Trailing TP1 (4 escalones) ---
        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Cant Trailing TP1", GroupName = "5. Trailing TP1", Order = 0)]
        public int CantTrailTP1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc1 Activ (%)", GroupName = "5. Trailing TP1", Order = 1)]
        public int TP1Act1 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc1 Stop (%)", GroupName = "5. Trailing TP1", Order = 2)]
        public int TP1Stp1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc2 Activ (%)", GroupName = "5. Trailing TP1", Order = 3)]
        public int TP1Act2 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc2 Stop (%)", GroupName = "5. Trailing TP1", Order = 4)]
        public int TP1Stp2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc3 Activ (%)", GroupName = "5. Trailing TP1", Order = 5)]
        public int TP1Act3 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc3 Stop (%)", GroupName = "5. Trailing TP1", Order = 6)]
        public int TP1Stp3 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc4 Activ (%)", GroupName = "5. Trailing TP1", Order = 7)]
        public int TP1Act4 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc4 Stop (%)", GroupName = "5. Trailing TP1", Order = 8)]
        public int TP1Stp4 { get; set; }

        // --- Trailing TP2 (6 escalones) ---
        [NinjaScriptProperty]
        [Range(1, 6)]
        [Display(Name = "Cant Trailing TP2", GroupName = "6. Trailing TP2", Order = 0)]
        public int CantTrailTP2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc1 Activ (%)", GroupName = "6. Trailing TP2", Order = 1)]
        public int TP2Act1 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc1 Stop (%)", GroupName = "6. Trailing TP2", Order = 2)]
        public int TP2Stp1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc2 Activ (%)", GroupName = "6. Trailing TP2", Order = 3)]
        public int TP2Act2 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc2 Stop (%)", GroupName = "6. Trailing TP2", Order = 4)]
        public int TP2Stp2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc3 Activ (%)", GroupName = "6. Trailing TP2", Order = 5)]
        public int TP2Act3 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc3 Stop (%)", GroupName = "6. Trailing TP2", Order = 6)]
        public int TP2Stp3 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc4 Activ (%)", GroupName = "6. Trailing TP2", Order = 7)]
        public int TP2Act4 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc4 Stop (%)", GroupName = "6. Trailing TP2", Order = 8)]
        public int TP2Stp4 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc5 Activ (%)", GroupName = "6. Trailing TP2", Order = 9)]
        public int TP2Act5 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc5 Stop (%)", GroupName = "6. Trailing TP2", Order = 10)]
        public int TP2Stp5 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc6 Activ (%)", GroupName = "6. Trailing TP2", Order = 11)]
        public int TP2Act6 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc6 Stop (%)", GroupName = "6. Trailing TP2", Order = 12)]
        public int TP2Stp6 { get; set; }

        #endregion

        #region Enums and Variables

        public enum LogMode { Off, Day, Month }
        public enum TPMode { Solo1a1, Con1a2 }

        private enum BotState
        {
            EsperandoCruce,
            ConfirmandoTendencia,
            BuscandoCanal,
            EsperandoBreakout,
            EnTrade,
            DiaTerminado
        }

        private enum Sesion { Asia, Europa, America, Ninguna }

        // State machine
        private BotState estado;
        private Sesion sesionActual;

        // EMA crossover tracking
        private int direccionCruce; // 1=alcista, -1=bajista
        private double atrImpulso;

        // Trend confirmation
        private int barrasConfirmadas;

        // Channel detection
        private int barrasComprimidas;
        private double canalResistencia;
        private double canalSoporte;
        private double tamanoCanal;
        private int canalStartBar;

        // Breakout waiting
        private int barrasEsperando;

        // Trade management
        private int tradeDirection; // 1=long, -1=short, 0=flat
        private double entryPrice;
        private double stopDistance;
        private int qtyTP1;
        private int qtyTP2;
        private bool breakevenHit;
        private int tp1StopNivel;
        private int tp2StopNivel;
        private bool tradeEnded;
        private bool tradeEndedByTakeProfit;
        private bool tradeCounted;
        private string lastExitReason;

        // Risk management
        private double dailyPnL;
        private double totalPnL;
        private int tradesToday;
        private double peakDailyPnL;
        private double riesgo1Micro;
        private int tradesPermitidosHoy;
        private int contratosCalculados;
        private bool dayHardStop;
        private int stopsPuros;
        private int maxStopsPuros;
        private int takeProfitsHoy;
        private int maxTakeProfits;
        private int breakevensHoy;
        private int maxBreakevens;

        // Session/time
        private DateTime lastResetDate;
        private TimeZoneInfo easternZone;
        private int horaCierreH;
        private int horaCierreM;

        // Indicators
        private NinjaTrader.NinjaScript.Indicators.EMA ema20;
        private NinjaTrader.NinjaScript.Indicators.EMA ema200;
        private NinjaTrader.NinjaScript.Indicators.ATR atr14;

        // Logging
        private string lastDecision;
        private string lastAction;
        private List<string> tradeLog;
        private string botHistoryDir;
        private string csvLogPath;
        private string csvDailyPath;
        private string csvBarLogPath;

        #endregion
    }
}
```

- [ ] **Step 2: Verify the file compiles (it won't yet — just checks syntax)**

This step is just to confirm the file is well-formed. It won't compile standalone without NinjaTrader but syntactically should be correct.

- [ ] **Step 3: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): scaffold with enums, parameters, and variables"
```

---

### Task 2: OnStateChange — Defaults, Configure, DataLoaded

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Add OnStateChange method after the variables region, before closing brace of class**

```csharp
        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ5P — EMA 20/200 Channel Breakout";
                Name = "MNQ5P";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 2;
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
                RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 200;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                // Setup defaults
                BarrasConfirmacion = 5;
                UmbralATR = 50;
                BarrasCanal = 10;
                MaxBarrasEspera = 30;
                ColchonStop = 5;

                // Risk defaults
                ColchonBreakeven = 5;
                BreakevenPct = 60;
                MaxTrades = 3;
                PerdidaMaxDiaria = 400;
                ModoTP = TPMode.Con1a2;

                // Session defaults
                HoraCierre = "15:50";
                OperarAsia = false;
                OperarEuropa = false;
                OperarAmerica = true;
                ModoLog = LogMode.Month;

                // Trailing TP1 defaults
                CantTrailTP1 = 4;
                TP1Act1 = 75; TP1Stp1 = 45;
                TP1Act2 = 85; TP1Stp2 = 60;
                TP1Act3 = 95; TP1Stp3 = 80;
                TP1Act4 = 99; TP1Stp4 = 95;

                // Trailing TP2 defaults
                CantTrailTP2 = 6;
                TP2Act1 = 50; TP2Stp1 = 18;
                TP2Act2 = 70; TP2Stp2 = 50;
                TP2Act3 = 85; TP2Stp3 = 60;
                TP2Act4 = 90; TP2Stp4 = 70;
                TP2Act5 = 95; TP2Stp5 = 84;
                TP2Act6 = 98; TP2Stp6 = 94;
            }
            else if (State == State.Configure)
            {
                maxStopsPuros = (int)Math.Round((double)MaxTrades / 2, MidpointRounding.AwayFromZero);
                maxTakeProfits = (int)Math.Round((double)MaxTrades / 2, MidpointRounding.AwayFromZero);
                maxBreakevens = (int)Math.Round((double)MaxTrades / 2, MidpointRounding.AwayFromZero);
                EntriesPerDirection = 2;
            }
            else if (State == State.DataLoaded)
            {
                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastResetDate = DateTime.MinValue;
                tradeLog = new List<string>();
                lastAction = "";
                lastDecision = "WAITING";

                ema20 = EMA(20);
                ema200 = EMA(200);
                atr14 = ATR(14);

                string[] hcParts = HoraCierre.Split(':');
                horaCierreH = int.Parse(hcParts[0]);
                horaCierreM = int.Parse(hcParts[1]);

                botHistoryDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"perficient\AI path lean\trading 7\bot\history");
                if (ModoLog != LogMode.Off)
                {
                    try { Directory.CreateDirectory(botHistoryDir); } catch {}
                    csvLogPath = Path.Combine(botHistoryDir, "mnq5p_trades_log.csv");
                    csvDailyPath = Path.Combine(botHistoryDir, "mnq5p_daily_log.csv");
                    csvBarLogPath = Path.Combine(botHistoryDir, "mnq5p_bar_log.csv");
                    InitCsvLogs();
                }
            }
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): add OnStateChange with defaults and indicator setup"
```

---

### Task 3: Session Management and OnBarUpdate Shell

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Add session helpers (identical to MNQ10minV2 pattern)**

```csharp
        #region Session Helpers

        private Sesion GetSesionParaHora(DateTime nyNow)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;

            if (t >= 18.0) return Sesion.Asia;
            if (t >= 2.0 && t < 9.5) return Sesion.Europa;
            double cierre = horaCierreH + horaCierreM / 60.0;
            if (t >= 9.5 && t < cierre) return Sesion.America;
            if (t < 2.0) return Sesion.Asia;

            return Sesion.Ninguna;
        }

        private bool IsSesionHabilitada(Sesion s)
        {
            switch (s)
            {
                case Sesion.Asia: return OperarAsia;
                case Sesion.Europa: return OperarEuropa;
                case Sesion.America: return OperarAmerica;
                default: return false;
            }
        }

        private bool IsSessionStart(DateTime nyNow, Sesion s)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            switch (s)
            {
                case Sesion.Asia: return h == 18 && m >= 0 && m <= 2;
                case Sesion.Europa: return h == 2 && m >= 0 && m <= 2;
                case Sesion.America: return h == 9 && m >= 30 && m <= 32;
                default: return false;
            }
        }

        private bool IsAfterSessionClose(DateTime nyNow, Sesion s)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;
            switch (s)
            {
                case Sesion.Asia: return t >= 2.0 && t < 18.0;
                case Sesion.Europa: return t >= 9.5;
                case Sesion.America: return h > horaCierreH || (h == horaCierreH && m >= horaCierreM);
                default: return true;
            }
        }

        private bool IsBeforeSessionOpen(DateTime nyNow, Sesion s)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;
            switch (s)
            {
                case Sesion.Asia: return false;
                case Sesion.Europa: return t < 2.0;
                case Sesion.America: return t < 9.5;
                default: return true;
            }
        }

        #endregion
```

- [ ] **Step 2: Add ResetDaily and ResetSession**

```csharp
        #region Reset

        private void ResetDaily()
        {
            sesionActual = Sesion.Ninguna;
            dayHardStop = false;
            dailyPnL = 0;
            peakDailyPnL = 0;
            riesgo1Micro = 0;
            tradesPermitidosHoy = MaxTrades;
            contratosCalculados = 0;
            tradesToday = 0;
            stopsPuros = 0;
            takeProfitsHoy = 0;
            breakevensHoy = 0;
            ResetSetupState();
            lastDecision = "NEW_DAY";
            lastAction = "";
        }

        private void ResetSession()
        {
            ResetSetupState();
            lastDecision = string.Format("NEW_SESSION_{0}", sesionActual);
        }

        private void ResetSetupState()
        {
            estado = BotState.EsperandoCruce;
            tradeDirection = 0;
            entryPrice = 0;
            stopDistance = 0;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            tradeCounted = false;
            lastExitReason = "";
            direccionCruce = 0;
            atrImpulso = 0;
            barrasConfirmadas = 0;
            barrasComprimidas = 0;
            barrasEsperando = 0;
            canalResistencia = 0;
            canalSoporte = 0;
            tamanoCanal = 0;
            canalStartBar = 0;
        }

        #endregion
```

- [ ] **Step 3: Add OnBarUpdate with session management and state machine dispatch**

```csharp
        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);

            DateTime tradingDay = nyNow.Hour >= 18 ? nyNow.Date : nyNow.Date.AddDays(-1);

            if (tradingDay != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                {
                    LogDailyCsv(lastResetDate);
                    if (Position.MarketPosition != MarketPosition.Flat)
                        FlattenAll("CierreDia");
                }
                ResetDaily();
                lastResetDate = tradingDay;
            }

            LogBarCsv(nyNow);

            Sesion sesionParaHora = GetSesionParaHora(nyNow);
            bool sesionHabilitada = IsSesionHabilitada(sesionParaHora);

            if (sesionParaHora != sesionActual && sesionParaHora != Sesion.Ninguna)
            {
                if (sesionActual != Sesion.Ninguna && estado != BotState.DiaTerminado)
                {
                    if (Position.MarketPosition != MarketPosition.Flat)
                        FlattenAll("CierreSesion");
                }
                sesionActual = sesionParaHora;
                if (sesionHabilitada && estado != BotState.DiaTerminado)
                    ResetSession();
                else if (!sesionHabilitada)
                    estado = BotState.DiaTerminado;
            }

            if (estado == BotState.DiaTerminado)
            {
                if (dayHardStop) return;
                if (sesionParaHora != Sesion.Ninguna && sesionHabilitada && IsSessionStart(nyNow, sesionParaHora))
                {
                    double presupuestoRestante = dailyPnL < 0 ? PerdidaMaxDiaria - Math.Abs(dailyPnL) : PerdidaMaxDiaria;
                    if (presupuestoRestante > 0 && tradesToday < MaxTrades)
                        ResetSession();
                    else
                    {
                        dayHardStop = true;
                        return;
                    }
                }
                else return;
            }

            bool afterSessionClose = IsAfterSessionClose(nyNow, sesionActual);
            if (afterSessionClose && IsSesionHabilitada(sesionActual))
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                return;
            }

            bool fueraDeSesion = !IsSesionHabilitada(sesionActual) || IsBeforeSessionOpen(nyNow, sesionActual) || afterSessionClose;
            if (fueraDeSesion) return;

            if (tradeEnded) ProcesarFinTrade();

            switch (estado)
            {
                case BotState.EsperandoCruce:
                    ProcesarCruce();
                    break;
                case BotState.ConfirmandoTendencia:
                    ProcesarConfirmacion();
                    break;
                case BotState.BuscandoCanal:
                    ProcesarCanal();
                    break;
                case BotState.EsperandoBreakout:
                    ProcesarBreakout();
                    break;
                case BotState.EnTrade:
                    MonitorearTrade();
                    break;
            }
        }

        #endregion
```

- [ ] **Step 4: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): add session management, resets, and OnBarUpdate shell"
```

---

### Task 4: State EsperandoCruce — EMA Crossover Detection

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement ProcesarCruce**

```csharp
        #region Estado: EsperandoCruce

        private void ProcesarCruce()
        {
            if (CurrentBar < 2) return;

            double ema20Prev = ema20[1];
            double ema200Prev = ema200[1];
            double ema20Curr = ema20[0];
            double ema200Curr = ema200[0];

            // Cruce alcista: EMA20 cruza por encima de EMA200
            if (ema20Prev <= ema200Prev && ema20Curr > ema200Curr)
            {
                direccionCruce = 1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                estado = BotState.ConfirmandoTendencia;
                lastDecision = string.Format("CRUCE_ALCISTA EMA20={0:F2} EMA200={1:F2} ATR={2:F2}", ema20Curr, ema200Curr, atrImpulso);
                return;
            }

            // Cruce bajista: EMA20 cruza por debajo de EMA200
            if (ema20Prev >= ema200Prev && ema20Curr < ema200Curr)
            {
                direccionCruce = -1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                estado = BotState.ConfirmandoTendencia;
                lastDecision = string.Format("CRUCE_BAJISTA EMA20={0:F2} EMA200={1:F2} ATR={2:F2}", ema20Curr, ema200Curr, atrImpulso);
                return;
            }

            lastDecision = string.Format("ESPERANDO_CRUCE EMA20={0:F2} EMA200={1:F2}", ema20Curr, ema200Curr);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement EsperandoCruce state - EMA crossover detection"
```

---

### Task 5: State ConfirmandoTendencia — Trend Confirmation

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement ProcesarConfirmacion**

```csharp
        #region Estado: ConfirmandoTendencia

        private void ProcesarConfirmacion()
        {
            bool precioConfirmado;

            if (direccionCruce == 1)
                precioConfirmado = Close[0] > ema20[0] && Close[0] > ema200[0];
            else
                precioConfirmado = Close[0] < ema20[0] && Close[0] < ema200[0];

            if (precioConfirmado)
            {
                barrasConfirmadas++;
                if (barrasConfirmadas >= BarrasConfirmacion)
                {
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("TENDENCIA_CONFIRMADA dir={0} barras={1}",
                        direccionCruce == 1 ? "ALCISTA" : "BAJISTA", barrasConfirmadas);
                    return;
                }
                lastDecision = string.Format("CONFIRMANDO {0}/{1} dir={2}",
                    barrasConfirmadas, BarrasConfirmacion, direccionCruce == 1 ? "ALC" : "BAJ");
            }
            else
            {
                barrasConfirmadas = 0;
                estado = BotState.EsperandoCruce;
                lastDecision = "CONFIRMACION_FALLIDA vuelve a EsperandoCruce";
            }
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement ConfirmandoTendencia state"
```

---

### Task 6: State BuscandoCanal — ATR Compression Detection

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement ProcesarCanal**

```csharp
        #region Estado: BuscandoCanal

        private void ProcesarCanal()
        {
            if (atrImpulso <= 0) { estado = BotState.EsperandoCruce; return; }

            double atrActual = atr14[0];
            double umbral = atrImpulso * (UmbralATR / 100.0);

            if (atrActual < umbral)
            {
                barrasComprimidas++;

                if (barrasComprimidas >= BarrasCanal)
                {
                    // Calculate channel from last BarrasCanal bars
                    canalResistencia = double.MinValue;
                    canalSoporte = double.MaxValue;
                    for (int i = 0; i < BarrasCanal; i++)
                    {
                        if (High[i] > canalResistencia) canalResistencia = High[i];
                        if (Low[i] < canalSoporte) canalSoporte = Low[i];
                    }
                    tamanoCanal = canalResistencia - canalSoporte;
                    canalStartBar = CurrentBar;
                    barrasEsperando = 0;
                    estado = BotState.EsperandoBreakout;

                    // Draw channel
                    Draw.HorizontalLine(this, "CanalRes", canalResistencia, Brushes.Lime, DashStyleHelper.Dash, 2);
                    Draw.HorizontalLine(this, "CanalSop", canalSoporte, Brushes.Red, DashStyleHelper.Dash, 2);

                    lastDecision = string.Format("CANAL_DETECTADO R={0:F2} S={1:F2} tam={2:F2} ATR={3:F2}<{4:F2}",
                        canalResistencia, canalSoporte, tamanoCanal, atrActual, umbral);
                    return;
                }

                lastDecision = string.Format("COMPRESION {0}/{1} ATR={2:F2}<{3:F2}",
                    barrasComprimidas, BarrasCanal, atrActual, umbral);
            }
            else
            {
                barrasComprimidas = 0;
                lastDecision = string.Format("ATR_EXPANDIDO ATR={0:F2}>={1:F2} reset", atrActual, umbral);
            }

            // Check if EMA cross invalidated (price crossed wrong side)
            bool tendenciaValida;
            if (direccionCruce == 1)
                tendenciaValida = ema20[0] > ema200[0];
            else
                tendenciaValida = ema20[0] < ema200[0];

            if (!tendenciaValida)
            {
                estado = BotState.EsperandoCruce;
                lastDecision = "TENDENCIA_INVALIDADA EMAs cruzaron de vuelta";
            }
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement BuscandoCanal state - ATR compression detection"
```

---

### Task 7: State EsperandoBreakout — Channel Breakout Entry

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement ProcesarBreakout with position sizing and order placement**

```csharp
        #region Estado: EsperandoBreakout

        private void ProcesarBreakout()
        {
            barrasEsperando++;

            if (barrasEsperando > MaxBarrasEspera)
            {
                RemoveDrawObject("CanalRes");
                RemoveDrawObject("CanalSop");
                barrasComprimidas = 0;
                estado = BotState.BuscandoCanal;
                lastDecision = string.Format("BREAKOUT_TIMEOUT {0} barras, vuelve a BuscandoCanal", barrasEsperando);
                return;
            }

            // Breakout al alza
            if (Close[0] > canalResistencia)
            {
                CalcularRiesgoYEntrar(1);
                return;
            }

            // Breakout a la baja
            if (Close[0] < canalSoporte)
            {
                CalcularRiesgoYEntrar(-1);
                return;
            }

            lastDecision = string.Format("ESPERANDO_BREAKOUT {0}/{1} R={2:F2} S={3:F2}",
                barrasEsperando, MaxBarrasEspera, canalResistencia, canalSoporte);
        }

        private void CalcularRiesgoYEntrar(int dir)
        {
            stopDistance = tamanoCanal + ColchonStop;

            // Position sizing
            riesgo1Micro = stopDistance * Instrument.MasterInstrument.PointValue;
            double presupuestoDisponible = dailyPnL < 0 ? PerdidaMaxDiaria - Math.Abs(dailyPnL) : PerdidaMaxDiaria;
            int tradesRestantes = MaxTrades - tradesToday;

            if (tradesRestantes <= 0)
            {
                estado = BotState.DiaTerminado;
                dayHardStop = true;
                lastDecision = "DIA_TERMINADO_MAX_TRADES";
                return;
            }

            double presupuestoIdeal = presupuestoDisponible / tradesRestantes;

            if (presupuestoIdeal >= riesgo1Micro)
            {
                contratosCalculados = (int)Math.Floor(presupuestoIdeal / riesgo1Micro);
                tradesPermitidosHoy = tradesToday + tradesRestantes;
            }
            else if (riesgo1Micro <= presupuestoDisponible)
            {
                contratosCalculados = 1;
                tradesPermitidosHoy = tradesToday + (int)Math.Floor(presupuestoDisponible / riesgo1Micro);
            }
            else
            {
                contratosCalculados = 0;
                estado = BotState.DiaTerminado;
                lastDecision = string.Format("FUERA_PRESUPUESTO riesgo=${0:F2} > disp=${1:F2}", riesgo1Micro, presupuestoDisponible);
                return;
            }
            if (tradesPermitidosHoy > MaxTrades)
                tradesPermitidosHoy = MaxTrades;

            // Split TP1/TP2
            if (ModoTP == TPMode.Solo1a1)
            {
                qtyTP1 = contratosCalculados;
                qtyTP2 = 0;
            }
            else
            {
                if (contratosCalculados >= 3)
                {
                    qtyTP1 = contratosCalculados - 1;
                    qtyTP2 = 1;
                }
                else if (contratosCalculados == 2)
                {
                    qtyTP1 = 1;
                    qtyTP2 = 1;
                }
                else
                {
                    qtyTP1 = 0;
                    qtyTP2 = 1;
                }
            }

            // Place orders
            double stopLong = canalSoporte - ColchonStop;
            double stopShort = canalResistencia + ColchonStop;
            double tpTicks = stopDistance / TickSize;
            double tp2Ticks = (stopDistance * 2) / TickSize;

            if (dir == 1)
            {
                if (qtyTP1 > 0)
                {
                    SetStopLoss("TP1Long", CalculationMode.Price, stopLong, true);
                    SetProfitTarget("TP1Long", CalculationMode.Ticks, tpTicks);
                    EnterLong(qtyTP1, "TP1Long");
                }
                if (qtyTP2 > 0)
                {
                    SetStopLoss("TP2Long", CalculationMode.Price, stopLong, true);
                    SetProfitTarget("TP2Long", CalculationMode.Ticks, tp2Ticks);
                    EnterLong(qtyTP2, "TP2Long");
                }
                lastDecision = string.Format("ENTRY_LONG TP1x{0} TP2x{1} @{2:F2} stop={3:F2} canal={4:F2}",
                    qtyTP1, qtyTP2, Close[0], stopLong, tamanoCanal);
            }
            else
            {
                if (qtyTP1 > 0)
                {
                    SetStopLoss("TP1Short", CalculationMode.Price, stopShort, true);
                    SetProfitTarget("TP1Short", CalculationMode.Ticks, tpTicks);
                    EnterShort(qtyTP1, "TP1Short");
                }
                if (qtyTP2 > 0)
                {
                    SetStopLoss("TP2Short", CalculationMode.Price, stopShort, true);
                    SetProfitTarget("TP2Short", CalculationMode.Ticks, tp2Ticks);
                    EnterShort(qtyTP2, "TP2Short");
                }
                lastDecision = string.Format("ENTRY_SHORT TP1x{0} TP2x{1} @{2:F2} stop={3:F2} canal={4:F2}",
                    qtyTP1, qtyTP2, Close[0], stopShort, tamanoCanal);
            }

            Draw.Diamond(this, "Entry" + CurrentBar, true, 0, Close[0], dir == 1 ? Brushes.Lime : Brushes.Red);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement EsperandoBreakout state with position sizing and entry"
```

---

### Task 8: State EnTrade — Breakeven + Trailing (identical to MNQ10minV2)

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement MonitorearTrade with breakeven and trailing logic**

```csharp
        #region Estado: EnTrade

        private void MonitorearTrade()
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (!tradeEnded)
                {
                    tradeEnded = true;
                    if (lastExitReason == "TakeProfit")
                        tradeEndedByTakeProfit = true;
                }
                return;
            }

            double unrealPts = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];

            string signalTP1 = tradeDirection == 1 ? "TP1Long" : "TP1Short";
            string signalTP2 = tradeDirection == 1 ? "TP2Long" : "TP2Short";

            // Breakeven
            if (!breakevenHit && unrealPts >= stopDistance * (BreakevenPct / 100.0))
            {
                double beStop = tradeDirection == 1 ? entryPrice + ColchonBreakeven : entryPrice - ColchonBreakeven;
                if (qtyTP1 > 0) SetStopLoss(signalTP1, CalculationMode.Price, beStop, true);
                if (qtyTP2 > 0) SetStopLoss(signalTP2, CalculationMode.Price, beStop, true);
                breakevenHit = true;
                lastDecision = string.Format("BREAKEVEN stop={0:F2}", beStop);
                return;
            }

            // Trailing TP1
            if (qtyTP1 > 0 && breakevenHit)
            {
                if (CantTrailTP1 >= 4 && tp1StopNivel < 4 && unrealPts >= stopDistance * (TP1Act4 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp4 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp4 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, true);
                    tp1StopNivel = 4;
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp4, tp1Stop);
                    return;
                }
                if (CantTrailTP1 >= 3 && tp1StopNivel < 3 && unrealPts >= stopDistance * (TP1Act3 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp3 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp3 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, true);
                    tp1StopNivel = 3;
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp3, tp1Stop);
                    return;
                }
                if (CantTrailTP1 >= 2 && tp1StopNivel < 2 && unrealPts >= stopDistance * (TP1Act2 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp2 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp2 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, true);
                    tp1StopNivel = 2;
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp2, tp1Stop);
                    return;
                }
                if (tp1StopNivel < 1 && unrealPts >= stopDistance * (TP1Act1 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp1 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp1 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, true);
                    tp1StopNivel = 1;
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp1, tp1Stop);
                    return;
                }
            }

            // Trailing TP2
            if (qtyTP2 > 0 && breakevenHit)
            {
                double tp2Target = stopDistance * 2;

                if (CantTrailTP2 >= 6 && tp2StopNivel < 6 && unrealPts >= tp2Target * (TP2Act6 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp6 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp6 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 6;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp6, nuevoStop);
                    return;
                }
                if (CantTrailTP2 >= 5 && tp2StopNivel < 5 && unrealPts >= tp2Target * (TP2Act5 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp5 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp5 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 5;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp5, nuevoStop);
                    return;
                }
                if (CantTrailTP2 >= 4 && tp2StopNivel < 4 && unrealPts >= tp2Target * (TP2Act4 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp4 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp4 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 4;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp4, nuevoStop);
                    return;
                }
                if (CantTrailTP2 >= 3 && tp2StopNivel < 3 && unrealPts >= tp2Target * (TP2Act3 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp3 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp3 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 3;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp3, nuevoStop);
                    return;
                }
                if (CantTrailTP2 >= 2 && tp2StopNivel < 2 && unrealPts >= tp2Target * (TP2Act2 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp2 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp2 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 2;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp2, nuevoStop);
                    return;
                }
                if (tp2StopNivel < 1 && unrealPts >= tp2Target * (TP2Act1 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp1 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp1 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, true);
                    tp2StopNivel = 1;
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp1, nuevoStop);
                    return;
                }
            }

            lastDecision = string.Format("EN_TRADE {0} entry={1:F2} unreal={2:F2}pts",
                tradeDirection == 1 ? "LONG" : "SHORT", entryPrice, unrealPts);
        }

        #endregion
```

- [ ] **Step 2: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement EnTrade state with breakeven and trailing TP1/TP2"
```

---

### Task 9: Post-Trade Transition and OnExecutionUpdate/OnOrderUpdate

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement ProcesarFinTrade — after trade ends, decide next state**

```csharp
        #region Post-Trade

        private void ProcesarFinTrade()
        {
            tradeEnded = false;
            tradeDirection = 0;
            entryPrice = 0;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            breakevenHit = false;
            tradeCounted = false;
            string exitReason = lastExitReason;

            if (tradeEndedByTakeProfit)
            {
                takeProfitsHoy++;
                tradeEndedByTakeProfit = false;

                if (tradesToday >= MaxTrades)
                {
                    estado = BotState.DiaTerminado;
                    dayHardStop = true;
                }
                else if (takeProfitsHoy >= maxTakeProfits)
                    estado = BotState.DiaTerminado;
                else if (tradesToday >= tradesPermitidosHoy)
                    estado = BotState.DiaTerminado;
                else
                {
                    // Back to BuscandoCanal — trend is still valid, look for new channel
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = "POST_TP vuelve a BuscandoCanal";
                }
            }
            else if (exitReason == "StopLoss")
            {
                stopsPuros++;
                tradeEndedByTakeProfit = false;

                if (tradesToday >= MaxTrades)
                {
                    estado = BotState.DiaTerminado;
                    dayHardStop = true;
                }
                else if (stopsPuros >= maxStopsPuros)
                    estado = BotState.DiaTerminado;
                else if (tradesToday >= tradesPermitidosHoy)
                    estado = BotState.DiaTerminado;
                else
                {
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = "POST_SL vuelve a BuscandoCanal";
                }
            }
            else
            {
                // Breakeven exit
                breakevensHoy++;
                tradeEndedByTakeProfit = false;

                double perdidaReal = dailyPnL < 0 ? Math.Abs(dailyPnL) : 0;
                double presupuestoRestante = PerdidaMaxDiaria - perdidaReal;
                if (riesgo1Micro > 0 && presupuestoRestante >= riesgo1Micro)
                    tradesPermitidosHoy = tradesToday + (int)Math.Floor(presupuestoRestante / riesgo1Micro);
                if (tradesPermitidosHoy > MaxTrades)
                    tradesPermitidosHoy = MaxTrades;

                if (tradesToday >= MaxTrades)
                {
                    estado = BotState.DiaTerminado;
                    dayHardStop = true;
                }
                else if (breakevensHoy >= maxBreakevens)
                    estado = BotState.DiaTerminado;
                else if (tradesToday >= tradesPermitidosHoy)
                    estado = BotState.DiaTerminado;
                else
                {
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("POST_BE vuelve a BuscandoCanal tradesRest={0}", tradesPermitidosHoy - tradesToday);
                }
            }

            RemoveDrawObject("CanalRes");
            RemoveDrawObject("CanalSop");
        }

        #endregion
```

- [ ] **Step 2: Implement OnExecutionUpdate (identical pattern to MNQ10minV2)**

```csharp
        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;

            string orderName = execution.Order.Name;

            bool isLongEntry = orderName == "TP1Long" || orderName == "TP2Long";
            bool isShortEntry = orderName == "TP1Short" || orderName == "TP2Short";

            if (isLongEntry)
            {
                if (entryPrice == 0) entryPrice = price;
                tradeDirection = 1;
                if (!tradeCounted) { tradesToday++; tradeCounted = true; }
                estado = BotState.EnTrade;
                lastAction = string.Format("FILL LONG {0} x{1} @{2:F2}", orderName, quantity, price);
                tradeLog.Add(lastAction);

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "LONG", price, 0, orderName);
            }
            else if (isShortEntry)
            {
                if (entryPrice == 0) entryPrice = price;
                tradeDirection = -1;
                if (!tradeCounted) { tradesToday++; tradeCounted = true; }
                estado = BotState.EnTrade;
                lastAction = string.Format("FILL SHORT {0} x{1} @{2:F2}", orderName, quantity, price);
                tradeLog.Add(lastAction);

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "SHORT", price, 0, orderName);
            }
            else if (orderName == "Stop loss" || orderName == "Profit target")
            {
                if (tradeDirection == 0) return;

                string dir = tradeDirection == 1 ? "LONG" : "SHORT";
                string reason;
                if (orderName == "Profit target")
                    reason = "TakeProfit";
                else if (breakevenHit)
                    reason = "Breakeven";
                else
                    reason = "StopLoss";

                double pnl = 0;
                if (tradeDirection == 1)
                    pnl = (price - entryPrice) * Instrument.MasterInstrument.PointValue * quantity;
                else
                    pnl = (entryPrice - price) * Instrument.MasterInstrument.PointValue * quantity;

                dailyPnL += pnl;
                totalPnL += pnl;

                lastAction = string.Format("EXIT {0} {1} x{2} @{3:F2} pnl=${4:F2}", dir, reason, quantity, price, pnl);
                tradeLog.Add(lastAction);

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", dir, price, pnl, reason);

                lastExitReason = reason;

                if (marketPosition == MarketPosition.Flat)
                {
                    tradeEnded = true;
                    if (reason == "TakeProfit")
                        tradeEndedByTakeProfit = true;

                    if (dailyPnL > peakDailyPnL) peakDailyPnL = dailyPnL;
                    double drawdown = peakDailyPnL - dailyPnL;
                    if (drawdown >= PerdidaMaxDiaria)
                    {
                        estado = BotState.DiaTerminado;
                        dayHardStop = true;
                    }
                }
            }
        }

        #endregion
```

- [ ] **Step 3: Implement OnOrderUpdate (stop rejection handling)**

```csharp
        #region OnOrderUpdate

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (orderState == OrderState.Rejected && order.Name == "Stop loss")
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong();
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort();

                estado = BotState.DiaTerminado;
                lastDecision = string.Format("STOP_RECHAZADO cerrar posicion @{0:F2}", Close[0]);
            }
        }

        #endregion
```

- [ ] **Step 4: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): implement post-trade transitions, OnExecutionUpdate, OnOrderUpdate"
```

---

### Task 10: Helpers — FlattenAll, CancelOrders, Logging

**Files:**
- Modify: `MNQ5P.cs`

- [ ] **Step 1: Implement FlattenAll and CancelAllPendingOrders**

```csharp
        #region Helpers

        private void FlattenAll(string reason)
        {
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (qtyTP1 > 0) ExitLong("X_" + reason, "TP1Long");
                if (qtyTP2 > 0) ExitLong("X_" + reason, "TP2Long");
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (qtyTP1 > 0) ExitShort("X_" + reason, "TP1Short");
                if (qtyTP2 > 0) ExitShort("X_" + reason, "TP2Short");
            }

            Draw.Diamond(this, "Exit" + CurrentBar, true, 0, Close[0], Brushes.Yellow);
            tradeDirection = 0;
            lastAction = string.Format("FLATTEN {0}", reason);
        }

        private void CancelAllPendingOrders()
        {
            foreach (Order order in Account.Orders)
            {
                if (order.Instrument == Instrument &&
                    (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted))
                {
                    CancelOrder(order);
                }
            }
        }

        #endregion
```

- [ ] **Step 2: Implement CSV logging methods**

```csharp
        #region Logging

        private void InitCsvLogs()
        {
            if (!File.Exists(csvLogPath))
                File.WriteAllText(csvLogPath, "Timestamp;Tipo;Dir;Precio;PnL;Signal\n");
            if (!File.Exists(csvDailyPath))
                File.WriteAllText(csvDailyPath, "Fecha;Trades;PnL;Acumulado\n");
            if (!File.Exists(csvBarLogPath))
                File.WriteAllText(csvBarLogPath, "Timestamp;Open;High;Low;Close;Vol;EMA20;EMA200;ATR;Estado\n");
        }

        private void LogTradeCsv(DateTime nyTime, string tipo, string dir, double precio, double pnl, string signal)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0};{1};{2};{3};{4};{5}\n",
                    nyTime.ToString("yyyy-MM-dd HH:mm:ss"), tipo, dir,
                    precio.ToString("F2").Replace('.', ','),
                    pnl.ToString("F2").Replace('.', ','), signal);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime tradingDay)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0};{1};{2};{3}\n",
                    tradingDay.ToString("yyyy-MM-dd"), tradesToday,
                    dailyPnL.ToString("F2").Replace('.', ','),
                    totalPnL.ToString("F2").Replace('.', ','));
                File.AppendAllText(csvDailyPath, line);
            }
            catch {}
        }

        private void LogBarCsv(DateTime nyTime)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}\n",
                    nyTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Open[0].ToString("F2").Replace('.', ','),
                    High[0].ToString("F2").Replace('.', ','),
                    Low[0].ToString("F2").Replace('.', ','),
                    Close[0].ToString("F2").Replace('.', ','),
                    Volume[0],
                    ema20[0].ToString("F2").Replace('.', ','),
                    ema200[0].ToString("F2").Replace('.', ','),
                    atr14[0].ToString("F2").Replace('.', ','),
                    estado);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        #endregion
```

- [ ] **Step 3: Commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): add FlattenAll, CancelOrders, and CSV logging"
```

---

### Task 11: Final Assembly and Compilation Check

**Files:**
- Modify: `MNQ5P.cs` (assemble all pieces into proper order)

- [ ] **Step 1: Verify the complete file has correct region ordering and all braces close properly**

Expected region order:
1. Using declarations
2. Parameters
3. Enums and Variables
4. Lifecycle (OnStateChange)
5. OnBarUpdate
6. Estado: EsperandoCruce
7. Estado: ConfirmandoTendencia
8. Estado: BuscandoCanal
9. Estado: EsperandoBreakout
10. Estado: EnTrade
11. Post-Trade
12. OnExecutionUpdate
13. OnOrderUpdate
14. Session Helpers
15. Reset
16. Helpers
17. Logging

- [ ] **Step 2: Run `dotnet build csim` to verify compilation (will need ATR/EMA stubs if not present)**

```bash
dotnet build csim
```

If compilation fails due to missing EMA/ATR stubs in csim, that's expected — MNQ5P is for NinjaTrader, not csim. The file structure is correct.

- [ ] **Step 3: Final commit**

```bash
git add MNQ5P.cs
git commit -m "feat(MNQ5P): complete strategy - EMA channel breakout with trailing/risk management"
```

---

## Summary

| Task | Description | Key Files |
|------|-------------|-----------|
| 1 | Scaffold: namespace, enums, parameters, variables | MNQ5P.cs (create) |
| 2 | OnStateChange: defaults, indicators | MNQ5P.cs |
| 3 | Session management + OnBarUpdate shell | MNQ5P.cs |
| 4 | EsperandoCruce: EMA crossover | MNQ5P.cs |
| 5 | ConfirmandoTendencia: trend validation | MNQ5P.cs |
| 6 | BuscandoCanal: ATR compression | MNQ5P.cs |
| 7 | EsperandoBreakout: entry + position sizing | MNQ5P.cs |
| 8 | EnTrade: breakeven + trailing | MNQ5P.cs |
| 9 | Post-trade + OnExecutionUpdate + OnOrderUpdate | MNQ5P.cs |
| 10 | Helpers: flatten, cancel, logging | MNQ5P.cs |
| 11 | Final assembly + compilation check | MNQ5P.cs |
