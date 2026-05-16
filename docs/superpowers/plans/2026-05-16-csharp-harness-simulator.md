# C# Harness Simulator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET console app that executes MNQ10minV2.cs unmodified against NinjaTrader tick export files, producing identical results to NinjaTrader Playback.

**Architecture:** Mock the NinjaTrader framework (Strategy base class, orders, positions, drawing) in stub files. A tick reader feeds data through the real strategy's OnBarUpdate tick by tick, with an order engine that simulates fills on the next tick with 1-tick slippage.

**Tech Stack:** .NET 9, C# console app, no external dependencies.

---

## File Structure

```
csim/
├── csim.csproj                          — .NET 9 console project
├── Program.cs                           — CLI parsing, tick reader, main loop, summary output
├── NinjaTrader/
│   ├── Cbi/
│   │   ├── Account.cs                   — Account class with Orders list
│   │   ├── Instrument.cs                — Instrument stub
│   │   └── Order.cs                     — Order class (Name, OrderState, Quantity, Price)
│   ├── Data/
│   │   └── MarketDataEventArgs.cs       — Stub (unused but needed for using)
│   ├── NinjaScript/
│   │   ├── Strategy.cs                  — Base class: bar data, order methods, state lifecycle
│   │   ├── Enums.cs                     — All enums (State, Calculate, MarketPosition, etc.)
│   │   ├── Position.cs                  — Position tracking class
│   │   ├── Execution.cs                 — Execution wrapping fill data
│   │   └── NinjaScriptBase.cs           — Shared properties (CurrentBar, etc.)
│   ├── Gui/
│   │   └── DashStyleHelper.cs           — DashStyleHelper enum stub
│   └── NinjaScript.DrawingTools/
│       └── Draw.cs                      — Static Draw class with all no-op methods
├── Stubs/
│   ├── Attributes.cs                    — NinjaScriptProperty, Display, Range attributes
│   └── Brushes.cs                       — System.Windows.Media.Brushes stub
└── OrderEngine.cs                       — Manages pending orders, stop/target evaluation, fills
```

MNQ10minV2.cs is included via a `<Compile Include="..\..\MNQ10minV2.cs" />` directive in csim.csproj — no symlink, no copy.

---

### Task 1: Project Scaffold and Compilation Stubs

**Files:**
- Create: `csim/csim.csproj`
- Create: `csim/NinjaTrader/NinjaScript/Enums.cs`
- Create: `csim/NinjaTrader/Cbi/Order.cs`
- Create: `csim/NinjaTrader/Cbi/Account.cs`
- Create: `csim/NinjaTrader/Cbi/Instrument.cs`
- Create: `csim/NinjaTrader/Data/MarketDataEventArgs.cs`
- Create: `csim/NinjaTrader/NinjaScript/Position.cs`
- Create: `csim/NinjaTrader/NinjaScript/Execution.cs`
- Create: `csim/NinjaTrader/Gui/DashStyleHelper.cs`
- Create: `csim/NinjaTrader/NinjaScript.DrawingTools/Draw.cs`
- Create: `csim/Stubs/Attributes.cs`
- Create: `csim/Stubs/Brushes.cs`

- [ ] **Step 1: Create csim.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>CSimulator</RootNamespace>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\MNQ10minV2.cs" Link="MNQ10minV2.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Enums.cs**

All enums referenced by MNQ10minV2.cs:

```csharp
namespace NinjaTrader.NinjaScript
{
    public enum State { SetDefaults, Configure, DataLoaded, Historical, Realtime, Terminated }
    public enum Calculate { OnBarClose, OnEachTick, OnPriceChange }
    public enum MaximumBarsLookBack { TwoHundredFiftySix, Infinite }
    public enum OrderFillResolution { Standard, High }
    public enum StartBehavior { WaitUntilFlat, AdoptAccountPosition, ImmediatelySubmit }
    public enum TimeInForce { Gtc, Day }
    public enum RealtimeErrorHandling { StopCancelClose, IgnoreAllErrors }
    public enum StopTargetHandling { PerEntryExecution, ByStrategyPosition }
    public enum EntryHandling { AllEntries, UniqueEntries }
    public enum CalculationMode { Currency, Percent, Price, Ticks }
}

namespace NinjaTrader.Cbi
{
    public enum MarketPosition { Flat, Long, Short }
    public enum OrderState { Initialized, Submitted, Accepted, Working, Filled, Cancelled, Rejected }
}
```

- [ ] **Step 3: Create Order.cs**

```csharp
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
```

- [ ] **Step 4: Create Account.cs**

```csharp
using System.Collections.Generic;

namespace NinjaTrader.Cbi
{
    public class Account
    {
        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
```

- [ ] **Step 5: Create Instrument.cs**

```csharp
namespace NinjaTrader.Cbi
{
    public class Instrument
    {
        public string FullName { get; set; } = "MNQ 06-26";
    }
}
```

- [ ] **Step 6: Create MarketDataEventArgs.cs**

```csharp
namespace NinjaTrader.Data
{
    public class MarketDataEventArgs { }
}
```

