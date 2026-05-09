# Tick Simulator MNQ10minV2 — Design Spec

## Objetivo

Crear un simulador Python que replique **tick-a-tick** la lógica de `MNQ10minV2.cs` usando datos históricos exportados de NinjaTrader, produciendo resultados idénticos al bot operando en NinjaTrader real.

## Problema que resuelve

El backtest existente (`backtest_mnq10minv2.py`) agrega ticks a barras de 2 min antes de simular, perdiendo:
- El orden exacto de High/Low intra-barra
- La evaluación tick-a-tick de stops, targets y trailing
- La precisión en el precio de llenado (fill price)

## Input: Datos de NinjaTrader

### Formato tick export

Archivo `.txt` exportado desde NinjaTrader con el formato:
```
YYYYMMDD HHMMSS microseconds;Last;Bid;Ask;Volume
20260506 050000 0560000;28335.5;28335.5;28335.75;1
```

- Timestamp en **UTC** (restar 4h para Eastern Time, o 5h si no hay DST)
- `Last` = precio de la última transacción (el que usa el bot)
- `Volume` = contratos en ese tick

### Cómo exportar desde NinjaTrader 8

1. Abrir el chart de MNQ con período deseado
2. Ir a **Tools → Export → Tick Data** (o usar Data Export en Control Center)
3. Seleccionar instrumento: MNQ 06-26 (o el contrato activo)
4. Seleccionar rango de fechas (ej: 2026-05-06 a 2026-05-06)
5. Tipo: **Last** (solo necesitamos Last, Bid, Ask)
6. Formato: separador `;`, incluir microsegundos
7. Guardar en `historicos test/` del proyecto

### Archivo existente de referencia

`historicos test/MNQ 06-26-tickyaaa.Last.txt` — 1,649,822 ticks del 6 de mayo 2026.

## Arquitectura del Simulador

```
tick_simulator_mnq10minv2.py
├── parse_tick_file()         # Lee el .txt, convierte a ET
├── BarBuilder                # Acumula ticks → barra 2min (para logging)
├── SimulatorEngine           # Máquina de estados (port 1:1 del C#)
│   ├── state: EsperandoRango | OrdenesPuestas | EnTrade | DiaTerminado
│   ├── on_tick(price, time)  # Procesa cada tick
│   ├── procesar_rango()
│   ├── monitorear_ordenes()
│   ├── monitorear_trade()
│   └── procesar_fin_trade()
└── LogWriter                 # Genera CSVs idénticos al bot
    ├── write_trade_log()
    ├── write_bar_log()
    └── write_daily_log()
```

## Máquina de Estados (port exacto del C#)

### EsperandoRango
- Antes de 09:30 ET: ignorar (`ESPERANDO_0930`)
- 09:32-09:40 ET: actualizar `rangoHigh` / `rangoLow` con cada tick
- Después de 09:40: calcular `stopDistance = rangoPts + ColchonStop`, transitar a OrdenesPuestas

### OrdenesPuestas
- **Cooldown** (50 segundos post-breakeven o post-TP): esperar antes de buscar nueva entrada
- **Flip** (post stop-loss): entrada inmediata en dirección contraria
- **Re-entry** (post-BE/TP): esperar que precio vuelva al rango, luego nueva ruptura
- **Trigger de entrada**: si `tick_price >= rangoHigh` → LONG; si `tick_price <= rangoLow` → SHORT
- Fill price = tick actual (market order en OnEachTick)

### EnTrade
En cada tick se evalúa (en este orden):
1. **StopLoss/Target** — si precio cruza stop o target, exit
2. **Breakeven** al 60% de stopDistance → mover stop a entry+5
3. **Trailing TP1**: escalones 75%→45%, 85%→60%, 95%→80%, 99%→95%
4. **Trailing TP2**: escalones 50%→25%, 70%→50%, 85%→60%, 90%→75%, 95%→84%, 98%→94%

### DiaTerminado
Causas: max trades, max stops, max TPs, max BEs, hora de cierre.

