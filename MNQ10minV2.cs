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
        [Display(Name = "Max Trades/Dia", GroupName = "1. Risk", Order = 2)]
        public int MaxTrades { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Hora Cierre (HH:mm)", GroupName = "2. Sesion", Order = 1)]
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
        private int tradeDirection;       // 1=long, -1=short, 0=flat
        private double entryPrice;
        private double stopDistance;       // in points

        private double rangoHigh;
        private double rangoLow;
        private double rangoPuntos;
        private int rangoStartBar;
        private int rangoEndBar;

        private bool longUsado;
        private bool shortUsado;
        private bool reentryPriceInRange;
        private bool breakevenHit;
        private int tp2StopNivel;
        private bool tradeEnded;
        private bool tradeEndedByTakeProfit;
        private string lastExitReason;
        private int lastExitDirection;
        private double dailyPnL;
        private double totalPnL;
        private int tradesToday;

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
                Description = "MNQ10minV2 — Breakout rango 09:32-09:40 con 2 contratos";
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

                ColchonStop = 30;
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

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);
            DateTime nyDate = nyNow.Date;

            if (nyDate != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                {
                    LogDailyCsv(lastResetDate);
                    if (Position.MarketPosition != MarketPosition.Flat)
                        FlattenAll("CierreDia");
                }
                ResetDaily();
                lastResetDate = nyDate;
            }

            bool firstTick = IsFirstTickOfBar;

            if (firstTick) LogBarCsv(nyNow);

            if (estado == BotState.DiaTerminado)
            {
                lastDecision = "DIA_TERMINADO";
                if (firstTick) WriteTelemetry(nyNow);
                return;
            }

            bool afterClose = nyNow.Hour > horaCierreH || (nyNow.Hour == horaCierreH && nyNow.Minute >= horaCierreM);
            if (afterClose)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    FlattenAll("CierreForzado");
                estado = BotState.DiaTerminado;
                lastDecision = "CIERRE_FORZADO";
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
            tradeDirection = 0;
            entryPrice = 0;
            tp2StopNivel = -1;

            if (tradeEndedByTakeProfit)
            {
                estado = BotState.DiaTerminado;
                lastDecision = "DIA_TERMINADO_TP";
            }
            else
            {
                longUsado = false;
                shortUsado = false;
                reentryPriceInRange = false;
                estado = BotState.OrdenesPuestas;
                lastDecision = string.Format("REENTRY_ORDENES H={0:F2} L={1:F2}", rangoHigh, rangoLow);
            }

            tradeEndedByTakeProfit = false;
            breakevenHit = false;
        }

        #endregion

        #region Estado: EsperandoRango

        private void ProcesarRango(DateTime nyNow)
        {
            bool antesVentana = nyNow.Hour < 9 || (nyNow.Hour == 9 && nyNow.Minute < 32);
            bool enVentana = (nyNow.Hour == 9 && nyNow.Minute >= 32 && nyNow.Minute <= 40);

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

            int barsBack = CurrentBar - rangoStartBar;
            Draw.Rectangle(this, "Rango" + nyNow.ToString("yyyyMMdd"), false,
                barsBack, rangoHigh, 0, rangoLow, Brushes.Transparent, Brushes.DodgerBlue, 30);

            ColocarOrdenes();

            estado = BotState.OrdenesPuestas;
            lastDecision = string.Format("ORDENES_PUESTAS H={0:F2} L={1:F2} pts={2:F2} stop={3:F2}",
                rangoHigh, rangoLow, rangoPuntos, stopDistance);
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
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                estado = BotState.EnTrade;
                lastDecision = string.Format("EN_TRADE {0}", tradeDirection == 1 ? "LONG" : "SHORT");
                return;
            }

            if (tradesToday >= MaxTrades)
            {
                estado = BotState.DiaTerminado;
                lastDecision = "DIA_TERMINADO_MAX_TRADES";
                return;
            }

            double stopLong = rangoLow - ColchonStop;
            double stopShort = rangoHigh + ColchonStop;
            double tpTicks = stopDistance / TickSize;
            double tp2Ticks = (stopDistance * 2) / TickSize;

            bool esReentry = tradesToday > 0;

            if (esReentry && !reentryPriceInRange)
            {
                if (Close[0] > rangoLow && Close[0] < rangoHigh)
                {
                    reentryPriceInRange = true;
                    ColocarOrdenes();
                }
                else
                {
                    lastDecision = string.Format("REENTRY_ESPERA_RANGO H={0:F2} L={1:F2}", rangoHigh, rangoLow);
                    return;
                }
            }

            if (!longUsado && Close[0] >= rangoHigh)
            {
                SetStopLoss("TP1Long", CalculationMode.Price, stopLong, false);
                SetProfitTarget("TP1Long", CalculationMode.Ticks, tpTicks);
                EnterLong(1, "TP1Long");

                SetStopLoss("TP2Long", CalculationMode.Price, stopLong, false);
                SetProfitTarget("TP2Long", CalculationMode.Ticks, tp2Ticks);
                EnterLong(1, "TP2Long");

                RemoveDrawObject("BuyLevel");
                RemoveDrawObject("SellLevel");
                lastDecision = string.Format("ENTRY_LONG x2 @{0:F2} stop={1:F2}", Close[0], stopLong);
                return;
            }

            if (!shortUsado && Close[0] <= rangoLow)
            {
                SetStopLoss("TP1Short", CalculationMode.Price, stopShort, false);
                SetProfitTarget("TP1Short", CalculationMode.Ticks, tpTicks);
                EnterShort(1, "TP1Short");

                SetStopLoss("TP2Short", CalculationMode.Price, stopShort, false);
                SetProfitTarget("TP2Short", CalculationMode.Ticks, tp2Ticks);
                EnterShort(1, "TP2Short");

                RemoveDrawObject("BuyLevel");
                RemoveDrawObject("SellLevel");
                lastDecision = string.Format("ENTRY_SHORT x2 @{0:F2}", Close[0]);
                return;
            }

            lastDecision = string.Format("ESPERANDO_RUPTURA H={0:F2} L={1:F2}", rangoHigh, rangoLow);
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

            if (!breakevenHit && unrealPts >= stopDistance * 0.65)
            {
                double beStop = tradeDirection == 1 ? entryPrice + 5 : entryPrice - 5;
                string signalTP1 = tradeDirection == 1 ? "TP1Long" : "TP1Short";
                string signalTP2 = tradeDirection == 1 ? "TP2Long" : "TP2Short";
                SetStopLoss(signalTP1, CalculationMode.Price, beStop, false);
                SetStopLoss(signalTP2, CalculationMode.Price, beStop, false);
                breakevenHit = true;
                lastDecision = string.Format("BREAKEVEN_BOTH stop={0:F2}", beStop);
                return;
            }

            if (breakevenHit && tp2StopNivel < 0 && unrealPts >= stopDistance * 0.80)
            {
                double tp1Stop = tradeDirection == 1
                    ? entryPrice + (stopDistance * 0.55)
                    : entryPrice - (stopDistance * 0.55);
                string signalTP1 = tradeDirection == 1 ? "TP1Long" : "TP1Short";
                SetStopLoss(signalTP1, CalculationMode.Price, tp1Stop, false);
                tp2StopNivel = 0;
                lastDecision = string.Format("TP1_STOP55={0:F2}", tp1Stop);
                return;
            }

            double tp2Target = stopDistance * 2;
            string tp2Signal = tradeDirection == 1 ? "TP2Long" : "TP2Short";

            if (tp2StopNivel < 3 && unrealPts >= tp2Target * 0.98)
            {
                double nuevoStop = tradeDirection == 1
                    ? entryPrice + (tp2Target * 0.95)
                    : entryPrice - (tp2Target * 0.95);
                SetStopLoss(tp2Signal, CalculationMode.Price, nuevoStop, false);
                tp2StopNivel = 3;
                lastDecision = string.Format("TP2_STOP_98pct stop={0:F2}", nuevoStop);
                return;
            }

            if (tp2StopNivel < 2 && unrealPts >= tp2Target * 0.90)
            {
                double nuevoStop = tradeDirection == 1
                    ? entryPrice + (tp2Target * 0.75)
                    : entryPrice - (tp2Target * 0.75);
                SetStopLoss(tp2Signal, CalculationMode.Price, nuevoStop, false);
                tp2StopNivel = 2;
                lastDecision = string.Format("TP2_STOP_90pct stop={0:F2}", nuevoStop);
                return;
            }

            if (tp2StopNivel < 1 && unrealPts >= tp2Target * 0.85)
            {
                double nuevoStop = tradeDirection == 1
                    ? entryPrice + (tp2Target * 0.55)
                    : entryPrice - (tp2Target * 0.55);
                SetStopLoss(tp2Signal, CalculationMode.Price, nuevoStop, false);
                tp2StopNivel = 1;
                lastDecision = string.Format("TP2_STOP_85pct stop={0:F2}", nuevoStop);
                return;
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
                if (orderName == "TP1Long") tradesToday++;
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL LONG {0} @{1:F2}", orderName, price);
                tradeLog.Add(lastAction);

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "LONG", price, 0, orderName);
            }
            else if (isShortEntry)
            {
                if (entryPrice == 0) entryPrice = price;
                tradeDirection = -1;
                shortUsado = true;
                longUsado = true;
                if (orderName == "TP1Short") tradesToday++;
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL SHORT {0} @{1:F2}", orderName, price);
                tradeLog.Add(lastAction);

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
                    pnl = (price - entryPrice) * 2.0 * quantity;
                else if (tradeDirection == -1)
                    pnl = (entryPrice - price) * 2.0 * quantity;

                dailyPnL += pnl;
                totalPnL += pnl;

                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}", dir, reason, price, pnl);
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
                }
            }
        }

        #endregion

        #region Helpers

        private void ResetDaily()
        {
            dailyPnL = 0;
            tradesToday = 0;
            estado = BotState.EsperandoRango;
            tradeDirection = 0;
            rangoStartBar = 0;
            rangoEndBar = 0;
            breakevenHit = false;
            tp2StopNivel = -1;
            tradeEnded = false;
            tradeEndedByTakeProfit = false;
            lastExitReason = "";
            rangoHigh = double.MinValue;
            rangoLow = double.MaxValue;
            rangoPuntos = 0;
            stopDistance = 0;
            entryPrice = 0;
            longUsado = false;
            shortUsado = false;
            reentryPriceInRange = false;
            lastDecision = "NEW_DAY";
            lastAction = "";
        }

        private void FlattenAll(string reason)
        {
            string dir = tradeDirection == 1 ? "LONG" : "SHORT";

            if (Position.MarketPosition == MarketPosition.Long)
            {
                ExitLong("X_" + reason, "TP1Long");
                ExitLong("X_" + reason, "TP2Long");
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort("X_" + reason, "TP1Short");
                ExitShort("X_" + reason, "TP2Short");
            }

            Draw.Diamond(this, "Exit" + CurrentBar, true, 0, Close[0], Brushes.Yellow);
            lastAction = string.Format("FLATTEN {0} {1}", dir, reason);
            tradeDirection = 0;
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
                    File.WriteAllText(csvDailyPath, "Date,DailyPnL,TotalPnL,Trades,RangoPts\n");
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
                    unrealizedPts, unrealizedPts * 2.0,
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
