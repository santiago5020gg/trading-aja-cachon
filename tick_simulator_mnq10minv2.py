"""
Tick-by-tick simulator for MNQ10minV2 NinjaTrader strategy.

Reads NinjaTrader tick export files and processes each tick through a state machine
that is an exact port of the C# bot MNQ10minV2.cs. Generates CSV logs identical
in format to what the real bot produces.

Usage:
  python tick_simulator_mnq10minv2.py [options] <tick_file>

Examples:
  python tick_simulator_mnq10minv2.py "historicos test/MNQ 06-26-tickyaaa.Last.txt"
  python tick_simulator_mnq10minv2.py --colchon 10 --trades 3 "historicos test/MNQ 06-26-tickyaaa.Last.txt"
"""
import sys
import os
import argparse
from datetime import datetime, timedelta


# ─── CLI Arguments ────────────────────────────────────────────────────────────

def parse_args():
    parser = argparse.ArgumentParser(description="Tick-by-tick simulator MNQ10minV2")
    parser.add_argument("--colchon", type=int, default=5, help="ColchonStop in points (default: 5)")
    parser.add_argument("--trades", type=int, default=2, help="MaxTrades per day (default: 2)")
    parser.add_argument("--contratos", type=int, default=2, help="MicroContratos (default: 2)")
    parser.add_argument("--modo", choices=["1a1", "1a2"], default="1a2", help="TP mode (default: 1a2)")
    parser.add_argument("--cierre", type=str, default="15:50", help="Hora cierre HH:MM ET (default: 15:50)")
    parser.add_argument("--utc-offset", type=int, default=4, help="Hours to subtract for ET (default: 4)")
    parser.add_argument("--output-dir", type=str, default="sim_output", help="Output directory (default: sim_output)")
    parser.add_argument("-v", "--verbose", action="store_true", help="Show extra detail")
    parser.add_argument("file", help="Tick .txt file path")
    return parser.parse_args()


# ─── Constants ────────────────────────────────────────────────────────────────

TICK_SIZE = 0.25
POINT_VALUE = 2.0  # $2 per point per micro contract


def round_to_tick(price):
    """Round a price to the nearest tick increment (0.25)."""
    return round(price / TICK_SIZE) * TICK_SIZE


# ─── State Machine ────────────────────────────────────────────────────────────

