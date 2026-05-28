# OrderEngine Fill Fidelity — Design Spec

## Problem

The csim OrderEngine produces different fill prices than NinjaTrader for the same tick data and strategy logic. Over multiple trades these small differences ($0.25-$1.50 per trade) cascade into significantly different daily P&L, which changes position sizing calculations, which further diverges results.

## Root Causes

### 1. Trailing stops not rounded to tick size
MNQ10minV2 calculates trailing stop prices like `entryPrice + (stopDistance * pct)`, producing values like 24084.0125. NinjaTrader automatically rounds all stop/target prices to tick boundaries (0.25 for MNQ). The csim OrderEngine stores and evaluates these unrounded prices.

### 2. Stop/target evaluation uses Last instead of Bid/Ask
NinjaTrader evaluates stops against Bid (for longs) and Ask (for shorts). The csim uses the Last price for both evaluation and fill. This causes stops to trigger on different ticks and fill at different prices.

### 3. Stop fill price uses Last instead of Bid/Ask
When a long stop triggers, NinjaTrader fills at the Bid (sell to exit). When a short stop triggers, it fills at the Ask (buy to cover). The csim fills all stops at the Last price.

### 4. Target evaluation uses Last instead of Bid/Ask
Long targets should trigger when Bid >= targetPrice. Short targets should trigger when Ask <= targetPrice. The csim uses Last for both.

## Design

### Change 1: Round stops to tick size in SetStop()

```csharp
public void SetStop(string fromEntry, double price)
{
    price = Math.Round(price / _tickSize) * _tickSize;
    // ... rest unchanged
}
```

### Change 2: EvaluateStopsAndTargets uses Bid/Ask

```csharp
public void EvaluateStopsAndTargets(double price, DateTime time)
{
    double bid = _strategy.CurrentBid;
    double ask = _strategy.CurrentAsk;

    foreach (var entry in snapshot)
    {
        if (entry.Direction == MarketPosition.Long)
        {
            // Long: stop triggers on Bid <= stopPrice, target on Bid >= targetPrice
            if (entry.StopPrice > 0 && bid <= entry.StopPrice)
                stopHit = true;
            else if (entry.TargetPrice > 0 && bid >= entry.TargetPrice)
                targetHit = true;
        }
        else
        {
            // Short: stop triggers on Ask >= stopPrice, target on Ask <= targetPrice
            if (entry.StopPrice > 0 && ask >= entry.StopPrice)
                stopHit = true;
            else if (entry.TargetPrice > 0 && ask <= entry.TargetPrice)
                targetHit = true;
        }
    }
}
```

### Change 3: Stop fill price uses Bid/Ask

```csharp
// Stop fill: sell at Bid (long exit), buy at Ask (short exit)
// Target fill: exact target price (limit order, no change)
double fillPrice;
if (targetHit)
    fillPrice = entry.TargetPrice;
else
    fillPrice = entry.Direction == MarketPosition.Long ? bid : ask;
```

### Change 4: Target fill price stays at exact target (no change needed)
Limit orders fill at the limit price. Already correct.

## Expected Impact

- Trade #2 (trailing stop): csim currently fills at 24084.01, should fill at 24084.00 (rounded) or lower (Bid)
- Trade #3 (stop loss): csim fills at 24071.25, NT fills at 24070.75 — difference likely from Bid vs Last
- Overall: fills should be within $0-$0.50 of NinjaTrader instead of current $0.50-$2.00

## Out of Scope

- Flip entry fill behavior (already uses same-tick price)
- Market exit fills (already use Bid/Ask correctly)
- The inherent difference from Playback vs simulation timing (NinjaTrader processes bars slightly differently at speed)