- [ ] **Step 7: Create Position.cs**

```csharp
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
```

- [ ] **Step 8: Create Execution.cs**

```csharp
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
```

- [ ] **Step 9: Create DashStyleHelper.cs**

```csharp
namespace NinjaTrader.Gui
{
    public enum DashStyleHelper { Solid, Dash, Dot, DashDot, DashDotDot }
}
```

- [ ] **Step 10: Create Draw.cs**

```csharp
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public static class Draw
    {
        public static object Rectangle(object owner, string tag, bool isAutoScale, int startBar, double startY, int endBar, double endY, Brush outlineBrush, Brush areaBrush, int opacity) => null;
        public static object Text(object owner, string tag, string text, int barsAgo, double y, Brush brush) => null;
        public static object HorizontalLine(object owner, string tag, double price, Brush brush, DashStyleHelper dash, int width) => null;
        public static object ArrowUp(object owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush) => null;
        public static object ArrowDown(object owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush) => null;
        public static object Diamond(object owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush) => null;
    }
}
```

- [ ] **Step 11: Create Attributes.cs**

```csharp
using System;

namespace NinjaTrader.NinjaScript
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NinjaScriptPropertyAttribute : Attribute { }
}
```

Note: `Display` and `Range` come from `System.ComponentModel.DataAnnotations` which is built-in.

- [ ] **Step 12: Create Brushes.cs**

```csharp
namespace System.Windows.Media
{
    public class Brush { }

    public static class Brushes
    {
        public static Brush Transparent { get; } = new Brush();
        public static Brush DodgerBlue { get; } = new Brush();
        public static Brush Lime { get; } = new Brush();
        public static Brush Red { get; } = new Brush();
        public static Brush White { get; } = new Brush();
        public static Brush Yellow { get; } = new Brush();
        public static Brush Cyan { get; } = new Brush();
        public static Brush Orange { get; } = new Brush();
        public static Brush Magenta { get; } = new Brush();
        public static Brush Gold { get; } = new Brush();
    }
}
```

- [ ] **Step 13: Verify compilation attempt**

Run: `cd csim && dotnet build 2>&1 | head -50`

Expected: Compilation will FAIL because Strategy base class doesn't exist yet. This confirms all stubs are wired and the only missing piece is the Strategy base class.

- [ ] **Step 14: Commit scaffold**

```bash
git add csim/
git commit -m "feat(csim): project scaffold with NinjaTrader stubs"
```

---

### Task 2: Strategy Base Class

**Files:**
- Create: `csim/NinjaTrader/NinjaScript/Strategy.cs`

This is the core mock — it must provide every property and method MNQ10minV2.cs references on `this` or the base class.

- [ ] **Step 1: Create Strategy.cs**

```csharp
using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Strategies
{
    public abstract class Strategy
    {
        // --- State lifecycle ---
        public State State { get; set; }
        protected virtual void OnStateChange() { }
        protected virtual void OnBarUpdate() { }
        protected virtual void OnExecutionUpdate(Execution execution, string executionId,
            double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time) { }

        // --- Bar data (ring buffers of size 1 for [0] access) ---
        public PriceSeries Close { get; } = new PriceSeries();
        public PriceSeries High { get; } = new PriceSeries();
        public PriceSeries Low { get; } = new PriceSeries();
        public PriceSeries Open { get; } = new PriceSeries();
        public VolumeSeries Volume { get; } = new VolumeSeries();
        public TimeSeries Time { get; } = new TimeSeries();

        // --- Properties ---
        public int CurrentBar { get; set; }
        public bool IsFirstTickOfBar { get; set; }
        public double TickSize { get; set; } = 0.25;
        public Position Position { get; set; } = new Position();
        public Account Account { get; set; } = new Account();
        public Instrument Instrument { get; set; } = new Instrument();

        // --- Configuration properties ---
        public string Name { get; set; }
        public string Description { get; set; }
        public Calculate Calculate { get; set; }
        public int EntriesPerDirection { get; set; }
        public EntryHandling EntryHandling { get; set; }
        public bool IsExitOnSessionCloseStrategy { get; set; }
        public int ExitOnSessionCloseSeconds { get; set; }
        public bool IsFillLimitOnTouch { get; set; }
        public MaximumBarsLookBack MaximumBarsLookBack { get; set; }
        public OrderFillResolution OrderFillResolution { get; set; }
        public int Slippage { get; set; }
        public StartBehavior StartBehavior { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public bool TraceOrders { get; set; }
        public RealtimeErrorHandling RealtimeErrorHandling { get; set; }
        public StopTargetHandling StopTargetHandling { get; set; }
        public int BarsRequiredToTrade { get; set; }
        public bool IsInstantiatedOnEachOptimizationIteration { get; set; }
        public bool IsOverlay { get; set; }

        // --- Order engine (set by harness) ---
        internal OrderEngine _orderEngine;

        // --- Order methods ---
        protected void EnterLong(int quantity, string signalName)
        {
            _orderEngine.SubmitEntry(signalName, MarketPosition.Long, quantity);
        }

        protected void EnterShort(int quantity, string signalName)
        {
            _orderEngine.SubmitEntry(signalName, MarketPosition.Short, quantity);
        }

        protected void ExitLong(string signalName, string fromEntry)
        {
            _orderEngine.SubmitMarketExit(fromEntry, MarketPosition.Long, signalName);
        }

        protected void ExitShort(string signalName, string fromEntry)
        {
            _orderEngine.SubmitMarketExit(fromEntry, MarketPosition.Short, signalName);
        }

        protected void SetStopLoss(string fromEntry, CalculationMode mode, double value, bool isSimulated)
        {
            _orderEngine.SetStop(fromEntry, value);
        }

        protected void SetProfitTarget(string fromEntry, CalculationMode mode, double value)
        {
            _orderEngine.SetTarget(fromEntry, value, mode);
        }

        protected void CancelOrder(Order order)
        {
            _orderEngine.CancelOrder(order);
        }

        // --- Drawing (no-op) ---
        protected void RemoveDrawObject(string tag) { }

        // --- Harness API ---
        public void Initialize()
        {
            State = State.SetDefaults;
            OnStateChange();
            State = State.Configure;
            OnStateChange();
            State = State.DataLoaded;
            OnStateChange();
        }

        public void TriggerOnBarUpdate()
        {
            OnBarUpdate();
        }

        public void TriggerOnExecutionUpdate(Execution exec, double price, int qty,
            MarketPosition mktPos, DateTime time)
        {
            OnExecutionUpdate(exec, Guid.NewGuid().ToString(), price, qty, mktPos,
                exec.Order?.Name ?? "", time);
        }
    }

    // --- Series classes for [0] indexing ---
    public class PriceSeries
    {
        private double _current;
        public double this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(double val) => _current = val;
    }

    public class VolumeSeries
    {
        private long _current;
        public long this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(long val) => _current = val;
    }

    public class TimeSeries
    {
        private DateTime _current;
        public DateTime this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(DateTime val) => _current = val;
    }
}
```

