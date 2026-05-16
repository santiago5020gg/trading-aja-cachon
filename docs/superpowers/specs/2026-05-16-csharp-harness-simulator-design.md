# C# Harness Simulator for MNQ10minV2

## Goal

Execute `MNQ10minV2.cs` directly against NinjaTrader tick export files without modifying the strategy file. The harness mocks the NinjaTrader framework and feeds ticks through the real C# code, producing results identical to NinjaTrader Playback.

## Why

The existing Python tick simulator is a manual port that diverges from the C# in subtle ways (entry prices, trailing activation timing, CierreForzado logging). Running the actual C# eliminates all translation errors.

## Architecture

```
csim/
├── Program.cs                 — Entry point, CLI args, tick reader, simulation loop
├── NinjaTrader/
│   ├── Strategy.cs            — Base class with virtual OnBarUpdate/OnExecutionUpdate
│   ├── Enums.cs               — MarketPosition, OrderState, Calculate, State, etc.
│   ├── Order.cs               — Order class (Name, OrderState, Price, Quantity)
│   ├── Execution.cs           — Execution class wrapping Order + fill details
│   ├── Position.cs            — Position tracking (MarketPosition property)
│   ├── Account.cs             — Account.Orders collection
│   ├── Draw.cs                — No-op stubs for all Draw.* methods
│   ├── Instrument.cs          — Instrument stub
│   ├── Attributes.cs          — NinjaScriptProperty, Display, Range (no-op)
│   └── Brushes.cs             — Brush/color stubs
├── MNQ10minV2.cs              — Symlink or copy of the real strategy (unmodified)
└── csim.csproj                — .NET 8 console project
```

## Strategy Base Class Design

The `Strategy` base class provides:

- `protected virtual void OnBarUpdate()` — overridden by MNQ10minV2
- `protected virtual void OnExecutionUpdate(...)` — overridden by MNQ10minV2
- `protected virtual void OnStateChange()` — overridden by MNQ10minV2
- Properties: `Close[0]`, `High[0]`, `Low[0]`, `Open[0]`, `Volume[0]`, `Time[0]`
- Properties: `CurrentBar`, `IsFirstTickOfBar`, `TickSize`, `Position`, `Account`, `Instrument`
- Order methods: `EnterLong()`, `EnterShort()`, `ExitLong()`, `ExitShort()`, `SetStopLoss()`, `SetProfitTarget()`, `CancelOrder()`
- State management: `State` property cycling through SetDefaults → Configure → DataLoaded

The harness calls a public `Initialize()` method that triggers `OnStateChange()` for each state, then a public `ProcessTick()` method that updates bar data and calls `OnBarUpdate()`.

## Tick Processing (OnEachTick with 1-minute bars)

Replicating NinjaTrader's behavior with `Calculate.OnEachTick`:

1. Read tick: `timestamp;last;bid;ask;volume`
2. If tick's minute differs from current bar's minute → start new bar:
   - Finalize previous bar (High/Low/Close locked)
   - New bar: Open=High=Low=Close=lastPrice, IsFirstTickOfBar=true
   - Increment CurrentBar
3. Otherwise update current bar:
   - Close = lastPrice
   - High = max(High, lastPrice)
   - Low = min(Low, lastPrice)
   - IsFirstTickOfBar = false
4. Call `OnBarUpdate()`
5. After OnBarUpdate: process pending order fills (see Order Engine)

## Order Engine

### Fill Model

- **Market entries** (EnterLong/EnterShort): order queued as pending. Filled on NEXT tick at `nextTickPrice ± slippage(0.25)`. Triggers OnExecutionUpdate with entry fill.
- **Stop loss**: each tick, check if price crossed stop level. If long: `Close[0] <= stopPrice` → fill at stopPrice. If short: `Close[0] >= stopPrice` → fill at stopPrice.
- **Profit target**: each tick, check if price crossed target. If long: `Close[0] >= targetPrice` → fill at targetPrice. If short: `Close[0] <= targetPrice` → fill at targetPrice.
- **Market exits** (ExitLong/ExitShort for CierreForzado): fill immediately at Close[0]. Triggers OnExecutionUpdate.

### StopTargetHandling.PerEntryExecution

Each entry signal (TP1Long, TP2Long, TP1Short, TP2Short) has its own independent stop and target levels. When `SetStopLoss("TP1Long", ...)` is called, it only affects that specific entry's stop.

### Order State

- `EnterLong(qty, name)` → creates Order with state=Working
- Fill → order transitions to Filled, OnExecutionUpdate fires
- `CancelOrder(order)` → order removed from active list

## Position Tracking

- `Position.MarketPosition` reflects aggregate: Flat, Long, or Short
- When all contracts for all entries are closed → MarketPosition = Flat
- The strategy checks `Position.MarketPosition == MarketPosition.Flat` to detect trade end

## What Gets Mocked (no-op)

- All `Draw.*` methods — no chart rendering
- `RemoveDrawObject()` — no-op
- `TimeZoneInfo` — works natively in .NET

## What Stays Functional

- CSV logging (File.AppendAllText) — writes real trade/daily/bar logs
- Telemetry JSON — optional, can disable via flag
- All trading logic, state machine, trailing, risk engine — runs as-is

## CLI Interface

```bash
dotnet run --project csim -- [options] <tick_file>

Options:
  --colchon <int>         ColchonStop (default: 5)
  --colchon-be <int>      ColchonBreakeven (default: 5)
  --breakeven-pct <int>   BreakevenPct (default: 60)
  --trades <int>          MaxTrades (default: 2)
  --perdida-max <float>   PerdidaMaxDiaria (default: 400)
  --modo <1a1|1a2>        ModoTP (default: 1a2)
  --cierre <HH:mm>        HoraCierre (default: 15:50)
  --output-dir <path>     Override CSV output directory
  --no-telemetry          Disable JSON telemetry writes
```

## Output

Same CSV files as the real bot:
- `mnq10minv2_trades_log.csv`
- `mnq10minv2_daily_log.csv`
- `mnq10minv2_bar_log.csv`

Plus a summary table printed to stdout (same format as Python simulator).

## Key Constraint

**MNQ10minV2.cs must compile without any modifications.** The NinjaTrader stubs must provide every class, enum, method, and property that the strategy references. If something is missing, it gets added to the stubs — never to the strategy.

## Validation

Run both simulators (Python and C# harness) on the same tick file. The C# harness should produce results matching the NinjaTrader Playback Grid export (the ground truth), not the Python simulator.
