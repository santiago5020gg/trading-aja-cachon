# MNQ Trading Bot — Operativa "En Caliente"

## Project overview

NinjaTrader 8 automated strategy for MNQ (Micro E-mini Nasdaq) futures. Opera con barras de 2 minutos usando SMA20/SMA200, trailing bar-a-bar, y tres tipos de entrada: tendencia 09:32, pullback a SMA20, y ruptura de rango.

## Architecture

- **MNQEnCalienteBot.cs** — La estrategia principal de NinjaTrader. C# targeting NinjaTrader 8 NinjaScript API. Incluye: entradas por tendencia/pullback/ruptura, trailing bar-a-bar, breathe period, breakeven, scaling in, y gestion de riesgo diaria.
- **MCPBridge.cs** — AddOn de NinjaTrader. Servidor HTTP (localhost:8500) que expone datos del chart al MCP bridge.
- **MCPBridgeIndicator.cs** — Indicador de NinjaTrader que va en el chart y alimenta OHLCV + SMA20 + SMA200 al bridge.
- **mcp-ninjatrader/** — Node.js MCP server que conecta Claude Code a NinjaTrader via HTTP. Se comunica con MCPBridgeIndicator (localhost:8500).

## Key conventions

- The bot is a single-file NinjaScript strategy — do not split into multiple files
- NinjaTrader uses C# with its own NinjaScript base classes (Strategy, Indicator)
- All times are New York (Eastern) timezone
- The MCP bridge requires MCPBridgeIndicator added to an active NinjaTrader chart
- NinjaTrader may output JSON with Spanish locale (decimal commas instead of dots) — the MCP bridge sanitizes this in `fetchNT()`

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
- `get_bar_history` (count 1-750) — historial de barras con OHLCV + SMA20 + SMA200
- `get_market_context` — snapshot completo con analisis de tendencia
- `playback_goto` (targetTime) — avanza el Playback hasta una fecha/hora y pausa automaticamente
- `playback_pause` — pausa el Playback
- `playback_resume` — resume el Playback
- `playback_status` — estado actual del Playback (idle, seeking, arrived, paused, playing)

## Known issues

- NinjaTrader Playback a velocidad maxima puede saltarse el chequeo de target en el indicador. Usar rampa de desaceleracion (1000x lejos, 1x cerca del target).
- El usuario avanza manualmente el Playback a velocidad max y dice "listo" para que Claude capture cada dia.

## Development workflow

- Edit C# in this repo, then copy to NinjaTrader and recompile
- Los archivos deben copiarse a `C:\Users\<USUARIO>\OneDrive - Perficient, Inc\Documents\NinjaTrader 8\bin\Custom\`:
  - `MNQEnCalienteBot.cs` → `Strategies/`
  - `MCPBridge.cs` → `AddOns/`
  - `MCPBridgeIndicator.cs` → `Indicators/`
- MCP bridge runs via `node mcp-ninjatrader/index.js` (stdio transport, launched by Claude Code)
- Test connection: use `ping` MCP tool, then `get_current_bar`

## Language

The user communicates in Spanish. Respond in Spanish.
