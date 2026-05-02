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
    }
}