- [ ] **Step 2: Verify compilation**

Run: `cd csim && dotnet build 2>&1 | head -50`

Expected: Will fail because `OrderEngine` class doesn't exist yet. All NinjaTrader API references from MNQ10minV2.cs should resolve except OrderEngine.

- [ ] **Step 3: Commit**

```bash
git add csim/NinjaTrader/NinjaScript/Strategy.cs
git commit -m "feat(csim): Strategy base class with order method stubs"
```

---

### Task 3: Order Engine

**Files:**
- Create: `csim/OrderEngine.cs`

The order engine manages pending entries, stop losses, profit targets, and evaluates fills each tick.

- [ ] **Step 1: Create OrderEngine.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace CSimulator
{
    internal class EntryState
    {
        public string SignalName { get; set; }
        public MarketPosition Direction { get; set; }
        public int Quantity { get; set; }
        public bool Filled { get; set; }
        public double FillPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public bool TargetInTicks { get; set; }
        public double TargetTicks { get; set; }
    }

    internal class PendingEntry
    {
        public string SignalName { get; set; }
        public MarketPosition Direction { get; set; }
        public int Quantity { get; set; }
    }

    internal class PendingMarketExit
    {
        public string FromEntry { get; set; }
        public MarketPosition Direction { get; set; }
        public string ExitSignalName { get; set; }
    }

    internal class OrderEngine
    {
        private readonly Strategy _strategy;
        private readonly double _tickSize;
        private readonly int _slippage;

        private List<PendingEntry> _pendingEntries = new List<PendingEntry>();
        private List<PendingMarketExit> _pendingExits = new List<PendingMarketExit>();
        private Dictionary<string, EntryState> _activeEntries = new Dictionary<string, EntryState>();
        private Dictionary<string, double> _presetStops = new Dictionary<string, double>();
        private Dictionary<string, (double value, CalculationMode mode)> _presetTargets = new Dictionary<string, (double, CalculationMode)>();

        public OrderEngine(Strategy strategy, double tickSize, int slippage)
        {
            _strategy = strategy;
            _tickSize = tickSize;
            _slippage = slippage;
        }

        public void SubmitEntry(string signalName, MarketPosition direction, int quantity)
        {
            _pendingEntries.Add(new PendingEntry
            {
                SignalName = signalName,
                Direction = direction,
                Quantity = quantity
            });

            // Register order in Account.Orders
            var order = new Order
            {
                Name = signalName,
                OrderState = OrderState.Working,
                Quantity = quantity,
                Instrument = _strategy.Instrument
            };
            _strategy.Account.Orders.Add(order);
        }

        public void SubmitMarketExit(string fromEntry, MarketPosition direction, string exitSignalName)
        {
            _pendingExits.Add(new PendingMarketExit
            {
                FromEntry = fromEntry,
                Direction = direction,
                ExitSignalName = exitSignalName
            });
        }

        public void SetStop(string fromEntry, double price)
        {
            if (_activeEntries.ContainsKey(fromEntry))
                _activeEntries[fromEntry].StopPrice = price;
            else
                _presetStops[fromEntry] = price;
        }

        public void SetTarget(string fromEntry, double value, CalculationMode mode)
        {
            if (_activeEntries.ContainsKey(fromEntry))
            {
                var entry = _activeEntries[fromEntry];
                if (mode == CalculationMode.Ticks)
                {
                    entry.TargetInTicks = true;
                    entry.TargetTicks = value;
                    entry.TargetPrice = entry.Direction == MarketPosition.Long
                        ? entry.FillPrice + (value * _tickSize)
                        : entry.FillPrice - (value * _tickSize);
                }
                else
                {
                    entry.TargetPrice = value;
                }
            }
            else
            {
                _presetTargets[fromEntry] = (value, mode);
            }
        }

        public void CancelOrder(Order order)
        {
            order.OrderState = OrderState.Cancelled;
            _pendingEntries.RemoveAll(e => e.SignalName == order.Name);
            _strategy.Account.Orders.Remove(order);
        }

        /// <summary>
        /// Called AFTER OnBarUpdate each tick. Fills pending entries and checks stops/targets.
        /// </summary>
        public void ProcessTick(double price, DateTime time)
        {
            // 1. Fill pending market exits
            ProcessMarketExits(price, time);

            // 2. Fill pending entries (next-tick fill with slippage)
            ProcessPendingEntries(price, time);

            // 3. Evaluate stops and targets on active entries
            EvaluateStopsAndTargets(price, time);
        }

        /// <summary>
        /// Called BEFORE OnBarUpdate to fill entries submitted on previous tick.
        /// </summary>
        public void FillPendingEntries(double price, DateTime time)
        {
            if (_pendingEntries.Count == 0) return;

            var toFill = _pendingEntries.ToList();
            _pendingEntries.Clear();

            foreach (var pending in toFill)
            {
                double fillPrice = pending.Direction == MarketPosition.Long
                    ? price + (_slippage * _tickSize)
                    : price - (_slippage * _tickSize);

                var entry = new EntryState
                {
                    SignalName = pending.SignalName,
                    Direction = pending.Direction,
                    Quantity = pending.Quantity,
                    Filled = true,
                    FillPrice = fillPrice
                };

                // Apply preset stop/target
                if (_presetStops.ContainsKey(pending.SignalName))
                {
                    entry.StopPrice = _presetStops[pending.SignalName];
                    _presetStops.Remove(pending.SignalName);
                }
                if (_presetTargets.ContainsKey(pending.SignalName))
                {
                    var (value, mode) = _presetTargets[pending.SignalName];
                    if (mode == CalculationMode.Ticks)
                    {
                        entry.TargetInTicks = true;
                        entry.TargetTicks = value;
                        entry.TargetPrice = pending.Direction == MarketPosition.Long
                            ? fillPrice + (value * _tickSize)
                            : fillPrice - (value * _tickSize);
                    }
                    else
                    {
                        entry.TargetPrice = value;
                    }
                    _presetTargets.Remove(pending.SignalName);
                }

                _activeEntries[pending.SignalName] = entry;
                UpdatePosition();

                // Remove from Account.Orders
                var acctOrder = _strategy.Account.Orders.FirstOrDefault(o => o.Name == pending.SignalName);
                if (acctOrder != null)
                {
                    acctOrder.OrderState = OrderState.Filled;
                    _strategy.Account.Orders.Remove(acctOrder);
                }

                // Fire OnExecutionUpdate for entry
                var exec = new Execution
                {
                    Order = new Order { Name = pending.SignalName, OrderState = OrderState.Filled },
                    Price = fillPrice,
                    Quantity = pending.Quantity,
                    MarketPosition = _strategy.Position.MarketPosition
                };
                _strategy.TriggerOnExecutionUpdate(exec, fillPrice, pending.Quantity,
                    _strategy.Position.MarketPosition, time);
            }
        }

        private void ProcessPendingEntries(double price, DateTime time)
        {
            // Entries are filled on the NEXT tick via FillPendingEntries()
            // This method is intentionally empty — fill happens at start of next tick cycle
        }

        private void ProcessMarketExits(double price, DateTime time)
        {
            if (_pendingExits.Count == 0) return;

            var exits = _pendingExits.ToList();
            _pendingExits.Clear();

            foreach (var exit in exits)
            {
                if (!_activeEntries.ContainsKey(exit.FromEntry)) continue;

                var entry = _activeEntries[exit.FromEntry];
                _activeEntries.Remove(exit.FromEntry);
                UpdatePosition();

                var exec = new Execution
                {
                    Order = new Order { Name = exit.ExitSignalName, OrderState = OrderState.Filled },
                    Price = price,
                    Quantity = entry.Quantity,
                    MarketPosition = _strategy.Position.MarketPosition
                };
                _strategy.TriggerOnExecutionUpdate(exec, price, entry.Quantity,
                    _strategy.Position.MarketPosition, time);
            }
        }

        private void EvaluateStopsAndTargets(double price, DateTime time)
        {
            var toRemove = new List<string>();

            foreach (var kvp in _activeEntries)
            {
                var entry = kvp.Value;
                if (!entry.Filled) continue;

                bool stopped = false;
                bool targetHit = false;
                double fillPrice = 0;

                if (entry.Direction == MarketPosition.Long)
                {
                    if (entry.StopPrice > 0 && price <= entry.StopPrice)
                    {
                        stopped = true;
                        fillPrice = entry.StopPrice;
                    }
                    else if (entry.TargetPrice > 0 && price >= entry.TargetPrice)
                    {
                        targetHit = true;
                        fillPrice = entry.TargetPrice;
                    }
                }
                else // Short
                {
                    if (entry.StopPrice > 0 && price >= entry.StopPrice)
                    {
                        stopped = true;
                        fillPrice = entry.StopPrice;
                    }
                    else if (entry.TargetPrice > 0 && price <= entry.TargetPrice)
                    {
                        targetHit = true;
                        fillPrice = entry.TargetPrice;
                    }
                }

                if (stopped || targetHit)
                {
                    toRemove.Add(kvp.Key);

                    // Determine resulting position after this fill
                    // We need to compute it after removal
                }
            }

            foreach (var key in toRemove)
            {
                var entry = _activeEntries[key];
                _activeEntries.Remove(key);
                UpdatePosition();

                string orderName = (entry.StopPrice > 0 &&
                    ((entry.Direction == MarketPosition.Long && _lastEvalPrice <= entry.StopPrice) ||
                     (entry.Direction == MarketPosition.Short && _lastEvalPrice >= entry.StopPrice)))
                    ? "Stop loss" : "Profit target";

                // Determine fill price
                double exitPrice = orderName == "Stop loss" ? entry.StopPrice : entry.TargetPrice;

                var exec = new Execution
                {
                    Order = new Order { Name = orderName, OrderState = OrderState.Filled },
                    Price = exitPrice,
                    Quantity = entry.Quantity,
                    MarketPosition = _strategy.Position.MarketPosition
                };
                _strategy.TriggerOnExecutionUpdate(exec, exitPrice, entry.Quantity,
                    _strategy.Position.MarketPosition, time);
            }

            _lastEvalPrice = price;
        }

        private double _lastEvalPrice;

        private void UpdatePosition()
        {
            int longQty = _activeEntries.Values
                .Where(e => e.Filled && e.Direction == MarketPosition.Long)
                .Sum(e => e.Quantity);
            int shortQty = _activeEntries.Values
                .Where(e => e.Filled && e.Direction == MarketPosition.Short)
                .Sum(e => e.Quantity);

            if (longQty > shortQty)
            {
                _strategy.Position.MarketPosition = MarketPosition.Long;
                _strategy.Position.Quantity = longQty - shortQty;
            }
            else if (shortQty > longQty)
            {
                _strategy.Position.MarketPosition = MarketPosition.Short;
                _strategy.Position.Quantity = shortQty - longQty;
            }
            else
            {
                _strategy.Position.MarketPosition = MarketPosition.Flat;
                _strategy.Position.Quantity = 0;
            }
        }

        public void Reset()
        {
            _pendingEntries.Clear();
            _pendingExits.Clear();
            _activeEntries.Clear();
            _presetStops.Clear();
            _presetTargets.Clear();
            _strategy.Position.MarketPosition = MarketPosition.Flat;
            _strategy.Position.Quantity = 0;
            _strategy.Account.Orders.Clear();
        }

        public bool HasPendingEntries => _pendingEntries.Count > 0;
        public bool HasActivePositions => _activeEntries.Count > 0;
    }
}
```

- [ ] **Step 2: Verify compilation**

Run: `cd csim && dotnet build 2>&1`

Expected: Should compile successfully (or nearly — fix any remaining issues).

- [ ] **Step 3: Commit**

```bash
git add csim/OrderEngine.cs
git commit -m "feat(csim): order engine with fill/stop/target simulation"
```

---

### Task 4: Program.cs — Tick Reader and Main Loop

**Files:**
- Create: `csim/Program.cs`

- [ ] **Step 1: Create Program.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript.Strategies;

namespace CSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ParseArgs(args);
            if (config == null) return;

            var strategy = new MNQ10minV2();
            ApplyConfig(strategy, config);

            var engine = new OrderEngine(strategy, 0.25, 1);
            strategy._orderEngine = engine;
            strategy.Initialize();

            RunSimulation(strategy, engine, config);
        }

        static Config ParseArgs(string[] args)
        {
            var config = new Config();
            int i = 0;
            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "--colchon": config.ColchonStop = int.Parse(args[++i]); break;
                    case "--colchon-be": config.ColchonBreakeven = int.Parse(args[++i]); break;
                    case "--breakeven-pct": config.BreakevenPct = int.Parse(args[++i]); break;
                    case "--trades": config.MaxTrades = int.Parse(args[++i]); break;
                    case "--perdida-max": config.PerdidaMaxDiaria = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--modo":
                        config.ModoTP = args[++i] == "1a1" ? "Solo1a1" : "Con1a2";
                        break;
                    case "--cierre": config.HoraCierre = args[++i]; break;
                    case "--output-dir": config.OutputDir = args[++i]; break;
                    case "--no-telemetry": config.NoTelemetry = true; break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return null;
                    default:
                        if (!args[i].StartsWith("--"))
                            config.TickFile = args[i];
                        break;
                }
                i++;
            }

            if (string.IsNullOrEmpty(config.TickFile))
            {
                Console.Error.WriteLine("Error: tick file path required");
                PrintUsage();
                return null;
            }

            if (!File.Exists(config.TickFile))
            {
                Console.Error.WriteLine($"Error: file not found: {config.TickFile}");
                return null;
            }

            return config;
        }

        static void ApplyConfig(MNQ10minV2 strategy, Config config)
        {
            strategy.ColchonStop = config.ColchonStop;
            strategy.ColchonBreakeven = config.ColchonBreakeven;
            strategy.BreakevenPct = config.BreakevenPct;
            strategy.MaxTrades = config.MaxTrades;
            strategy.PerdidaMaxDiaria = config.PerdidaMaxDiaria;
            strategy.HoraCierre = config.HoraCierre;

            // Set ModoTP enum
            var tpField = typeof(MNQ10minV2).GetProperty("ModoTP");
            var tpEnumType = tpField.PropertyType;
            tpField.SetValue(strategy, Enum.Parse(tpEnumType, config.ModoTP));

            // Set ModoOperacion to Visualizar (uses virtual position tracking)
            var opField = typeof(MNQ10minV2).GetProperty("ModoOperacion");
            var opEnumType = opField.PropertyType;
            opField.SetValue(strategy, Enum.Parse(opEnumType, "Operar"));

            // Disable telemetry by redirecting path
            if (config.NoTelemetry)
            {
                var telField = typeof(MNQ10minV2).GetField("telemetryPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (telField != null) telField.SetValue(strategy, Path.Combine(Path.GetTempPath(), "mnq_noop.json"));
            }

            // Override bot history dir if output-dir specified
            if (!string.IsNullOrEmpty(config.OutputDir))
            {
                var dirField = typeof(MNQ10minV2).GetField("botHistoryDir",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dirField != null)
                {
                    Directory.CreateDirectory(config.OutputDir);
                    dirField.SetValue(strategy, config.OutputDir);
                }
            }
        }

        static void RunSimulation(Strategy strategy, OrderEngine engine, Config config)
        {
            Console.WriteLine("=== PARAMETROS ===");
            Console.WriteLine($"  ColchonStop: {config.ColchonStop} | ColchonBE: {config.ColchonBreakeven} | BreakevenPct: {config.BreakevenPct}%");
            Console.WriteLine($"  Max Trades/Dia: {config.MaxTrades} | PerdidaMaxDiaria: ${config.PerdidaMaxDiaria:F2}");
            Console.WriteLine($"  Modo TP: {config.ModoTP}");
            Console.WriteLine($"  Hora Cierre: {config.HoraCierre} ET");
            Console.WriteLine($"  Archivo: {config.TickFile}");
            Console.WriteLine();

            long tickCount = 0;
            DateTime currentBarTime = DateTime.MinValue;
            int barMinute = -1;

            using var reader = new StreamReader(config.TickFile);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Parse: 20260401 050000 1480000;24012.75;24012.75;24013.25;1
                var parts = line.Split(';');
                if (parts.Length < 5) continue;

                string timeStr = parts[0].Trim();
                if (!double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lastPrice)) continue;
                if (!long.TryParse(parts[4], out long volume)) volume = 1;

                // Parse timestamp: "20260401 050000 1480000"
                // Format: yyyyMMdd HHmmss fffffff
                DateTime tickTime = ParseTickTime(timeStr);
                if (tickTime == DateTime.MinValue) continue;

                tickCount++;

                // Fill pending entries from previous tick
                engine.FillPendingEntries(lastPrice, tickTime);

                // Determine if new bar (1-minute bars)
                int tickMinute = tickTime.Hour * 60 + tickTime.Minute;
                bool newBar = tickMinute != barMinute;

                if (newBar)
                {
                    barMinute = tickMinute;
                    strategy.CurrentBar++;
                    strategy.IsFirstTickOfBar = true;
                    strategy.Open[0] = lastPrice;
                    strategy.High[0] = lastPrice;
                    strategy.Low[0] = lastPrice;
                }
                else
                {
                    strategy.IsFirstTickOfBar = false;
                    if (lastPrice > strategy.High[0]) strategy.High[0] = lastPrice;
                    if (lastPrice < strategy.Low[0]) strategy.Low[0] = lastPrice;
                }

                strategy.Close[0] = lastPrice;
                strategy.Volume[0] = volume;
                strategy.Time[0] = tickTime;

                // Call OnBarUpdate
                strategy.TriggerOnBarUpdate();

                // Evaluate stops/targets after strategy logic
                engine.ProcessTick(lastPrice, tickTime);
            }

            Console.WriteLine($"\nTicks procesados: {tickCount:N0}");
            PrintSummary(config);
        }

        static DateTime ParseTickTime(string timeStr)
        {
            // "20260401 050000 1480000" or "20260401 050000 148"
            try
            {
                var spaceParts = timeStr.Split(' ');
                if (spaceParts.Length < 2) return DateTime.MinValue;

                string datePart = spaceParts[0]; // 20260401
                string timePart = spaceParts[1]; // 050000

                int year = int.Parse(datePart.Substring(0, 4));
                int month = int.Parse(datePart.Substring(4, 2));
                int day = int.Parse(datePart.Substring(6, 2));
                int hour = int.Parse(timePart.Substring(0, 2));
                int min = int.Parse(timePart.Substring(2, 2));
                int sec = int.Parse(timePart.Substring(4, 2));

                return new DateTime(year, month, day, hour, min, sec);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        static void PrintSummary(Config config)
        {
            // Read the daily log CSV and print summary table
            string dailyPath = string.IsNullOrEmpty(config.OutputDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"perficient\AI path lean\trading 7\bot\history", "mnq10minv2_daily_log.csv")
                : Path.Combine(config.OutputDir, "mnq10minv2_daily_log.csv");

            if (!File.Exists(dailyPath))
            {
                Console.WriteLine("  (no daily log found for summary)");
                return;
            }

            var lines = File.ReadAllLines(dailyPath);
            if (lines.Length <= 1) return;

            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("RESUMEN");
            Console.WriteLine(new string('=', 80));

            string currentMonth = "";
            double monthPnL = 0;
            int monthTrades = 0;
            double monthAccum = 0;
            int totalDays = 0;
            int totalTrades = 0;
            double totalPnL = 0;

            for (int idx = 1; idx < lines.Length; idx++)
            {
                var cols = lines[idx].Split(',');
                if (cols.Length < 4) continue;

                string dateStr = cols[0];
                if (!DateTime.TryParse(dateStr, out DateTime date)) continue;

                // Parse with comma-as-decimal (Spanish locale in CSV)
                string pnlStr = cols[1] + (cols.Length > 2 && !cols[2].StartsWith("2") ? "." + cols[2] : "");
                // The CSV uses comma decimal: "106,50" -> need to handle
                double dailyPnl = ParseCsvDouble(cols, 1);
                int trades = ParseCsvInt(cols, 3);

                if (trades == 0 && dailyPnl == 0) continue;

                string monthKey = date.ToString("MMMM yyyy", new CultureInfo("es-ES"));
                if (monthKey != currentMonth)
                {
                    if (currentMonth != "")
                        PrintMonthFooter(monthPnL, monthTrades);

                    currentMonth = monthKey;
                    monthPnL = 0;
                    monthTrades = 0;
                    monthAccum = 0;
                    Console.WriteLine($"\n{char.ToUpper(monthKey[0])}{monthKey.Substring(1)}\n");
                    Console.WriteLine("  dia   PnL          #trades   acumulado");
                }

                monthPnL += dailyPnl;
                monthTrades += trades;
                monthAccum += dailyPnl;
                totalDays++;
                totalTrades += trades;
                totalPnL += dailyPnl;

                Console.WriteLine($"  {date.Day,-5} ${dailyPnl,-12:F2} {trades,-9} ${monthAccum:F2}");
            }

            if (currentMonth != "")
                PrintMonthFooter(monthPnL, monthTrades);

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine($"  Dias operados: {totalDays}");
            Console.WriteLine($"  Total Trades: {totalTrades}");
            Console.WriteLine($"  PnL Total: ${totalPnL:F2}");
            Console.WriteLine($"  Promedio diario: ${(totalDays > 0 ? totalPnL / totalDays : 0):F2}");
        }

        static void PrintMonthFooter(double pnl, int trades)
        {
            Console.WriteLine("  ---   ---          ---       ---");
            Console.WriteLine($"  MES   ${pnl:F2}  {trades}       ");
        }

        static double ParseCsvDouble(string[] cols, int startIdx)
        {
            // Handle Spanish decimal comma: "106,50" is split into cols[1]="106" cols[2]="50"
            // But also handle normal format
            string val = cols[startIdx];
            if (startIdx + 1 < cols.Length)
            {
                string next = cols[startIdx + 1];
                if (next.Length <= 2 && int.TryParse(next, out _))
                    val = cols[startIdx] + "." + next;
            }
            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;
            return 0;
        }

        static int ParseCsvInt(string[] cols, int idx)
        {
            // Account for shifted indices due to decimal commas
            // In the CSV: Date,DailyPnL(may be 2 cols),TotalPnL(may be 2 cols),Trades,...
            // Trades is at logical index 3, but may be at actual index 5 if both PnL fields use commas
            // We need to count from the end or use a smarter parser
            // For now, search for the first pure integer after the PnL fields
            for (int i = idx; i < Math.Min(idx + 4, cols.Length); i++)
            {
                if (int.TryParse(cols[i], out int result) && result >= 0 && result <= 20)
                    return result;
            }
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run --project csim -- [options] <tick_file>");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  --colchon <int>       ColchonStop (default: 5)");
            Console.WriteLine("  --colchon-be <int>    ColchonBreakeven (default: 5)");
            Console.WriteLine("  --breakeven-pct <int> BreakevenPct (default: 60)");
            Console.WriteLine("  --trades <int>        MaxTrades (default: 2)");
            Console.WriteLine("  --perdida-max <float> PerdidaMaxDiaria (default: 400)");
            Console.WriteLine("  --modo <1a1|1a2>      ModoTP (default: 1a2)");
            Console.WriteLine("  --cierre <HH:mm>      HoraCierre (default: 15:50)");
            Console.WriteLine("  --output-dir <path>   Output directory for CSV logs");
            Console.WriteLine("  --no-telemetry        Disable JSON telemetry");
        }
    }

    class Config
    {
        public int ColchonStop { get; set; } = 5;
        public int ColchonBreakeven { get; set; } = 5;
        public int BreakevenPct { get; set; } = 60;
        public int MaxTrades { get; set; } = 2;
        public double PerdidaMaxDiaria { get; set; } = 400;
        public string ModoTP { get; set; } = "Con1a2";
        public string HoraCierre { get; set; } = "15:50";
        public string TickFile { get; set; }
        public string OutputDir { get; set; }
        public bool NoTelemetry { get; set; }
    }
}
```

