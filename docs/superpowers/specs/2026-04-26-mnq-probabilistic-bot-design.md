# MNQ Probabilistic Bot — Design Specification

## Overview

Automated NinjaScript strategy for NinjaTrader 8.1.6.3 (64-bit Windows) that trades MNQ (Micro E-mini Nasdaq) on a 2-minute chart using a probabilistic entry scoring system and dynamic position sizing.

**Architecture:** Single monolithic `.cs` file (~1200-1500 lines) with `#region` organization.

**Goal:** Win 3-4 days per week (ideal 5), $270+ per winning day, max $270 daily loss.

---

## 1. Trading Session

| Parameter | Value |
|---|---|
| Instrument | MNQ (Micro E-mini Nasdaq) |
| Timeframe | 2-minute bars |
| Start time | 09:40 NY (10 min after open) |
| Last new trade | 15:25 NY |
| Flatten all | 15:40 NY |
| Time zone | Eastern (New York) |

---

## 2. Indicators Used

| Indicator | Period | Purpose |
|---|---|---|
| SMA | 20 | Fast trend, pullback target |
| SMA | 200 | Slow trend, regime detection |
| ATR | 14 | Volatility, stop loss, normalization |
| RSI | 7 | Mean reversion probability |
| StdDev | 20 | Z-score calculation |
| Volume SMA | 20 | Volume confirmation baseline |

---

## 3. Regime Detection

The bot classifies the market into 5 states:

### State 1 — BULLISH (Trend)
```
SMA_20 > SMA_200
AND |SMA_20 - SMA_200| / ATR_14 >= 1.0
AND slope_SMA_20 > 0
Action: Seek LONG entries on pullback to SMA 20
```

### State 2 — BEARISH (Trend)
```
SMA_20 < SMA_200
AND |SMA_20 - SMA_200| / ATR_14 >= 1.0
AND slope_SMA_20 < 0
Action: Seek SHORT entries on pullback to SMA 20
```

### State 3 — COUNTER-TREND BULLISH
```
SMA_20 < SMA_200 (bearish market)
AND |Price - SMA_200| / ATR_14 < 1.5
AND RSI_7 < 30
AND Z_score vs SMA_200 < -2.0
Action: LONG with restrictions (score >= 75, size 25%)
```

### State 4 — COUNTER-TREND BEARISH
```
SMA_20 > SMA_200 (bullish market)
AND |Price - SMA_200| / ATR_14 < 1.5
AND RSI_7 > 70
AND Z_score vs SMA_200 > 2.0
Action: SHORT with restrictions (score >= 75, size 25%)
```

### State 5 — RANGE (No Trade)
```
|SMA_20 - SMA_200| / ATR_14 < 1.0
AND |slope_SMA_20| < 0.05
AND |slope_SMA_200| < 0.02
Action: No operations, panel shows "RANGE — NO TRADES"
```

---

## 4. Probabilistic Entry Score (0-100)

### Composite Formula

```
S_raw = 0.20*F_zscore + 0.15*F_rsi + 0.20*F_slope + 0.10*F_atr
      + 0.10*F_volume + 0.15*F_pullback + 0.10*F_candle

Score = 100 / (1 + e^(-12 * (S_raw - 0.5)))
```

### Factor Definitions

**F_zscore — Proximity to SMA 20:**
```
Z = (Close - SMA_20) / StdDev_20
F_zscore = e^(-Z^2 / 2)
```

**F_rsi — RSI(7) mean reversion:**
```
LONGS:  F_rsi = 1 / (1 + e^(0.15 * (RSI - 40)))
SHORTS: F_rsi = 1 / (1 + e^(-0.15 * (RSI - 60)))
```

**F_slope — SMA 20 slope (momentum):**
```
slope = (SMA_20[0] - SMA_20[5]) / (5 * ATR_14)
LONGS:  F_slope = 1 / (1 + e^(-10 * slope))
SHORTS: F_slope = 1 / (1 + e^(10 * slope))
```

