# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

NinjaTrader 8 automated strategy for MNQ (Micro E-mini Nasdaq) futures. La estrategia activa es **MNQ10minV2** — ruptura de rango de los primeros 10 minutos (09:30-09:40 ET) con trailing stops parametrizables y modos TP 1:1 / 1:2.

## Architecture

### NinjaTrader C# (single-file strategies)

- **MNQ10minV2.cs** — Estrategia activa. Ruptura de rango 09:30-09:40, estados (EsperandoRango→OrdenesPuestas→EnTrade→DiaTerminado), trailing stops multi-escalon para TP1/TP2, modos 1a1 y 1a2, motor de riesgo con position sizing dinamico (calcula contratos y trades permitidos basado en PerdidaMaxDiaria y rango). Specs: `docs/superpowers/specs/2026-05-06-mnq10min-v2-design.md`, `docs/superpowers/specs/2026-05-15-risk-engine-position-sizing-design.md`
- **MCPBridge.cs** — AddOn de NinjaTrader. Servidor HTTP (localhost:8500) que expone datos del chart al MCP bridge.
- **MCPBridgeIndicator.cs** — Indicador de NinjaTrader que va en el chart y alimenta OHLCV + SMA20 + SMA200 + ATR + RSI al bridge.

### C# harness simulator (csim/)

Ejecuta `MNQ10minV2.cs` directamente contra tick exports sin NinjaTrader. El proyecto .NET 9 incluye mocks del framework NinjaScript (`Strategy`, `Order`, `Position`, etc.) y un `OrderEngine` que simula fills. Compila el archivo real via `<Compile Include="..\MNQ10minV2.cs" />` — sin modificarlo.

