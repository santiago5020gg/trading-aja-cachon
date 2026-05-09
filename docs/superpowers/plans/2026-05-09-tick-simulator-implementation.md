# Tick Simulator MNQ10minV2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Python tick-by-tick simulator that replicates `MNQ10minV2.cs` exactly, using NinjaTrader tick exports, producing identical CSV logs.

**Architecture:** Single-file Python script (`tick_simulator_mnq10minv2.py`) with classes for tick parsing, bar building, simulation engine (state machine port of C#), and CSV log writing. Processes ~1.6M ticks sequentially, evaluating the full state machine on each tick.

**Tech Stack:** Python 3.10+ (stdlib only — no external dependencies)

---

## File Structure

| File | Purpose |
|------|---------|
| `tick_simulator_mnq10minv2.py` | The simulator (single file, all logic) |
| `sim_output/` | Directory for simulation CSV output |

---

### Task 1: Tick File Parser + CLI Setup

**Files:**
- Create: `tick_simulator_mnq10minv2.py`

- [ ] **Step 1: Create the file with CLI argument parsing and tick loading**

```python
"""
Tick Simulator MNQ10minV2 — Simula la estrategia tick-a-tick usando datos exportados de NinjaTrader.

Uso:
  python tick_simulator_mnq10minv2.py [opciones] <archivo_ticks.txt>

Opciones:
  --colchon N        Colchon Stop en pts (default: 5)
  --trades N         Max trades por dia (default: 2)
  --contratos N      Micro contratos (default: 2)
  --modo 1a1|1a2     Modo TP (default: 1a2)
  --cierre HH:MM     Hora cierre (default: 15:50)
  --utc-offset N     Horas a restar para convertir a ET (default: 4)
  --output-dir DIR   Directorio para CSVs de salida (default: sim_output)
  -v, --verbose      Detalle por tick en momentos clave
"""
import sys
import argparse
from datetime import datetime, timedelta
from dataclasses import dataclass, field
from typing import List, Optional, Tuple
import os


def parse_args():
    parser = argparse.ArgumentParser(description="Tick Simulator MNQ10minV2")
    parser.add_argument("--colchon", type=int, default=5, help="Colchon Stop pts (default: 5)")
    parser.add_argument("--trades", type=int, default=2, help="Max trades/dia (default: 2)")
    parser.add_argument("--contratos", type=int, default=2, help="Micro contratos (default: 2)")
    parser.add_argument("--modo", choices=["1a1", "1a2"], default="1a2", help="Modo TP (default: 1a2)")
    parser.add_argument("--cierre", type=str, default="15:50", help="Hora cierre HH:MM (default: 15:50)")
    parser.add_argument("--utc-offset", type=int, default=4, help="Horas a restar para ET (default: 4)")
    parser.add_argument("--output-dir", type=str, default="sim_output", help="Dir salida CSVs")
    parser.add_argument("-v", "--verbose", action="store_true")
    parser.add_argument("file", help="Archivo de ticks NinjaTrader (.txt)")
    return parser.parse_args()


@dataclass
class Tick:
    dt_et: datetime
    price: float
    volume: int


def load_ticks(filepath: str, utc_offset: int) -> List[Tick]:
    """Load NinjaTrader tick export.
    Format: YYYYMMDD HHMMSS microseconds;Last;Bid;Ask;Volume
    Converts UTC -> ET by subtracting utc_offset hours."""
    ticks = []
    with open(filepath, "r", buffering=8*1024*1024) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(";")
            if len(parts) < 5:
                continue
            dt_field = parts[0]
            dt_parts = dt_field.split()
            if len(dt_parts) < 2:
                continue

            date_part = dt_parts[0]
            time_part = dt_parts[1]

            year = int(date_part[:4])
            month = int(date_part[4:6])
            day = int(date_part[6:8])
            hour = int(time_part[:2])
            minute = int(time_part[2:4])
            second = int(time_part[4:6])

            dt_utc = datetime(year, month, day, hour, minute, second)
            dt_et = dt_utc - timedelta(hours=utc_offset)

            price = float(parts[1])
            volume = int(parts[4])

            ticks.append(Tick(dt_et=dt_et, price=price, volume=volume))

    return ticks


if __name__ == "__main__":
    args = parse_args()
    print(f"Cargando ticks de: {args.file}")
    ticks = load_ticks(args.file, args.utc_offset)
    print(f"Total ticks cargados: {len(ticks):,}")
    if ticks:
        print(f"Rango: {ticks[0].dt_et} — {ticks[-1].dt_et}")
```

- [ ] **Step 2: Test the parser runs**

Run: `python tick_simulator_mnq10minv2.py "historicos test/MNQ 06-26-tickyaaa.Last.txt"`

Expected output:
```
Cargando ticks de: historicos test/MNQ 06-26-tickyaaa.Last.txt
Total ticks cargados: 1,649,822
Rango: 2026-05-06 01:00:00 — 2026-05-06 23:59:xx
```

- [ ] **Step 3: Commit**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "feat: tick simulator skeleton with tick parser and CLI"
```

---

### Task 2: BarBuilder Class

**Files:**
- Modify: `tick_simulator_mnq10minv2.py`

- [ ] **Step 1: Add BarBuilder class that accumulates ticks into 2-min bars**

Add after the `load_ticks` function:

```python
@dataclass
class Bar:
    dt_open: datetime
    open: float
    high: float
    low: float
    close: float
    volume: int


class BarBuilder:
    """Accumulates ticks into 2-minute bars aligned on even minutes.
    NinjaTrader bar at 09:32 contains ticks from 09:32:00 to 09:33:59."""

    def __init__(self):
        self.current_bar: Optional[Bar] = None
        self.current_bar_minute: int = -1
        self.current_bar_hour: int = -1
        self.current_bar_date = None
        self.completed_bars: List[Bar] = []

    def _bar_key(self, dt: datetime) -> Tuple[int, int, int]:
        """Returns (date_ordinal, hour, even_minute) for grouping."""
        bar_minute = dt.minute if dt.minute % 2 == 0 else dt.minute - 1
        return (dt.toordinal(), dt.hour, bar_minute)

    def on_tick(self, tick: Tick) -> Optional[Bar]:
        """Process a tick. Returns a completed Bar if a new bar period started, else None."""
        key = self._bar_key(tick.dt_et)
        completed = None

        if self.current_bar is None:
            self.current_bar = Bar(
                dt_open=tick.dt_et.replace(minute=key[2], second=0, microsecond=0),
                open=tick.price, high=tick.price, low=tick.price,
                close=tick.price, volume=tick.volume
            )
            self._current_key = key
        elif key != self._current_key:
            completed = self.current_bar
            self.completed_bars.append(completed)
            self.current_bar = Bar(
                dt_open=tick.dt_et.replace(minute=key[2], second=0, microsecond=0),
                open=tick.price, high=tick.price, low=tick.price,
                close=tick.price, volume=tick.volume
            )
            self._current_key = key
        else:
            self.current_bar.high = max(self.current_bar.high, tick.price)
            self.current_bar.low = min(self.current_bar.low, tick.price)
            self.current_bar.close = tick.price
            self.current_bar.volume += tick.volume

        return completed

    def flush(self) -> Optional[Bar]:
        """Flush the current in-progress bar (end of day)."""
        if self.current_bar:
            bar = self.current_bar
            self.completed_bars.append(bar)
            self.current_bar = None
            return bar
        return None
```

- [ ] **Step 2: Verify bar construction by printing first few bars**

Add temporary test at bottom of `__main__`:
```python
    bb = BarBuilder()
    bar_count = 0
    for tick in ticks[:50000]:
        completed = bb.on_tick(tick)
        if completed and bar_count < 5:
            print(f"  Bar: {completed.dt_open.strftime('%H:%M')} O={completed.open} H={completed.high} L={completed.low} C={completed.close} V={completed.volume}")
            bar_count += 1
```

Run: `python tick_simulator_mnq10minv2.py "historicos test/MNQ 06-26-tickyaaa.Last.txt"`

Verify the first bars match the beginning of the bar_log.csv for May 6 (e.g. 00:00 bar open ~28315.50).

- [ ] **Step 3: Remove temp test code, commit**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "feat: add BarBuilder class for 2-min bar construction from ticks"
```

---

### Task 3: Simulation Engine — State Machine Core

**Files:**
- Modify: `tick_simulator_mnq10minv2.py`

- [ ] **Step 1: Add SimulatorEngine class with state machine skeleton**

Add after BarBuilder:

```python
class SimulatorEngine:
    """Tick-by-tick simulation of MNQ10minV2 state machine.
    Exact port of the C# logic in MNQ10minV2.cs."""

    def __init__(self, colchon_stop: int, max_trades: int, micro_contratos: int,
                 modo_tp: str, hora_cierre: str):
        self.colchon_stop = colchon_stop
        self.max_trades = max_trades
        self.micro_contratos = micro_contratos
        self.modo_tp = modo_tp

        hc_parts = hora_cierre.split(":")
        self.hora_cierre_h = int(hc_parts[0])
        self.hora_cierre_m = int(hc_parts[1])

        # Compute derived params (same as C# State.Configure)
        self.max_stops_puros = round(max_trades / 2)
        self.max_take_profits = round(max_trades / 2)
        self.max_breakevens = round(max_trades / 2)

        if modo_tp == "1a1":
            self.qty_tp1 = micro_contratos
            self.qty_tp2 = 0
        else:
            if micro_contratos >= 3:
                self.qty_tp1 = micro_contratos - 1
                self.qty_tp2 = 1
            elif micro_contratos == 2:
                self.qty_tp1 = 1
                self.qty_tp2 = 1
            else:
                self.qty_tp1 = 0
                self.qty_tp2 = 1

        # Daily state
        self._reset_daily()

        # Results
        self.trade_logs: List[dict] = []
        self.daily_logs: List[dict] = []
        self.bar_logs: List[dict] = []
        self.total_pnl: float = 0.0

    def _reset_daily(self):
        self.estado = "EsperandoRango"
        self.daily_pnl = 0.0
        self.trades_today = 0
        self.stops_puros = 0
        self.take_profits_hoy = 0
        self.breakevens_hoy = 0
        self.cooldown_activo = False
        self.cooldown_start_time: Optional[datetime] = None
        self.trade_direction = 0  # 1=long, -1=short, 0=flat
        self.entry_price = 0.0
        self.stop_distance = 0.0
        self.rango_high = float('-inf')
        self.rango_low = float('inf')
        self.rango_pts = 0.0
        self.breakeven_hit = False
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1
        self.pending_flip = False
        self.pending_flip_direction = 0
        self.trade_counted = False
        self.long_usado = False
        self.short_usado = False
        self.reentry_price_in_range = False
        self.last_decision = "NEW_DAY"
        self.last_exit_reason = ""
        # TP tracking
        self.tp1_active = False
        self.tp2_active = False
        self.tp1_stop = 0.0
        self.tp2_stop = 0.0
        self.tp1_target = 0.0
        self.tp2_target = 0.0
        self.trade_ended = False
        self.trade_ended_by_tp = False
        self.current_date = None

    def on_tick(self, tick: Tick):
        """Process a single tick through the state machine."""
        ny_now = tick.dt_et
        ny_date = ny_now.date()

        # Day reset
        if self.current_date is None:
            self.current_date = ny_date
        elif ny_date != self.current_date:
            self._end_day(tick)
            self._reset_daily()
            self.current_date = ny_date

        if self.estado == "DiaTerminado":
            return

        before_open = ny_now.hour < 9 or (ny_now.hour == 9 and ny_now.minute < 30)
        after_close = (ny_now.hour > self.hora_cierre_h or
                      (ny_now.hour == self.hora_cierre_h and ny_now.minute >= self.hora_cierre_m))

        if after_close:
            if self.trade_direction != 0:
                self._flatten_all(tick, "CierreForzado")
            self.estado = "DiaTerminado"
            self.last_decision = "CIERRE_FORZADO"
            return

        if self.trade_ended:
            self._procesar_fin_trade(tick)

        if self.estado == "EsperandoRango":
            self._procesar_rango(tick)
        elif self.estado == "OrdenesPuestas":
            self._monitorear_ordenes(tick)
        elif self.estado == "EnTrade":
            self._monitorear_trade(tick)

    def _end_day(self, tick: Tick):
        """Log daily result at end of day."""
        if self.trade_direction != 0:
            self._flatten_all(tick, "CierreDia")
        self.daily_logs.append({
            "date": self.current_date,
            "daily_pnl": self.daily_pnl,
            "total_pnl": self.total_pnl,
            "trades": self.trades_today,
            "rango_pts": self.rango_pts
        })

    def _procesar_rango(self, tick: Tick):
        """State: EsperandoRango — accumulate rango from 09:32 to 09:40."""
        ny_now = tick.dt_et
        antes_ventana = ny_now.hour < 9 or (ny_now.hour == 9 and ny_now.minute < 32)
        en_ventana = (ny_now.hour == 9 and 32 <= ny_now.minute <= 40)

        if antes_ventana:
            self.last_decision = "ESPERANDO_0930"
            return

        if en_ventana:
            if tick.price > self.rango_high:
                self.rango_high = tick.price
            if tick.price < self.rango_low:
                self.rango_low = tick.price
            self.last_decision = f"ACUMULANDO_RANGO H={self.rango_high:.2f} L={self.rango_low:.2f}"
            return

        # After window: compute stop and transition
        if self.rango_high == float('-inf') or self.rango_low == float('inf'):
            self.estado = "DiaTerminado"
            self.last_decision = "RANGO_INVALIDO"
            return

        self.rango_pts = self.rango_high - self.rango_low
        self.stop_distance = self.rango_pts + self.colchon_stop
        self.estado = "OrdenesPuestas"
        self.last_decision = f"ORDENES_PUESTAS H={self.rango_high:.2f} L={self.rango_low:.2f}"
        # Check this tick for immediate breakout
        self._monitorear_ordenes(tick)

    def _monitorear_ordenes(self, tick: Tick):
        """State: OrdenesPuestas — wait for breakout or handle flip/reentry."""
        if self.trades_today >= self.max_trades:
            self.estado = "DiaTerminado"
            self.last_decision = "DIA_TERMINADO_MAX_TRADES"
            return

        # Cooldown check (50 seconds)
        if self.cooldown_activo:
            elapsed = (tick.dt_et - self.cooldown_start_time).total_seconds()
            if elapsed < 50:
                self.last_decision = f"COOLDOWN {elapsed:.0f}s/50s"
                return
            self.cooldown_activo = False

        # Flip logic (post stop loss)
        if self.pending_flip:
            self.pending_flip = False
            direction = self.pending_flip_direction
            self._enter_trade(tick, direction)
            self.last_decision = f"FLIP_{'LONG' if direction == 1 else 'SHORT'} @{tick.price:.2f}"
            return

        # Re-entry: wait for price to return inside range
        if self.trades_today > 0 and not self.reentry_price_in_range:
            if self.rango_low < tick.price < self.rango_high:
                self.reentry_price_in_range = True
            else:
                self.last_decision = f"REENTRY_ESPERA_RANGO H={self.rango_high:.2f} L={self.rango_low:.2f}"
                return

        # Breakout detection
        if tick.price >= self.rango_high:
            self._enter_trade(tick, 1)  # LONG
            return
        elif tick.price <= self.rango_low:
            self._enter_trade(tick, -1)  # SHORT
            return

        self.last_decision = f"ESPERANDO_RUPTURA H={self.rango_high:.2f} L={self.rango_low:.2f}"

    def _enter_trade(self, tick: Tick, direction: int):
        """Execute entry at current tick price."""
        self.trade_direction = direction
        self.entry_price = tick.price
        self.breakeven_hit = False
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1
        self.trade_ended = False
        self.trade_ended_by_tp = False
        self.trade_counted = True
        self.trades_today += 1

        stop_long = self.rango_low - self.colchon_stop
        stop_short = self.rango_high + self.colchon_stop

        if direction == 1:
            self.long_usado = True
            self.short_usado = True
            self.tp1_stop = stop_long
            self.tp2_stop = stop_long
            self.tp1_target = self.entry_price + self.stop_distance
            self.tp2_target = self.entry_price + self.stop_distance * 2
        else:
            self.short_usado = True
            self.long_usado = True
            self.tp1_stop = stop_short
            self.tp2_stop = stop_short
            self.tp1_target = self.entry_price - self.stop_distance
            self.tp2_target = self.entry_price - self.stop_distance * 2

        self.tp1_active = self.qty_tp1 > 0
        self.tp2_active = self.qty_tp2 > 0

        self.estado = "EnTrade"
        dir_str = "LONG" if direction == 1 else "SHORT"
        self.last_decision = f"ENTRY_{dir_str} @{tick.price:.2f} stop={self.stop_distance:.2f}"

        # Log entry
        self._log_trade(tick.dt_et, "ENTRY", dir_str, tick.price, 0, "TP1" + ("Long" if direction == 1 else "Short"))
        if self.qty_tp1 > 0 and self.qty_tp2 > 0:
            self._log_trade(tick.dt_et, "ENTRY", dir_str, tick.price, 0, "TP2" + ("Long" if direction == 1 else "Short"))

    def _monitorear_trade(self, tick: Tick):
        """State: EnTrade — check stops, targets, trailing on each tick."""
        if self.trade_direction == 0:
            return

        d = self.trade_direction
        price = tick.price

        # Check TP1 target hit
        tp1_hit = False
        if self.tp1_active:
            if d == 1 and price >= self.tp1_target:
                tp1_hit = True
            elif d == -1 and price <= self.tp1_target:
                tp1_hit = True

        # Check TP2 target hit
        tp2_hit = False
        if self.tp2_active:
            if d == 1 and price >= self.tp2_target:
                tp2_hit = True
            elif d == -1 and price <= self.tp2_target:
                tp2_hit = True

        # Check TP1 stop hit
        tp1_stopped = False
        if self.tp1_active:
            if d == 1 and price <= self.tp1_stop:
                tp1_stopped = True
            elif d == -1 and price >= self.tp1_stop:
                tp1_stopped = True

        # Check TP2 stop hit
        tp2_stopped = False
        if self.tp2_active:
            if d == 1 and price <= self.tp2_stop:
                tp2_stopped = True
            elif d == -1 and price >= self.tp2_stop:
                tp2_stopped = True

        # Process TP hits
        dir_str = "LONG" if d == 1 else "SHORT"
        if tp1_hit:
            exit_price = self.tp1_target
            pnl = self.stop_distance * 2.0 * self.qty_tp1
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp1_active = False
            self._log_trade(tick.dt_et, "EXIT", dir_str, exit_price, pnl, "TakeProfit")

        if tp2_hit:
            exit_price = self.tp2_target
            pnl = self.stop_distance * 2.0 * 2.0 * self.qty_tp2
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp2_active = False
            self._log_trade(tick.dt_et, "EXIT", dir_str, exit_price, pnl, "TakeProfit")

        if tp1_hit or tp2_hit:
            if not self.tp1_active and not self.tp2_active:
                self.trade_ended = True
                self.trade_ended_by_tp = True
                self.last_exit_reason = "TakeProfit"
                return

        # Process stop hits (only if not already hit by TP)
        if tp1_stopped and not tp1_hit:
            stop_price = self.tp1_stop
            if self.breakeven_hit:
                pnl = (stop_price - self.entry_price) * 2.0 * self.qty_tp1 if d == 1 else (self.entry_price - stop_price) * 2.0 * self.qty_tp1
            else:
                pnl = -self.stop_distance * 2.0 * self.qty_tp1
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp1_active = False
            reason = "Breakeven" if self.breakeven_hit else "StopLoss"
            self._log_trade(tick.dt_et, "EXIT", dir_str, stop_price, pnl, reason)

        if tp2_stopped and not tp2_hit:
            stop_price = self.tp2_stop
            if self.breakeven_hit:
                pnl = (stop_price - self.entry_price) * 2.0 * self.qty_tp2 if d == 1 else (self.entry_price - stop_price) * 2.0 * self.qty_tp2
            else:
                pnl = -self.stop_distance * 2.0 * self.qty_tp2
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp2_active = False
            reason = "Breakeven" if self.breakeven_hit else "StopLoss"
            self._log_trade(tick.dt_et, "EXIT", dir_str, stop_price, pnl, reason)

        # Check if all positions closed
        if not self.tp1_active and not self.tp2_active:
            if tp1_hit or tp2_hit:
                self.trade_ended = True
                self.trade_ended_by_tp = True
                self.last_exit_reason = "TakeProfit"
            elif self.breakeven_hit:
                self.trade_ended = True
                self.trade_ended_by_tp = False
                self.last_exit_reason = "Breakeven"
            else:
                self.trade_ended = True
                self.trade_ended_by_tp = False
                self.last_exit_reason = "StopLoss"
            return

        # Trailing logic (evaluate on each tick based on current price)
        if d == 1:
            unrealized = price - self.entry_price
        else:
            unrealized = self.entry_price - price

        sd = self.stop_distance

        # Breakeven at 60%
        if not self.breakeven_hit and unrealized >= sd * 0.60:
            be_stop = self.entry_price + 5 if d == 1 else self.entry_price - 5
            if self.tp1_active:
                self.tp1_stop = be_stop
            if self.tp2_active:
                self.tp2_stop = be_stop
            self.breakeven_hit = True

        # Trailing TP1: 75%->45%, 85%->60%, 95%->80%, 99%->95%
        if self.tp1_active and self.breakeven_hit:
            if self.tp1_stop_nivel < 4 and unrealized >= sd * 0.99:
                self.tp1_stop = self.entry_price + (sd * 0.95) if d == 1 else self.entry_price - (sd * 0.95)
                self.tp1_stop_nivel = 4
            elif self.tp1_stop_nivel < 3 and unrealized >= sd * 0.95:
                self.tp1_stop = self.entry_price + (sd * 0.80) if d == 1 else self.entry_price - (sd * 0.80)
                self.tp1_stop_nivel = 3
            elif self.tp1_stop_nivel < 2 and unrealized >= sd * 0.85:
                self.tp1_stop = self.entry_price + (sd * 0.60) if d == 1 else self.entry_price - (sd * 0.60)
                self.tp1_stop_nivel = 2
            elif self.tp1_stop_nivel < 1 and unrealized >= sd * 0.75:
                self.tp1_stop = self.entry_price + (sd * 0.45) if d == 1 else self.entry_price - (sd * 0.45)
                self.tp1_stop_nivel = 1

        # Trailing TP2: 50%->25%, 70%->50%, 85%->60%, 90%->75%, 95%->84%, 98%->94%
        if self.tp2_active and self.breakeven_hit:
            tp2_target_dist = sd * 2
            if self.tp2_stop_nivel < 6 and unrealized >= tp2_target_dist * 0.98:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.94) if d == 1 else self.entry_price - (tp2_target_dist * 0.94)
                self.tp2_stop_nivel = 6
            elif self.tp2_stop_nivel < 5 and unrealized >= tp2_target_dist * 0.95:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.84) if d == 1 else self.entry_price - (tp2_target_dist * 0.84)
                self.tp2_stop_nivel = 5
            elif self.tp2_stop_nivel < 4 and unrealized >= tp2_target_dist * 0.90:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.75) if d == 1 else self.entry_price - (tp2_target_dist * 0.75)
                self.tp2_stop_nivel = 4
            elif self.tp2_stop_nivel < 3 and unrealized >= tp2_target_dist * 0.85:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.60) if d == 1 else self.entry_price - (tp2_target_dist * 0.60)
                self.tp2_stop_nivel = 3
            elif self.tp2_stop_nivel < 2 and unrealized >= tp2_target_dist * 0.70:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.50) if d == 1 else self.entry_price - (tp2_target_dist * 0.50)
                self.tp2_stop_nivel = 2
            elif self.tp2_stop_nivel < 1 and unrealized >= tp2_target_dist * 0.50:
                self.tp2_stop = self.entry_price + (tp2_target_dist * 0.25) if d == 1 else self.entry_price - (tp2_target_dist * 0.25)
                self.tp2_stop_nivel = 1

        self.last_decision = f"EN_TRADE {'LONG' if d == 1 else 'SHORT'} entry={self.entry_price:.2f} unreal={unrealized:.2f}pts"

    def _procesar_fin_trade(self, tick: Tick):
        """Handle post-trade transitions (same as C# ProcesarFinTrade)."""
        self.trade_ended = False
        prev_direction = self.trade_direction
        exit_reason = self.last_exit_reason
        self.trade_direction = 0
        self.entry_price = 0
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1
        self.breakeven_hit = False
        self.trade_counted = False

        if self.trade_ended_by_tp:
            self.take_profits_hoy += 1
            self.trade_ended_by_tp = False
            if self.take_profits_hoy >= self.max_take_profits:
                self.estado = "DiaTerminado"
                self.last_decision = f"DIA_TERMINADO_MAX_TP ({self.take_profits_hoy})"
            elif self.trades_today >= self.max_trades:
                self.estado = "DiaTerminado"
                self.last_decision = "DIA_TERMINADO_MAX_TRADES"
            else:
                self.pending_flip = False
                self.reentry_price_in_range = False
                self.cooldown_activo = True
                self.cooldown_start_time = tick.dt_et
                self.estado = "OrdenesPuestas"
                self.last_decision = f"POST_TP_COOLDOWN_50s"
        elif exit_reason == "StopLoss":
            self.stops_puros += 1
            self.trade_ended_by_tp = False
            if self.stops_puros >= self.max_stops_puros:
                self.estado = "DiaTerminado"
                self.last_decision = f"DIA_TERMINADO_MAX_STOPS ({self.stops_puros})"
            elif self.trades_today >= self.max_trades:
                self.estado = "DiaTerminado"
                self.last_decision = "DIA_TERMINADO_MAX_TRADES"
            else:
                self.pending_flip = True
                self.pending_flip_direction = -1 if prev_direction == 1 else 1
                self.estado = "OrdenesPuestas"
                self.last_decision = f"FLIP_PENDING dir={'LONG' if self.pending_flip_direction == 1 else 'SHORT'}"
        elif self.trades_today >= self.max_trades:
            self.trade_ended_by_tp = False
            self.estado = "DiaTerminado"
            self.last_decision = "DIA_TERMINADO_MAX_TRADES"
        else:
            # Breakeven exit
            self.trade_ended_by_tp = False
            self.breakevens_hoy += 1
            if self.breakevens_hoy >= self.max_breakevens:
                self.estado = "DiaTerminado"
                self.last_decision = f"DIA_TERMINADO_MAX_BE ({self.breakevens_hoy})"
            elif self.trades_today >= self.max_trades:
                self.estado = "DiaTerminado"
                self.last_decision = "DIA_TERMINADO_MAX_TRADES"
            else:
                self.pending_flip = False
                self.reentry_price_in_range = False
                self.cooldown_activo = True
                self.cooldown_start_time = tick.dt_et
                self.estado = "OrdenesPuestas"
                self.last_decision = f"COOLDOWN_50s"

    def _flatten_all(self, tick: Tick, reason: str):
        """Force close all positions at current price."""
        d = self.trade_direction
        dir_str = "LONG" if d == 1 else "SHORT"

        if self.tp1_active:
            pnl = (tick.price - self.entry_price) * 2.0 * self.qty_tp1 if d == 1 else (self.entry_price - tick.price) * 2.0 * self.qty_tp1
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp1_active = False
            self._log_trade(tick.dt_et, "EXIT", dir_str, tick.price, pnl, reason)

        if self.tp2_active:
            pnl = (tick.price - self.entry_price) * 2.0 * self.qty_tp2 if d == 1 else (self.entry_price - tick.price) * 2.0 * self.qty_tp2
            self.daily_pnl += pnl
            self.total_pnl += pnl
            self.tp2_active = False
            self._log_trade(tick.dt_et, "EXIT", dir_str, tick.price, pnl, reason)

        self.trade_direction = 0

    def _log_trade(self, dt: datetime, action: str, direction: str, price: float, pnl: float, reason: str):
        """Record a trade event."""
        self.trade_logs.append({
            "date": dt.strftime("%Y-%m-%d"),
            "time": dt.strftime("%H:%M"),
            "action": action,
            "direction": direction,
            "entry_price": self.entry_price if action == "ENTRY" else self.entry_price,
            "exit_price": price if action == "EXIT" else 0,
            "stop_dist": self.stop_distance,
            "rango_high": self.rango_high if self.rango_high != float('-inf') else 0,
            "rango_low": self.rango_low if self.rango_low != float('inf') else 0,
            "rango_pts": self.rango_pts,
            "pnl": pnl,
            "daily_pnl": self.daily_pnl,
            "total_pnl": self.total_pnl,
            "exit_reason": reason,
            "trades_today": self.trades_today
        })

    def finalize(self):
        """Call at end of all ticks to flush the last day."""
        if self.current_date:
            self.daily_logs.append({
                "date": self.current_date,
                "daily_pnl": self.daily_pnl,
                "total_pnl": self.total_pnl,
                "trades": self.trades_today,
                "rango_pts": self.rango_pts
            })
```

- [ ] **Step 2: Commit**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "feat: add SimulatorEngine with complete state machine"
```

---

### Task 4: CSV Log Writer + Main Loop Integration

**Files:**
- Modify: `tick_simulator_mnq10minv2.py`

- [ ] **Step 1: Add CSV writer functions and wire up the main loop**

Add after SimulatorEngine class:

```python
def write_trades_csv(trade_logs: List[dict], output_dir: str):
    """Write trades_log.csv in same format as the real bot (Spanish decimal commas)."""
    path = os.path.join(output_dir, "mnq10minv2_trades_log.csv")
    header = "Date,Time,Action,Direction,EntryPrice,ExitPrice,StopDist,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday\n"
    with open(path, "w") as f:
        f.write(header)
        for t in trade_logs:
            line = "{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}\n".format(
                t["date"], t["time"], t["action"], t["direction"],
                f"{t['entry_price']:.2f}".replace(".", ","),
                f"{t['exit_price']:.2f}".replace(".", ","),
                f"{t['stop_dist']:.2f}".replace(".", ","),
                f"{t['rango_high']:.2f}".replace(".", ","),
                f"{t['rango_low']:.2f}".replace(".", ","),
                f"{t['rango_pts']:.2f}".replace(".", ","),
                f"{t['pnl']:.2f}".replace(".", ","),
                f"{t['daily_pnl']:.2f}".replace(".", ","),
                f"{t['total_pnl']:.2f}".replace(".", ","),
                t["exit_reason"], t["trades_today"]
            )
            f.write(line)
    print(f"  trades_log: {path} ({len(trade_logs)} entries)")


def write_daily_csv(daily_logs: List[dict], output_dir: str):
    """Write daily_log.csv in same format as the real bot."""
    path = os.path.join(output_dir, "mnq10minv2_daily_log.csv")
    header = "Date,DailyPnL,TotalPnL,Trades,RangoPts\n"
    with open(path, "w") as f:
        f.write(header)
        for d in daily_logs:
            line = "{},{},{},{},{}\n".format(
                d["date"],
                f"{d['daily_pnl']:.2f}".replace(".", ","),
                f"{d['total_pnl']:.2f}".replace(".", ","),
                d["trades"],
                f"{d['rango_pts']:.2f}".replace(".", ",")
            )
            f.write(line)
    print(f"  daily_log: {path} ({len(daily_logs)} entries)")


def write_bar_csv(bar_logs: List[dict], output_dir: str):
    """Write bar_log.csv in same format as the real bot."""
    path = os.path.join(output_dir, "mnq10minv2_bar_log.csv")
    header = "Date,Time,Open,High,Low,Close,Volume,Estado,Position,DailyPnL,TotalPnL,TradesToday,Decision\n"
    with open(path, "w") as f:
        f.write(header)
        for b in bar_logs:
            line = "{},{},{},{},{},{},{},{},{},{},{},{},{}\n".format(
                b["date"], b["time"],
                f"{b['open']:.2f}".replace(".", ","),
                f"{b['high']:.2f}".replace(".", ","),
                f"{b['low']:.2f}".replace(".", ","),
                f"{b['close']:.2f}".replace(".", ","),
                b["volume"],
                b["estado"], b["position"],
                f"{b['daily_pnl']:.2f}".replace(".", ","),
                f"{b['total_pnl']:.2f}".replace(".", ","),
                b["trades_today"], b["decision"]
            )
            f.write(line)
    print(f"  bar_log: {path} ({len(bar_logs)} entries)")
```

- [ ] **Step 2: Replace the `__main__` block with the full simulation loop**

```python
if __name__ == "__main__":
    args = parse_args()

    print("=== TICK SIMULATOR MNQ10minV2 ===")
    print(f"  Colchon Stop: {args.colchon} pts")
    print(f"  Max Trades/Dia: {args.trades}")
    print(f"  Micro Contratos: {args.contratos}")
    print(f"  Modo TP: {args.modo}")
    print(f"  Hora Cierre: {args.cierre}")
    print(f"  UTC Offset: -{args.utc_offset}h")
    print(f"  Archivo: {args.file}")
    print()

    # Load ticks
    print("Cargando ticks...")
    ticks = load_ticks(args.file, args.utc_offset)
    print(f"  Total ticks: {len(ticks):,}")
    if not ticks:
        print("ERROR: No se cargaron ticks.")
        sys.exit(1)
    print(f"  Rango: {ticks[0].dt_et} — {ticks[-1].dt_et}")
    print()

    # Create output dir
    os.makedirs(args.output_dir, exist_ok=True)

    # Run simulation
    print("Simulando tick-a-tick...")
    engine = SimulatorEngine(
        colchon_stop=args.colchon,
        max_trades=args.trades,
        micro_contratos=args.contratos,
        modo_tp=args.modo,
        hora_cierre=args.cierre
    )
    bar_builder = BarBuilder()

    for i, tick in enumerate(ticks):
        # Process through engine
        engine.on_tick(tick)

        # Build bars for logging
        completed_bar = bar_builder.on_tick(tick)
        if completed_bar:
            pos = "LONG" if engine.trade_direction == 1 else "SHORT" if engine.trade_direction == -1 else "FLAT"
            engine.bar_logs.append({
                "date": completed_bar.dt_open.strftime("%Y-%m-%d"),
                "time": completed_bar.dt_open.strftime("%H:%M"),
                "open": completed_bar.open,
                "high": completed_bar.high,
                "low": completed_bar.low,
                "close": completed_bar.close,
                "volume": completed_bar.volume,
                "estado": engine.estado,
                "position": pos,
                "daily_pnl": engine.daily_pnl,
                "total_pnl": engine.total_pnl,
                "trades_today": engine.trades_today,
                "decision": engine.last_decision
            })

        # Progress indicator
        if (i + 1) % 500000 == 0:
            print(f"  Procesados {i+1:,} ticks...")

    # Flush last bar and finalize
    last_bar = bar_builder.flush()
    if last_bar:
        pos = "LONG" if engine.trade_direction == 1 else "SHORT" if engine.trade_direction == -1 else "FLAT"
        engine.bar_logs.append({
            "date": last_bar.dt_open.strftime("%Y-%m-%d"),
            "time": last_bar.dt_open.strftime("%H:%M"),
            "open": last_bar.open, "high": last_bar.high,
            "low": last_bar.low, "close": last_bar.close,
            "volume": last_bar.volume,
            "estado": engine.estado, "position": pos,
            "daily_pnl": engine.daily_pnl, "total_pnl": engine.total_pnl,
            "trades_today": engine.trades_today, "decision": engine.last_decision
        })
    engine.finalize()

    print(f"  Simulacion completada.")
    print()

    # Print trade summary
    print("=== TRADES ===")
    for t in engine.trade_logs:
        print(f"  {t['date']} {t['time']}  {t['action']:<5} {t['direction']:<5} @{t['exit_price'] if t['action']=='EXIT' else t['entry_price']:.2f}  PnL=${t['pnl']:.2f}  {t['exit_reason']}")
    print()

    # Print daily summary
    print("=== RESUMEN DIARIO ===")
    for d in engine.daily_logs:
        print(f"  {d['date']}  Trades={d['trades']}  PnL=${d['daily_pnl']:.2f}  Acum=${d['total_pnl']:.2f}")
    print()

    # Write CSVs
    print("Escribiendo CSVs...")
    write_trades_csv(engine.trade_logs, args.output_dir)
    write_daily_csv(engine.daily_logs, args.output_dir)
    write_bar_csv(engine.bar_logs, args.output_dir)
    print()
    print(f"PnL Total: ${engine.total_pnl:.2f}")
```

- [ ] **Step 3: Run the simulation and compare with real bot logs**

Run: `python tick_simulator_mnq10minv2.py "historicos test/MNQ 06-26-tickyaaa.Last.txt"`

Expected output should show:
```
=== TRADES ===
  2026-05-06 09:41  ENTRY LONG  @28457.00  ...
  2026-05-06 11:26  EXIT  LONG  @28577.25  PnL=$240.50  Breakeven
  2026-05-06 12:58  EXIT  LONG  @28520.25  PnL=$126.50  Breakeven
```

Compare against real bot log:
```
09:41  ENTRY LONG  28457.00  (TP1Long + TP2Long)
11:26  EXIT  LONG  28577.25  PnL=$240.50  (Breakeven)
12:58  EXIT  LONG  28520.25  PnL=$126.50  (Breakeven)
Daily PnL = $367.00
```

- [ ] **Step 4: Commit**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "feat: complete tick simulator with CSV output and main loop"
```

---

### Task 5: Validation and Debugging Against Real Logs

**Files:**
- Modify: `tick_simulator_mnq10minv2.py` (if adjustments needed)

- [ ] **Step 1: Run simulation and capture actual output**

Run: `python tick_simulator_mnq10minv2.py "historicos test/MNQ 06-26-tickyaaa.Last.txt" -v`

Compare entry price, exit prices, PnL values, and timestamps against the real log values:
- Entry: 28457.00 at 09:41
- TP1 exit: 28577.25 at 11:26 (Breakeven, PnL $240.50)
- TP2 exit: 28520.25 at 12:58 (Breakeven, PnL $126.50)
- Daily PnL: $367.00

- [ ] **Step 2: If entry price differs — debug rango window**

The rango window is 09:32-09:40. Verify that:
- `rango_high` matches 28458.00 (from real log)
- `rango_low` matches 28336.50 (from real log)
- `rango_pts` = 121.50
- `stop_distance` = 121.50 + 5 = 126.50
- Entry triggers when tick >= 28458.00 (rango_high)

If the entry price is 28458.00 instead of 28457.00, the issue is whether the C# bot uses `Close[0] >= rangoHigh` (where Close[0] is the bar close at the time of evaluation) vs the actual tick that crosses. Check the real log entry price to calibrate.

- [ ] **Step 3: If exit prices differ — debug trailing levels**

For TP1 exit at 28577.25 (Breakeven):
- stopDistance = 126.50
- entry = 28457.00
- Breakeven stop at 60%: entry + 5 = 28462.00
- TP1 trailing escalones:
  - 75% (94.875pts): stop → entry + 45% = 28457 + 56.925 = 28513.925
  - 85% (107.525pts): stop → entry + 60% = 28457 + 75.9 = 28532.9
  - 95% (120.175pts): stop → entry + 80% = 28457 + 101.2 = 28558.2
  - 99% (125.235pts): stop → entry + 95% = 28457 + 120.175 = 28577.175

So TP1 stop at nivel 4 (99%) = 28577.175. The exit at 28577.25 means price dropped TO 28577.175 (rounded to tick = 28577.25). This confirms trailing is correct.

- [ ] **Step 4: If times differ — check UTC offset**

The tick file starts at `20260506 050000` which is 05:00 UTC. With offset 4, that's 01:00 ET.
Verify the first tick in the loaded data shows `2026-05-06 01:00:00 ET`.

- [ ] **Step 5: Commit final validated version**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "feat: validated tick simulator matches real bot output for May 6"
```

---

### Task 6: Compare CSV Output Format

**Files:**
- Modify: `tick_simulator_mnq10minv2.py` (minor formatting fixes)

- [ ] **Step 1: Diff the sim output against real bot logs**

Run:
```bash
diff <(head -15 sim_output/mnq10minv2_trades_log.csv) <(head -15 bot/history/mnq10minv2_trades_log.csv)
```

Check that:
- Column headers match exactly
- Decimal format matches (Spanish commas: `28457,00` not `28457.00`)
- Date format matches (`2026-05-06`)
- Time format matches (`09:41`)

- [ ] **Step 2: Fix any formatting discrepancies found in Step 1**

Common issues to watch:
- The real bot logs entry for BOTH TP1 and TP2 with the same ENTRY line (two rows)
- The real bot uses the signal name (TP1Long, TP2Long) as ExitReason on entry rows
- Exit rows use "Breakeven" / "StopLoss" / "TakeProfit"

Adjust `_log_trade` and `write_trades_csv` if needed to match exactly.

- [ ] **Step 3: Commit formatting fixes**

```bash
git add tick_simulator_mnq10minv2.py
git commit -m "fix: align CSV output format with real bot logs"
```

---

## Validation Checklist

After all tasks are complete, verify:

1. **Entry price** matches real log (28457.00)
2. **Rango** matches (H=28458.00, L=28336.50, pts=121.50)
3. **TP1 exit** matches (28577.25 at 11:26, PnL $240.50)
4. **TP2 exit** matches (28520.25 at 12:58, PnL $126.50)
5. **Daily PnL** matches ($367.00)
6. **CSV format** is identical (Spanish decimals, same columns)
7. **Bar log** has reasonable entries (one per 2-min bar during session)

## NinjaTrader Export Instructions (for user reference)

To export tick data from NinjaTrader 8:
1. Open **Control Center** → **Tools** → **Historical Data**
2. Select instrument: MNQ 06-26 (or active contract)
3. Set date range (e.g., 2026-05-06 to 2026-05-06)
4. Data type: **Last** (tick-by-tick)
5. Click **Export**
6. Save as `.txt` in the `historicos test/` folder
7. The format will be: `YYYYMMDD HHMMSS microseconds;Last;Bid;Ask;Volume`