class TickSimulator:
    """Exact port of MNQ10minV2.cs state machine operating tick-by-tick."""

    def __init__(self, colchon_stop, max_trades, micro_contratos, modo_tp, hora_cierre, utc_offset, output_dir, verbose):
        self.colchon_stop = colchon_stop
        self.max_trades = max_trades
        self.micro_contratos = micro_contratos
        self.modo_tp = modo_tp
        self.utc_offset = utc_offset
        self.output_dir = output_dir
        self.verbose = verbose

        # Parse hora cierre
        hc_parts = hora_cierre.split(":")
        self.hora_cierre_h = int(hc_parts[0])
        self.hora_cierre_m = int(hc_parts[1])

        # Compute derived params
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

        # Bar builder state
        self.current_bar_start = None  # datetime of current bar period start
        self.bar_open = 0.0
        self.bar_high = -999999.0
        self.bar_low = 999999.0
        self.bar_close = 0.0
        self.bar_volume = 0

        # Output logs
        self.trade_rows = []
        self.bar_rows = []
        self.daily_rows = []

        # Per-day state (reset each day)
        self.reset_daily()

        self.total_pnl = 0.0
        self.last_reset_date = None

    def reset_daily(self):
        """Reset all per-day state variables."""
        self.estado = "EsperandoRango"
        self.trade_direction = 0  # 1=long, -1=short, 0=flat
        self.entry_price = 0.0
        self.stop_distance = 0.0

        self.rango_high = -999999.0
        self.rango_low = 999999.0
        self.rango_pts = 0.0

        self.qty_tp1_active = False
        self.qty_tp2_active = False

        self.long_usado = False
        self.short_usado = False
        self.reentry_price_in_range = False
        self.breakeven_hit = False
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1

        self.pending_flip = False
        self.pending_flip_direction = 0

        self.daily_pnl = 0.0
        self.trades_today = 0
        self.stops_puros = 0
        self.take_profits_hoy = 0
        self.breakevens_hoy = 0

        self.cooldown_activo = False
        self.cooldown_start_time = None  # datetime when cooldown started

        self.tp1_stop = 0.0
        self.tp2_stop = 0.0
        self.tp1_target = 0.0
        self.tp2_target = 0.0

        self.last_decision = "NEW_DAY"

    def run(self, filepath):
        """Process entire tick file."""
        print(f"=== PARAMETROS ===")
        print(f"  ColchonStop: {self.colchon_stop}")
        print(f"  Max Trades/Dia: {self.max_trades}")
        print(f"  Micro Contratos: {self.micro_contratos}")
        print(f"  Modo TP: {self.modo_tp} (TP1 x{self.qty_tp1}, TP2 x{self.qty_tp2})")
        print(f"  Max Stops: {self.max_stops_puros} | Max TPs: {self.max_take_profits} | Max BEs: {self.max_breakevens}")
        print(f"  Hora Cierre: {self.hora_cierre_h:02d}:{self.hora_cierre_m:02d} ET")
        print(f"  UTC Offset: -{self.utc_offset}h")
        print(f"  Archivo: {filepath}")
        print()

        tick_count = 0
        buf_size = 8 * 1024 * 1024  # 8MB buffer

        with open(filepath, "r", buffering=buf_size) as f:
            for line in f:
                line = line.rstrip('\n\r')
                if not line:
                    continue

                # Parse tick line: YYYYMMDD HHMMSS microseconds;Last;Bid;Ask;Volume
                semi_pos = line.find(';')
                if semi_pos < 0:
                    continue

                dt_field = line[:semi_pos]
                rest = line[semi_pos + 1:]

                # Parse datetime
                # Format: "20260506 050000 0560000"
                # dt_field split by space: [date, time, microseconds]
                space1 = dt_field.find(' ')
                if space1 < 0:
                    continue
                space2 = dt_field.find(' ', space1 + 1)
                if space2 < 0:
                    continue

                date_part = dt_field[:space1]
                time_part = dt_field[space1 + 1:space2]

                year = int(date_part[0:4])
                month = int(date_part[4:6])
                day = int(date_part[6:8])
                hour = int(time_part[0:2])
                minute = int(time_part[2:4])
                second = int(time_part[4:6])

                # Convert UTC to ET
                # Build ET time directly (avoid datetime overhead for speed)
                # Simple subtraction: handle day rollback
                et_hour = hour - self.utc_offset
                et_day = day
                et_month = month
                et_year = year
                if et_hour < 0:
                    et_hour += 24
                    et_day -= 1
                    if et_day < 1:
                        et_month -= 1
                        if et_month < 1:
                            et_month = 12
                            et_year -= 1
                        # Days in month (simplified)
                        if et_month in (1, 3, 5, 7, 8, 10, 12):
                            et_day = 31
                        elif et_month in (4, 6, 9, 11):
                            et_day = 30
                        else:
                            et_day = 29 if (et_year % 4 == 0 and (et_year % 100 != 0 or et_year % 400 == 0)) else 28

                et_date = (et_year, et_month, et_day)
                et_minute = minute
                et_second = second

                # Parse price fields
                fields = rest.split(';')
                if len(fields) < 4:
                    continue

                last_price = float(fields[0])
                volume = int(fields[3])

                tick_count += 1

                # Day change check
                if self.last_reset_date is not None and et_date != self.last_reset_date:
                    # End of previous day
                    self._end_day()
                    self.reset_daily()

                self.last_reset_date = et_date

                # Update bar builder
                self._update_bar(et_year, et_month, et_day, et_hour, et_minute, et_second, last_price, volume)

                # Process tick through state machine
                self._process_tick(et_hour, et_minute, et_second, last_price)

        # End last day
        if self.last_reset_date is not None:
            self._end_day()

        print(f"Ticks procesados: {tick_count:,}")
        print()

        # Write output CSVs
        self._write_csvs()

        # Print summary
        self._print_summary()

    def _update_bar(self, year, month, day, hour, minute, second, price, volume):
        """Accumulate ticks into 2-min bars aligned on even minutes."""
        # Bar alignment: even minutes
        bar_minute = minute if minute % 2 == 0 else minute - 1
        bar_key = (year, month, day, hour, bar_minute)

        if self.current_bar_start is not None and bar_key != self.current_bar_start:
            # New bar period - emit the completed bar
            self._emit_bar()
            # Start new bar
            self.current_bar_start = bar_key
            self.bar_open = price
            self.bar_high = price
            self.bar_low = price
            self.bar_close = price
            self.bar_volume = volume
        elif self.current_bar_start is None:
            # First tick ever
            self.current_bar_start = bar_key
            self.bar_open = price
            self.bar_high = price
            self.bar_low = price
            self.bar_close = price
            self.bar_volume = volume
        else:
            # Same bar - update
            if price > self.bar_high:
                self.bar_high = price
            if price < self.bar_low:
                self.bar_low = price
            self.bar_close = price
            self.bar_volume += volume

    def _emit_bar(self):
        """Log the completed 2-min bar."""
        if self.current_bar_start is None:
            return

        year, month, day, hour, minute = self.current_bar_start
        date_str = f"{year:04d}-{month:02d}-{day:02d}"
        time_str = f"{hour:02d}:{minute:02d}"

        pos = "LONG" if self.trade_direction == 1 else ("SHORT" if self.trade_direction == -1 else "FLAT")

        self.bar_rows.append((
            date_str, time_str,
            self.bar_open, self.bar_high, self.bar_low, self.bar_close,
            self.bar_volume, self.estado, pos,
            self.daily_pnl, self.total_pnl, self.trades_today, self.last_decision
        ))

    def _end_day(self):
        """Finalize the day: force close if in trade, log daily result."""
        if self.trade_direction != 0:
            # Force close at last price
            self._force_close(self.bar_close, "CierreForzado")

        # Emit last bar if any
        self._emit_bar()

        # Daily log
        if self.last_reset_date:
            year, month, day = self.last_reset_date
            date_str = f"{year:04d}-{month:02d}-{day:02d}"
            self.daily_rows.append((date_str, self.daily_pnl, self.total_pnl, self.trades_today, self.rango_pts))

    def _process_tick(self, hour, minute, second, price):
        """Main state machine - process a single tick."""

        if self.estado == "DiaTerminado":
            return

        # Check after close
        after_close = (hour > self.hora_cierre_h or
                       (hour == self.hora_cierre_h and minute >= self.hora_cierre_m))

        if after_close:
            if self.trade_direction != 0:
                self._force_close(price, "CierreForzado")
            self.estado = "DiaTerminado"
            self.last_decision = "CIERRE_FORZADO"
            return

        # Before open
        before_open = hour < 9 or (hour == 9 and minute < 30)
        if before_open:
            return

        # ─── State: EsperandoRango ────────────────────────────────────────────
        if self.estado == "EsperandoRango":
            # C# checks: nyNow.Minute >= 32 && nyNow.Minute <= 40 using Time[0].
            # On a 2-min chart with OnEachTick, Time[0] = bar close timestamp.
            # Bar opening at 09:30 has Time[0] = 09:32. So C# "minute >= 32" means
            # ticks from 09:30:00 onward. "minute <= 40" means bars up to Time[0]=09:40,
            # which is the bar opening at 09:38 (ticks 09:38:00-09:39:59).
            # The transition happens at the first tick of the 09:40 bar.
            antes_ventana = hour < 9 or (hour == 9 and minute < 30)
            en_ventana = (hour == 9 and minute >= 30 and minute <= 39)

            if antes_ventana:
                self.last_decision = "ESPERANDO_0930"
                return

            if en_ventana:
                if price > self.rango_high:
                    self.rango_high = price
                if price < self.rango_low:
                    self.rango_low = price
                self.last_decision = f"ACUMULANDO_RANGO H={self.rango_high:.2f} L={self.rango_low:.2f}"
                return

            # Past window (minute >= 40) - transition
            if self.rango_high == -999999.0 or self.rango_low == 999999.0:
                self.estado = "DiaTerminado"
                self.last_decision = "RANGO_INVALIDO"
                return

            self.rango_pts = self.rango_high - self.rango_low
            self.stop_distance = self.rango_pts + self.colchon_stop

            self.estado = "OrdenesPuestas"
            self.last_decision = f"ORDENES_PUESTAS H={self.rango_high:.2f} L={self.rango_low:.2f} pts={self.rango_pts:.2f} stop={self.stop_distance:.2f}"

            if self.verbose:
                print(f"  [09:{minute:02d}:{second:02d}] Rango formado: H={self.rango_high:.2f} L={self.rango_low:.2f} pts={self.rango_pts:.2f} stopDist={self.stop_distance:.2f}")

            # IMPORTANT: After transition, check if this same tick triggers a breakout
            # (C# continues processing in the same OnBarUpdate call)
            self._check_ordenes(hour, minute, second, price)
            return

        # ─── State: OrdenesPuestas ────────────────────────────────────────────
        if self.estado == "OrdenesPuestas":
            self._check_ordenes(hour, minute, second, price)
            return

        # ─── State: EnTrade ───────────────────────────────────────────────────
        if self.estado == "EnTrade":
            self._monitor_trade(hour, minute, second, price)
            return

    def _check_ordenes(self, hour, minute, second, price):
        """OrdenesPuestas state: check for entry signals."""

        if self.trades_today >= self.max_trades:
            self.estado = "DiaTerminado"
            self.last_decision = "DIA_TERMINADO_MAX_TRADES"
            return

        # Cooldown check (50 seconds)
        if self.cooldown_activo:
            if self.cooldown_start_time is not None:
                # Calculate elapsed seconds
                cd_h, cd_m, cd_s = self.cooldown_start_time
                current_secs = hour * 3600 + minute * 60 + second
                cd_secs = cd_h * 3600 + cd_m * 60 + cd_s
                elapsed = current_secs - cd_secs
                if elapsed < 0:
                    elapsed += 86400  # day wrap (shouldn't happen within session)
                if elapsed < 50:
                    self.last_decision = f"COOLDOWN {elapsed:.0f}s/50s"
                    return
            self.cooldown_activo = False

        # Pending flip (immediate entry after stop loss)
        if self.pending_flip:
            self.pending_flip = False
            direction = self.pending_flip_direction
            self._enter_trade(direction, price, hour, minute, second, "FLIP")
            return

        # Re-entry: wait for price to return inside range
        if self.trades_today > 0 and not self.reentry_price_in_range:
            if price > self.rango_low and price < self.rango_high:
                self.reentry_price_in_range = True
            else:
                self.last_decision = f"REENTRY_ESPERA_RANGO H={self.rango_high:.2f} L={self.rango_low:.2f}"
                return

        # Breakout detection (C# uses Close[0] which is current tick price)
        if price >= self.rango_high:
            self._enter_trade(1, price, hour, minute, second, "BREAKOUT")
            return

        if price <= self.rango_low:
            self._enter_trade(-1, price, hour, minute, second, "BREAKOUT")
            return

        self.last_decision = f"ESPERANDO_RUPTURA H={self.rango_high:.2f} L={self.rango_low:.2f}"

    def _enter_trade(self, direction, price, hour, minute, second, reason):
        """Enter a new trade."""
        self.trade_direction = direction
        self.entry_price = price
        self.long_usado = True
        self.short_usado = True
        self.trades_today += 1
        self.breakeven_hit = False
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1

        # Set stops and targets
        if direction == 1:
            self.tp1_stop = self.rango_low - self.colchon_stop
            self.tp2_stop = self.rango_low - self.colchon_stop
            self.tp1_target = price + self.stop_distance
            self.tp2_target = price + self.stop_distance * 2
        else:
            self.tp1_stop = self.rango_high + self.colchon_stop
            self.tp2_stop = self.rango_high + self.colchon_stop
            self.tp1_target = price - self.stop_distance
            self.tp2_target = price - self.stop_distance * 2

        self.qty_tp1_active = self.qty_tp1 > 0
        self.qty_tp2_active = self.qty_tp2 > 0

        self.estado = "EnTrade"
        dir_str = "LONG" if direction == 1 else "SHORT"
        self.last_decision = f"ENTRY_{dir_str} @{price:.2f} stop={self.tp1_stop:.2f}"

        if self.verbose:
            print(f"  [{hour:02d}:{minute:02d}:{second:02d}] ENTRY {dir_str} @{price:.2f} (reason={reason}) stop={self.tp1_stop:.2f} tp1={self.tp1_target:.2f} tp2={self.tp2_target:.2f}")

        # Log entry rows (one per active leg)
        date_str = f"{self.last_reset_date[0]:04d}-{self.last_reset_date[1]:02d}-{self.last_reset_date[2]:02d}"
        time_str = f"{hour:02d}:{minute:02d}"

        if self.qty_tp1 > 0:
            self.trade_rows.append((
                date_str, time_str, "ENTRY", dir_str, price, 0.0,
                self.stop_distance, self.rango_high, self.rango_low, self.rango_pts,
                0.0, self.daily_pnl, self.total_pnl, f"TP1{dir_str}", self.trades_today
            ))
        if self.qty_tp2 > 0:
            self.trade_rows.append((
                date_str, time_str, "ENTRY", dir_str, price, 0.0,
                self.stop_distance, self.rango_high, self.rango_low, self.rango_pts,
                0.0, self.daily_pnl, self.total_pnl, f"TP2{dir_str}", self.trades_today
            ))

    def _monitor_trade(self, hour, minute, second, price):
        """EnTrade state: check stops, targets, trailing on each tick."""
        d = self.trade_direction
        entry = self.entry_price
        sd = self.stop_distance

        # Unrealized points
        unrealized = (price - entry) if d == 1 else (entry - price)

        # ─── Check TP targets hit ─────────────────────────────────────────────
        tp1_target_hit = False
        tp2_target_hit = False

        if self.qty_tp1_active:
            if d == 1 and price >= self.tp1_target:
                tp1_target_hit = True
            elif d == -1 and price <= self.tp1_target:
                tp1_target_hit = True

        if self.qty_tp2_active:
            if d == 1 and price >= self.tp2_target:
                tp2_target_hit = True
            elif d == -1 and price <= self.tp2_target:
                tp2_target_hit = True

        # ─── Check stops hit ──────────────────────────────────────────────────
        tp1_stopped = False
        tp2_stopped = False

        if self.qty_tp1_active:
            if d == 1 and price <= self.tp1_stop:
                tp1_stopped = True
            elif d == -1 and price >= self.tp1_stop:
                tp1_stopped = True

        if self.qty_tp2_active:
            if d == 1 and price <= self.tp2_stop:
                tp2_stopped = True
            elif d == -1 and price >= self.tp2_stop:
                tp2_stopped = True

        # ─── Process TP target hits ───────────────────────────────────────────
        pnl = 0.0
        exit_reasons = []

        if tp1_target_hit:
            tp1_pnl = sd * POINT_VALUE * self.qty_tp1
            pnl += tp1_pnl
            self.qty_tp1_active = False
            exit_reasons.append(("TP1", "TakeProfit", tp1_pnl, self.tp1_target))

        if tp2_target_hit:
            tp2_pnl = sd * 2 * POINT_VALUE * self.qty_tp2
            pnl += tp2_pnl
            self.qty_tp2_active = False
            exit_reasons.append(("TP2", "TakeProfit", tp2_pnl, self.tp2_target))

        # ─── Process stop hits (only if not already hit target) ───────────────
        if tp1_stopped and not tp1_target_hit:
            stop_price = self.tp1_stop
            if self.breakeven_hit:
                tp1_pnl = ((stop_price - entry) * POINT_VALUE * self.qty_tp1) if d == 1 else ((entry - stop_price) * POINT_VALUE * self.qty_tp1)
            else:
                tp1_pnl = -sd * POINT_VALUE * self.qty_tp1
            pnl += tp1_pnl
            self.qty_tp1_active = False
            reason = "Breakeven" if self.breakeven_hit else "StopLoss"
            exit_reasons.append(("TP1", reason, tp1_pnl, stop_price))

        if tp2_stopped and not tp2_target_hit:
            stop_price = self.tp2_stop
            if self.breakeven_hit:
                tp2_pnl = ((stop_price - entry) * POINT_VALUE * self.qty_tp2) if d == 1 else ((entry - stop_price) * POINT_VALUE * self.qty_tp2)
            else:
                tp2_pnl = -sd * POINT_VALUE * self.qty_tp2
            pnl += tp2_pnl
            self.qty_tp2_active = False
            reason = "Breakeven" if self.breakeven_hit else "StopLoss"
            exit_reasons.append(("TP2", reason, tp2_pnl, stop_price))

        # ─── If any exits happened ────────────────────────────────────────────
        if exit_reasons:
            # Log each exit
            date_str = f"{self.last_reset_date[0]:04d}-{self.last_reset_date[1]:02d}-{self.last_reset_date[2]:02d}"
            time_str = f"{hour:02d}:{minute:02d}"
            dir_str = "LONG" if d == 1 else "SHORT"

            for leg, reason, leg_pnl, exit_price in exit_reasons:
                self.daily_pnl += leg_pnl
                self.total_pnl += leg_pnl

                self.trade_rows.append((
                    date_str, time_str, "EXIT", dir_str, self.entry_price, exit_price,
                    self.stop_distance, self.rango_high, self.rango_low, self.rango_pts,
                    leg_pnl, self.daily_pnl, self.total_pnl, reason, self.trades_today
                ))

                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] EXIT {leg} {reason} @{exit_price:.2f} pnl=${leg_pnl:.2f} (daily=${self.daily_pnl:.2f})")

        # ─── Check if trade is fully closed ───────────────────────────────────
        if not self.qty_tp1_active and not self.qty_tp2_active:
            # Determine overall exit reason
            reasons_set = set(r[1] for r in exit_reasons)
            if "TakeProfit" in reasons_set:
                overall_reason = "TakeProfit"
            elif "StopLoss" in reasons_set:
                overall_reason = "StopLoss"
            else:
                overall_reason = "Breakeven"

            self._post_trade_transition(overall_reason, hour, minute, second)
            return

        # ─── If partially exited, continue with remaining ─────────────────────
        if exit_reasons:
            return  # Already processed partial exit, continue monitoring next tick

        # ─── Trailing logic (only if no exits this tick) ──────────────────────

        # Breakeven at 60%
        if not self.breakeven_hit and unrealized >= sd * 0.60:
            be_stop = round_to_tick(entry + 5) if d == 1 else round_to_tick(entry - 5)
            if self.qty_tp1_active:
                self.tp1_stop = be_stop
            if self.qty_tp2_active:
                self.tp2_stop = be_stop
            self.breakeven_hit = True
            self.last_decision = f"BREAKEVEN stop={be_stop:.2f}"
            if self.verbose:
                print(f"  [{hour:02d}:{minute:02d}:{second:02d}] BREAKEVEN activated @{be_stop:.2f} (unrealized={unrealized:.2f}, threshold={sd * 0.60:.2f})")
            return

        # Trailing TP1: 75%->45%, 85%->60%, 95%->80%, 99%->95%
        if self.qty_tp1_active and self.breakeven_hit:
            if self.tp1_stop_nivel < 4 and unrealized >= sd * 0.99:
                self.tp1_stop = round_to_tick(entry + (sd * 0.95)) if d == 1 else round_to_tick(entry - (sd * 0.95))
                self.tp1_stop_nivel = 4
                self.last_decision = f"TP1_STOP95={self.tp1_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP1 trailing nivel 4: stop={self.tp1_stop:.2f}")
                return
            elif self.tp1_stop_nivel < 3 and unrealized >= sd * 0.95:
                self.tp1_stop = round_to_tick(entry + (sd * 0.80)) if d == 1 else round_to_tick(entry - (sd * 0.80))
                self.tp1_stop_nivel = 3
                self.last_decision = f"TP1_STOP80={self.tp1_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP1 trailing nivel 3: stop={self.tp1_stop:.2f}")
                return
            elif self.tp1_stop_nivel < 2 and unrealized >= sd * 0.85:
                self.tp1_stop = round_to_tick(entry + (sd * 0.60)) if d == 1 else round_to_tick(entry - (sd * 0.60))
                self.tp1_stop_nivel = 2
                self.last_decision = f"TP1_STOP60={self.tp1_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP1 trailing nivel 2: stop={self.tp1_stop:.2f}")
                return
            elif self.tp1_stop_nivel < 1 and unrealized >= sd * 0.75:
                self.tp1_stop = round_to_tick(entry + (sd * 0.45)) if d == 1 else round_to_tick(entry - (sd * 0.45))
                self.tp1_stop_nivel = 1
                self.last_decision = f"TP1_STOP45={self.tp1_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP1 trailing nivel 1: stop={self.tp1_stop:.2f}")
                return

        # Trailing TP2: 50%->25%, 70%->50%, 85%->60%, 90%->75%, 95%->84%, 98%->94%
        if self.qty_tp2_active and self.breakeven_hit:
            tp2_target_dist = sd * 2

            if self.tp2_stop_nivel < 6 and unrealized >= tp2_target_dist * 0.98:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.94)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.94))
                self.tp2_stop_nivel = 6
                self.last_decision = f"TP2_STOP94={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 6: stop={self.tp2_stop:.2f}")
                return
            elif self.tp2_stop_nivel < 5 and unrealized >= tp2_target_dist * 0.95:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.84)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.84))
                self.tp2_stop_nivel = 5
                self.last_decision = f"TP2_STOP84={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 5: stop={self.tp2_stop:.2f}")
                return
            elif self.tp2_stop_nivel < 4 and unrealized >= tp2_target_dist * 0.90:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.75)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.75))
                self.tp2_stop_nivel = 4
                self.last_decision = f"TP2_STOP75={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 4: stop={self.tp2_stop:.2f}")
                return
            elif self.tp2_stop_nivel < 3 and unrealized >= tp2_target_dist * 0.85:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.60)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.60))
                self.tp2_stop_nivel = 3
                self.last_decision = f"TP2_STOP60={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 3: stop={self.tp2_stop:.2f}")
                return
            elif self.tp2_stop_nivel < 2 and unrealized >= tp2_target_dist * 0.70:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.50)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.50))
                self.tp2_stop_nivel = 2
                self.last_decision = f"TP2_STOP50={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 2: stop={self.tp2_stop:.2f}")
                return
            elif self.tp2_stop_nivel < 1 and unrealized >= tp2_target_dist * 0.50:
                self.tp2_stop = round_to_tick(entry + (tp2_target_dist * 0.25)) if d == 1 else round_to_tick(entry - (tp2_target_dist * 0.25))
                self.tp2_stop_nivel = 1
                self.last_decision = f"TP2_STOP25={self.tp2_stop:.2f}"
                if self.verbose:
                    print(f"  [{hour:02d}:{minute:02d}:{second:02d}] TP2 trailing nivel 1: stop={self.tp2_stop:.2f}")
                return

    def _post_trade_transition(self, exit_reason, hour, minute, second):
        """Handle state transition after a trade is fully closed."""
        prev_direction = self.trade_direction
        self.trade_direction = 0
        self.entry_price = 0
        self.breakeven_hit = False
        self.tp1_stop_nivel = 0
        self.tp2_stop_nivel = -1

        if exit_reason == "TakeProfit":
            self.take_profits_hoy += 1
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
                self.cooldown_start_time = (hour, minute, second)
                self.estado = "OrdenesPuestas"
                self.last_decision = f"POST_TP_COOLDOWN_50s"

        elif exit_reason == "StopLoss":
            self.stops_puros += 1
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

        else:  # Breakeven
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
                self.cooldown_start_time = (hour, minute, second)
                self.estado = "OrdenesPuestas"
                self.last_decision = f"COOLDOWN_50s"

    def _force_close(self, price, reason):
        """Force close remaining positions at given price."""
        if self.trade_direction == 0:
            return

        d = self.trade_direction
        entry = self.entry_price
        pnl = 0.0

        date_str = f"{self.last_reset_date[0]:04d}-{self.last_reset_date[1]:02d}-{self.last_reset_date[2]:02d}"
        dir_str = "LONG" if d == 1 else "SHORT"

        if self.qty_tp1_active:
            tp1_pnl = ((price - entry) * POINT_VALUE * self.qty_tp1) if d == 1 else ((entry - price) * POINT_VALUE * self.qty_tp1)
            pnl += tp1_pnl
            self.daily_pnl += tp1_pnl
            self.total_pnl += tp1_pnl
            self.qty_tp1_active = False

            self.trade_rows.append((
                date_str, "", "EXIT", dir_str, entry, price,
                self.stop_distance, self.rango_high, self.rango_low, self.rango_pts,
                tp1_pnl, self.daily_pnl, self.total_pnl, reason, self.trades_today
            ))

        if self.qty_tp2_active:
            tp2_pnl = ((price - entry) * POINT_VALUE * self.qty_tp2) if d == 1 else ((entry - price) * POINT_VALUE * self.qty_tp2)
            pnl += tp2_pnl
            self.daily_pnl += tp2_pnl
            self.total_pnl += tp2_pnl
            self.qty_tp2_active = False

            self.trade_rows.append((
                date_str, "", "EXIT", dir_str, entry, price,
                self.stop_distance, self.rango_high, self.rango_low, self.rango_pts,
                tp2_pnl, self.daily_pnl, self.total_pnl, reason, self.trades_today
            ))

        self.trade_direction = 0

        if self.verbose:
            print(f"  FORCE_CLOSE {reason} @{price:.2f} pnl=${pnl:.2f}")

    def _write_csvs(self):
        """Write output CSV files with Spanish locale (decimal comma)."""
        os.makedirs(self.output_dir, exist_ok=True)

        def fmt_price(v):
            """Format price with comma decimal separator."""
            return f"{v:.2f}".replace('.', ',')

        def fmt_pnl(v):
            """Format PnL with comma decimal separator."""
            return f"{v:.2f}".replace('.', ',')

        # trades_log.csv
        trades_path = os.path.join(self.output_dir, "mnq10minv2_trades_log.csv")
        with open(trades_path, "w", encoding="utf-8") as f:
            f.write("Date,Time,Action,Direction,EntryPrice,ExitPrice,StopDist,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday\n")
            for row in self.trade_rows:
                date, time, action, direction, entry_p, exit_p, stop_d, rh, rl, rp, pnl_v, daily, total, reason, trades = row
                line = f"{date},{time},{action},{direction},{fmt_price(entry_p)},{fmt_price(exit_p)},{fmt_price(stop_d)},{fmt_price(rh)},{fmt_price(rl)},{fmt_price(rp)},{fmt_pnl(pnl_v)},{fmt_pnl(daily)},{fmt_pnl(total)},{reason},{trades}\n"
                f.write(line)
        print(f"  mnq10minv2_trades_log.csv: {len(self.trade_rows)} rows")

        # bar_log.csv
        bar_path = os.path.join(self.output_dir, "mnq10minv2_bar_log.csv")
        with open(bar_path, "w", encoding="utf-8") as f:
            f.write("Date,Time,Open,High,Low,Close,Volume,Estado,Position,DailyPnL,TotalPnL,TradesToday,Decision\n")
            for row in self.bar_rows:
                date, time, o, h, l, c, vol, estado, pos, daily, total, trades, decision = row
                line = f"{date},{time},{fmt_price(o)},{fmt_price(h)},{fmt_price(l)},{fmt_price(c)},{vol},{estado},{pos},{fmt_pnl(daily)},{fmt_pnl(total)},{trades},{decision}\n"
                f.write(line)
        print(f"  mnq10minv2_bar_log.csv: {len(self.bar_rows)} rows")

        # daily_log.csv
        daily_path = os.path.join(self.output_dir, "mnq10minv2_daily_log.csv")
        with open(daily_path, "w", encoding="utf-8") as f:
            f.write("Date,DailyPnL,TotalPnL,Trades,RangoPts\n")
            for row in self.daily_rows:
                date, daily, total, trades, rango = row
                line = f"{date},{fmt_pnl(daily)},{fmt_pnl(total)},{trades},{fmt_price(rango)}\n"
                f.write(line)
        print(f"  mnq10minv2_daily_log.csv: {len(self.daily_rows)} rows")

    def _print_summary(self):
        """Print summary to stdout."""
        print()
        print("=" * 80)
        print("RESUMEN")
        print("=" * 80)

        for row in self.daily_rows:
            date, daily, total, trades, rango = row
            print(f"  {date}: PnL=${daily:.2f} | Trades={trades} | Rango={rango:.2f}pts | Acum=${total:.2f}")

        print()
        total_days = len(self.daily_rows)
        total_trades = sum(r[3] for r in self.daily_rows)
        total_pnl = self.daily_rows[-1][2] if self.daily_rows else 0.0
        print(f"  Dias: {total_days}")
        print(f"  Total Trades: {total_trades}")
        print(f"  PnL Total: ${total_pnl:.2f}")

        if self.daily_rows:
            avg = sum(r[1] for r in self.daily_rows) / total_days
            print(f"  Promedio diario: ${avg:.2f}")

        print()
        print("Archivos generados en:", self.output_dir)


# ─── Main ─────────────────────────────────────────────────────────────────────

def main():
    args = parse_args()

    sim = TickSimulator(
        colchon_stop=args.colchon,
        max_trades=args.trades,
        micro_contratos=args.contratos,
        modo_tp=args.modo,
        hora_cierre=args.cierre,
        utc_offset=args.utc_offset,
        output_dir=args.output_dir,
        verbose=args.verbose
    )

    sim.run(args.file)


if __name__ == "__main__":
    main()