- [ ] **Step 2: Build and verify compilation**

Run: `cd csim && dotnet build`

Expected: Successful compilation. Fix any remaining type mismatches.

- [ ] **Step 3: Commit**

```bash
git add csim/Program.cs
git commit -m "feat(csim): main loop with tick reader, CLI args, and summary output"
```

---

### Task 5: Compilation Fixes and First Run

**Files:**
- Modify: any files with compilation errors

- [ ] **Step 1: Build and collect all errors**

Run: `cd csim && dotnet build 2>&1`

Fix ALL compilation errors. Common expected issues:
- Missing `using` statements
- MNQ10minV2 references to `TimeZoneInfo` (works natively)
- File I/O paths (works natively)
- `Directory.CreateDirectory` (works natively)

- [ ] **Step 2: Fix all errors iteratively until build succeeds**

Keep running `dotnet build` and fixing until output shows:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 3: First test run**

Run: `cd csim && dotnet run -- --colchon 5 --colchon-be 5 --breakeven-pct 60 --trades 3 --perdida-max 500 --modo 1a2 --output-dir ../sim_output_csim --no-telemetry "../historicos test/MNQ 06-26-MNQJUN26-abril-mayo.Last.txt" 2>&1 | head -60`

Expected: Should start processing ticks and produce output. May crash on runtime issues — fix them.

