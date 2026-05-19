using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript
{
    public class Execution
    {
        public Order Order { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public MarketPosition MarketPosition { get; set; }
        public string ExecutionId { get; set; }
        public string OrderId { get; set; }
    }
}