- **Program.cs** — Entry point, CLI args, tick reader, simulation loop (2-min bars, OnEachTick)
- **OrderEngine.cs** — Motor de fills: entradas market en next-tick Ask/Bid, stops/targets evaluados tick-a-tick
- **NinjaTrader/** — Stubs: Strategy base class, Enums, Order, Execution, Position, Account, Draw (no-op), Brushes

Salida en `csim/output/<timestamp>_<months>-<params>/` (trades_log, daily_log, bar_log CSVs).

### Python (simulacion y backtesting)

- **tick_simulator_mnq10minv2.py** — Port exacto tick-a-tick del C# bot. Lee exports de ticks de NinjaTrader, procesa cada tick por la state machine, genera CSV logs identicos al bot real. Usar skill `sync-cs-to-simulator` para mantener sincronizado con el C#.
- **backtest_mnq10minv2.py** — Backtester sobre barras de 2 min. Acepta 3 formatos: bar_log CSV del bot, export .txt barras NT, export .txt ticks NT. Genera resumen diario + detalle de trades.
- **gen_report.py** — Genera reporte consolidado desde daily_log CSV (tabla mensual con PnL, trades, acumulado).

### MCP bridge

- **mcp-ninjatrader/** — Node.js MCP server (ES modules, `@modelcontextprotocol/sdk`). Conecta Claude Code a NinjaTrader via HTTP localhost:8500. Transport: stdio.

### Data directories

- **bot/history/** — CSV logs del bot real (bar_log, trades_log, daily_log)
- **historicos test/** — Exports de NinjaTrader (ticks y barras) para backtesting
- **sim_output/** — Salida del tick simulator
- **graficas/** — Capturas markdown de dias completos (barras 2min con OHLCV + SMAs)
- **docs/superpowers/specs/** — Documentos de diseño de cada version de estrategia

## Commands

### C# harness simulator (preferred — runs real C# code)

```bash
# Build and run (desde raiz del repo)
dotnet run --project csim -- "historicos test/MNQ 03-26-enero-febrero-marzo.Last.txt"
dotnet run --project csim -- --colchon 10 --trades 3 --modo 1a2 "file.txt"
dotnet run --project csim -- --perdida-max 500 --cierre 15:45 --no-telemetry "file.txt"

# Build only (verificar compilacion tras editar MNQ10minV2.cs)
dotnet build csim
```

### Python simulator/backtester

```bash
# Tick simulator (tick-by-tick, high fidelity)
python tick_simulator_mnq10minv2.py "historicos test/MNQ 03-26-enero-febrero-marzo.Last.txt"
python tick_simulator_mnq10minv2.py --colchon 10 --trades 3 --modo 1a2 "file.txt"

# Backtester (bar-based, faster)
python backtest_mnq10minv2.py                                    # usa bot/history/mnq10minv2_bar_log.csv
python backtest_mnq10minv2.py "historicos test/MNQ 06-26.Last.txt"
python backtest_mnq10minv2.py --desde 2026-01-01 --hasta 2026-03-31 -v "file.txt"

# Report from daily_log
python gen_report.py csim/output/<run-dir>/mnq10minv2_daily_log.csv
```

### MCP bridge

```bash
cd mcp-ninjatrader && npm install   # solo primera vez
node mcp-ninjatrader/index.js       # lanzado automaticamente por Claude Code via stdio
```

## Key conventions

- Each NinjaScript strategy is a single .cs file — do not split into multiple files
- NinjaTrader uses C# with NinjaScript base classes (Strategy, Indicator, AddOn)
- All times are New York (Eastern) timezone
- NinjaTrader may output JSON with Spanish locale (decimal commas instead of dots) — the MCP bridge sanitizes this in `fetchNT()`
- The tick simulator must be an exact port of the C# logic — use the `sync-cs-to-simulator` skill after modifying MNQ10minV2.cs
- MNQ10minV2 parameters are fully exposed as NinjaScript properties for NinjaTrader optimization (walk-forward)
- csim compiles MNQ10minV2.cs unmodified — any new NinjaScript API used in the strategy must have a stub in `csim/NinjaTrader/`. If `dotnet build csim` fails after a strategy edit, add/update the relevant stub
- CSV logs use Spanish locale (comma as decimal separator) — parsers must handle this (see `ParseDailyCsvLine` pattern in Program.cs and gen_report.py)

## Chart capture — graficas/

Cuando el usuario dice "captura el dia" o "listo captura", se debe capturar el dia completo de barras de 2 minutos y guardarlo en `graficas/`.

### Como capturar

1. Verificar la fecha actual con `get_current_bar` — el usuario posiciona el Playback a las 23:58-23:59 del dia a capturar
2. Llamar `get_bar_history` con count=750 (cubre 25h en barras de 2 min)
3. Filtrar solo las barras del dia calendario (00:00 a 23:58 del mismo dia, NO mezclar con el dia anterior)
4. Generar el archivo markdown con el formato estandar

### Formato del archivo

- Nombre: `graficas/2min-febrero-DD-2026.md` (o el mes correspondiente)
- Secciones:
  - **Resumen del dia**: instrumento, periodo, rango capturado, total barras, apertura 00:00, cierre 23:58, high/low del dia, rango total, apertura/cierre/high/low sesion regular, SMA20/SMA200 en apertura regular, spread, alineacion
  - **Sesion nocturna (00:00-08:00)**: tabla con OHLCV + SMA20 + SMA200 + ENCIMA/DEBAJO (240 barras)
  - **Pre-apertura (08:00-09:30)**: tabla con OHLCV + SMA20 + SMA200 + ENCIMA/DEBAJO (45 barras)
  - **Sesion regular (09:30-16:00)**: tabla con OHLCV + SMA20 + SMA200 + ENCIMA/DEBAJO (195 barras)
  - **Momentos clave**: detalle de la barra de 09:32 (open, close, high, low, movimiento, color VERDE/ROJA, rango, distancia a SMAs)
  - **Post-market y sesion nocturna siguiente (16:00-23:58)**: tabla con OHLCV + SMA20 + SMA200 + ENCIMA/DEBAJO (210 barras)

### Columnas de cada tabla de sesion

`| Hora | Open | High | Low | Close | Vol | SMA20 | SMA200 | Precio vs SMA20 | Precio vs SMA200 |`

Las columnas "Precio vs SMA20/SMA200" usan ENCIMA o DEBAJO para que otro Claude pueda leer el archivo y entender exactamente donde estaba el precio respecto a las medias moviles en cada barra de 2 minutos, sin necesidad de ver NinjaTrader.

### Proposito

Estos archivos son una representacion completa de la grafica de 2 minutos. Cualquier Claude que lea uno de estos archivos puede entender el dia completo como si estuviera viendo el chart: donde abrio, como se movio el precio respecto a SMA20 y SMA200, momentos clave, y el cierre.

## MCP tools disponibles

- `ping` — verifica conexion con NinjaTrader
- `get_current_bar` — barra actual con OHLCV + todos los indicadores
- `get_bar_history` (count 1-22000, from/to, sessionOnly) — historial de barras con OHLCV + SMA20 + SMA200. Soporta rango por fechas `from`/`to` (formato yyyy-MM-dd o yyyy-MM-dd HH:mm) o por count. `sessionOnly=true` filtra solo sesion regular (09:30-16:00 ET).
- `get_market_context` — snapshot completo con analisis de tendencia
- `playback_goto` (targetTime) — avanza el Playback hasta una fecha/hora y pausa automaticamente
- `playback_pause` — pausa el Playback
- `playback_resume` — resume el Playback
- `playback_status` — estado actual del Playback (idle, seeking, arrived, paused, playing)

## Development workflow

- Edit C# in this repo, then copy to NinjaTrader and recompile
- Los archivos deben copiarse a `C:\Users\<USUARIO>\OneDrive - Perficient, Inc\Documents\NinjaTrader 8\bin\Custom\`:
  - `MNQ10minV2.cs` → `Strategies/`
  - `MCPBridge.cs` → `AddOns/`
  - `MCPBridgeIndicator.cs` → `Indicators/`
- After modifying MNQ10minV2.cs:
  1. `dotnet build csim` — verificar que compila contra los stubs
  2. `dotnet run --project csim -- <tick_file>` — validar logica con simulacion real
  3. Run `sync-cs-to-simulator` skill to update the Python tick simulator
- Validate changes with: `python tick_simulator_mnq10minv2.py` on tick data and compare CSV output
- MCP bridge runs via `node mcp-ninjatrader/index.js` (stdio transport, launched by Claude Code)
- Test MCP connection: use `ping` tool, then `get_current_bar`

## Known issues

- NinjaTrader Playback a velocidad maxima puede saltarse el chequeo de target en el indicador. Usar rampa de desaceleracion (1000x lejos, 1x cerca del target).
- El usuario avanza manualmente el Playback a velocidad max y dice "listo" para que Claude capture cada dia.

## Language

The user communicates in Spanish. Respond in Spanish.
