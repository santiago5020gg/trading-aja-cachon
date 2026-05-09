"""
Backtester para MNQ10minV2 — simula la estrategia sobre datos de barras de 2 minutos.
Acepta tres formatos de entrada:
  1. bar_log CSV del bot (separador coma, decimales espanoles)
  2. Export .txt de NinjaTrader barras (separador ;, formato YYYYMMDD HHMMSS;O;H;L;C;V)
  3. Export .txt de NinjaTrader ticks (separador ;, formato YYYYMMDD HHMMSS us;Last;Bid;Ask;Vol)

Uso:
  python backtest_mnq10minv2.py [opciones] [archivo]

Opciones:
  --trades N         Max trades por dia (default: 2)
  --contratos N      Micro contratos (default: 2)
  --modo 1a1|1a2    Modo TP (default: 1a2)
  --utc-offset N    Horas a restar del archivo para obtener ET (default: 4 para .txt, 0 para .csv)
  --desde YYYY-MM-DD Fecha inicio
  --hasta YYYY-MM-DD Fecha fin
  -v, --verbose      Mostrar detalle por trade

Ejemplos:
  python backtest_mnq10minv2.py                           # usa bar_log.csv (ya en ET)
  python backtest_mnq10minv2.py "historicos test/MNQ.txt" # export NT barras (UTC)
  python backtest_mnq10minv2.py "historicos test/ticks.txt" # export NT ticks (UTC, auto-detecta)
  python backtest_mnq10minv2.py --utc-offset 5 file.txt  # offset manual
"""
import sys
import argparse
from datetime import datetime, timedelta
from dataclasses import dataclass, field
from typing import List, Optional


def parse_args():
    parser = argparse.ArgumentParser(description="Backtester MNQ10minV2")
    parser.add_argument("--trades", type=int, default=2, help="Max trades por dia (default: 2)")
    parser.add_argument("--contratos", type=int, default=2, help="Micro contratos (default: 2)")
    parser.add_argument("--modo", choices=["1a1", "1a2"], default="1a2", help="Modo TP: 1a1 o 1a2 (default: 1a2)")
    parser.add_argument("--utc-offset", type=int, default=None, help="Horas a restar para convertir a ET (default: 4 para .txt, 0 para .csv)")
    parser.add_argument("--desde", type=str, default=None, help="Fecha inicio YYYY-MM-DD (inclusive)")
    parser.add_argument("--hasta", type=str, default=None, help="Fecha fin YYYY-MM-DD (inclusive)")
    parser.add_argument("--2min", dest="already_2min", action="store_true", help="Archivo .txt ya tiene barras de 2min (no agregar)")
    parser.add_argument("-v", "--verbose", action="store_true", help="Detalle por trade")
    parser.add_argument("file", nargs="?", default=r"bot\history\mnq10minv2_bar_log.csv", help="Archivo de barras (.csv o .txt)")
    return parser.parse_args()


# ─── Parametros de la estrategia ───────────────────────────────────────────────
COLCHON_STOP = 5
HORA_CIERRE_H = 15
HORA_CIERRE_M = 50


def compute_params(max_trades: int, micro_contratos: int, modo_tp: str):
    max_stops_puros = round(max_trades / 2)
    max_take_profits = round(max_trades / 2)
    max_breakevens = round(max_trades / 2)

    if modo_tp == "1a1":
        qty_tp1 = micro_contratos
        qty_tp2 = 0
    else:
        if micro_contratos >= 3:
            qty_tp1 = micro_contratos - 1
            qty_tp2 = 1
        elif micro_contratos == 2:
            qty_tp1 = 1
            qty_tp2 = 1
        else:
            qty_tp1 = 0
            qty_tp2 = 1

    return max_stops_puros, max_take_profits, max_breakevens, qty_tp1, qty_tp2