- [ ] **Step 4: Commit working state**

```bash
git add csim/
git commit -m "feat(csim): first working compilation and run"
```

---

### Task 6: Validation Against NinjaTrader Grid

**Files:**
- Possibly modify: `csim/OrderEngine.cs`, `csim/NinjaTrader/NinjaScript/Strategy.cs`

- [ ] **Step 1: Run on abril-mayo data and compare**

Run the simulator:
```bash
cd csim && dotnet run -- --colchon 5 --colchon-be 5 --breakeven-pct 60 --trades 3 --perdida-max 500 --modo 1a2 --output-dir ../sim_output_csim "../historicos test/MNQ 06-26-MNQJUN26-abril-mayo.Last.txt"
```

Compare output to NinjaTrader Grid (`resultados operaciones/NinjaTrader Grid mayo 2026-05-16 04-33 p. m..csv`):
- Entry prices should match within 0.25 (slippage)
- Exit reasons should match
- PnL per trade should match

- [ ] **Step 2: Fix any discrepancies**

Common issues to fix:
- Order of operations (fill before or after OnBarUpdate)
- Stop evaluation (should check against Close[0], not High/Low)
- CierreForzado timing

- [ ] **Step 3: Run with Solo1a1 params and compare to Python simulator**

```bash
cd csim && dotnet run -- --colchon 5 --colchon-be 8 --breakeven-pct 35 --trades 3 --perdida-max 500 --modo 1a1 --output-dir ../sim_output_csim_1a1 "../historicos test/MNQ 06-26-MNQJUN26-abril-mayo.Last.txt"
```

- [ ] **Step 4: Commit validated version**

```bash
git add csim/
git commit -m "feat(csim): validated against NinjaTrader Grid results"
```

---

### Task 7: Summary Output Polish

**Files:**
- Modify: `csim/Program.cs`

- [ ] **Step 1: Fix CSV parsing for Spanish decimal format**

The MNQ10minV2.cs writes CSV with the system locale. On Windows with Spanish locale, decimals use comma. The summary parser needs to handle this correctly. Test by reading the generated daily_log.csv and verifying the summary table matches.

- [ ] **Step 2: Add tick count and timing to output**

Add elapsed time measurement so user sees how fast the simulation runs.

- [ ] **Step 3: Commit**

```bash
git add csim/Program.cs
git commit -m "fix(csim): robust CSV parsing and timing output"
```
