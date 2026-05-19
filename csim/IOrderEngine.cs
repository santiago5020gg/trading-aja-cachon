using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace CSimulator
{
    internal interface IOrderEngine
    {
        void SubmitEntry(string signalName, MarketPosition direction, int quantity);
        void SubmitMarketExit(string fromEntry, MarketPosition direction, string exitSignalName);
        void SetStop(string fromEntry, double price);
        void SetTarget(string fromEntry, double value, CalculationMode mode);
        void CancelOrder(Order order);
    }
}
