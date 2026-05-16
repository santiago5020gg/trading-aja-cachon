namespace NinjaTrader.Cbi
{
    public class Order
    {
        public string Name { get; set; }
        public OrderState OrderState { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double StopPrice { get; set; }
        public double LimitPrice { get; set; }
        public Instrument Instrument { get; set; }
        public string SignalName { get; set; }
        public Order() { OrderState = OrderState.Initialized; }
    }
}