@dataclass
class Trade:
    direction: int  # 1=long, -1=short
    entry_price: float = 0.0
    tp1_stop: float = 0.0
    tp2_stop: float = 0.0
    tp1_active: bool = True
    tp2_active: bool = True
    breakeven_hit: bool = False
    tp1_stop_nivel: int = 0
    tp2_stop_nivel: int = -1  # C# initializes to -1
    stop_distance: float = 0.0
    tp1_target: float = 0.0
    tp2_target: float = 0.0


@dataclass
class DayResult:
    date: str = ""
    rango_high: float = 0.0
    rango_low: float = 0.0
    rango_pts: float = 0.0
    trades: int = 0
    stops: int = 0
    breakevens: int = 0
    take_profits: int = 0
    pnl: float = 0.0
    details: List[str] = field(default_factory=list)


def parse_bar_line_csv(line: str):
    """Parse bot bar_log CSV: Date,Time,O(int),O(dec),H(int),H(dec),L(int),L(dec),C(int),C(dec),Vol,..."""
    parts = line.strip().split(",")
    if len(parts) < 19:
        return None
    date = parts[0]
    time = parts[1]
    o = float(parts[2] + "." + parts[3])
    h = float(parts[4] + "." + parts[5])
    l = float(parts[6] + "." + parts[7])
    c = float(parts[8] + "." + parts[9])
    vol = int(parts[10])
    return date, time, o, h, l, c, vol


def load_bars_csv(filepath: str):
    bars = []
    with open(filepath, "r") as f:
        for line in f:
            parsed = parse_bar_line_csv(line)
            if parsed:
                bars.append(parsed)
    return bars


def load_bars_txt(filepath: str, utc_offset: int = 4):
    """Load NinjaTrader .txt export: YYYYMMDD HHMMSS;O;H;L;C;V (1-min bars in UTC)
    Converts to ET by subtracting utc_offset hours, then aggregates to 2-min bars
    aligned on even minutes (00:00, 00:02, 00:04...) to match NinjaTrader bar alignment."""
    bars_1min = []
    with open(filepath, "r") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(";")
            if len(parts) < 6:
                continue
            dt_str = parts[0]
            date_part = dt_str[:8]
            time_part = dt_str[9:] if len(dt_str) > 9 else dt_str[8:]

            year = int(date_part[:4])
            month = int(date_part[4:6])
            day = int(date_part[6:8])
            hour = int(time_part[:2])
            minute = int(time_part[2:4])

            dt_utc = datetime(year, month, day, hour, minute)
            dt_et = dt_utc - timedelta(hours=utc_offset)

            date = dt_et.strftime("%Y-%m-%d")
            time_str = dt_et.strftime("%H:%M")
            et_minute = dt_et.minute

            o = float(parts[1])
            h = float(parts[2])
            l = float(parts[3])
            c = float(parts[4])
            v = int(parts[5])

            bars_1min.append((date, time_str, et_minute, o, h, l, c, v))

    # Aggregate to 2-min bars aligned on EVEN minutes (like NinjaTrader)
    bars_2min = []
    i = 0
    while i < len(bars_1min):
        b1 = bars_1min[i]
        b1_minute = b1[2]

        # If this bar starts on an even minute and next bar is the odd pair
        if b1_minute % 2 == 0 and i + 1 < len(bars_1min):
            b2 = bars_1min[i + 1]
            if b1[0] == b2[0]:  # same date
                date = b1[0]
                time_str = b1[1]
                o = b1[3]
                h = max(b1[4], b2[4])
                l = min(b1[5], b2[5])
                c = b2[6]
                v = b1[7] + b2[7]
                bars_2min.append((date, time_str, o, h, l, c, v))
                i += 2
                continue

        # Odd-starting bar or unpaired: skip to next even alignment
        # If bar is on odd minute, it means we're misaligned - include as single bar
        if b1_minute % 2 != 0:
            # This odd bar doesn't have a preceding even partner, include solo
            bars_2min.append((b1[0], b1[1], b1[3], b1[4], b1[5], b1[6], b1[7]))
        else:
            # Even bar without a pair (end of day)
            bars_2min.append((b1[0], b1[1], b1[3], b1[4], b1[5], b1[6], b1[7]))
        i += 1

    return bars_2min


