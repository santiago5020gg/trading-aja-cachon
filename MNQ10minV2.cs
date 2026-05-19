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
    public class MNQ10minV2 : Strategy
    {
        #region Parameters

        [NinjaScriptProperty]
        [Display(Name = "Colchon Stop (pts)", GroupName = "1. Risk", Order = 1)]
        public int ColchonStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Colchon Breakeven (pts)", GroupName = "1. Risk", Order = 2)]
        public int ColchonBreakeven { get; set; }

        [NinjaScriptProperty]
        [Range(1, 99)]
        [Display(Name = "Breakeven Activacion (%)", GroupName = "1. Risk", Order = 3)]
        public int BreakevenPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Trades/Dia", GroupName = "1. Risk", Order = 4)]
        public int MaxTrades { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pérdida Máxima Diaria ($)", GroupName = "1. Risk", Order = 5)]
        public double PerdidaMaxDiaria { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo TP (1a1 o 1a2)", GroupName = "1. Risk", Order = 6)]
        public TPMode ModoTP { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Operacion", GroupName = "1. Risk", Order = 7)]
        public OperationMode ModoOperacion { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Hora Cierre (HH:mm)", GroupName = "2. Sesion", Order = 1)]
        public string HoraCierre { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar Asia (18:02-02:00 ET)", GroupName = "2. Sesion", Order = 2)]
        public bool OperarAsia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar Europa (02:02-09:30 ET)", GroupName = "2. Sesion", Order = 3)]
        public bool OperarEuropa { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Operar America (09:32-HoraCierre ET)", GroupName = "2. Sesion", Order = 4)]
        public bool OperarAmerica { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Log", GroupName = "3. Logging", Order = 1)]
        public LogMode ModoLog { get; set; }

        // --- Trailing TP1 ---
        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Cant Trailing TP1", GroupName = "4. Trailing TP1", Order = 0)]
        public int CantTrailTP1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc1 Activ (%)", GroupName = "4. Trailing TP1", Order = 1)]
        public int TP1Act1 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc1 Stop (%)", GroupName = "4. Trailing TP1", Order = 2)]
        public int TP1Stp1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc2 Activ (%)", GroupName = "4. Trailing TP1", Order = 3)]
        public int TP1Act2 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc2 Stop (%)", GroupName = "4. Trailing TP1", Order = 4)]
        public int TP1Stp2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc3 Activ (%)", GroupName = "4. Trailing TP1", Order = 5)]
        public int TP1Act3 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc3 Stop (%)", GroupName = "4. Trailing TP1", Order = 6)]
        public int TP1Stp3 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc4 Activ (%)", GroupName = "4. Trailing TP1", Order = 7)]
        public int TP1Act4 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP1 Esc4 Stop (%)", GroupName = "4. Trailing TP1", Order = 8)]
        public int TP1Stp4 { get; set; }

        // --- Trailing TP2 ---
        [NinjaScriptProperty]
        [Range(1, 6)]
        [Display(Name = "Cant Trailing TP2", GroupName = "5. Trailing TP2", Order = 0)]
        public int CantTrailTP2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc1 Activ (%)", GroupName = "5. Trailing TP2", Order = 1)]
        public int TP2Act1 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc1 Stop (%)", GroupName = "5. Trailing TP2", Order = 2)]
        public int TP2Stp1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc2 Activ (%)", GroupName = "5. Trailing TP2", Order = 3)]
        public int TP2Act2 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc2 Stop (%)", GroupName = "5. Trailing TP2", Order = 4)]
        public int TP2Stp2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc3 Activ (%)", GroupName = "5. Trailing TP2", Order = 5)]
        public int TP2Act3 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc3 Stop (%)", GroupName = "5. Trailing TP2", Order = 6)]
        public int TP2Stp3 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc4 Activ (%)", GroupName = "5. Trailing TP2", Order = 7)]
        public int TP2Act4 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc4 Stop (%)", GroupName = "5. Trailing TP2", Order = 8)]
        public int TP2Stp4 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc5 Activ (%)", GroupName = "5. Trailing TP2", Order = 9)]
        public int TP2Act5 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc5 Stop (%)", GroupName = "5. Trailing TP2", Order = 10)]
        public int TP2Stp5 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc6 Activ (%)", GroupName = "5. Trailing TP2", Order = 11)]
        public int TP2Act6 { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "TP2 Esc6 Stop (%)", GroupName = "5. Trailing TP2", Order = 12)]
        public int TP2Stp6 { get; set; }

        #endregion

        #region Variables

        public enum LogMode { Off, Day, Month }
        public enum TPMode { Solo1a1, Con1a2 }
        public enum OperationMode { Operar, Visualizar }

        private enum BotState
        {
            EsperandoRango,
            OrdenesPuestas,
            EnTrade,
            DiaTerminado
        }

        private enum Sesion { Asia, Europa, America, Ninguna }

        private BotState estado;
        private Sesion sesionActual;
        private int tradeDirection;       // 1=long, -1=short, 0=flat
        private double entryPrice;
        private double stopDistance;       // in points

        private double rangoHigh;
        private double rangoLow;
        private double rangoPuntos;
        private int rangoStartBar;
        private int rangoEndBar;

        private int qtyTP1;
        private int qtyTP2;

        private bool longUsado;
        private bool shortUsado;
        private bool reentryPriceInRange;
        private bool breakevenHit;
        private int tp1StopNivel;
        private int tp2StopNivel;
        private bool tradeEnded;
        private bool tradeEndedByTakeProfit;
        private bool pendingFlip;
        private int pendingFlipDirection;
        private bool tradeCounted;
        private string lastExitReason;
        private int lastExitDirection;
        private double dailyPnL;
        private double totalPnL;
        private int tradesToday;

        private int stopsPuros;
        private int maxStopsPuros;
        private int takeProfitsHoy;
        private int maxTakeProfits;
        private int breakevensHoy;
        private int maxBreakevens;
        private bool cooldownActivo;
        private DateTime breakevenExitTime;

        private double peakDailyPnL;
        private double riesgo1Micro;
        private int tradesPermitidosHoy;
        private int contratosCalculados;
        private bool dayHardStop;

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

        // Visualizar mode
        private bool vizTradeActive;
        private double vizStopTP1;
        private double vizStopTP2;
        private bool vizTP1Active;
        private bool vizTP2Active;
        private int vizTradeNum;
        private string vizPrefix;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQ10minV2 — Breakout rango 09:32-09:40";
                Name = "MNQ10minV2";
                Calculate = Calculate.OnEachTick;
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
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                IsInstantiatedOnEachOptimizationIteration = true;
                IsOverlay = true;

                ColchonStop = 5;
                ColchonBreakeven = 5;
                BreakevenPct = 60;
                MaxTrades = 2;
                PerdidaMaxDiaria = 400;
                ModoTP = TPMode.Con1a2;
                ModoOperacion = OperationMode.Operar;
                HoraCierre = "15:50";
                OperarAsia = false;
                OperarEuropa = false;
                OperarAmerica = true;
                ModoLog = LogMode.Month;

                // Trailing TP1 defaults (activacion% -> stop%)
                CantTrailTP1 = 4;
                TP1Act1 = 75; TP1Stp1 = 45;
                TP1Act2 = 85; TP1Stp2 = 60;
                TP1Act3 = 95; TP1Stp3 = 80;
                TP1Act4 = 99; TP1Stp4 = 95;

                // Trailing TP2 defaults (activacion% -> stop%)
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

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);

            // Trading day starts at 18:00 ET (Asia open) and ends at 15:50 ET next calendar day
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

            bool firstTick = IsFirstTickOfBar;

            if (firstTick) LogBarCsv(nyNow);

            // Determine which session we're in and if it's enabled
            Sesion sesionParaHora = GetSesionParaHora(nyNow);
            bool sesionHabilitada = IsSesionHabilitada(sesionParaHora);

            // Session transition: if we moved to a new session, reset session state
            if (sesionParaHora != sesionActual && sesionParaHora != Sesion.Ninguna)
            {
                if (sesionActual != Sesion.Ninguna && estado != BotState.DiaTerminado)
                {
                    if (ModoOperacion == OperationMode.Visualizar && vizTradeActive)
                        VizSalir("CierreSesion");
                    else if (Position.MarketPosition != MarketPosition.Flat)
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
                if (firstTick)
                {
                    CancelAllPendingOrders();
                    LimpiarDibujosFueraSesion();
                }
                // If day was hard-stopped (drawdown, max trades global), don't revive
                if (dayHardStop)
                {
                    lastDecision = "DIA_TERMINADO_HARD_STOP";
                    if (firstTick) WriteTelemetry(nyNow);
                    return;
                }
                // Check if a new enabled session starts — revive with remaining budget
                if (sesionParaHora != Sesion.Ninguna && sesionHabilitada && IsSessionStart(nyNow, sesionParaHora))
                {
                    double presupuestoRestante = dailyPnL < 0 ? PerdidaMaxDiaria - Math.Abs(dailyPnL) : PerdidaMaxDiaria;
                    if (presupuestoRestante > 0 && tradesToday < MaxTrades)
                    {
                        ResetSession();
                    }
                    else
                    {
                        dayHardStop = true;
                        lastDecision = "DIA_TERMINADO_SIN_PRESUPUESTO";
                        if (firstTick) WriteTelemetry(nyNow);
                        return;
                    }
                }
                else
                {
                    lastDecision = "DIA_TERMINADO";
                    if (firstTick) WriteTelemetry(nyNow);
                    return;
                }
            }

            // Check if we're outside the active session's operating window
            bool sesionActualHabilitada = IsSesionHabilitada(sesionActual);
            bool afterSessionClose = IsAfterSessionClose(nyNow, sesionActual);
            bool beforeSessionOpen = IsBeforeSessionOpen(nyNow, sesionActual);
            bool fueraDeSesion = !sesionActualHabilitada || beforeSessionOpen || afterSessionClose;

            if (fueraDeSesion && firstTick)
            {
                CancelAllPendingOrders();
                LimpiarDibujosFueraSesion();
            }

            if (afterSessionClose && sesionActualHabilitada)
            {
                if (ModoOperacion == OperationMode.Visualizar && vizTradeActive)
                    VizSalir("CierreForzado");
                else if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                lastDecision = "CIERRE_FORZADO";
                if (firstTick) WriteTelemetry(nyNow);
                return;
            }

            if (fueraDeSesion)
            {
                if (firstTick) WriteTelemetry(nyNow);
                return;
            }

            if (tradeEnded)
            {
                ProcesarFinTrade();
            }

            switch (estado)
            {
                case BotState.EsperandoRango:
                    ProcesarRango(nyNow);
                    break;
                case BotState.OrdenesPuestas:
                    MonitorearOrdenes(nyNow);
                    break;
                case BotState.EnTrade:
                    MonitorearTrade(nyNow);
                    break;
            }

            if (firstTick) WriteTelemetry(nyNow);
        }

        #endregion

        #region Transicion post-trade

        private void ProcesarFinTrade()
        {
            tradeEnded = false;
            vizTradeActive = false;
            LimpiarNivelesTrailing();
            int prevDirection = tradeDirection;
            string exitReason = lastExitReason;
            tradeDirection = 0;
            entryPrice = 0;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            breakevenHit = false;
            tradeCounted = false;

            if (tradeEndedByTakeProfit)
            {
                takeProfitsHoy++;
                tradeEndedByTakeProfit = false;

                if (tradesToday >= MaxTrades)
                {
                    estado = BotState.DiaTerminado;
                    dayHardStop = true;
                    lastDecision = "DIA_TERMINADO_MAX_TRADES";
                }
                else if (takeProfitsHoy >= maxTakeProfits)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = string.Format("DIA_TERMINADO_MAX_TP ({0})", takeProfitsHoy);
                }
                else if (tradesToday >= tradesPermitidosHoy)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = "DIA_TERMINADO_TRADES_SESION";
                }
                else
                {
                    pendingFlip = false;
                    reentryPriceInRange = false;
                    cooldownActivo = true;
                    breakevenExitTime = Time[0];
                    estado = BotState.OrdenesPuestas;
                    lastDecision = string.Format("POST_TP_COOLDOWN_50s H={0:F2} L={1:F2}", rangoHigh, rangoLow);
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
                    lastDecision = "DIA_TERMINADO_MAX_TRADES";
                }
                else if (stopsPuros >= maxStopsPuros)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = string.Format("DIA_TERMINADO_MAX_STOPS ({0})", stopsPuros);
                }
                else if (tradesToday >= tradesPermitidosHoy)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = "DIA_TERMINADO_TRADES_SESION";
                }
                else
                {
                    pendingFlip = true;
                    pendingFlipDirection = prevDirection == 1 ? -1 : 1;
                    estado = BotState.OrdenesPuestas;
                    lastDecision = string.Format("FLIP_PENDING dir={0}", pendingFlipDirection == 1 ? "LONG" : "SHORT");
                }
            }
            else
            {
                // Breakeven exit — recalcular presupuesto restante
                tradeEndedByTakeProfit = false;
                breakevensHoy++;

                // Recalcular trades permitidos basado en pérdida real acumulada
                double perdidaRealAcumulada = dailyPnL < 0 ? Math.Abs(dailyPnL) : 0;
                double presupuestoRestante = PerdidaMaxDiaria - perdidaRealAcumulada;
                if (riesgo1Micro > 0 && presupuestoRestante >= riesgo1Micro)
                    tradesPermitidosHoy = tradesToday + (int)Math.Floor(presupuestoRestante / riesgo1Micro);
                if (tradesPermitidosHoy > MaxTrades)
                    tradesPermitidosHoy = MaxTrades;

                if (tradesToday >= MaxTrades)
                {
                    estado = BotState.DiaTerminado;
                    dayHardStop = true;
                    lastDecision = "DIA_TERMINADO_MAX_TRADES";
                }
                else if (breakevensHoy >= maxBreakevens)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = string.Format("DIA_TERMINADO_MAX_BE ({0})", breakevensHoy);
                }
                else if (tradesToday >= tradesPermitidosHoy)
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = "DIA_TERMINADO_TRADES_SESION";
                }
                else
                {
                    pendingFlip = false;
                    reentryPriceInRange = false;
                    cooldownActivo = true;
                    breakevenExitTime = Time[0];
                    estado = BotState.OrdenesPuestas;
                    lastDecision = string.Format("COOLDOWN_50s H={0:F2} L={1:F2} tradesRest={2}", rangoHigh, rangoLow, tradesPermitidosHoy - tradesToday);
                }
            }
        }

        #endregion

        #region Estado: EsperandoRango

        private void ProcesarRango(DateTime nyNow)
        {
            int rangoInicioH, rangoInicioM, rangoFinH, rangoFinM;
            GetRangoHorario(sesionActual, out rangoInicioH, out rangoInicioM, out rangoFinH, out rangoFinM);

            double tNow = nyNow.Hour + nyNow.Minute / 60.0;
            double tInicio = rangoInicioH + rangoInicioM / 60.0;
            double tFin = rangoFinH + rangoFinM / 60.0;

            bool antesVentana;
            bool enVentana;

            if (sesionActual == Sesion.Asia && tNow < 2.0)
            {
                // After midnight — we're past the range window
                antesVentana = false;
                enVentana = false;
            }
            else if (sesionActual == Sesion.Asia)
            {
                antesVentana = tNow < tInicio;
                enVentana = tNow >= tInicio && tNow <= tFin;
            }
            else
            {
                antesVentana = tNow < tInicio;
                enVentana = tNow >= tInicio && tNow <= tFin;
            }

            if (antesVentana)
            {
                lastDecision = "ESPERANDO_0930";
                return;
            }

            if (enVentana)
            {
                if (rangoStartBar == 0) rangoStartBar = CurrentBar;
                rangoEndBar = CurrentBar;
                if (High[0] > rangoHigh) rangoHigh = High[0];
                if (Low[0] < rangoLow) rangoLow = Low[0];
                lastDecision = string.Format("ACUMULANDO_RANGO H={0:F2} L={1:F2}", rangoHigh, rangoLow);
                return;
            }

            if (rangoHigh == double.MinValue || rangoLow == double.MaxValue)
            {
                lastDecision = "RANGO_INVALIDO";
                estado = BotState.DiaTerminado;
                return;
            }

            rangoPuntos = rangoHigh - rangoLow;
            stopDistance = rangoPuntos + ColchonStop;

            // --- Motor de Riesgo: Position Sizing Dinamico ---
            riesgo1Micro = (rangoPuntos + ColchonStop) * Instrument.MasterInstrument.PointValue;
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
                tradesPermitidosHoy = tradesToday;
                estado = BotState.DiaTerminado;
                lastDecision = string.Format("FUERA_PRESUPUESTO riesgo=${0:F2} > max=${1:F2}", riesgo1Micro, presupuestoDisponible);
                return;
            }
            if (tradesPermitidosHoy > MaxTrades)
                tradesPermitidosHoy = MaxTrades;

            // --- Split TP1/TP2 segun contratos y modo ---
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
            // --- Dibujo del rango ---
            int barsBack = CurrentBar - rangoStartBar;
            Draw.Rectangle(this, "Rango" + nyNow.ToString("yyyyMMdd"), false,
                barsBack, rangoHigh, 0, rangoLow, Brushes.Transparent, Brushes.DodgerBlue, 30);

            double midPrice = (rangoHigh + rangoLow) / 2.0;
            int midBar = barsBack / 2;
            Draw.Text(this, "RangoPts" + nyNow.ToString("yyyyMMdd"),
                string.Format("{0:F0} pts", rangoPuntos),
                midBar, midPrice, Brushes.White);

            ColocarOrdenes();

            estado = BotState.OrdenesPuestas;
            lastDecision = string.Format("ORDENES_PUESTAS H={0:F2} L={1:F2} pts={2:F2} stop={3:F2} qty={4} trades={5}",
                rangoHigh, rangoLow, rangoPuntos, stopDistance, contratosCalculados, tradesPermitidosHoy);
        }

        #endregion

        #region Estado: OrdenesPuestas

        private void ColocarOrdenes()
        {
            if (!longUsado)
                Draw.HorizontalLine(this, "BuyLevel", rangoHigh, Brushes.Lime, DashStyleHelper.Dash, 2);

            if (!shortUsado)
                Draw.HorizontalLine(this, "SellLevel", rangoLow, Brushes.Red, DashStyleHelper.Dash, 2);
        }

        private void MonitorearOrdenes(DateTime nyNow)
        {
            if (ModoOperacion == OperationMode.Visualizar && vizTradeActive)
            {
                estado = BotState.EnTrade;
                lastDecision = string.Format("VIZ_EN_TRADE {0}", tradeDirection == 1 ? "LONG" : "SHORT");
                return;
            }

            if (Position.MarketPosition != MarketPosition.Flat)
            {
                estado = BotState.EnTrade;
                lastDecision = string.Format("EN_TRADE {0}", tradeDirection == 1 ? "LONG" : "SHORT");
                return;
            }

            if (tradesToday >= tradesPermitidosHoy)
            {
                estado = BotState.DiaTerminado;
                lastDecision = "DIA_TERMINADO_MAX_TRADES";
                return;
            }

            if (cooldownActivo)
            {
                double elapsed = (Time[0] - breakevenExitTime).TotalSeconds;
                if (elapsed < 50)
                {
                    lastDecision = string.Format("COOLDOWN {0:F0}s/50s", elapsed);
                    return;
                }
                cooldownActivo = false;
            }

            double stopLong = rangoLow - ColchonStop;
            double stopShort = rangoHigh + ColchonStop;
            double tpTicks = stopDistance / TickSize;
            double tp2Ticks = (stopDistance * 2) / TickSize;

            // Flip inmediato tras stop loss
            if (pendingFlip)
            {
                pendingFlip = false;
                if (ModoOperacion == OperationMode.Visualizar)
                {
                    double flipStop = pendingFlipDirection == 1 ? stopLong : stopShort;
                    VizEntrar(pendingFlipDirection, Close[0], flipStop);
                    lastDecision = string.Format("FLIP_{0} VIZ @{1:F2} stop={2:F2}",
                        pendingFlipDirection == 1 ? "LONG" : "SHORT", Close[0], flipStop);
                }
                else
                {
                    if (pendingFlipDirection == 1)
                    {
                        if (qtyTP1 > 0)
                        {
                            SetStopLoss("TP1Long", CalculationMode.Price, stopLong, false);
                            SetProfitTarget("TP1Long", CalculationMode.Ticks, tpTicks);
                            EnterLong(qtyTP1, "TP1Long");
                        }
                        if (qtyTP2 > 0)
                        {
                            SetStopLoss("TP2Long", CalculationMode.Price, stopLong, false);
                            SetProfitTarget("TP2Long", CalculationMode.Ticks, tp2Ticks);
                            EnterLong(qtyTP2, "TP2Long");
                        }
                        lastDecision = string.Format("FLIP_LONG TP1x{0} TP2x{1} @{2:F2} stop={3:F2}", qtyTP1, qtyTP2, Close[0], stopLong);
                    }
                    else
                    {
                        if (qtyTP1 > 0)
                        {
                            SetStopLoss("TP1Short", CalculationMode.Price, stopShort, false);
                            SetProfitTarget("TP1Short", CalculationMode.Ticks, tpTicks);
                            EnterShort(qtyTP1, "TP1Short");
                        }
                        if (qtyTP2 > 0)
                        {
                            SetStopLoss("TP2Short", CalculationMode.Price, stopShort, false);
                            SetProfitTarget("TP2Short", CalculationMode.Ticks, tp2Ticks);
                            EnterShort(qtyTP2, "TP2Short");
                        }
                        lastDecision = string.Format("FLIP_SHORT TP1x{0} TP2x{1} @{2:F2} stop={3:F2}", qtyTP1, qtyTP2, Close[0], stopShort);
                    }
                }
                return;
            }

            // Re-entry tras breakeven: esperar que precio vuelva al rango
            if (tradesToday > 0 && !reentryPriceInRange)
            {
                if (Close[0] > rangoLow && Close[0] < rangoHigh)
                    reentryPriceInRange = true;
                else
                {
                    lastDecision = string.Format("REENTRY_ESPERA_RANGO H={0:F2} L={1:F2}", rangoHigh, rangoLow);
                    return;
                }
            }

            if (Close[0] >= rangoHigh)
            {
                if (ModoOperacion == OperationMode.Visualizar)
                {
                    VizEntrar(1, Close[0], stopLong);
                }
                else
                {
                    if (qtyTP1 > 0)
                    {
                        SetStopLoss("TP1Long", CalculationMode.Price, stopLong, false);
                        SetProfitTarget("TP1Long", CalculationMode.Ticks, tpTicks);
                        EnterLong(qtyTP1, "TP1Long");
                    }

                    if (qtyTP2 > 0)
                    {
                        SetStopLoss("TP2Long", CalculationMode.Price, stopLong, false);
                        SetProfitTarget("TP2Long", CalculationMode.Ticks, tp2Ticks);
                        EnterLong(qtyTP2, "TP2Long");
                    }
                }

                lastDecision = string.Format("ENTRY_LONG TP1x{0} TP2x{1} @{2:F2} stop={3:F2}", qtyTP1, qtyTP2, Close[0], stopLong);
                return;
            }

            if (Close[0] <= rangoLow)
            {
                if (ModoOperacion == OperationMode.Visualizar)
                {
                    VizEntrar(-1, Close[0], stopShort);
                }
                else
                {
                    if (qtyTP1 > 0)
                    {
                        SetStopLoss("TP1Short", CalculationMode.Price, stopShort, false);
                        SetProfitTarget("TP1Short", CalculationMode.Ticks, tpTicks);
                        EnterShort(qtyTP1, "TP1Short");
                    }

                    if (qtyTP2 > 0)
                    {
                        SetStopLoss("TP2Short", CalculationMode.Price, stopShort, false);
                        SetProfitTarget("TP2Short", CalculationMode.Ticks, tp2Ticks);
                        EnterShort(qtyTP2, "TP2Short");
                    }
                }

                lastDecision = string.Format("ENTRY_SHORT TP1x{0} TP2x{1} @{2:F2}", qtyTP1, qtyTP2, Close[0]);
                return;
            }

            lastDecision = string.Format("ESPERANDO_RUPTURA H={0:F2} L={1:F2}", rangoHigh, rangoLow);
        }

        #endregion

        #region Estado: EnTrade

        private void MonitorearTrade(DateTime nyNow)
        {
            if (ModoOperacion == OperationMode.Visualizar)
            {
                if (!vizTradeActive)
                {
                    tradeEnded = true;
                    lastExitDirection = tradeDirection;
                    return;
                }

                VizMonitorear();
                return;
            }

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (!tradeEnded)
                {
                    tradeEnded = true;
                    if (lastExitReason == "TakeProfit")
                        tradeEndedByTakeProfit = true;
                    lastExitDirection = tradeDirection;
                }
                return;
            }

            double unrealPts = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];

            string signalTP1 = tradeDirection == 1 ? "TP1Long" : "TP1Short";
            string signalTP2 = tradeDirection == 1 ? "TP2Long" : "TP2Short";

            // Breakeven — aplica a ambos TPs activos
            if (!breakevenHit && unrealPts >= stopDistance * (BreakevenPct / 100.0))
            {
                double beStop = tradeDirection == 1 ? entryPrice + ColchonBreakeven : entryPrice - ColchonBreakeven;
                if (qtyTP1 > 0) SetStopLoss(signalTP1, CalculationMode.Price, beStop, false);
                if (qtyTP2 > 0) SetStopLoss(signalTP2, CalculationMode.Price, beStop, false);
                breakevenHit = true;
                BorrarActivacion("BE_Act");
                lastDecision = string.Format("BREAKEVEN stop={0:F2}", beStop);
                return;
            }

            // Trailing TP1: escalones configurables (respeta CantTrailTP1)
            if (qtyTP1 > 0 && breakevenHit)
            {
                if (CantTrailTP1 >= 4 && tp1StopNivel < 4 && unrealPts >= stopDistance * (TP1Act4 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp4 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp4 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, false);
                    tp1StopNivel = 4;
                    BorrarActivacion("TP1_Act4");
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp4, tp1Stop);
                    return;
                }

                if (CantTrailTP1 >= 3 && tp1StopNivel < 3 && unrealPts >= stopDistance * (TP1Act3 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp3 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp3 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, false);
                    tp1StopNivel = 3;
                    BorrarActivacion("TP1_Act3");
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp3, tp1Stop);
                    return;
                }

                if (CantTrailTP1 >= 2 && tp1StopNivel < 2 && unrealPts >= stopDistance * (TP1Act2 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp2 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp2 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, false);
                    tp1StopNivel = 2;
                    BorrarActivacion("TP1_Act2");
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp2, tp1Stop);
                    return;
                }

                if (tp1StopNivel < 1 && unrealPts >= stopDistance * (TP1Act1 / 100.0))
                {
                    double tp1Stop = tradeDirection == 1
                        ? entryPrice + (stopDistance * (TP1Stp1 / 100.0))
                        : entryPrice - (stopDistance * (TP1Stp1 / 100.0));
                    SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, false);
                    tp1StopNivel = 1;
                    BorrarActivacion("TP1_Act1");
                    lastDecision = string.Format("TP1_STOP{0}={1:F2}", TP1Stp1, tp1Stop);
                    return;
                }
            }

            // Trailing TP2: escalones configurables (respeta CantTrailTP2)
            if (qtyTP2 > 0 && breakevenHit)
            {
                double tp2Target = stopDistance * 2;

                if (CantTrailTP2 >= 6 && tp2StopNivel < 6 && unrealPts >= tp2Target * (TP2Act6 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp6 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp6 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 6;
                    BorrarActivacion("TP2_Act6");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp6, nuevoStop);
                    return;
                }

                if (CantTrailTP2 >= 5 && tp2StopNivel < 5 && unrealPts >= tp2Target * (TP2Act5 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp5 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp5 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 5;
                    BorrarActivacion("TP2_Act5");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp5, nuevoStop);
                    return;
                }

                if (CantTrailTP2 >= 4 && tp2StopNivel < 4 && unrealPts >= tp2Target * (TP2Act4 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp4 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp4 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 4;
                    BorrarActivacion("TP2_Act4");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp4, nuevoStop);
                    return;
                }

                if (CantTrailTP2 >= 3 && tp2StopNivel < 3 && unrealPts >= tp2Target * (TP2Act3 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp3 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp3 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 3;
                    BorrarActivacion("TP2_Act3");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp3, nuevoStop);
                    return;
                }

                if (CantTrailTP2 >= 2 && tp2StopNivel < 2 && unrealPts >= tp2Target * (TP2Act2 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp2 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp2 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 2;
                    BorrarActivacion("TP2_Act2");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp2, nuevoStop);
                    return;
                }

                if (tp2StopNivel < 1 && unrealPts >= tp2Target * (TP2Act1 / 100.0))
                {
                    double nuevoStop = tradeDirection == 1
                        ? entryPrice + (tp2Target * (TP2Stp1 / 100.0))
                        : entryPrice - (tp2Target * (TP2Stp1 / 100.0));
                    SetStopLoss(signalTP2, CalculationMode.Price, nuevoStop, false);
                    tp2StopNivel = 1;
                    BorrarActivacion("TP2_Act1");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp1, nuevoStop);
                    return;
                }
            }

            lastDecision = string.Format("EN_TRADE {0} entry={1:F2} unreal={2:F2}pts",
                tradeDirection == 1 ? "LONG" : "SHORT", entryPrice, unrealPts);
        }

        #endregion

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
                longUsado = true;
                shortUsado = true;
                if (!tradeCounted)
                {
                    tradesToday++;
                    tradeCounted = true;
                }
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL LONG {0} x{1} @{2:F2}", orderName, quantity, price);
                tradeLog.Add(lastAction);
                DibujarNivelesTrailing();

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "LONG", price, 0, orderName);
            }
            else if (isShortEntry)
            {
                if (entryPrice == 0) entryPrice = price;
                tradeDirection = -1;
                shortUsado = true;
                longUsado = true;
                if (!tradeCounted)
                {
                    tradesToday++;
                    tradeCounted = true;
                }
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL SHORT {0} x{1} @{2:F2}", orderName, quantity, price);
                tradeLog.Add(lastAction);
                DibujarNivelesTrailing();

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "SHORT", price, 0, orderName);
            }
            else if (orderName == "Stop loss" || orderName == "Profit target")
            {
                if (tradeDirection == 0) return;

                double pnl = 0;
                string dir = tradeDirection == 1 ? "LONG" : "SHORT";
                string reason;
                if (orderName == "Profit target")
                    reason = "TakeProfit";
                else if (breakevenHit)
                    reason = "Breakeven";
                else
                    reason = "StopLoss";

                if (tradeDirection == 1)
                    pnl = (price - entryPrice) * Instrument.MasterInstrument.PointValue * quantity;
                else if (tradeDirection == -1)
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
                    lastExitDirection = tradeDirection;
                    tradeEnded = true;

                    if (reason == "TakeProfit")
                        tradeEndedByTakeProfit = true;

                    // Trailing Daily Drawdown check
                    if (dailyPnL > peakDailyPnL)
                        peakDailyPnL = dailyPnL;
                    double drawdownDesdeElPeak = peakDailyPnL - dailyPnL;
                    if (drawdownDesdeElPeak >= PerdidaMaxDiaria)
                    {
                        estado = BotState.DiaTerminado;
                        dayHardStop = true;
                        lastDecision = string.Format("DIA_TERMINADO_DRAWDOWN peak=${0:F2} actual=${1:F2} dd=${2:F2}",
                            peakDailyPnL, dailyPnL, drawdownDesdeElPeak);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private void GetRangoHorario(Sesion s, out int inicioH, out int inicioM, out int finH, out int finM)
        {
            switch (s)
            {
                case Sesion.Asia:
                    inicioH = 18; inicioM = 2; finH = 18; finM = 10;
                    break;
                case Sesion.Europa:
                    inicioH = 2; inicioM = 2; finH = 2; finM = 10;
                    break;
                default: // America
                    inicioH = 9; inicioM = 32; finH = 9; finM = 40;
                    break;
            }
        }

        private Sesion GetSesionParaHora(DateTime nyNow)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;

            // Asia: 18:00 - 02:00 (next day)
            if (t >= 18.0) return Sesion.Asia;
            // Europa: 02:00 - 09:30
            if (t >= 2.0 && t < 9.5) return Sesion.Europa;
            // America: 09:30 - 15:50 (horaCierre)
            double cierre = horaCierreH + horaCierreM / 60.0;
            if (t >= 9.5 && t < cierre) return Sesion.America;
            // Between 00:00-02:00 is still Asia (wraps midnight)
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

        private bool IsBeforeSessionOpen(DateTime nyNow, Sesion s)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;
            switch (s)
            {
                case Sesion.Asia: return h == 18 && m < 0; // never true, Asia starts at 18:00
                case Sesion.Europa: return t < 2.0;
                case Sesion.America: return t < 9.5;
                default: return true;
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

        private void ResetSession()
        {
            cooldownActivo = false;
            estado = BotState.EsperandoRango;
            tradeDirection = 0;
            rangoStartBar = 0;
            rangoEndBar = 0;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            pendingFlip = false;
            pendingFlipDirection = 0;
            tradeCounted = false;
            lastExitReason = "";
            rangoHigh = double.MinValue;
            rangoLow = double.MaxValue;
            rangoPuntos = 0;
            stopDistance = 0;
            entryPrice = 0;
            longUsado = false;
            shortUsado = false;
            reentryPriceInRange = false;
            vizTradeActive = false;
            vizTP1Active = false;
            vizTP2Active = false;
            vizStopTP1 = 0;
            vizStopTP2 = 0;
            riesgo1Micro = 0;
            contratosCalculados = 0;
            lastDecision = string.Format("NEW_SESSION_{0}", sesionActual);
        }

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
            cooldownActivo = false;
            estado = BotState.EsperandoRango;
            tradeDirection = 0;
            rangoStartBar = 0;
            rangoEndBar = 0;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            pendingFlip = false;
            pendingFlipDirection = 0;
            tradeCounted = false;
            lastExitReason = "";
            rangoHigh = double.MinValue;
            rangoLow = double.MaxValue;
            rangoPuntos = 0;
            stopDistance = 0;
            entryPrice = 0;
            longUsado = false;
            shortUsado = false;
            reentryPriceInRange = false;
            vizTradeActive = false;
            vizTP1Active = false;
            vizTP2Active = false;
            vizStopTP1 = 0;
            vizStopTP2 = 0;
            vizTradeNum = 0;
            vizPrefix = "";
            lastDecision = "NEW_DAY";
            lastAction = "";
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

        private void LimpiarDibujosFueraSesion()
        {
            RemoveDrawObject("BuyLevel");
            RemoveDrawObject("SellLevel");
        }

        private void FlattenAll(string reason)
        {
            string dir = tradeDirection == 1 ? "LONG" : "SHORT";

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
            LimpiarNivelesTrailing();
            lastAction = string.Format("FLATTEN {0} {1}", dir, reason);
            tradeDirection = 0;
        }

        private void VizEntrar(int dir, double price, double stopPrice)
        {
            vizTradeNum++;
            vizPrefix = "V" + vizTradeNum + "_";
            entryPrice = price;
            tradeDirection = dir;
            vizTradeActive = true;
            vizTP1Active = qtyTP1 > 0;
            vizTP2Active = qtyTP2 > 0;
            vizStopTP1 = stopPrice;
            vizStopTP2 = stopPrice;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            if (dir == 1) { longUsado = true; shortUsado = true; }
            else { shortUsado = true; longUsado = true; }
            if (!tradeCounted) { tradesToday++; tradeCounted = true; }
            estado = BotState.EnTrade;

            // Entry marker (arrow like real trade)
            if (dir == 1)
                Draw.ArrowUp(this, vizPrefix + "EntryArrow", true, 0, price - (TickSize * 4), Brushes.Lime);
            else
                Draw.ArrowDown(this, vizPrefix + "EntryArrow", true, 0, price + (TickSize * 4), Brushes.Red);

            Draw.HorizontalLine(this, vizPrefix + "Entry", price, Brushes.White, DashStyleHelper.Solid, 2);
            Draw.Text(this, vizPrefix + "EntryT", dir == 1 ? "LONG" : "SHORT", 0, price, Brushes.White);
            Draw.HorizontalLine(this, vizPrefix + "Stop", stopPrice, Brushes.Red, DashStyleHelper.Solid, 2);
            Draw.Text(this, vizPrefix + "StopT", "STOP", 0, stopPrice, Brushes.Red);

            if (vizTP1Active)
            {
                Draw.HorizontalLine(this, vizPrefix + "StpTP1", stopPrice, Brushes.Red, DashStyleHelper.Solid, 1);
                Draw.Text(this, vizPrefix + "StpTP1T", "STP TP1", 0, stopPrice, Brushes.Cyan);
            }
            if (vizTP2Active)
            {
                Draw.HorizontalLine(this, vizPrefix + "StpTP2", stopPrice, Brushes.Red, DashStyleHelper.Solid, 1);
                Draw.Text(this, vizPrefix + "StpTP2T", "STP TP2", 0, stopPrice, Brushes.Orange);
            }

            VizDibujarNiveles();

            lastAction = string.Format("VIZ_ENTRY {0} @{1:F2} stop={2:F2}", dir == 1 ? "LONG" : "SHORT", price, stopPrice);
            tradeLog.Add(lastAction);
        }

        private void VizMonitorear()
        {
            double unrealPts = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];

            int prevTP1Nivel = tp1StopNivel;
            int prevTP2Nivel = tp2StopNivel;
            bool prevBE = breakevenHit;

            // --- TP1 contract ---
            if (vizTP1Active)
            {
                bool tp1Stopped = (tradeDirection == 1 && Close[0] <= vizStopTP1) ||
                                  (tradeDirection == -1 && Close[0] >= vizStopTP1);
                if (tp1Stopped)
                {
                    vizTP1Active = false;
                    string tp1Reason;
                    Brush tp1Color;
                    if (tp1StopNivel > 0) { tp1Reason = "TP1 Trailing"; tp1Color = Brushes.Cyan; lastExitReason = "Trailing"; }
                    else if (breakevenHit) { tp1Reason = "TP1 BE"; tp1Color = Brushes.Yellow; lastExitReason = "Breakeven"; }
                    else { tp1Reason = "TP1 SL"; tp1Color = Brushes.Red; lastExitReason = "StopLoss"; }
                    Draw.Diamond(this, vizPrefix + "TP1Exit", true, 0, Close[0], Brushes.White);
                    Draw.Text(this, vizPrefix + "TP1ExitT", tp1Reason, 0, Close[0] + (TickSize * 8), tp1Color);
                    RemoveDrawObject(vizPrefix + "StpTP1");
                    RemoveDrawObject(vizPrefix + "StpTP1T");
                }
                else if (unrealPts >= stopDistance)
                {
                    vizTP1Active = false;
                    lastExitReason = "TakeProfit";
                    Draw.Diamond(this, vizPrefix + "TP1Exit", true, 0, Close[0], Brushes.White);
                    Draw.Text(this, vizPrefix + "TP1ExitT", "TP1 TP", 0, Close[0] + (TickSize * 8), Brushes.Lime);
                    RemoveDrawObject(vizPrefix + "StpTP1");
                    RemoveDrawObject(vizPrefix + "StpTP1T");
                }
                else
                {
                    if (!breakevenHit && unrealPts >= stopDistance * (BreakevenPct / 100.0))
                    {
                        double beStop = tradeDirection == 1 ? entryPrice + ColchonBreakeven : entryPrice - ColchonBreakeven;
                        vizStopTP1 = beStop;
                        vizStopTP2 = beStop;
                        breakevenHit = true;
                        lastDecision = string.Format("VIZ_BE stop={0:F2}", beStop);
                    }

                    if (breakevenHit)
                    {
                        if (CantTrailTP1 >= 4 && tp1StopNivel < 4 && unrealPts >= stopDistance * (TP1Act4 / 100.0))
                        { vizStopTP1 = tradeDirection == 1 ? entryPrice + (stopDistance * (TP1Stp4 / 100.0)) : entryPrice - (stopDistance * (TP1Stp4 / 100.0)); tp1StopNivel = 4; }
                        else if (CantTrailTP1 >= 3 && tp1StopNivel < 3 && unrealPts >= stopDistance * (TP1Act3 / 100.0))
                        { vizStopTP1 = tradeDirection == 1 ? entryPrice + (stopDistance * (TP1Stp3 / 100.0)) : entryPrice - (stopDistance * (TP1Stp3 / 100.0)); tp1StopNivel = 3; }
                        else if (CantTrailTP1 >= 2 && tp1StopNivel < 2 && unrealPts >= stopDistance * (TP1Act2 / 100.0))
                        { vizStopTP1 = tradeDirection == 1 ? entryPrice + (stopDistance * (TP1Stp2 / 100.0)) : entryPrice - (stopDistance * (TP1Stp2 / 100.0)); tp1StopNivel = 2; }
                        else if (tp1StopNivel < 1 && unrealPts >= stopDistance * (TP1Act1 / 100.0))
                        { vizStopTP1 = tradeDirection == 1 ? entryPrice + (stopDistance * (TP1Stp1 / 100.0)) : entryPrice - (stopDistance * (TP1Stp1 / 100.0)); tp1StopNivel = 1; }
                    }
                }
            }

            // --- TP2 contract ---
            if (vizTP2Active)
            {
                bool tp2Stopped = (tradeDirection == 1 && Close[0] <= vizStopTP2) ||
                                  (tradeDirection == -1 && Close[0] >= vizStopTP2);
                if (tp2Stopped)
                {
                    vizTP2Active = false;
                    string tp2Reason;
                    Brush tp2Color;
                    if (tp2StopNivel > 0) { tp2Reason = "TP2 Trailing"; tp2Color = Brushes.Orange; lastExitReason = "Trailing"; }
                    else if (breakevenHit) { tp2Reason = "TP2 BE"; tp2Color = Brushes.Yellow; lastExitReason = "Breakeven"; }
                    else { tp2Reason = "TP2 SL"; tp2Color = Brushes.Red; lastExitReason = "StopLoss"; }
                    Draw.Diamond(this, vizPrefix + "TP2Exit", true, 0, Close[0], Brushes.White);
                    Draw.Text(this, vizPrefix + "TP2ExitT", tp2Reason, 0, Close[0] - (TickSize * 8), tp2Color);
                    RemoveDrawObject(vizPrefix + "StpTP2");
                    RemoveDrawObject(vizPrefix + "StpTP2T");
                }
                else if (unrealPts >= stopDistance * 2)
                {
                    vizTP2Active = false;
                    lastExitReason = "TakeProfit";
                    Draw.Diamond(this, vizPrefix + "TP2Exit", true, 0, Close[0], Brushes.White);
                    Draw.Text(this, vizPrefix + "TP2ExitT", "TP2 TP", 0, Close[0] - (TickSize * 8), Brushes.Gold);
                    RemoveDrawObject(vizPrefix + "StpTP2");
                    RemoveDrawObject(vizPrefix + "StpTP2T");
                }
                else
                {
                    if (!breakevenHit && unrealPts >= stopDistance * (BreakevenPct / 100.0))
                    {
                        double beStop = tradeDirection == 1 ? entryPrice + ColchonBreakeven : entryPrice - ColchonBreakeven;
                        vizStopTP2 = beStop;
                        breakevenHit = true;
                    }

                    if (breakevenHit)
                    {
                        double tp2Target = stopDistance * 2;
                        if (CantTrailTP2 >= 6 && tp2StopNivel < 6 && unrealPts >= tp2Target * (TP2Act6 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp6 / 100.0)) : entryPrice - (tp2Target * (TP2Stp6 / 100.0)); tp2StopNivel = 6; }
                        else if (CantTrailTP2 >= 5 && tp2StopNivel < 5 && unrealPts >= tp2Target * (TP2Act5 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp5 / 100.0)) : entryPrice - (tp2Target * (TP2Stp5 / 100.0)); tp2StopNivel = 5; }
                        else if (CantTrailTP2 >= 4 && tp2StopNivel < 4 && unrealPts >= tp2Target * (TP2Act4 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp4 / 100.0)) : entryPrice - (tp2Target * (TP2Stp4 / 100.0)); tp2StopNivel = 4; }
                        else if (CantTrailTP2 >= 3 && tp2StopNivel < 3 && unrealPts >= tp2Target * (TP2Act3 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp3 / 100.0)) : entryPrice - (tp2Target * (TP2Stp3 / 100.0)); tp2StopNivel = 3; }
                        else if (CantTrailTP2 >= 2 && tp2StopNivel < 2 && unrealPts >= tp2Target * (TP2Act2 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp2 / 100.0)) : entryPrice - (tp2Target * (TP2Stp2 / 100.0)); tp2StopNivel = 2; }
                        else if (tp2StopNivel < 1 && unrealPts >= tp2Target * (TP2Act1 / 100.0))
                        { vizStopTP2 = tradeDirection == 1 ? entryPrice + (tp2Target * (TP2Stp1 / 100.0)) : entryPrice - (tp2Target * (TP2Stp1 / 100.0)); tp2StopNivel = 1; }
                    }
                }
            }

            // Borrar activaciones alcanzadas y mover stops visualmente
            if (!prevBE && breakevenHit)
            {
                RemoveDrawObject(vizPrefix + "BE");
                RemoveDrawObject(vizPrefix + "BET");
                // Remove original shared stop, draw separate TP1/TP2 stops
                RemoveDrawObject(vizPrefix + "Stop");
                RemoveDrawObject(vizPrefix + "StopT");
            }
            if (tp1StopNivel > prevTP1Nivel && vizTP1Active)
            {
                RemoveDrawObject(vizPrefix + "TP1_" + tp1StopNivel);
                RemoveDrawObject(vizPrefix + "TP1_" + tp1StopNivel + "T");
            }
            if (tp2StopNivel > prevTP2Nivel && vizTP2Active)
            {
                RemoveDrawObject(vizPrefix + "TP2_" + tp2StopNivel);
                RemoveDrawObject(vizPrefix + "TP2_" + tp2StopNivel + "T");
            }

            // Redraw stop lines at current positions
            if (vizTP1Active)
            {
                Draw.HorizontalLine(this, vizPrefix + "StpTP1", vizStopTP1, Brushes.Red, DashStyleHelper.Solid, 1);
                Draw.Text(this, vizPrefix + "StpTP1T", "STP TP1", 0, vizStopTP1, Brushes.Cyan);
            }
            if (vizTP2Active)
            {
                Draw.HorizontalLine(this, vizPrefix + "StpTP2", vizStopTP2, Brushes.Red, DashStyleHelper.Solid, 1);
                Draw.Text(this, vizPrefix + "StpTP2T", "STP TP2", 0, vizStopTP2, Brushes.Orange);
            }

            // Both contracts done
            if (!vizTP1Active && !vizTP2Active)
            {
                VizSalir(lastExitReason ?? "StopLoss");
                return;
            }

            lastDecision = string.Format("VIZ {0} entry={1:F2} unreal={2:F2} TP1={3} TP2={4}",
                tradeDirection == 1 ? "L" : "S", entryPrice, unrealPts,
                vizTP1Active ? "ON" : "OFF", vizTP2Active ? "ON" : "OFF");
        }

        private void VizSalir(string reason)
        {
            vizTradeActive = false;
            lastExitReason = reason;
            lastExitDirection = tradeDirection;
            tradeEnded = true;
            if (reason == "TakeProfit") tradeEndedByTakeProfit = true;

            // Exit marker (arrow opposite to entry, like real trade)
            if (tradeDirection == 1)
                Draw.ArrowDown(this, vizPrefix + "ExitArrow", true, 0, Close[0] + (TickSize * 4), Brushes.Magenta);
            else
                Draw.ArrowUp(this, vizPrefix + "ExitArrow", true, 0, Close[0] - (TickSize * 4), Brushes.Magenta);

            Draw.Diamond(this, vizPrefix + "Exit", true, 0, Close[0], Brushes.Yellow);
            Draw.Text(this, vizPrefix + "ExitT", reason, 0, Close[0], Brushes.Yellow);

            lastAction = string.Format("VIZ_EXIT {0} {1} @{2:F2}", tradeDirection == 1 ? "LONG" : "SHORT", reason, Close[0]);
            tradeLog.Add(lastAction);
        }

        private void VizDibujarNiveles()
        {
            if (entryPrice == 0 || stopDistance == 0) return;

            double beActPrice = tradeDirection == 1
                ? entryPrice + (stopDistance * (BreakevenPct / 100.0))
                : entryPrice - (stopDistance * (BreakevenPct / 100.0));
            Draw.HorizontalLine(this, vizPrefix + "BE", beActPrice, Brushes.Yellow, DashStyleHelper.Dot, 1);
            Draw.Text(this, vizPrefix + "BET", string.Format("{0}%", BreakevenPct), 0, beActPrice, Brushes.Yellow);

            // TP1 activaciones (solo CantTrailTP1 escalones)
            if (qtyTP1 > 0)
            {
                double[] tp1Acts = { TP1Act1, TP1Act2, TP1Act3, TP1Act4 };
                for (int i = 0; i < CantTrailTP1; i++)
                {
                    double actPrice = tradeDirection == 1
                        ? entryPrice + (stopDistance * (tp1Acts[i] / 100.0))
                        : entryPrice - (stopDistance * (tp1Acts[i] / 100.0));
                    Draw.HorizontalLine(this, vizPrefix + "TP1_" + (i + 1), actPrice, Brushes.Cyan, DashStyleHelper.Dot, 1);
                    Draw.Text(this, vizPrefix + "TP1_" + (i + 1) + "T", string.Format("{0}%", (int)tp1Acts[i]), 0, actPrice, Brushes.Cyan);
                }

                // TP1 target line (1:1)
                double tp1Target = tradeDirection == 1
                    ? entryPrice + stopDistance
                    : entryPrice - stopDistance;
                Draw.HorizontalLine(this, vizPrefix + "TP1Line", tp1Target, Brushes.Lime, DashStyleHelper.Dash, 2);
                Draw.Text(this, vizPrefix + "TP1LineT", "TP1", 0, tp1Target, Brushes.Lime);
            }

            // TP2 activaciones (solo CantTrailTP2 escalones)
            if (qtyTP2 > 0)
            {
                double tp2Target = stopDistance * 2;
                double[] tp2Acts = { TP2Act1, TP2Act2, TP2Act3, TP2Act4, TP2Act5, TP2Act6 };
                for (int i = 0; i < CantTrailTP2; i++)
                {
                    double actPrice = tradeDirection == 1
                        ? entryPrice + (tp2Target * (tp2Acts[i] / 100.0))
                        : entryPrice - (tp2Target * (tp2Acts[i] / 100.0));
                    Draw.HorizontalLine(this, vizPrefix + "TP2_" + (i + 1), actPrice, Brushes.Orange, DashStyleHelper.Dot, 1);
                    Draw.Text(this, vizPrefix + "TP2_" + (i + 1) + "T", string.Format("{0}%", (int)tp2Acts[i]), 0, actPrice, Brushes.Orange);
                }

                // TP2 target line (1:2)
                double tp2Line = tradeDirection == 1
                    ? entryPrice + tp2Target
                    : entryPrice - tp2Target;
                Draw.HorizontalLine(this, vizPrefix + "TP2Line", tp2Line, Brushes.Gold, DashStyleHelper.Dash, 2);
                Draw.Text(this, vizPrefix + "TP2LineT", "TP2", 0, tp2Line, Brushes.Gold);
            }
        }

        private void DibujarNivelesTrailing()
        {
            if (entryPrice == 0 || stopDistance == 0) return;

            if (!breakevenHit)
            {
                double beActPrice = tradeDirection == 1
                    ? entryPrice + (stopDistance * (BreakevenPct / 100.0))
                    : entryPrice - (stopDistance * (BreakevenPct / 100.0));
                Draw.HorizontalLine(this, "BE_Act", beActPrice, Brushes.Yellow, DashStyleHelper.Dot, 1);
                Draw.Text(this, "BE_ActT", string.Format("{0}%", BreakevenPct), 0, beActPrice, Brushes.Yellow);
            }

            // TP1 activaciones (solo CantTrailTP1)
            if (qtyTP1 > 0)
            {
                double[] tp1Acts = { TP1Act1, TP1Act2, TP1Act3, TP1Act4 };

                for (int i = 0; i < CantTrailTP1; i++)
                {
                    if (tp1StopNivel > i) continue;
                    double actPrice = tradeDirection == 1
                        ? entryPrice + (stopDistance * (tp1Acts[i] / 100.0))
                        : entryPrice - (stopDistance * (tp1Acts[i] / 100.0));

                    string actTag = "TP1_Act" + (i + 1);
                    Draw.HorizontalLine(this, actTag, actPrice, Brushes.Cyan, DashStyleHelper.Dot, 1);
                    Draw.Text(this, actTag + "T", string.Format("{0}%", (int)tp1Acts[i]), 0, actPrice, Brushes.Cyan);
                }
            }

            // TP2 activaciones (solo CantTrailTP2)
            if (qtyTP2 > 0)
            {
                double tp2Target = stopDistance * 2;
                double[] tp2Acts = { TP2Act1, TP2Act2, TP2Act3, TP2Act4, TP2Act5, TP2Act6 };

                for (int i = 0; i < CantTrailTP2; i++)
                {
                    if (tp2StopNivel > i) continue;
                    double actPrice = tradeDirection == 1
                        ? entryPrice + (tp2Target * (tp2Acts[i] / 100.0))
                        : entryPrice - (tp2Target * (tp2Acts[i] / 100.0));

                    string actTag = "TP2_Act" + (i + 1);
                    Draw.HorizontalLine(this, actTag, actPrice, Brushes.Orange, DashStyleHelper.Dot, 1);
                    Draw.Text(this, actTag + "T", string.Format("{0}%", (int)tp2Acts[i]), 0, actPrice, Brushes.Orange);
                }
            }
        }

        private void BorrarActivacion(string tag)
        {
            RemoveDrawObject(tag);
            RemoveDrawObject(tag + "T");
        }

        private void LimpiarNivelesTrailing()
        {
            RemoveDrawObject("BE_Act");
            RemoveDrawObject("BE_ActT");
            for (int i = 1; i <= 4; i++)
            {
                RemoveDrawObject("TP1_Act" + i);
                RemoveDrawObject("TP1_Act" + i + "T");
            }
            for (int i = 1; i <= 6; i++)
            {
                RemoveDrawObject("TP2_Act" + i);
                RemoveDrawObject("TP2_Act" + i + "T");
            }
        }

        #endregion

        #region Logging

        private void InitCsvLogs()
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                bool overwrite = ModoLog == LogMode.Day;
                if (overwrite || !File.Exists(csvLogPath))
                    File.WriteAllText(csvLogPath, "Date,Time,Action,Direction,EntryPrice,ExitPrice,StopDist,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday\n");
                if (overwrite || !File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,RangoPts,MaxTrades,PerdidaMaxDiaria,ModoTP,ColchonStop,ColchonBreakeven,BreakevenPct,ContratosCalc,TradesPermitidos,StopsPuros,TakeProfits,Breakevens,PeakPnL\n");
                if (overwrite || !File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Date,Time,Open,High,Low,Close,Volume,Estado,Position,DailyPnL,TotalPnL,TradesToday,Decision\n");
            }
            catch {}
        }

        private void LogTradeCsv(DateTime nyNow, string action, string dir, double price, double pnl, string exitReason)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                double exitPrice = action == "EXIT" ? price : 0;
                double entryP = action == "ENTRY" ? price : entryPrice;
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1},{2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12},{13}\n",
                    nyNow, action, dir, entryP, exitPrice, stopDistance,
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
                string line = string.Format("{0:yyyy-MM-dd},{1:F2},{2:F2},{3},{4:F2},{5},{6:F2},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16:F2}\n",
                    nyDate, dailyPnL, totalPnL, tradesToday, rangoPuntos,
                    MaxTrades, PerdidaMaxDiaria, ModoTP, ColchonStop, ColchonBreakeven, BreakevenPct,
                    contratosCalculados, tradesPermitidosHoy, stopsPuros, takeProfitsHoy, breakevensHoy, peakDailyPnL);
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
                string line = string.Format("{0:yyyy-MM-dd},{0:HH:mm},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6},{7},{8:F2},{9:F2},{10},{11}\n",
                    nyNow, Open[0], High[0], Low[0], Close[0], (long)Volume[0],
                    estado, pos, dailyPnL, totalPnL, tradesToday, lastDecision);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        #endregion

        #region Telemetry

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
  ""strategy"": ""MNQ10minV2"",
  ""price"": {1:F2},
  ""rangoHigh"": {2:F2},
  ""rangoLow"": {3:F2},
  ""rangoPuntos"": {4:F2},
  ""position"": ""{5}"",
  ""estado"": ""{6}"",
  ""decision"": ""{7}"",
  ""entryPrice"": {8:F2},
  ""stopDistance"": {9:F2},
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
                    rangoPuntos, pos, estado,
                    lastDecision.Replace("\"", "'"),
                    entryPrice, stopDistance,
                    unrealizedPts, unrealizedPts * Instrument.MasterInstrument.PointValue,
                    dailyPnL, totalPnL, tradesToday,
                    longUsado ? "true" : "false",
                    shortUsado ? "true" : "false",
                    lastAction.Replace("\"", "'"), recentTrades);

                File.WriteAllText(telemetryPath, json);
            }
            catch {}
        }

        #endregion
    }
}
