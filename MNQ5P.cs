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
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MNQ5P : Strategy
    {
        #region Parameters

        // --- 1. Setup ---
        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Barras Confirmacion", GroupName = "1. Setup", Order = 1)]
        public int BarrasConfirmacion { get; set; }

        [NinjaScriptProperty]
        [Range(10, 100)]
        [Display(Name = "Umbral ATR (%)", GroupName = "1. Setup", Order = 2)]
        public int UmbralATR { get; set; }

        [NinjaScriptProperty]
        [Range(3, 50)]
        [Display(Name = "Barras Canal", GroupName = "1. Setup", Order = 3)]
        public int BarrasCanal { get; set; }

        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Max Barras Espera Breakout", GroupName = "1. Setup", Order = 4)]
        public int MaxBarrasEspera { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Colchon Stop (pts)", GroupName = "1. Setup", Order = 5)]
        public int ColchonStop { get; set; }

        // --- 2. Risk ---
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
        [Display(Name = "Perdida Maxima Diaria ($)", GroupName = "2. Risk", Order = 4)]
        public double PerdidaMaxDiaria { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo TP", GroupName = "2. Risk", Order = 5)]
        public TPMode ModoTP { get; set; }

        // --- 3. Sesion ---
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
        [Display(Name = "Modo Log", GroupName = "3. Sesion", Order = 5)]
        public LogMode ModoLog { get; set; }

        // --- 5. Trailing TP1 ---
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

        // --- 6. Trailing TP2 ---
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

        #region Variables

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

        private BotState estado;
        private Sesion sesionActual;
        private int tradeDirection;       // 1=long, -1=short, 0=flat
        private double entryPrice;
        private double stopDistance;       // in points

        // EMA crossover state
        private int direccionCruce;       // 1=alcista, -1=bajista
        private double atrImpulso;        // ATR(14) at crossover moment

        // Trend confirmation
        private int barrasConfirmadas;

        // Channel detection
        private int barrasComprimidas;
        private double resistencia;
        private double soporte;
        private double tamanoCanal;

        // Breakout waiting
        private int barrasEsperando;

        // Position sizing
        private int qtyTP1;
        private int qtyTP2;

        // Trade management
        private bool breakevenHit;
        private int tp1StopNivel;
        private int tp2StopNivel;
        private bool tradeEnded;
        private bool tradeEndedByTakeProfit;
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

        // Indicators
        private EMA ema20;
        private EMA ema200;
        private ATR atr14;

        // Logging
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
                Description = "MNQ5P — EMA crossover + ATR compression channel breakout";
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

                // 1. Setup
                BarrasConfirmacion = 5;
                UmbralATR = 70;
                BarrasCanal = 10;
                MaxBarrasEspera = 30;
                ColchonStop = 5;

                // 2. Risk
                ColchonBreakeven = 5;
                BreakevenPct = 60;
                MaxTrades = 3;
                PerdidaMaxDiaria = 400;
                ModoTP = TPMode.Con1a2;

                // 3. Sesion
                HoraCierre = "15:50";
                OperarAsia = false;
                OperarEuropa = false;
                OperarAmerica = true;
                ModoLog = LogMode.Month;

                // 5. Trailing TP1
                CantTrailTP1 = 4;
                TP1Act1 = 75; TP1Stp1 = 45;
                TP1Act2 = 85; TP1Stp2 = 60;
                TP1Act3 = 95; TP1Stp3 = 80;
                TP1Act4 = 99; TP1Stp4 = 95;

                // 6. Trailing TP2
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
                ema20 = EMA(20);
                ema200 = EMA(200);
                atr14 = ATR(14);

                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                lastResetDate = DateTime.MinValue;
                tradeLog = new List<string>();
                lastAction = "";
                lastDecision = "WAITING";

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

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);

            // Trading day starts at 18:00 ET (Asia open)
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

            // Determine which session we're in and if it's enabled
            Sesion sesionParaHora = GetSesionParaHora(nyNow);
            bool sesionHabilitada = IsSesionHabilitada(sesionParaHora);

            // Session transition
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
                CancelAllPendingOrders();

                if (dayHardStop)
                {
                    lastDecision = "DIA_TERMINADO_HARD_STOP";
                    return;
                }

                // Check if a new enabled session starts
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
                        return;
                    }
                }
                else
                {
                    lastDecision = "DIA_TERMINADO";
                    return;
                }
            }

            // Check if we're outside the active session
            bool sesionActualHabilitada = IsSesionHabilitada(sesionActual);
            bool afterSessionClose = IsAfterSessionClose(nyNow, sesionActual);
            bool beforeSessionOpen = IsBeforeSessionOpen(nyNow, sesionActual);
            bool fueraDeSesion = !sesionActualHabilitada || beforeSessionOpen || afterSessionClose;

            if (fueraDeSesion)
                CancelAllPendingOrders();

            if (afterSessionClose && sesionActualHabilitada)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                lastDecision = "CIERRE_FORZADO";
                return;
            }

            if (fueraDeSesion)
                return;

            if (tradeEnded)
                ProcesarFinTrade();

            switch (estado)
            {
                case BotState.EsperandoCruce:
                    ProcesarEsperandoCruce();
                    break;
                case BotState.ConfirmandoTendencia:
                    ProcesarConfirmandoTendencia();
                    break;
                case BotState.BuscandoCanal:
                    ProcesarBuscandoCanal();
                    break;
                case BotState.EsperandoBreakout:
                    ProcesarEsperandoBreakout();
                    break;
                case BotState.EnTrade:
                    MonitorearTrade(nyNow);
                    break;
            }
        }

        #endregion

        #region Estado: EsperandoCruce

        private void ProcesarEsperandoCruce()
        {
            // Detect EMA crossover
            bool cruceAlcista = ema20[1] <= ema200[1] && ema20[0] > ema200[0];
            bool cruceBajista = ema20[1] >= ema200[1] && ema20[0] < ema200[0];

            if (cruceAlcista)
            {
                direccionCruce = 1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                estado = BotState.ConfirmandoTendencia;
                lastDecision = string.Format("CRUCE_ALCISTA EMA20={0:F2} > EMA200={1:F2} ATR={2:F2}",
                    ema20[0], ema200[0], atrImpulso);
            }
            else if (cruceBajista)
            {
                direccionCruce = -1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                estado = BotState.ConfirmandoTendencia;
                lastDecision = string.Format("CRUCE_BAJISTA EMA20={0:F2} < EMA200={1:F2} ATR={2:F2}",
                    ema20[0], ema200[0], atrImpulso);
            }
            else
            {
                lastDecision = string.Format("ESPERANDO_CRUCE EMA20={0:F2} EMA200={1:F2}", ema20[0], ema200[0]);
            }
        }

        #endregion

        #region Estado: ConfirmandoTendencia

        private void ProcesarConfirmandoTendencia()
        {
            // Track peak ATR during trend confirmation (impulse may develop after crossover)
            if (atr14[0] > atrImpulso)
                atrImpulso = atr14[0];

            bool confirmado;

            if (direccionCruce == 1)
                confirmado = Close[0] > ema20[0] && Close[0] > ema200[0];
            else
                confirmado = Close[0] < ema20[0] && Close[0] < ema200[0];

            if (confirmado)
            {
                barrasConfirmadas++;
                if (barrasConfirmadas >= BarrasConfirmacion)
                {
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("TENDENCIA_CONFIRMADA dir={0} barras={1} atrPeak={2:F2}",
                        direccionCruce == 1 ? "ALCISTA" : "BAJISTA", barrasConfirmadas, atrImpulso);
                }
                else
                {
                    lastDecision = string.Format("CONFIRMANDO {0}/{1} dir={2}",
                        barrasConfirmadas, BarrasConfirmacion, direccionCruce == 1 ? "ALC" : "BAJ");
                }
            }
            else
            {
                // Trend broken, back to waiting
                estado = BotState.EsperandoCruce;
                lastDecision = string.Format("CONFIRMACION_ROTA Close={0:F2} EMA20={1:F2} EMA200={2:F2}",
                    Close[0], ema20[0], ema200[0]);
            }
        }

        #endregion

        #region Estado: BuscandoCanal

        private void ProcesarBuscandoCanal()
        {
            // Validate EMAs haven't re-crossed
            if ((direccionCruce == 1 && ema20[0] <= ema200[0]) ||
                (direccionCruce == -1 && ema20[0] >= ema200[0]))
            {
                estado = BotState.EsperandoCruce;
                RemoveDrawObject("CanalResistencia");
                RemoveDrawObject("CanalSoporte");
                lastDecision = "CANAL_INVALIDO_RECRUCE_EMAs";
                return;
            }

            // Update peak ATR if still expanding (impulse not finished yet)
            if (barrasComprimidas == 0 && atr14[0] > atrImpulso)
                atrImpulso = atr14[0];

            // Check ATR compression
            double umbralCompresion = atrImpulso * (UmbralATR / 100.0);
            if (atr14[0] < umbralCompresion)
            {
                barrasComprimidas++;
            }
            else
            {
                barrasComprimidas = 0;
            }

            if (barrasComprimidas >= BarrasCanal)
            {
                // Calculate channel from last BarrasCanal bars
                resistencia = double.MinValue;
                soporte = double.MaxValue;

                for (int i = 0; i < BarrasCanal; i++)
                {
                    if (High[i] > resistencia) resistencia = High[i];
                    if (Low[i] < soporte) soporte = Low[i];
                }

                tamanoCanal = resistencia - soporte;
                barrasEsperando = 0;
                estado = BotState.EsperandoBreakout;

                // Draw channel levels
                Draw.HorizontalLine(this, "CanalResistencia", resistencia, Brushes.Lime, DashStyleHelper.Dash, 2);
                Draw.HorizontalLine(this, "CanalSoporte", soporte, Brushes.Red, DashStyleHelper.Dash, 2);

                lastDecision = string.Format("CANAL_FORMADO R={0:F2} S={1:F2} tam={2:F2} ATR={3:F2}<{4:F2}",
                    resistencia, soporte, tamanoCanal, atr14[0], umbralCompresion);
            }
            else
            {
                lastDecision = string.Format("BUSCANDO_CANAL comprimidas={0}/{1} ATR={2:F2} umbral={3:F2}",
                    barrasComprimidas, BarrasCanal, atr14[0], umbralCompresion);
            }
        }

        #endregion

        #region Estado: EsperandoBreakout

        private void ProcesarEsperandoBreakout()
        {
            barrasEsperando++;

            // Validate EMAs still aligned
            if ((direccionCruce == 1 && ema20[0] <= ema200[0]) ||
                (direccionCruce == -1 && ema20[0] >= ema200[0]))
            {
                estado = BotState.EsperandoCruce;
                RemoveDrawObject("CanalResistencia");
                RemoveDrawObject("CanalSoporte");
                lastDecision = "BREAKOUT_INVALIDO_RECRUCE_EMAs";
                return;
            }

            if (barrasEsperando > MaxBarrasEspera)
            {
                // Timeout — go back to look for new channel
                barrasComprimidas = 0;
                estado = BotState.BuscandoCanal;
                RemoveDrawObject("CanalResistencia");
                RemoveDrawObject("CanalSoporte");
                lastDecision = string.Format("BREAKOUT_TIMEOUT {0} barras", barrasEsperando);
                return;
            }

            // Check breakout
            if (Close[0] > resistencia)
            {
                // Long breakout
                EjecutarEntrada(1);
                return;
            }

            if (Close[0] < soporte)
            {
                // Short breakout
                EjecutarEntrada(-1);
                return;
            }

            lastDecision = string.Format("ESPERANDO_BREAKOUT R={0:F2} S={1:F2} barra={2}/{3}",
                resistencia, soporte, barrasEsperando, MaxBarrasEspera);
        }

        #endregion

        #region Entrada

        private void EjecutarEntrada(int dir)
        {
            // Position sizing
            stopDistance = tamanoCanal + ColchonStop;
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
                tradesPermitidosHoy = tradesToday;
                estado = BotState.DiaTerminado;
                lastDecision = string.Format("FUERA_PRESUPUESTO riesgo=${0:F2} > max=${1:F2}", riesgo1Micro, presupuestoDisponible);
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

            // Calculate stops and targets
            double stopLong = soporte - ColchonStop;
            double stopShort = resistencia + ColchonStop;
            double tpTicks = stopDistance / TickSize;
            double tp2Ticks = (stopDistance * 2) / TickSize;

            if (dir == 1)
            {
                // Validate stop viability
                if (Close[0] <= stopLong)
                {
                    lastDecision = string.Format("ENTRY_LONG_CANCELADA precio={0:F2} <= stop={1:F2}", Close[0], stopLong);
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    RemoveDrawObject("CanalResistencia");
                    RemoveDrawObject("CanalSoporte");
                    return;
                }

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

                Draw.Diamond(this, "Entry" + CurrentBar, true, 0, Close[0], Brushes.Lime);
                lastDecision = string.Format("ENTRY_LONG TP1x{0} TP2x{1} @{2:F2} stop={3:F2}",
                    qtyTP1, qtyTP2, Close[0], stopLong);
            }
            else
            {
                // Validate stop viability
                if (Close[0] >= stopShort)
                {
                    lastDecision = string.Format("ENTRY_SHORT_CANCELADA precio={0:F2} >= stop={1:F2}", Close[0], stopShort);
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    RemoveDrawObject("CanalResistencia");
                    RemoveDrawObject("CanalSoporte");
                    return;
                }

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

                Draw.Diamond(this, "Entry" + CurrentBar, true, 0, Close[0], Brushes.Red);
                lastDecision = string.Format("ENTRY_SHORT TP1x{0} TP2x{1} @{2:F2} stop={3:F2}",
                    qtyTP1, qtyTP2, Close[0], stopShort);
            }

            estado = BotState.EnTrade;
        }

        #endregion

        #region Estado: EnTrade

        private void MonitorearTrade(DateTime nyNow)
        {
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

            // Breakeven
            if (!breakevenHit && unrealPts >= stopDistance * (BreakevenPct / 100.0))
            {
                double beStop = tradeDirection == 1 ? entryPrice + ColchonBreakeven : entryPrice - ColchonBreakeven;
                if (qtyTP1 > 0) SetStopLoss(signalTP1, CalculationMode.Price, beStop, true);
                if (qtyTP2 > 0) SetStopLoss(signalTP2, CalculationMode.Price, beStop, true);
                breakevenHit = true;
                BorrarActivacion("BE_Act");
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
                    BorrarActivacion("TP1_Act4");
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
                    BorrarActivacion("TP1_Act3");
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
                    BorrarActivacion("TP1_Act2");
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
                    BorrarActivacion("TP1_Act1");
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
                    BorrarActivacion("TP2_Act6");
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
                    BorrarActivacion("TP2_Act5");
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
                    BorrarActivacion("TP2_Act4");
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
                    BorrarActivacion("TP2_Act3");
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
                    BorrarActivacion("TP2_Act2");
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
                    BorrarActivacion("TP2_Act1");
                    lastDecision = string.Format("TP2_STOP{0}={1:F2}", TP2Stp1, nuevoStop);
                    return;
                }
            }

            lastDecision = string.Format("EN_TRADE {0} entry={1:F2} unreal={2:F2}pts",
                tradeDirection == 1 ? "LONG" : "SHORT", entryPrice, unrealPts);
        }

        #endregion

        #region Transicion post-trade

        private void ProcesarFinTrade()
        {
            tradeEnded = false;
            LimpiarNivelesTrailing();
            RemoveDrawObject("CanalResistencia");
            RemoveDrawObject("CanalSoporte");

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
                    // Post-TP: look for new channel with same trend
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("POST_TP_BUSCANDO_CANAL dir={0}", direccionCruce == 1 ? "ALC" : "BAJ");
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
                    // Post-SL: look for new channel
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("POST_SL_BUSCANDO_CANAL dir={0}", direccionCruce == 1 ? "ALC" : "BAJ");
                }
            }
            else
            {
                // Breakeven exit
                tradeEndedByTakeProfit = false;
                breakevensHoy++;

                // Recalculate remaining budget
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
                    // Post-BE: look for new channel
                    barrasComprimidas = 0;
                    estado = BotState.BuscandoCanal;
                    lastDecision = string.Format("POST_BE_BUSCANDO_CANAL dir={0} tradesRest={1}",
                        direccionCruce == 1 ? "ALC" : "BAJ", tradesPermitidosHoy - tradesToday);
                }
            }
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

        #region Helpers

        private Sesion GetSesionParaHora(DateTime nyNow)
        {
            int h = nyNow.Hour;
            int m = nyNow.Minute;
            double t = h + m / 60.0;

            // Asia: 18:00 - 02:00 (next day)
            if (t >= 18.0) return Sesion.Asia;
            // Between 00:00-02:00 is still Asia (wraps midnight)
            if (t < 2.0) return Sesion.Asia;
            // Europa: 02:00 - 09:30
            if (t >= 2.0 && t < 9.5) return Sesion.Europa;
            // America: 09:30 - HoraCierre
            double cierre = horaCierreH + horaCierreM / 60.0;
            if (t >= 9.5 && t < cierre) return Sesion.America;

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
                case Sesion.Asia: return false; // Asia starts at 18:00, handled by session detection
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
            tradeDirection = 0;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            tradeCounted = false;
            lastExitReason = "";
            entryPrice = 0;
            stopDistance = 0;
            barrasComprimidas = 0;
            barrasEsperando = 0;
            resistencia = 0;
            soporte = 0;
            tamanoCanal = 0;
            riesgo1Micro = 0;
            contratosCalculados = 0;

            // If EMAs are already crossed and price confirms, skip to BuscandoCanal
            if (ema20[0] > ema200[0] && Close[0] > ema20[0] && Close[0] > ema200[0])
            {
                direccionCruce = 1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                barrasComprimidas = 0;
                estado = BotState.BuscandoCanal;
                lastDecision = string.Format("NEW_SESSION_{0} EMAs_YA_ALCISTAS directo_BuscandoCanal", sesionActual);
            }
            else if (ema20[0] < ema200[0] && Close[0] < ema20[0] && Close[0] < ema200[0])
            {
                direccionCruce = -1;
                atrImpulso = atr14[0];
                barrasConfirmadas = 0;
                barrasComprimidas = 0;
                estado = BotState.BuscandoCanal;
                lastDecision = string.Format("NEW_SESSION_{0} EMAs_YA_BAJISTAS directo_BuscandoCanal", sesionActual);
            }
            else
            {
                direccionCruce = 0;
                atrImpulso = 0;
                barrasConfirmadas = 0;
                estado = BotState.EsperandoCruce;
                lastDecision = string.Format("NEW_SESSION_{0}", sesionActual);
            }
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
            estado = BotState.EsperandoCruce;
            tradeDirection = 0;
            breakevenHit = false;
            tp1StopNivel = 0;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            tradeCounted = false;
            lastExitReason = "";
            entryPrice = 0;
            stopDistance = 0;
            direccionCruce = 0;
            atrImpulso = 0;
            barrasConfirmadas = 0;
            barrasComprimidas = 0;
            barrasEsperando = 0;
            resistencia = 0;
            soporte = 0;
            tamanoCanal = 0;
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

            // TP1 activations
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

            // TP2 activations
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
                    File.WriteAllText(csvLogPath, "Timestamp;Tipo;Dir;Precio;PnL;Signal\n");
                if (overwrite || !File.Exists(csvDailyPath))
                    File.WriteAllText(csvDailyPath, "Fecha;Trades;PnL;Acumulado\n");
                if (overwrite || !File.Exists(csvBarLogPath))
                    File.WriteAllText(csvBarLogPath, "Timestamp;Open;High;Low;Close;Vol;EMA20;EMA200;ATR;Estado\n");
            }
            catch {}
        }

        private void LogTradeCsv(DateTime nyNow, string action, string dir, double price, double pnl, string signal)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm};{1};{2};{3};{4};{5}\n",
                    nyNow, action, dir,
                    price.ToString("F2").Replace('.', ','),
                    pnl.ToString("F2").Replace('.', ','),
                    signal);
                File.AppendAllText(csvLogPath, line);
            }
            catch {}
        }

        private void LogDailyCsv(DateTime nyDate)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd};{1};{2};{3}\n",
                    nyDate, tradesToday,
                    dailyPnL.ToString("F2").Replace('.', ','),
                    totalPnL.ToString("F2").Replace('.', ','));
                File.AppendAllText(csvDailyPath, line);
            }
            catch {}
        }

        private void LogBarCsv(DateTime nyNow)
        {
            if (ModoLog == LogMode.Off) return;
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm};{1};{2};{3};{4};{5};{6};{7};{8};{9}\n",
                    nyNow,
                    Open[0].ToString("F2").Replace('.', ','),
                    High[0].ToString("F2").Replace('.', ','),
                    Low[0].ToString("F2").Replace('.', ','),
                    Close[0].ToString("F2").Replace('.', ','),
                    (long)Volume[0],
                    ema20[0].ToString("F2").Replace('.', ','),
                    ema200[0].ToString("F2").Replace('.', ','),
                    atr14[0].ToString("F2").Replace('.', ','),
                    estado);
                File.AppendAllText(csvBarLogPath, line);
            }
            catch {}
        }

        #endregion
    }
}