def detect_tick_file(filepath: str) -> bool:
    """Detect if a .txt file is tick data vs 1-min bars by checking the first data line.
    Tick format has microseconds in the datetime field: YYYYMMDD HHMMSS uuuuuu;...
    Bar format: YYYYMMDD HHMMSS;..."""
    with open(filepath, "r") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(";")
            dt_field = parts[0]
            # Tick data has a space-separated microsecond field: "YYYYMMDD HHMMSS uuuuuu"
            dt_parts = dt_field.split()
            if len(dt_parts) >= 3:
                return True
            return False
    return False


def load_bars_tick(filepath: str, utc_offset: int = 4):
    """Load NinjaTrader tick export: YYYYMMDD HHMMSS microseconds;Last;Bid;Ask;Volume (UTC)
    Builds 2-min bars aligned on even minutes from tick-by-tick data."""
    from collections import defaultdict

    bars_raw = defaultdict(list)  # key: (date_et, HH:MM_even) -> list of Last prices + volumes

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

            # Align to 2-min bar on even minute
            bar_minute = dt_et.minute if dt_et.minute % 2 == 0 else dt_et.minute - 1
            bar_key = (dt_et.strftime("%Y-%m-%d"), f"{dt_et.hour:02d}:{bar_minute:02d}")

            last_price = float(parts[1])
            volume = int(parts[4])

            bars_raw[bar_key].append((last_price, volume))

    # Build OHLCV bars
    bars_2min = []
    for key in sorted(bars_raw.keys()):
        ticks = bars_raw[key]
        date, time_str = key
        o = ticks[0][0]
        c = ticks[-1][0]
        h = max(t[0] for t in ticks)
        l = min(t[0] for t in ticks)
        v = sum(t[1] for t in ticks)
        bars_2min.append((date, time_str, o, h, l, c, v))

    return bars_2min


def load_bars_txt_direct(filepath: str, utc_offset: int = 0):
    """Load .txt bars directly without aggregation (already 2-min bars)."""
    bars = []
    with open(filepath, "r") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(";")
            if len(parts) < 6:
                continue
            dt_str = parts[0]
            date_part = dt_str[:8]
            time_part = dt_str[9:] if len(dt_str) > 9 else dt_str[8:]

            year = int(date_part[:4])
            month = int(date_part[4:6])
            day = int(date_part[6:8])
            hour = int(time_part[:2])
            minute = int(time_part[2:4])

            dt = datetime(year, month, day, hour, minute)
            if utc_offset:
                dt = dt - timedelta(hours=utc_offset)

            date = dt.strftime("%Y-%m-%d")
            time_str = dt.strftime("%H:%M")

            o = float(parts[1])
            h = float(parts[2])
            l = float(parts[3])
            c = float(parts[4])
            v = int(parts[5])

            bars.append((date, time_str, o, h, l, c, v))
    return bars


def load_bars(filepath: str, utc_offset: int = 0, already_2min: bool = False):
    """Auto-detect format and load bars."""
    if filepath.lower().endswith(".txt"):
        if detect_tick_file(filepath):
            print("  Formato detectado: TICKS (construyendo barras 2min desde ticks)")
            return load_bars_tick(filepath, utc_offset)
        elif already_2min:
            print("  Formato detectado: BARRAS 2-min (carga directa)")
            return load_bars_txt_direct(filepath, utc_offset)
        else:
            print("  Formato detectado: BARRAS 1-min (agregando a 2min)")
            return load_bars_txt(filepath, utc_offset)
    else:
        return load_bars_csv(filepath)