## Gestión de Riesgo (port del C#)

```
maxStopsPuros   = Round(MaxTrades / 2)
maxTakeProfits  = Round(MaxTrades / 2)
maxBreakevens   = Round(MaxTrades / 2)
```

Distribución de contratos (modo 1a2):
- MicroContratos >= 3: TP1 = N-1, TP2 = 1
- MicroContratos == 2: TP1 = 1, TP2 = 1
- MicroContratos == 1: TP1 = 0, TP2 = 1

TP1 target = entry + stopDistance (1:1)
TP2 target = entry + stopDistance*2 (1:2)

## Parámetros CLI (iguales a la UI de NinjaTrader)

| Parámetro | CLI | Default | NinjaTrader UI |
|-----------|-----|---------|----------------|
| Colchon Stop | `--colchon N` | 5 | "Colchon Stop (pts)" |
| Max Trades/Dia | `--trades N` | 2 | "Max Trades/Dia" |
| Micro Contratos | `--contratos N` | 2 | "Micro Contratos" |
| Modo TP | `--modo 1a1\|1a2` | 1a2 | "Modo TP (1a1 o 1a2)" |
| Hora Cierre | `--cierre HH:MM` | 15:50 | "Hora Cierre (HH:mm)" |
| UTC Offset | `--utc-offset N` | 4 | — |
| Verbose | `-v` | off | — |
| Archivo | positional | — | — |

## Output: CSVs Idénticos

### trades_log.csv
```
Date,Time,Action,Direction,EntryPrice,ExitPrice,StopDist,RangoHigh,RangoLow,RangoPts,PnL,DailyPnL,TotalPnL,ExitReason,TradesToday
```
- Nota: usa formato decimal español (coma) como el C# original con locale

### bar_log.csv
```
Date,Time,Open,High,Low,Close,Volume,Estado,Position,DailyPnL,TotalPnL,TradesToday,Decision
```
- Una fila por cada barra de 2 minutos cerrada (construida desde ticks)
- Los campos Estado/Position/Decision reflejan el estado al cierre de la barra

### daily_log.csv
```
Date,DailyPnL,TotalPnL,Trades,RangoPts
```

## Diferencias Clave vs el Backtest Existente

| Aspecto | backtest_mnq10minv2.py | tick_simulator (nuevo) |
|---------|----------------------|------------------------|
| Granularidad | Barra 2min | Tick individual |
| Fill price | Aproxima (rangoHigh o open) | Tick exacto que cruza nivel |
| Trailing | Evalúa en close de barra | Evalúa en cada tick |
| Stops | Evalúa con H/L de barra | Evalúa en cada tick |
| Output | Resumen en consola | CSVs idénticos al bot |
| Orden H/L | Desconocido intra-barra | Exacto (tick-a-tick) |

## Validación

Criterio de éxito: correr sobre el archivo de ticks del 6 de mayo y obtener:
```
09:41  ENTRY LONG  28457.00
11:26  EXIT  LONG  28577.25  PnL=$240.50  (Breakeven TP1)
12:58  EXIT  LONG  28520.25  PnL=$126.50  (Breakeven TP2)
Daily PnL = $367.00
```

Si no coincide exactamente, debuggear tick por tick hasta encontrar la discrepancia.

## BarBuilder: Construcción de Barras desde Ticks

El `BarBuilder` acumula ticks para construir la barra de 2 minutos "viva":
- Open = primer tick del período
- High = max de todos los ticks
- Low = min de todos los ticks
- Close = último tick
- Volume = suma de volúmenes

Cada 2 minutos (alineados en minuto par: 09:30, 09:32, ...) se "cierra" la barra anterior y se abre una nueva. En NinjaTrader, la barra de 09:32 incluye ticks de 09:32:00 a 09:33:59.

## Consideraciones de Performance

- 1.6M ticks/día → con Python puro en ~10-20 segundos
- Sin dependencias externas (no pandas, no numpy) para simplicidad
- Buffer de lectura grande (8MB) para velocidad I/O
