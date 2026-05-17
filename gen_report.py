import sys
import os
from datetime import datetime

MONTH_NAMES = ["", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"]

def parse_daily_csv(line):
    """Parse daily CSV line with comma as decimal separator.
    Knows which field positions are integers vs floats/strings.
    Integer positions (0-indexed logical): 3,5,8,9,10,11,12,13,14,15
    """
    INTEGER_POSITIONS = {3, 5, 8, 9, 10, 11, 12, 13, 14, 15}

    if len(line) < 10:
        return None
    date_str = line[:10]
    if len(line) < 11 or line[10] != ',':
        return None
    rest = line[11:].split(',')
    tokens = [date_str]
    idx = 0
    while idx < len(rest):
        logical_pos = len(tokens)
        if logical_pos in INTEGER_POSITIONS:
            tokens.append(rest[idx])
            idx += 1
        elif idx + 1 < len(rest) and len(rest[idx + 1]) == 2 and rest[idx + 1].isdigit():
            tokens.append(rest[idx] + '.' + rest[idx + 1])
            idx += 2
        else:
            tokens.append(rest[idx])
            idx += 1
    return tokens


def parse_dir_name(dir_name):
    """Parse directory name like 'abr-may-cs5-cbe5-bk60-mt3-pmd500-1a2' into title parts."""
    month_abbrevs = {
        "ene": "Enero", "feb": "Febrero", "mar": "Marzo", "abr": "Abril",
        "may": "Mayo", "jun": "Junio", "jul": "Julio", "ago": "Agosto",
        "sep": "Septiembre", "oct": "Octubre", "nov": "Noviembre", "dic": "Diciembre"
    }
    filter_labels = {
        "cs": "Colchon Stop", "cbe": "Colchon BE", "bk": "Breakeven %",
        "mt": "Max Trades", "pmd": "Perdida Max", "1a1": "Solo 1a1", "1a2": "Con 1a2"
    }

    parts = dir_name.split("-")
    months_found = []
    filters_found = []

    i = 0
    while i < len(parts):
        p = parts[i]
        if p in month_abbrevs:
            months_found.append(month_abbrevs[p])
        elif p.startswith("cs") and p[2:].isdigit():
            filters_found.append(f"Colchon Stop: {p[2:]}")
        elif p.startswith("cbe") and p[3:].isdigit():
            filters_found.append(f"Colchon BE: {p[3:]}")
        elif p.startswith("bk") and p[2:].isdigit():
            filters_found.append(f"Breakeven: {p[2:]}%")
        elif p.startswith("mt") and p[2:].isdigit():
            filters_found.append(f"Max Trades: {p[2:]}")
        elif p.startswith("pmd") and p[3:].isdigit():
            filters_found.append(f"Perdida Max: ${p[3:]}")
        elif p in ("1a1", "1a2"):
            filters_found.append(f"Modo TP: {'Solo 1a1' if p == '1a1' else 'Con 1a2'}")
        i += 1

    if months_found:
        title = f"Simulacion {' - '.join(months_found)}"
    else:
        title = f"Simulacion {dir_name}"

    subtitle = " | ".join(filters_found) if filters_found else dir_name

    return {"title": title, "subtitle": subtitle}


def build_title(dir_name, days, first_data_line):
    """Build title from dir name or from CSV data."""
    MONTH_NAMES_FULL = ["", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"]

    # Try parsing from structured dir name (csim/output format)
    result = parse_dir_name(dir_name)
    if result["subtitle"] != dir_name:
        # Successfully parsed filters from dir name
        # But update title with actual months from data
        if days:
            month_set = sorted(set((d[0].year, d[0].month) for d in days))
            month_strs = [f"{MONTH_NAMES_FULL[m]} {y}" for y, m in month_set]
            result["title"] = f"Simulacion {' - '.join(month_strs)}"
        return result

    # Fallback: extract params from CSV daily_log columns
    # Header: Date,DailyPnL,TotalPnL,Trades,RangoPts,MaxTrades,PerdidaMaxDiaria,ModoTP,ColchonStop,ColchonBreakeven,BreakevenPct,...
    filters = []
    fields = parse_daily_csv(first_data_line)
    if fields and len(fields) >= 11:
        try:
            filters.append(f"Max Trades: {fields[5]}")
            filters.append(f"Perdida Max: ${fields[6].replace(',', '.')}")
            filters.append(f"Modo TP: {fields[7]}")
            filters.append(f"Colchon Stop: {fields[8]}")
            filters.append(f"Colchon BE: {fields[9]}")
            filters.append(f"Breakeven: {fields[10]}%")
        except (IndexError, ValueError):
            pass

    # Title from actual months in data
    title = f"Reporte {dir_name}"
    if days:
        month_set = sorted(set((d[0].year, d[0].month) for d in days))
        month_strs = [f"{MONTH_NAMES_FULL[m]} {y}" for y, m in month_set]
        title = f"Simulacion {' - '.join(month_strs)}"

    subtitle = " | ".join(filters) if filters else dir_name
    return {"title": title, "subtitle": subtitle}


def format_money(value):
    if value >= 0:
        return f"${value:.2f}"
    return f"$-{abs(value):.2f}"


def color_class(value):
    if value > 0:
        return "positive"
    elif value < 0:
        return "negative"
    return "zero"


def generate_html(output_dir):
    daily_path = os.path.join(output_dir, "mnq10minv2_daily_log.csv")
    if not os.path.exists(daily_path):
        print(f"Error: no encontrado {daily_path}")
        sys.exit(1)

    with open(daily_path, 'r') as f:
        lines = f.readlines()

    if len(lines) < 2:
        print("Error: archivo vacio")
        sys.exit(1)

    days = []
    for line in lines[1:]:
        line = line.strip()
        if not line:
            continue
        fields = parse_daily_csv(line)
        if not fields or len(fields) < 4:
            continue
        try:
            date = datetime.strptime(fields[0], "%Y-%m-%d")
            pnl = float(fields[1].replace(',', '.'))
            trades = int(fields[3])
        except (ValueError, IndexError):
            continue
        if trades == 0:
            continue
        days.append((date, pnl, trades))

    if not days:
        print("Error: no hay dias con trades")
        sys.exit(1)

    months = {}
    for date, pnl, trades in days:
        key = (date.year, date.month)
        if key not in months:
            months[key] = []
        months[key].append((date, pnl, trades))

    grand_total = sum(d[1] for d in days)
    grand_trades = sum(d[2] for d in days)
    days_operated = len(days)
    avg_daily = grand_total / days_operated if days_operated > 0 else 0

    # Extract title from folder name or CSV data
    dir_name = os.path.basename(os.path.normpath(output_dir))
    title_parts = build_title(dir_name, days, lines[1].strip() if len(lines) > 1 else "")

    html = f"""<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="UTF-8">
<title>{title_parts['title']}</title>
<style>
body {{
    font-family: 'Segoe UI', Consolas, monospace;
    background: #1a1a2e;
    color: #e0e0e0;
    padding: 30px;
    max-width: 900px;
    margin: 0 auto;
}}
h1 {{
    color: #00d4ff;
    border-bottom: 2px solid #00d4ff;
    padding-bottom: 10px;
}}
h2 {{
    color: #ffd700;
    margin-top: 40px;
}}
table {{
    width: 100%;
    border-collapse: collapse;
    margin: 15px 0;
    font-size: 14px;
}}
th {{
    background: #16213e;
    color: #00d4ff;
    padding: 8px 12px;
    text-align: right;
    border-bottom: 2px solid #0f3460;
}}
th:first-child {{
    text-align: left;
}}
td {{
    padding: 6px 12px;
    text-align: right;
    border-bottom: 1px solid #1f2937;
}}
td:first-child {{
    text-align: left;
}}
tr:hover {{
    background: #16213e;
}}
.positive {{
    color: #00ff88;
}}
.negative {{
    color: #ff4757;
}}
.zero {{
    color: #888;
}}
.month-total {{
    font-weight: bold;
    border-top: 2px solid #0f3460;
    background: #16213e;
}}
.grand-summary {{
    background: #0f3460;
    border-radius: 8px;
    padding: 20px;
    margin-top: 40px;
    font-size: 16px;
}}
.grand-summary .label {{
    color: #888;
}}
.grand-summary .value {{
    font-weight: bold;
    font-size: 18px;
}}
.subtitle {{
    color: #aaa;
    font-size: 14px;
    margin-top: -10px;
}}
</style>
</head>
<body>
<h1>{title_parts['title']}</h1>
<p class="subtitle">{title_parts['subtitle']}</p>
"""

    for (year, month), month_days in months.items():
        month_accum = 0
        month_trades = 0
        html += f"<h2>{MONTH_NAMES[month]} {year}</h2>\n"
        html += """<table>
<tr><th>Dia</th><th>PnL</th><th>#Trades</th><th>Acumulado</th></tr>
"""
        for date, pnl, trades in month_days:
            month_accum += pnl
            month_trades += trades
            html += f'<tr><td>{date.day}</td>'
            html += f'<td class="{color_class(pnl)}">{format_money(pnl)}</td>'
            html += f'<td>{trades}</td>'
            html += f'<td class="{color_class(month_accum)}">{format_money(month_accum)}</td></tr>\n'

        html += f'<tr class="month-total"><td>MES</td>'
        html += f'<td class="{color_class(month_accum)}">{format_money(month_accum)}</td>'
        html += f'<td>{month_trades}</td><td></td></tr>\n'
        html += "</table>\n"

    html += f"""
<div class="grand-summary">
<p><span class="label">Dias operados:</span> <span class="value">{days_operated}</span></p>
<p><span class="label">Total Trades:</span> <span class="value">{grand_trades}</span></p>
<p><span class="label">PnL Total:</span> <span class="value {color_class(grand_total)}">{format_money(grand_total)}</span></p>
<p><span class="label">Promedio diario:</span> <span class="value {color_class(avg_daily)}">{format_money(avg_daily)}</span></p>
</div>
</body>
</html>"""

    out_file = os.path.join(output_dir, "reporte.html")
    with open(out_file, 'w', encoding='utf-8') as f:
        f.write(html)

    print(f"Reporte generado: {out_file}")
    os.startfile(out_file)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python gen_report.py <directorio_output>")
        print("Ejemplo: python gen_report.py sim_output_mar")
        sys.exit(1)
    generate_html(sys.argv[1])