def simulate_day(day_bars, max_trades, max_stops_puros, max_take_profits, max_breakevens, qty_tp1, qty_tp2) -> Optional[DayResult]:
    result = DayResult(date=day_bars[0][0])

    state = "esperando_rango"
    rango_high = -999999.0
    rango_low = 999999.0
    rango_pts = 0.0
    stop_distance = 0.0

    trade: Optional[Trade] = None
    trades_today = 0
    stops_puros = 0
    take_profits_hoy = 0
    breakevens_hoy = 0
    long_usado = False
    short_usado = False
    reentry_price_in_range = False
    cooldown_activo = False
    cooldown_start_idx = -1
    pending_flip = False
    pending_flip_direction = 0
    daily_pnl = 0.0

    for bar_idx, bar in enumerate(day_bars):
        date, time_str, o, h, l, c, vol = bar
        hour = int(time_str.split(":")[0])
        minute = int(time_str.split(":")[1])

        if state == "dia_terminado":
            break

        before_open = hour < 9 or (hour == 9 and minute < 30)
        after_close = hour > HORA_CIERRE_H or (hour == HORA_CIERRE_H and minute >= HORA_CIERRE_M)

        if after_close:
            if trade:
                pnl = close_trade(trade, c, "CierreForzado", qty_tp1, qty_tp2)
                daily_pnl += pnl
                result.details.append(f"  CIERRE_FORZADO @{c:.2f} pnl=${pnl:.2f}")
                trade = None
            state = "dia_terminado"
            break

        if before_open:
            continue

        # ─── Estado: EsperandoRango ────────────────────────────────────────────
        # NinjaTrader uses bar close time for rango window (09:32-09:40).
        # Since our bars are keyed by OPEN time, the equivalent bars are 09:30-09:38.
        if state == "esperando_rango":
            en_ventana = hour == 9 and 30 <= minute <= 38
            if before_open:
                continue
            if en_ventana:
                if h > rango_high:
                    rango_high = h
                if l < rango_low:
                    rango_low = l
                continue
            # Ventana terminada (first bar after 09:38, i.e. 09:40+)
            if rango_high == -999999.0 or rango_low == 999999.0:
                state = "dia_terminado"
                break
            rango_pts = rango_high - rango_low
            stop_distance = rango_pts + COLCHON_STOP
            result.rango_high = rango_high
            result.rango_low = rango_low
            result.rango_pts = rango_pts
            state = "ordenes_puestas"

        # ─── Estado: OrdenesPuestas ────────────────────────────────────────────
        if state == "ordenes_puestas":
            if trades_today >= max_trades:
                state = "dia_terminado"
                break

            if cooldown_activo:
                bars_elapsed = bar_idx - cooldown_start_idx
                seconds_elapsed = bars_elapsed * 120  # 2-min bars
                if seconds_elapsed < 50:
                    continue
                cooldown_activo = False

            if pending_flip:
                pending_flip = False
                direction = pending_flip_direction
                entry_price = c
                trade = create_trade(direction, entry_price, stop_distance, rango_high, rango_low, qty_tp1, qty_tp2)
                trades_today += 1
                state = "en_trade"
                result.details.append(f"  FLIP {'LONG' if direction == 1 else 'SHORT'} @{entry_price:.2f}")
                continue

            # Re-entry: wait for price to return inside range (C# uses Close[0])
            if trades_today > 0 and not reentry_price_in_range:
                if rango_low < c < rango_high:
                    reentry_price_in_range = True
                else:
                    continue

            # C# OnEachTick: Close[0] is current tick price. EnterLong is market order.
            # On 2-min bars: if H crosses rangoHigh, the fill ~ rangoHigh (first touch).
            # If price opened above rangoHigh, fill ~ open (already past level).
            if h >= rango_high:
                direction = 1
                entry_price = max(rango_high, o) if o <= rango_high else o
                trade = create_trade(direction, entry_price, stop_distance, rango_high, rango_low, qty_tp1, qty_tp2)
                trades_today += 1
                state = "en_trade"
                result.details.append(f"  ENTRY LONG @{entry_price:.2f} stop={entry_price - stop_distance:.2f}")
            elif l <= rango_low:
                direction = -1
                entry_price = min(rango_low, o) if o >= rango_low else o
                trade = create_trade(direction, entry_price, stop_distance, rango_high, rango_low, qty_tp1, qty_tp2)
                trades_today += 1
                state = "en_trade"
                result.details.append(f"  ENTRY SHORT @{entry_price:.2f} stop={entry_price + stop_distance:.2f}")

        # ─── Estado: EnTrade ───────────────────────────────────────────────────
        if state == "en_trade" and trade:
            exit_reason, pnl = monitor_trade(trade, h, l, c, qty_tp1, qty_tp2)

            if exit_reason:
                daily_pnl += pnl
                prev_direction = trade.direction
                result.details.append(f"  EXIT {exit_reason} pnl=${pnl:.2f}")
                trade = None

                if exit_reason == "StopLoss":
                    stops_puros += 1
                    if stops_puros >= max_stops_puros:
                        state = "dia_terminado"
                    elif trades_today >= max_trades:
                        state = "dia_terminado"
                    else:
                        pending_flip = True
                        pending_flip_direction = -1 if prev_direction == 1 else 1
                        state = "ordenes_puestas"

                elif exit_reason == "TakeProfit":
                    take_profits_hoy += 1
                    if take_profits_hoy >= max_take_profits:
                        state = "dia_terminado"
                    elif trades_today >= max_trades:
                        state = "dia_terminado"
                    else:
                        reentry_price_in_range = False
                        cooldown_activo = True
                        cooldown_start_idx = bar_idx
                        state = "ordenes_puestas"

                elif exit_reason == "Breakeven":
                    breakevens_hoy += 1
                    if breakevens_hoy >= max_breakevens:
                        state = "dia_terminado"
                    elif trades_today >= max_trades:
                        state = "dia_terminado"
                    else:
                        reentry_price_in_range = False
                        cooldown_activo = True
                        cooldown_start_idx = bar_idx
                        state = "ordenes_puestas"

    result.trades = trades_today
    result.stops = stops_puros
    result.breakevens = breakevens_hoy
    result.take_profits = take_profits_hoy
    result.pnl = daily_pnl
    return result


