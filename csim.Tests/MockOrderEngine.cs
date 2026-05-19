using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using CSimulator;

namespace CSimulator.Tests
{
    internal class MockOrderEngine : IOrderEngine
    {
        public List<EntryRecord> Entries { get; } = new();
        public List<ExitRecord> Exits { get; } = new();
        public Dictionary<string, double> Stops { get; } = new();
        public Dictionary<string, (double Value, CalculationMode Mode)> Targets { get; } = new();
        public List<Order> CancelledOrders { get; } = new();

        public void SubmitEntry(string signalName, MarketPosition direction, int quantity)
        {
            Entries.Add(new EntryRecord(signalName, direction, quantity));
        }

        public void SubmitMarketExit(string fromEntry, MarketPosition direction, string exitSignalName)
        {
            Exits.Add(new ExitRecord(fromEntry, direction, exitSignalName));
        }

        public void SetStop(string fromEntry, double price)
        {
            Stops[fromEntry] = price;
        }

        public void SetTarget(string fromEntry, double value, CalculationMode mode)
        {
            Targets[fromEntry] = (value, mode);
        }

        public void CancelOrder(Order order)
        {
            CancelledOrders.Add(order);
        }

        public void Reset()
        {
            Entries.Clear();
            Exits.Clear();
            Stops.Clear();
            Targets.Clear();
            CancelledOrders.Clear();
        }
    }

    internal record EntryRecord(string SignalName, MarketPosition Direction, int Quantity);
    internal record ExitRecord(string FromEntry, MarketPosition Direction, string ExitSignalName);
}
