#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class MCPBridgeIndicator : Indicator
    {
        private SMA sma20;
        private SMA sma200;
        private ATR atr14;
        private RSI rsi7;
        private StdDev stdDev20;
        private SMA volSma20;
        private TimeZoneInfo easternZone;

        [NinjaScriptProperty]
        [Display(Name = "History Bars", Description = "Number of historical bars to send", GroupName = "MCP Bridge", Order = 1)]
        public int HistoryBars { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Feeds chart data to MCP Bridge for Claude Code integration";
                Name = "MCPBridgeIndicator";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                IsAutoScale = false;
                HistoryBars = 22000;
            }
            else if (State == State.DataLoaded)
            {
                sma20 = SMA(20);
                sma200 = SMA(200);
                atr14 = ATR(14);
                rsi7 = RSI(7, 3);
                stdDev20 = StdDev(20);
                volSma20 = SMA(Volume, 20);
                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 200) return;

            DateTime? target = MCPBridge.GetPlaybackTarget();
            if (target.HasValue)
            {
                if (Time[0] >= target.Value)
                {
                    NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 0;
                    MCPBridge.PlaybackTargetReached();
                }
                else
                {
                    double minutesAway = (target.Value - Time[0]).TotalMinutes;
                    if (minutesAway < 6)
                        NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 1;
                    else if (minutesAway < 30)
                        NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 4;
                    else if (minutesAway < 120)
                        NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 32;
                    else if (minutesAway < 480)
                        NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 256;
                    else
                        NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 1000;
                }
            }

            DateTime nyTime = TimeZoneInfo.ConvertTime(Time[0], easternZone);

            var data = new MCPBridgeData
            {
                Instrument = Instrument.FullName,
                BarPeriod = BarsPeriod.ToString(),
                Timestamp = Time[0],
                TimestampNY = nyTime.ToString("yyyy-MM-dd HH:mm:ss"),
                CurrentOpen = Open[0],
                CurrentHigh = High[0],
                CurrentLow = Low[0],
                CurrentClose = Close[0],
                CurrentVolume = Volume[0],
                SMA20 = sma20[0],
                SMA200 = sma200[0],
                ATR14 = atr14[0],
                RSI7 = rsi7[0],
                StdDev20 = stdDev20[0],
                VolumeSMA20 = volSma20[0],
                SMA20Slope = CurrentBar >= 5 ? (sma20[0] - sma20[5]) / 5.0 : 0,
                SMA200Slope = CurrentBar >= 5 ? (sma200[0] - sma200[5]) / 5.0 : 0,
                SMASpread = atr14[0] > 0 ? Math.Abs(sma20[0] - sma200[0]) / atr14[0] : 0,
                ZScore = stdDev20[0] > 0 ? (Close[0] - sma20[0]) / stdDev20[0] : 0
            };

            MCPBridge.AppendBar(new BarData
            {
                Time = Time[0],
                Open = Open[0],
                High = High[0],
                Low = Low[0],
                Close = Close[0],
                Volume = Volume[0],
                SMA20 = sma20[0],
                SMA200 = sma200[0]
            }, HistoryBars);

            data.History = null;
            MCPBridge.UpdateData(data);
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private MCPBridgeIndicator[] cacheMCPBridgeIndicator;
		public MCPBridgeIndicator MCPBridgeIndicator(int historyBars)
		{
			return MCPBridgeIndicator(Input, historyBars);
		}

		public MCPBridgeIndicator MCPBridgeIndicator(ISeries<double> input, int historyBars)
		{
			if (cacheMCPBridgeIndicator != null)
				for (int idx = 0; idx < cacheMCPBridgeIndicator.Length; idx++)
					if (cacheMCPBridgeIndicator[idx] != null && cacheMCPBridgeIndicator[idx].HistoryBars == historyBars && cacheMCPBridgeIndicator[idx].EqualsInput(input))
						return cacheMCPBridgeIndicator[idx];
			return CacheIndicator<MCPBridgeIndicator>(new MCPBridgeIndicator(){ HistoryBars = historyBars }, input, ref cacheMCPBridgeIndicator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.MCPBridgeIndicator MCPBridgeIndicator(int historyBars)
		{
			return indicator.MCPBridgeIndicator(Input, historyBars);
		}

		public Indicators.MCPBridgeIndicator MCPBridgeIndicator(ISeries<double> input , int historyBars)
		{
			return indicator.MCPBridgeIndicator(input, historyBars);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.MCPBridgeIndicator MCPBridgeIndicator(int historyBars)
		{
			return indicator.MCPBridgeIndicator(Input, historyBars);
		}

		public Indicators.MCPBridgeIndicator MCPBridgeIndicator(ISeries<double> input , int historyBars)
		{
			return indicator.MCPBridgeIndicator(input, historyBars);
		}
	}
}

#endregion
