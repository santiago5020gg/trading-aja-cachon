using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace CSimulator
{
    // ───────────────────────────────────────────────
    // State for a single entry signal (TP1Long, TP2Long, etc.)
    // ───────────────────────────────────────────────

    internal class EntryState
    {
        public string SignalName;
        public MarketPosition Direction;
        public int Quantity;
        public bool Filled;
        public double FillPrice;
        public double StopPrice;
        public double TargetPrice;
    }

    // ───────────────────────────────────────────────
    // Order Engine — simulates NinjaTrader fill logic
    // ───────────────────────────────────────────────

    internal class PendingEntry
    {
        public string SignalName;
        public MarketPosition Direction;
        public int Quantity;
    }

    internal class OrderEngine : IOrderEngine
    {
        private readonly Strategy _strategy;
        private readonly double _tickSize;

        // Active entries that have been filled and are still open
        private readonly List<EntryState> _activeEntries = new List<EntryState>();

        // Pending entries waiting to fill on next tick (OrderFillResolution.Standard)
        private readonly List<PendingEntry> _pendingEntries = new List<PendingEntry>();

        // Preset stops/targets (set before entry is filled)
        private readonly Dictionary<string, double> _presetStops = new Dictionary<string, double>();
        private readonly Dictionary<string, (double value, CalculationMode mode)> _presetTargets
            = new Dictionary<string, (double, CalculationMode)>();

        private int _executionCounter;

        internal OrderEngine(Strategy strategy, double tickSize)
        {
            _strategy = strategy;
            _tickSize = tickSize;
        }

        // ───────────────────────────────────────────
        // IOrderEngine implementation
        // ───────────────────────────────────────────

        public void SubmitEntry(string signalName, MarketPosition direction, int quantity)
        {
            // OrderFillResolution.Standard: queue entry for fill on next tick
            _pendingEntries.Add(new PendingEntry
            {
                SignalName = signalName,
                Direction = direction,
                Quantity = quantity
            });
        }

        public void SubmitMarketExit(string fromEntry, MarketPosition direction, string exitSignalName)
        {
            var entry = _activeEntries.FirstOrDefault(e =>
                e.SignalName == fromEntry && e.Filled);

            if (entry == null) return;

            // ExitLong (selling) fills at Bid; ExitShort (buying to cover) fills at Ask
            double price = entry.Direction == MarketPosition.Long
                ? _strategy.CurrentBid
                : _strategy.CurrentAsk;
            DateTime time = _strategy.Time[0];

            _activeEntries.Remove(entry);
            UpdatePosition();

            var execution = new Execution
            {
                Order = new Order
                {
                    Name = exitSignalName,
                    SignalName = exitSignalName,
                    OrderState = OrderState.Filled,
                    Quantity = entry.Quantity,
                    Price = price
                },
                Price = price,
                Quantity = entry.Quantity,
                MarketPosition = _strategy.Position.MarketPosition,
                ExecutionId = GenerateExecutionId(),
                OrderId = GenerateExecutionId()
            };

            _strategy.TriggerOnExecutionUpdate(
                execution,
                execution.ExecutionId,
                price,
                entry.Quantity,
                _strategy.Position.MarketPosition,
                execution.OrderId,
                time
            );
        }

        public void SetStop(string fromEntry, double price)
        {
            // If entry is already filled, update its stop directly
            var entry = _activeEntries.FirstOrDefault(e => e.SignalName == fromEntry && e.Filled);
            if (entry != null)
            {
                entry.StopPrice = price;
            }
            else
            {
                // Preset — store for when entry fills
                _presetStops[fromEntry] = price;
            }
        }

        public void SetTarget(string fromEntry, double value, CalculationMode mode)
        {
            // If entry is already filled, compute and update target
            var entry = _activeEntries.FirstOrDefault(e => e.SignalName == fromEntry && e.Filled);
            if (entry != null)
            {
                entry.TargetPrice = ComputeTargetPrice(entry.FillPrice, entry.Direction, value, mode);
            }
            else
            {
                // Preset — store for when entry fills
                _presetTargets[fromEntry] = (value, mode);
            }
        }

        public void CancelOrder(Order order)
        {
            if (order == null) return;

            // Remove from Account.Orders
            order.OrderState = OrderState.Cancelled;
            _strategy.Account.Orders.Remove(order);
        }

        // ───────────────────────────────────────────
        // Public methods called by the main loop
        // ───────────────────────────────────────────

        /// <summary>
        /// Fill pending entries at current Ask (long) or Bid (short).
        /// Called before OnBarUpdate on each tick (OrderFillResolution.Standard = next tick fill).
        /// </summary>
        public void FillPendingEntries(double price, DateTime time)
        {
            if (_pendingEntries.Count == 0) return;

            foreach (var pending in _pendingEntries)
            {
                // NinjaTrader fills longs at Ask, shorts at Bid
                double fillPrice = pending.Direction == MarketPosition.Long
                    ? _strategy.CurrentAsk
                    : _strategy.CurrentBid;

                var entry = new EntryState
                {
                    SignalName = pending.SignalName,
                    Direction = pending.Direction,
                    Quantity = pending.Quantity,
                    Filled = true,
                    FillPrice = fillPrice,
                    StopPrice = 0,
                    TargetPrice = 0
                };

                // Apply preset stop if exists
                if (_presetStops.TryGetValue(pending.SignalName, out double presetStop))
                {
                    entry.StopPrice = presetStop;
                    _presetStops.Remove(pending.SignalName);
                }

                // Apply preset target if exists
                if (_presetTargets.TryGetValue(pending.SignalName, out var presetTarget))
                {
                    entry.TargetPrice = ComputeTargetPrice(fillPrice, pending.Direction,
                        presetTarget.value, presetTarget.mode);
                    _presetTargets.Remove(pending.SignalName);
                }

                _activeEntries.Add(entry);

                // Add filled order to Account.Orders
                var order = new Order
                {
                    Name = pending.SignalName,
                    SignalName = pending.SignalName,
                    Quantity = pending.Quantity,
                    OrderState = OrderState.Filled,
                    Price = fillPrice
                };
                _strategy.Account.Orders.Add(order);

                // Update position
                UpdatePosition();

                // Fire OnExecutionUpdate
                var execution = new Execution
                {
                    Order = order,
                    Price = fillPrice,
                    Quantity = pending.Quantity,
                    MarketPosition = _strategy.Position.MarketPosition,
                    ExecutionId = GenerateExecutionId(),
                    OrderId = GenerateExecutionId()
                };

                _strategy.TriggerOnExecutionUpdate(
                    execution,
                    execution.ExecutionId,
                    fillPrice,
                    pending.Quantity,
                    _strategy.Position.MarketPosition,
                    execution.OrderId,
                    time
                );
            }

            _pendingEntries.Clear();
        }

        /// <summary>
        /// No-op: market exits now fill immediately in SubmitMarketExit.
        /// </summary>
        public void ProcessMarketExits(double price, DateTime time)
        {
        }

        /// <summary>
        /// Check if price hit any active entry's stop or target. Called after OnBarUpdate.
        /// </summary>
        public void EvaluateStopsAndTargets(double price, DateTime time)
        {
            // Iterate over a copy since we may remove entries
            var snapshot = new List<EntryState>(_activeEntries);

            foreach (var entry in snapshot)
            {
                if (!entry.Filled) continue;

                bool stopHit = false;
                bool targetHit = false;

                if (entry.Direction == MarketPosition.Long)
                {
                    // Stop: price drops to or below stop level
                    if (entry.StopPrice > 0 && price <= entry.StopPrice)
                        stopHit = true;
                    // Target: price rises to or above target level
                    else if (entry.TargetPrice > 0 && price >= entry.TargetPrice)
                        targetHit = true;
                }
                else // Short
                {
                    // Stop: price rises to or above stop level
                    if (entry.StopPrice > 0 && price >= entry.StopPrice)
                        stopHit = true;
                    // Target: price drops to or below target level
                    else if (entry.TargetPrice > 0 && price <= entry.TargetPrice)
                        targetHit = true;
                }

                if (stopHit || targetHit)
                {
                    double fillPrice = stopHit ? entry.StopPrice : entry.TargetPrice;
                    string orderName = stopHit ? "Stop loss" : "Profit target";

                    // Remove from active entries
                    _activeEntries.Remove(entry);

                    // Update position
                    UpdatePosition();

                    // Fire OnExecutionUpdate
                    var execution = new Execution
                    {
                        Order = new Order
                        {
                            Name = orderName,
                            SignalName = orderName,
                            OrderState = OrderState.Filled,
                            Quantity = entry.Quantity,
                            Price = fillPrice
                        },
                        Price = fillPrice,
                        Quantity = entry.Quantity,
                        MarketPosition = _strategy.Position.MarketPosition,
                        ExecutionId = GenerateExecutionId(),
                        OrderId = GenerateExecutionId()
                    };

                    _strategy.TriggerOnExecutionUpdate(
                        execution,
                        execution.ExecutionId,
                        fillPrice,
                        entry.Quantity,
                        _strategy.Position.MarketPosition,
                        execution.OrderId,
                        time
                    );
                }
            }
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────

        private double ComputeTargetPrice(double entryPrice, MarketPosition direction,
            double value, CalculationMode mode)
        {
            switch (mode)
            {
                case CalculationMode.Price:
                    return value;
                case CalculationMode.Ticks:
                    return direction == MarketPosition.Long
                        ? entryPrice + (value * _tickSize)
                        : entryPrice - (value * _tickSize);
                default:
                    return value; // Fallback: treat as price
            }
        }

        private void UpdatePosition()
        {
            if (_activeEntries.Count == 0)
            {
                _strategy.Position.MarketPosition = MarketPosition.Flat;
                _strategy.Position.Quantity = 0;
                _strategy.Position.AveragePrice = 0;
            }
            else
            {
                // All active entries should be same direction
                var first = _activeEntries[0];
                _strategy.Position.MarketPosition = first.Direction;
                _strategy.Position.Quantity = _activeEntries.Sum(e => e.Quantity);
                _strategy.Position.AveragePrice = _activeEntries.Average(e => e.FillPrice);
            }
        }

        private string GenerateExecutionId()
        {
            _executionCounter++;
            return $"exec_{_executionCounter}";
        }
    }
}