**F_atr — Volatility regime:**
```
VolRatio = AvgRange_last_10_bars / ATR_14

F_atr = 0.70                                          if VolRatio < 0.50
F_atr = 0.70 + 0.30 * (VolRatio - 0.50) / 0.20       if 0.50 <= VolRatio < 0.70
F_atr = 1.00                                          if 0.70 <= VolRatio <= 1.30
F_atr = 1.00 - 0.50 * (VolRatio - 1.30) / 0.70       if 1.30 < VolRatio <= 2.00
F_atr = 0.50                                          if VolRatio > 2.00
```

**F_volume — Volume confirmation:**
```
vol_ratio = Volume[0] / SMA_Volume_20
F_volume = 1 / (1 + e^(-5 * (vol_ratio - 1.2)))
```

**F_pullback — Optimal pullback distance:**
```
D = (Close - SMA_20) / ATR_14
LONGS:  F_pullback = e^(-0.5 * ((D + 0.5) / 0.7)^2)
SHORTS: F_pullback = e^(-0.5 * ((D - 0.5) / 0.7)^2)
```

**F_candle — Candlestick confirmation:**
```
body = |Close - Open|
range = High - Low
lower_wick = Min(Open,Close) - Low
upper_wick = High - Max(Open,Close)

LONGS:
  Bullish engulfing                                    -> 1.00
  Hammer (lower_wick > 2*body, upper_wick < 0.3*body) -> 0.85
  Strong bar (body > 0.6*range, close > open)          -> 0.60
  Any green bar                                        -> 0.30
  Red bar                                              -> 0.00

SHORTS: mirror inverse
```

### Decision Thresholds

| Score | Action | Size |
|---|---|---|
| >= 75 | STRONG ENTRY | 100% of calculated position size |
| 65-74 | MODERATE ENTRY | 75% of calculated position size |
| < 65 | NO TRADE | — |
| Counter-trend minimum | 75 always | 25% max size |

---

## 5. Position Sizing Formula

### Master Equation

```
N_final = floor(N_base * C_spread * C_slope * C_vol * DailyBudget * C_time)
N_final = clamp(N_final, 1, N_absolute_max)
```

### Components

**N_base — Conservative of fixed-fractional and Half-Kelly:**
```
SL = 1.5 * ATR_14 (points)  [trend trades]
SL = 2.0 * ATR_14 (points)  [counter-trend trades]
PointValue = $2.00

N_fixed = (270 * 0.33) / (SL * 2.00)

f* = WinRate - (1 - WinRate) / RR
f_adj = f* * 0.50
N_kelly = (270 * f_adj) / (SL * 2.00)

N_base = min(N_fixed, N_kelly)
```

WinRate and RR start at 0.55 and 1.5, updated via rolling window of last 50 trades persisted in CSV file.

**C_spread — SMA alignment confidence:**
```
SMA_Spread = |SMA_20 - SMA_200| / ATR_14

< 1.0       -> 0.50
1.0 to 4.0  -> 0.50 + 0.50 * (spread - 1.0) / 3.0
> 4.0       -> 1.00
Counter-trend: always 0.25
```

**C_slope — SMA 20 slope momentum:**
```
NormSlope = |SMA_20[0] - SMA_20[5]| / (5 * ATR_14)

< 0.05      -> 0.50
0.05 to 0.30 -> 0.50 + 0.50 * (slope - 0.05) / 0.25
> 0.30      -> 1.00

Agreement bonus:
  Both SMAs agree with trade direction -> x1.00
  Only SMA 20 agrees                   -> x0.75
  Neither agrees                       -> x0.50
```

**C_vol — Recent volatility adjustment:**
```
VolRatio = AvgRange_10_bars / ATR_14

< 0.70  -> 1.20 (calm, slightly more)
0.70-1.30 -> 1.00 (normal)
> 1.30  -> max(0.40, 1.0 / VolRatio)
```

**DailyBudget — Remaining daily risk:**
```
If PnL_daily <= 0:
  DailyBudget = (270 - |accumulated_losses|) / 270

If PnL_daily > 0:
  DailyBudget = min(1.25, 1.0 + (PnL_daily / 270) * 0.25)

If DailyBudget <= 0 -> STOP TRADING
```

**C_time — Time of day factor:**

| Time (NY) | C_time |
|---|---|
| 09:40 - 10:00 | 0.75 |
| 10:00 - 11:30 | 1.00 |
| 11:30 - 13:30 | 0.80 |
| 13:30 - 15:00 | 1.00 |
| 15:00 - 15:25 | 0.60 |

