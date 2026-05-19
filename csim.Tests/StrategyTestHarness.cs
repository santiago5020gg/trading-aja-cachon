using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace CSimulator.Tests
{
    internal class StrategyTestHarness
    {
        public MNQ10minV2 Strategy { get; }
        public MockOrderEngine OrderEngine { get; }

        private int _currentBar;

        public StrategyTestHarness(Action<MNQ10minV2> configure = null)
        {
            Strategy = new MNQ10minV2();
            OrderEngine = new MockOrderEngine();
            Strategy._orderEngine = OrderEngine;

            Strategy.Initialize();

            if (configure != null)
            {
                configure(Strategy);
                Strategy.Reconfigure();
            }

            Strategy.TickSize = 0.25;
            _currentBar = 20;
            Strategy.CurrentBar = _currentBar;
        }

        public void SetTime(DateTime easternTime)
        {
            Strategy.Time[0] = TimeZoneInfo.ConvertTimeToUtc(easternTime,
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            // The strategy converts Time[0] to Eastern — we need to store UTC so conversion yields Eastern
            // Actually, the strategy does: TimeZoneInfo.ConvertTime(Time[0], easternZone)
            // so Time[0] should be in local time or UTC... let's just set it as Eastern directly
            // since the stub TimeSeries just stores/returns the value, the strategy's ConvertTime
            // will interpret it. Let's store it raw and set timezone info properly.
            Strategy.Time[0] = easternTime;
        }

        public void SetPrice(double open, double high, double low, double close, long volume = 100)
        {
            Strategy.Open[0] = open;
            Strategy.High[0] = high;
            Strategy.Low[0] = low;
            Strategy.Close[0] = close;
            Strategy.Volume[0] = volume;
        }

        public void SetClose(double close)
        {
            Strategy.Close[0] = close;
            Strategy.High[0] = Math.Max(Strategy.High[0], close);
            Strategy.Low[0] = Math.Min(Strategy.Low[0], close);
        }

        public void NewBar(DateTime easternTime, double open, double high, double low, double close, long volume = 100)
        {
            _currentBar++;
            Strategy.CurrentBar = _currentBar;
            Strategy.IsFirstTickOfBar = true;
            SetTime(easternTime);
            SetPrice(open, high, low, close, volume);
            Strategy.TriggerOnBarUpdate();
            Strategy.IsFirstTickOfBar = false;
        }

        public void Tick(DateTime easternTime, double price)
        {
            SetTime(easternTime);
            Strategy.Close[0] = price;
            Strategy.High[0] = Math.Max(Strategy.High[0], price);
            Strategy.Low[0] = Math.Min(Strategy.Low[0], price);
            Strategy.TriggerOnBarUpdate();
        }

        public void SimulateExecution(string orderName, double price, int quantity, MarketPosition marketPosition, DateTime time)
        {
            var order = new Order { Name = orderName, OrderState = OrderState.Filled };
            var execution = new Execution
            {
                Order = order,
                Price = price,
                Quantity = quantity,
                MarketPosition = marketPosition,
                ExecutionId = Guid.NewGuid().ToString(),
                OrderId = Guid.NewGuid().ToString()
            };
            Strategy.TriggerOnExecutionUpdate(execution, execution.ExecutionId, price, quantity, marketPosition, execution.OrderId, time);
        }

        public void SetPosition(MarketPosition pos, int qty = 1, double avgPrice = 0)
        {
            Strategy.Position.MarketPosition = pos;
            Strategy.Position.Quantity = qty;
            Strategy.Position.AveragePrice = avgPrice;
        }
    }
}