def create_trade(direction: int, entry_price: float, stop_distance: float,
                 rango_high: float, rango_low: float, qty_tp1: int, qty_tp2: int) -> Trade:
    trade = Trade(direction=direction, entry_price=entry_price, stop_distance=stop_distance)

    if direction == 1:
        trade.tp1_stop = rango_low - COLCHON_STOP
        trade.tp2_stop = rango_low - COLCHON_STOP
        trade.tp1_target = entry_price + stop_distance
        trade.tp2_target = entry_price + stop_distance * 2
    else:
        trade.tp1_stop = rango_high + COLCHON_STOP
        trade.tp2_stop = rango_high + COLCHON_STOP
        trade.tp1_target = entry_price - stop_distance
        trade.tp2_target = entry_price - stop_distance * 2

    trade.tp1_active = qty_tp1 > 0
    trade.tp2_active = qty_tp2 > 0
    return trade


def monitor_trade(trade: Trade, high: float, low: float, close: float, qty_tp1: int, qty_tp2: int):
    """Returns (exit_reason, pnl) or (None, 0) if trade continues."""
    d = trade.direction
    entry = trade.entry_price
    sd = trade.stop_distance

    # Check stops hit by bar extremes
    if d == 1:
        price_for_stop = low
        unrealized = close - entry
    else:
        price_for_stop = high
        unrealized = entry - close

    # ─── Check TP1 target hit ──────────────────────────────────────────────
    tp1_hit = False
    if trade.tp1_active:
        if d == 1 and high >= trade.tp1_target:
            tp1_hit = True
        elif d == -1 and low <= trade.tp1_target:
            tp1_hit = True

    # ─── Check TP2 target hit ──────────────────────────────────────────────
    tp2_hit = False
    if trade.tp2_active:
        if d == 1 and high >= trade.tp2_target:
            tp2_hit = True
        elif d == -1 and low <= trade.tp2_target:
            tp2_hit = True

    # ─── Check stop hit ───────────────────────────────────────────────────
    tp1_stopped = False
    tp2_stopped = False
    if trade.tp1_active:
        if d == 1 and low <= trade.tp1_stop:
            tp1_stopped = True
        elif d == -1 and high >= trade.tp1_stop:
            tp1_stopped = True
    if trade.tp2_active:
        if d == 1 and low <= trade.tp2_stop:
            tp2_stopped = True
        elif d == -1 and high >= trade.tp2_stop:
            tp2_stopped = True

    # ─── Process TP hits ───────────────────────────────────────────────────
    pnl = 0.0

    if tp1_hit:
        tp1_pnl = sd * 2 * qty_tp1  # $2 per point per contract
        pnl += tp1_pnl
        trade.tp1_active = False

    if tp2_hit:
        tp2_pnl = sd * 2 * 2 * qty_tp2  # 2x target
        pnl += tp2_pnl
        trade.tp2_active = False

    if tp1_hit or tp2_hit:
        if not trade.tp1_active and not trade.tp2_active:
            return "TakeProfit", pnl
        # Partial TP - continue with remaining
        if tp1_hit and trade.tp2_active:
            # TP1 hit, TP2 still running - already accounted for
            pass

    # ─── Process stop hits ─────────────────────────────────────────────────
    if tp1_stopped and not tp1_hit:
        stop_price = trade.tp1_stop
        if trade.breakeven_hit:
            tp1_pnl = (stop_price - entry) * 2 * qty_tp1 if d == 1 else (entry - stop_price) * 2 * qty_tp1
        else:
            tp1_pnl = -sd * 2 * qty_tp1
        pnl += tp1_pnl
        trade.tp1_active = False

    if tp2_stopped and not tp2_hit:
        stop_price = trade.tp2_stop
        if trade.breakeven_hit:
            tp2_pnl = (stop_price - entry) * 2 * qty_tp2 if d == 1 else (entry - stop_price) * 2 * qty_tp2
        else:
            tp2_pnl = -sd * 2 * qty_tp2
        pnl += tp2_pnl
        trade.tp2_active = False

    if (tp1_stopped or tp2_stopped) and not trade.tp1_active and not trade.tp2_active:
        if trade.breakeven_hit:
            return "Breakeven", pnl
        else:
            return "StopLoss", pnl

    # If one leg stopped and the other hit TP in same bar
    if not trade.tp1_active and not trade.tp2_active:
        if tp1_hit or tp2_hit:
            return "TakeProfit", pnl
        return "Breakeven" if trade.breakeven_hit else "StopLoss", pnl

    # ─── Trailing logic (only on close, after checking stops/targets) ──────
    if d == 1:
        unrealized = close - entry
    else:
        unrealized = entry - close

    # Breakeven at 60%
    if not trade.breakeven_hit and unrealized >= sd * 0.60:
        be_stop = entry + 5 if d == 1 else entry - 5
        if trade.tp1_active:
            trade.tp1_stop = be_stop
        if trade.tp2_active:
            trade.tp2_stop = be_stop
        trade.breakeven_hit = True

    # Trailing TP1: 75%→45%, 85%→60%, 95%→80%, 99%→95%
    if trade.tp1_active and trade.breakeven_hit:
        if trade.tp1_stop_nivel < 4 and unrealized >= sd * 0.99:
            trade.tp1_stop = entry + (sd * 0.95) if d == 1 else entry - (sd * 0.95)
            trade.tp1_stop_nivel = 4
        elif trade.tp1_stop_nivel < 3 and unrealized >= sd * 0.95:
            trade.tp1_stop = entry + (sd * 0.80) if d == 1 else entry - (sd * 0.80)
            trade.tp1_stop_nivel = 3
        elif trade.tp1_stop_nivel < 2 and unrealized >= sd * 0.85:
            trade.tp1_stop = entry + (sd * 0.60) if d == 1 else entry - (sd * 0.60)
            trade.tp1_stop_nivel = 2
        elif trade.tp1_stop_nivel < 1 and unrealized >= sd * 0.75:
            trade.tp1_stop = entry + (sd * 0.45) if d == 1 else entry - (sd * 0.45)
            trade.tp1_stop_nivel = 1

    # Trailing TP2: 50%→25%, 70%→50%, 85%→60%, 90%→75%, 95%→84%, 98%→94%
    if trade.tp2_active and trade.breakeven_hit:
        tp2_target_dist = sd * 2
        if trade.tp2_stop_nivel < 6 and unrealized >= tp2_target_dist * 0.98:
            trade.tp2_stop = entry + (tp2_target_dist * 0.94) if d == 1 else entry - (tp2_target_dist * 0.94)
            trade.tp2_stop_nivel = 6
        elif trade.tp2_stop_nivel < 5 and unrealized >= tp2_target_dist * 0.95:
            trade.tp2_stop = entry + (tp2_target_dist * 0.84) if d == 1 else entry - (tp2_target_dist * 0.84)
            trade.tp2_stop_nivel = 5
        elif trade.tp2_stop_nivel < 4 and unrealized >= tp2_target_dist * 0.90:
            trade.tp2_stop = entry + (tp2_target_dist * 0.75) if d == 1 else entry - (tp2_target_dist * 0.75)
            trade.tp2_stop_nivel = 4
        elif trade.tp2_stop_nivel < 3 and unrealized >= tp2_target_dist * 0.85:
            trade.tp2_stop = entry + (tp2_target_dist * 0.60) if d == 1 else entry - (tp2_target_dist * 0.60)
            trade.tp2_stop_nivel = 3
        elif trade.tp2_stop_nivel < 2 and unrealized >= tp2_target_dist * 0.70:
            trade.tp2_stop = entry + (tp2_target_dist * 0.50) if d == 1 else entry - (tp2_target_dist * 0.50)
            trade.tp2_stop_nivel = 2
        elif trade.tp2_stop_nivel < 1 and unrealized >= tp2_target_dist * 0.50:
            trade.tp2_stop = entry + (tp2_target_dist * 0.25) if d == 1 else entry - (tp2_target_dist * 0.25)
            trade.tp2_stop_nivel = 1

    return None, 0


