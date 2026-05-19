using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript
{
    public class Position
    {
        public MarketPosition MarketPosition { get; set; } = MarketPosition.Flat;
        public int Quantity { get; set; }
        public double AveragePrice { get; set; }
    }
}