**Safety cap:**
```
N_absolute_max = floor(270 / (SL * $2.00))
N_final = max(1, min(N_final, N_absolute_max))
```

---

## 6. Entry Rules by Regime

### Bullish Trend (LONG)
1. SMA_20 > SMA_200
2. Price within +/-0.5 ATR of SMA_20
3. RSI_7 < 45
4. Score >= 65
5. Entry: buy on bullish confirmation bar
6. Stop: 1.5 * ATR below SMA_20
7. Target: 1.5:1 R:R fixed

### Bearish Trend (SHORT)
1. SMA_20 < SMA_200
2. Price within +/-0.5 ATR of SMA_20
3. RSI_7 > 55
4. Score >= 65
5. Entry: sell on bearish confirmation bar
6. Stop: 1.5 * ATR above SMA_20
7. Target: 1.5:1 R:R fixed

### Counter-Trend Bullish (LONG)
1. |Price - SMA_200| / ATR < 1.5
2. Price <= SMA_200
3. RSI_7 < 30
4. Z_score vs SMA_200 < -2.0
5. Volume > 1.5x average
6. Bullish candle pattern (engulfing/hammer)
7. Score >= 75
8. Stop: 2.0 * ATR below SMA_200
9. Target: 1.5:1 R:R fixed
10. Size: 25% of formula output

### Counter-Trend Bearish (SHORT)
1. |Price - SMA_200| / ATR < 1.5
2. Price >= SMA_200
3. RSI_7 > 70
4. Z_score vs SMA_200 > 2.0
5. Volume > 1.5x average
6. Bearish candle pattern
7. Score >= 75
8. Stop: 2.0 * ATR above SMA_200
9. Target: 1.5:1 R:R fixed
10. Size: 25% of formula output

---

## 7. Exit Rules

```
Exit when FIRST of these occurs:
  1. Price hits Take Profit (entry +/- 1.5 * SL distance)
  2. Price hits Stop Loss
  3. Time = 15:40 NY -> Flatten at market (close everything)
```

---

## 8. Daily Control Rules

```
STOP TRADING for the day if ANY of these is true:
  PnL_daily <= -$270     (max daily loss reached)
  OR PnL_daily >= +$270  (daily target reached)
  OR trades_taken >= 5   (max trades reached)
  OR time >= 15:25 NY    (no new trades after this)
```

---

## 9. Statistics Persistence

```
File: MNQBot_Stats.csv (in Documents/NinjaTrader 8/)
Saved after each trade:
  Date, Time, Direction, Contracts, EntryPrice, ExitPrice, PnL, Score, Regime

Rolling window of last 50 trades to update:
  -> WinRate (for Half-Kelly)
  -> RR ratio (for Half-Kelly)
  -> Recalculated at session start
```

---

## 10. Chart Panel (Visual HUD)

### Location
Upper-right corner, semi-transparent background, ~300x250px

### Displayed Information
```
Section 1 - Status:
  Bot name and version
  Current regime (BULLISH/BEARISH/COUNTER/RANGE/DAY DONE)
  Current entry score with progress bar (0-100)

Section 2 - Trade Info:
  Calculated contracts
  Stop loss (points and $)
  Take profit (points and $)
  R:R ratio

Section 3 - Daily Summary:
  Daily PnL ($)
  Trades taken / max (e.g., 2/5)
  Daily budget percentage
  Rolling WinRate

Section 4 - Indicators:
  NY time
  C_time factor
  ATR(14) value
  VolRatio
  SMA Spread (ATR units)
  SMA 20 slope
  RSI(7)
  Z-Score
```

### Color Coding
| Element | Condition | Color |
|---|---|---|
| BULLISH state | Seeking longs | Green |
| BEARISH state | Seeking shorts | Red |
| COUNTER state | Counter-trend | Orange |
| RANGE state | No trades | Gray |
| DAY DONE state | Target/MDL/5 trades | Blue |
| Score >= 75 | Strong entry | Bright green |
| Score 65-74 | Moderate entry | Yellow |
| Score < 65 | No entry | Gray |
| PnL positive | Winning | Green |
| PnL negative | Losing | Red |