def close_trade(trade: Trade, price: float, reason: str, qty_tp1: int, qty_tp2: int) -> float:
    """Force close remaining positions at given price."""
    d = trade.direction
    pnl = 0.0
    if trade.tp1_active:
        tp1_pnl = (price - trade.entry_price) * 2 * qty_tp1 if d == 1 else (trade.entry_price - price) * 2 * qty_tp1
        pnl += tp1_pnl
        trade.tp1_active = False
    if trade.tp2_active:
        tp2_pnl = (price - trade.entry_price) * 2 * qty_tp2 if d == 1 else (trade.entry_price - price) * 2 * qty_tp2
        pnl += tp2_pnl
        trade.tp2_active = False
    return pnl


def main():
    args = parse_args()

    max_trades = args.trades
    micro_contratos = args.contratos
    modo_tp = args.modo
    filepath = args.file

    # Auto-detect UTC offset: 4 for .txt files (NinjaTrader export in UTC), 0 for .csv (already ET)
    if args.utc_offset is not None:
        utc_offset = args.utc_offset
    elif filepath.lower().endswith(".txt"):
        utc_offset = 4
    else:
        utc_offset = 0

    max_stops_puros, max_take_profits, max_breakevens, qty_tp1, qty_tp2 = compute_params(max_trades, micro_contratos, modo_tp)

    print(f"=== PARAMETROS ===")
    print(f"  Max Trades/Dia: {max_trades}")
    print(f"  Micro Contratos: {micro_contratos}")
    print(f"  Modo TP: {modo_tp} (TP1 x{qty_tp1}, TP2 x{qty_tp2})")
    print(f"  Max Stops: {max_stops_puros} | Max TPs: {max_take_profits} | Max BEs: {max_breakevens}")
    print(f"  Archivo: {filepath}")
    print(f"  UTC Offset: -{utc_offset}h {'(auto)' if args.utc_offset is None else '(manual)'}")
    print()

    bars = load_bars(filepath, utc_offset, args.already_2min)
    print(f"Total barras: {len(bars)}")

    # Group by date
    days = {}
    for bar in bars:
        date = bar[0]
        if date not in days:
            days[date] = []
        days[date].append(bar)

    # Filter by date range and session
    valid_days = {}
    for date, day_bars in sorted(days.items()):
        if args.desde and date < args.desde:
            continue
        if args.hasta and date > args.hasta:
            continue
        has_session = any(
            int(b[1].split(":")[0]) >= 9 and int(b[1].split(":")[1]) >= 30
            for b in day_bars
            if int(b[1].split(":")[0]) > 9 or (int(b[1].split(":")[0]) == 9 and int(b[1].split(":")[1]) >= 30)
        )
        if has_session:
            valid_days[date] = day_bars

    print(f"Dias con sesion regular: {len(valid_days)}")
    print()

    # Run simulation
    results = []
    for date in sorted(valid_days.keys()):
        result = simulate_day(valid_days[date], max_trades, max_stops_puros, max_take_profits, max_breakevens, qty_tp1, qty_tp2)
        if result:
            results.append(result)

    # Print results table
    print("=" * 100)
    print(f"{'Fecha':<12} {'Rango':<8} {'RgPts':<7} {'Trades':<7} {'Stops':<6} {'BEs':<5} {'TPs':<5} {'PnL':>10} {'Acum':>10}")
    print("=" * 100)

    total_pnl = 0.0
    total_trades = 0
    total_stops = 0
    total_bes = 0
    total_tps = 0
    wins = 0
    losses = 0

    for r in results:
        total_pnl += r.pnl
        total_trades += r.trades
        total_stops += r.stops
        total_bes += r.breakevens
        total_tps += r.take_profits
        if r.pnl > 0:
            wins += 1
        elif r.pnl < 0:
            losses += 1

        rango_str = f"{r.rango_high:.0f}-{r.rango_low:.0f}" if r.rango_high > 0 else "N/A"
        print(f"{r.date:<12} {rango_str:<8} {r.rango_pts:<7.1f} {r.trades:<7} {r.stops:<6} {r.breakevens:<5} {r.take_profits:<5} ${r.pnl:>9.2f} ${total_pnl:>9.2f}")

    print("=" * 100)
    print(f"\nRESUMEN:")
    print(f"  Dias operados: {len(results)}")
    print(f"  Total trades: {total_trades}")
    print(f"  Stops puros: {total_stops}")
    print(f"  Breakevens: {total_bes}")
    print(f"  Take Profits: {total_tps}")
    print(f"  Dias ganadores: {wins} | Dias perdedores: {losses} | Dias neutros: {len(results) - wins - losses}")
    print(f"  PnL Total: ${total_pnl:.2f}")
    print(f"  Promedio diario: ${total_pnl / len(results):.2f}" if results else "")

    # Print details if verbose
    if args.verbose:
        print("\n\nDETALLE POR DIA:")
        print("-" * 80)
        for r in results:
            print(f"\n{r.date} (PnL: ${r.pnl:.2f}, Rango: {r.rango_pts:.1f}pts)")
            for detail in r.details:
                print(detail)


if __name__ == "__main__":
    main()
