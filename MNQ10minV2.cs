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
        private bool breakevenHit;
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
                Description = "MNQ10minV2 — Breakout rango 09:30-09:40 con stop market 1:1";
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

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;

            DateTime nyNow = TimeZoneInfo.ConvertTime(Time[0], easternZone);
            DateTime nyDate = nyNow.Date;

            if (nyDate != lastResetDate)
            {
                if (lastResetDate != DateTime.MinValue)
                    LogDailyCsv(lastResetDate);
                ResetDaily();
                lastResetDate = nyDate;
            }

            LogBarCsv(nyNow);

            if (estado == BotState.DiaTerminado)
            {
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
                    MonitorearOrdenes(nyNow);
                    break;
                case BotState.EnTrade:
                    MonitorearTrade(nyNow);
                    break;
            }

            WriteTelemetry(nyNow);
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
            double stopTicks = stopDistance / TickSize;

            if (!longUsado)
            {
                SetStopLoss("RangoLong", CalculationMode.Ticks, stopTicks, false);
                SetProfitTarget("RangoLong", CalculationMode.Ticks, stopTicks);
                EnterLongStopMarket(1, rangoHigh, "RangoLong");
            }

            if (!shortUsado)
            {
                SetStopLoss("RangoShort", CalculationMode.Ticks, stopTicks, false);
                SetProfitTarget("RangoShort", CalculationMode.Ticks, stopTicks);
                EnterShortStopMarket(1, rangoLow, "RangoShort");
            }
        }

        private void MonitorearOrdenes(DateTime nyNow)
        {
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                estado = BotState.EnTrade;
                lastDecision = string.Format("EN_TRADE {0}", tradeDirection == 1 ? "LONG" : "SHORT");
                return;
            }

            // Detect gap-through: price already past the stop level without triggering
            if (!longUsado && Close[0] >= rangoHigh)
            {
                double stopTicks = stopDistance / TickSize;
                SetStopLoss("RangoLong", CalculationMode.Ticks, stopTicks, false);
                SetProfitTarget("RangoLong", CalculationMode.Ticks, stopTicks);
                EnterLong(1, "RangoLong");
                lastDecision = string.Format("ENTRY_MARKET_LONG (gap) @{0:F2}", Close[0]);
                return;
            }

            if (!shortUsado && Close[0] <= rangoLow)
            {
                double stopTicks = stopDistance / TickSize;
                SetStopLoss("RangoShort", CalculationMode.Ticks, stopTicks, false);
                SetProfitTarget("RangoShort", CalculationMode.Ticks, stopTicks);
                EnterShort(1, "RangoShort");
                lastDecision = string.Format("ENTRY_MARKET_SHORT (gap) @{0:F2}", Close[0]);
                return;
            }

            // Normal: keep submitting stop market orders
            double stopTks = stopDistance / TickSize;

            if (!longUsado)
            {
                SetStopLoss("RangoLong", CalculationMode.Ticks, stopTks, false);
                SetProfitTarget("RangoLong", CalculationMode.Ticks, stopTks);
                EnterLongStopMarket(1, rangoHigh, "RangoLong");
            }

            if (!shortUsado)
            {
                SetStopLoss("RangoShort", CalculationMode.Ticks, stopTks, false);
                SetProfitTarget("RangoShort", CalculationMode.Ticks, stopTks);
                EnterShortStopMarket(1, rangoLow, "RangoShort");
            }

            lastDecision = string.Format("ESPERANDO_FILL H={0:F2} L={1:F2}", rangoHigh, rangoLow);
        }

        #endregion

        #region Estado: EnTrade

        private void MonitorearTrade(DateTime nyNow)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                // Trade completed (TP or SL hit via managed orders)
                if (tradesToday >= MaxTrades || (longUsado && shortUsado))
                {
                    estado = BotState.DiaTerminado;
                    lastDecision = "DIA_TERMINADO_MAX_TRADES";
                }
                else
                {
                    // Place order on opposite side
                    estado = BotState.OrdenesPuestas;
                    ColocarOrdenes();
                    lastDecision = string.Format("REENTRY lado={0}", !longUsado ? "LONG" : "SHORT");
                }
                tradeDirection = 0;
                return;
            }

            double unrealPts = tradeDirection == 1
                ? Close[0] - entryPrice
                : entryPrice - Close[0];

            if (!breakevenHit && unrealPts >= stopDistance * 0.55)
            {
                double beStop = tradeDirection == 1 ? entryPrice + 5 : entryPrice - 5;
                string signal = tradeDirection == 1 ? "RangoLong" : "RangoShort";
                SetStopLoss(signal, CalculationMode.Price, beStop, false);
                breakevenHit = true;
                lastDecision = string.Format("BREAKEVEN {0} stop={1:F2} (+5pts)",
                    tradeDirection == 1 ? "LONG" : "SHORT", beStop);
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

            // Entry fills — cancel opposite order by marking both sides used
            if (orderName == "RangoLong")
            {
                entryPrice = price;
                tradeDirection = 1;
                longUsado = true;
                shortUsado = true;
                tradesToday++;
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL LONG @{0:F2}", price);
                tradeLog.Add(string.Format("FILL LONG @{0:F2}", price));

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "LONG", price, 0, "");
            }
            else if (orderName == "RangoShort")
            {
                entryPrice = price;
                tradeDirection = -1;
                shortUsado = true;
                longUsado = true;
                tradesToday++;
                estado = BotState.EnTrade;

                lastAction = string.Format("FILL SHORT @{0:F2}", price);
                tradeLog.Add(string.Format("FILL SHORT @{0:F2}", price));

                DateTime entryNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(entryNY, "ENTRY", "SHORT", price, 0, "");
            }
            else if (orderName == "Stop loss" || orderName == "Profit target")
            {
                double pnl = 0;
                string dir = tradeDirection == 1 ? "LONG" : "SHORT";
                string reason = orderName == "Stop loss" ? "StopLoss" : "TakeProfit";

                if (tradeDirection == 1)
                    pnl = (price - entryPrice) * 2.0 * quantity;
                else if (tradeDirection == -1)
                    pnl = (entryPrice - price) * 2.0 * quantity;

                dailyPnL += pnl;
                totalPnL += pnl;

                lastAction = string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}", dir, reason, price, pnl);
                tradeLog.Add(string.Format("EXIT {0} {1} @{2:F2} pnl=${3:F2}", dir, reason, price, pnl));

                DateTime exitNY = TimeZoneInfo.ConvertTime(time, easternZone);
                LogTradeCsv(exitNY, "EXIT", dir, price, pnl, reason);

                Draw.Diamond(this, "Exit" + CurrentBar, true, 0, price, Brushes.Yellow);

                if (marketPosition == MarketPosition.Flat)
                {
                    tradeDirection = 0;
                    estado = BotState.DiaTerminado;
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
            string dir = tradeDirection == 1 ? "LONG" : "SHORT";

            if (Position.MarketPosition == MarketPosition.Long)
                ExitLong("X_" + reason, "RangoLong");
            else if (Position.MarketPosition == MarketPosition.Short)
                ExitShort("X_" + reason, "RangoShort");

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