### Chart Drawings
1. SMA 20 — blue line (width 2)
2. SMA 200 — red line (width 2)
3. Pullback zone — shaded band +/-0.5 ATR around SMA 20 (green=bullish, red=bearish)
4. Entry markers — green triangle (long) / red triangle (short) at entry bar
5. Stop loss — dashed red horizontal line
6. Take profit — dashed green horizontal line
7. Counter-trend zone — shaded band +/-1.5 ATR around SMA 200 (orange when active)

### Sound Alerts (Optional)
```
EnableSoundAlerts parameter (default: false)
- Sound on position entry
- Sound on profit close
- Sound on loss close
- Sound when day is done
```

---

## 11. Configurable Parameters

All parameters exposed in NinjaTrader properties window:

### General
- BarPeriod = 2 (minutes)

### Schedule
- StartTime = 09:40 NY
- LastTradeTime = 15:25 NY
- FlattenTime = 15:40 NY

### Risk Management
- MaxDailyLoss = 270 (USD)
- DailyProfitTarget = 270 (USD)
- MaxTradesPerDay = 5
- RiskPerTradeFraction = 0.33
- KellyFraction = 0.50
- RewardRiskRatio = 1.5

### Indicators
- SmaPeriodFast = 20
- SmaPeriodSlow = 200
- AtrPeriod = 14
- RsiPeriod = 7
- StdDevPeriod = 20
- VolumeSMAPeriod = 20
- CandleLookback = 10
- SlopeLookback = 5

### ATR and Stops
- AtrMultiplierTrend = 1.5
- AtrMultiplierCounter = 2.0

### Entry Score
- MinScoreTrend = 65
- MinScoreCounter = 75
- WeightZscore = 0.20
- WeightRsi = 0.15
- WeightSlope = 0.20
- WeightAtr = 0.10
- WeightVolume = 0.10
- WeightPullback = 0.15
- WeightCandle = 0.10

### Regime Detection
- SpreadThresholdLow = 1.0
- SpreadThresholdHigh = 4.0
- SlopeThresholdLow = 0.05
- SlopeThresholdHigh = 0.30
- RangeMaxSlope20 = 0.05
- RangeMaxSlope200 = 0.02

### Position Sizing
- CounterTrendSizePct = 0.25
- ModerateScoreSizePct = 0.75
- MaxAntiMartingale = 1.25

### Statistics
- InitialWinRate = 0.55
- InitialRR = 1.5
- RollingWindowSize = 50

### Visual
- ShowPanel = true
- ShowPullbackZone = true
- ShowCounterZone = true
- ShowEntryMarkers = true
- ShowStopTPLines = true
- EnableSoundAlerts = false
- SmaFastColor = Blue
- SmaSlowColor = Red
- PanelOpacity = 80

---

## 12. File Structure

```
Single file deployment:
  Documents/NinjaTrader 8/bin/Custom/Strategies/MNQProbabilisticBot.cs

Runtime data:
  Documents/NinjaTrader 8/MNQBot_Stats.csv

Internal code organization (#region blocks):
  #region Parameters
  #region Variables and State
  #region Lifecycle (OnStateChange, OnBarUpdate)
  #region Regime Detection
  #region Entry Score Calculation
  #region Position Sizing
  #region Entry Management
  #region Exit Management
  #region Daily Control
  #region Statistics Persistence
  #region Chart Panel Drawing
  #region Chart Drawings (SMAs, zones, markers)
  #region Helper Methods (sigmoid, clamp, math utilities)
```

---

## 13. Expected Performance Profile

```
Based on probabilistic model with the defined parameters:

Per trade:
  Risk: ~$90 (1/3 of MDL)
  Target: ~$135 (1.5:1 R:R)
  Contracts: typically 1-4 depending on ATR and confluence

Per day (target):
  Winning day: +$270 or more (2-3 winning trades)
  Losing day: -$270 max (3 losing trades or combination)

Per week (target):
  3-4 winning days out of 5 (60-80% daily win rate)
  Weekly target: +$540 to +$810 net
  Weekly worst case: -$270 (1 win, 4 losses unlikely with 55%+ edge)

These are estimates based on the mathematical model.
Actual performance depends on market conditions and parameter calibration.
Backtesting with NinjaTrader Strategy Analyzer is required before live trading.
```
