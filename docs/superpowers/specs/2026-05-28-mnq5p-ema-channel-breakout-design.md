# MNQ5P — EMA Channel Breakout Strategy Design

## Overview

Estrategia NinjaTrader para MNQ que detecta breakouts de canales de acumulación formados después de cruces de EMA 20/200. Utiliza compresión de ATR para identificar el canal automáticamente y el mismo sistema de trailing/breakeven/riesgo de MNQ10minV2.

## State Machine

```
EsperandoCruce → ConfirmandoTendencia → BuscandoCanal → EsperandoBreakout → EnTrade
                                                                              ↓
                                                                    DiaTerminado
```

### Estados

| Estado | Acción | Transición salida |
|--------|--------|-------------------|
| EsperandoCruce | Monitorea EMA20 vs EMA200 | Cruce detectado → ConfirmandoTendencia |
| ConfirmandoTendencia | Verifica precio del lado correcto de ambas EMAs por N barras | BarrasConfirmacion cumplidas → BuscandoCanal. Precio cruza al lado incorrecto → EsperandoCruce |
| BuscandoCanal | Mide ATR, espera compresión | ATR comprimido por BarrasCanal barras → EsperandoBreakout. ATR se expande → reinicia conteo |
| EsperandoBreakout | Espera cierre de barra fuera del canal | Close > resistencia (long) o Close < soporte (short) → EnTrade. MaxBarrasEspera superadas → BuscandoCanal |
| EnTrade | Gestión breakeven + trailing TP1/TP2 | Trade cerrado + trades disponibles → BuscandoCanal. MaxTrades o PerdidaMax → DiaTerminado |
| DiaTerminado | Detiene operación | Nueva sesión → EsperandoCruce |

### Invalidaciones

- ConfirmandoTendencia: si precio cierra del lado incorrecto → EsperandoCruce
- BuscandoCanal: si ATR se expande sin completar BarrasCanal → reinicia conteo (permanece en BuscandoCanal)
- EsperandoBreakout: si pasan MaxBarrasEspera barras → BuscandoCanal
- Post-trade: si precio cruza EMAs en dirección contraria durante búsqueda → EsperandoCruce

## Parameters

### Setup (5 condiciones principales)

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| BarrasConfirmacion | int | 5 | Barras consecutivas con precio del lado correcto de ambas EMAs |
| UmbralATR | int | 50 | % — ATR actual debe ser < este % del ATR del impulso para ser compresión |
| BarrasCanal | int | 10 | Mínimo barras con ATR comprimido para declarar canal válido |
| MaxBarrasEspera | int | 30 | Barras máximas esperando breakout antes de invalidar canal |
| ColchonStop | int | 5 | Puntos adicionales para el stop (debajo soporte o encima resistencia) |

### Sesión

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| OperarAsia | bool | false | Habilita sesión asiática (18:00 ET) |
| OperarEuropa | bool | false | Habilita sesión europea (02:00 ET) |
| OperarAmerica | bool | true | Habilita sesión americana (09:30 ET) |
| HoraCierre | string | "15:50" | Hora cierre forzado ET |

### Riesgo (idéntico a MNQ10minV2)

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| MaxTrades | int | 3 | Trades máximos por día |
| PerdidaMaxDiaria | double | 400 | Pérdida máxima diaria USD |
| ModoTP | enum | Con1a2 | Solo1a1 o Con1a2 |
| BreakevenPct | int | 60 | % stopDistance para activar breakeven |
| ColchonBreakeven | int | 5 | Puntos colchón en breakeven |

### Trailing TP1 (4 escalones)

| Parámetro | Default | Descripción |
|-----------|---------|-------------|
| CantTrailTP1 | 4 | Escalones activos |
| TP1Act1-4 | 75, 85, 95, 99 | % activación por escalón |
| TP1Stp1-4 | 45, 60, 80, 95 | % stop por escalón |

### Trailing TP2 (6 escalones)

| Parámetro | Default | Descripción |
|-----------|---------|-------------|
| CantTrailTP2 | 6 | Escalones activos |
| TP2Act1-6 | 50, 70, 85, 90, 95, 98 | % activación por escalón |
| TP2Stp1-6 | 18, 50, 60, 70, 84, 94 | % stop por escalón |

## Entry Logic

### 1. Cruce EMA

```
Alcista: EMA20[1] <= EMA200[1] AND EMA20[0] > EMA200[0]
Bajista: EMA20[1] >= EMA200[1] AND EMA20[0] < EMA200[0]
```

Se guarda `direccionCruce` y `atrImpulso` = ATR(14) en el momento del cruce.

### 2. Confirmación tendencia

- Alcista: Close > EMA20 AND Close > EMA200 por BarrasConfirmacion barras consecutivas
- Bajista: Close < EMA20 AND Close < EMA200 por BarrasConfirmacion barras consecutivas

### 3. Detección canal (compresión ATR)

- Cada barra: si ATR(14) actual < atrImpulso × (UmbralATR / 100) → incrementa contador
- Si no → reinicia contador
- Cuando contador >= BarrasCanal:
  - resistencia = Highest High de últimas BarrasCanal barras
  - soporte = Lowest Low de últimas BarrasCanal barras
  - tamanoCanal = resistencia - soporte

### 4. Breakout

- Long: Close > resistencia
- Short: Close < soporte
- Dirección de breakout independiente de dirección del cruce (opera ambas)

### 5. Orden de entrada

- Market order al cierre de la barra de breakout
- stopDistance = tamanoCanal + ColchonStop

## Exit / Risk Management

### Stop Loss

- Long: soporte - ColchonStop
- Short: resistencia + ColchonStop

### Take Profit

- TP1 = entrada ± stopDistance (1:1)
- TP2 = entrada ± stopDistance × 2 (1:2)

### Position Sizing (idéntico a MNQ10minV2)

```
riesgo1Micro = stopDistance × PointValue
presupuestoDisponible = PerdidaMaxDiaria - |dailyPnL|
contratosCalculados = Floor(presupuestoIdeal / riesgo1Micro)
```

Split TP1/TP2 según ModoTP:
- Solo1a1: todos los contratos a TP1
- Con1a2: ≥3 → (Qty-1) TP1 + 1 TP2; 2 → 1+1; 1 → TP2

### Breakeven

Cuando unrealizedPts >= stopDistance × (BreakevenPct/100):
- Mueve stop a entrada ± ColchonBreakeven

### Trailing TP1

Escalones progresivos. Cada escalón se activa cuando:
- unrealPts >= stopDistance × (TP1ActX / 100)
- Stop se mueve a: entrada ± stopDistance × (TP1StpX / 100)

### Trailing TP2

Similar pero contra tp2Target = stopDistance × 2:
- unrealPts >= tp2Target × (TP2ActX / 100)
- Stop se mueve a: entrada ± tp2Target × (TP2StpX / 100)

### Hard Stops

- MaxTrades alcanzado → DiaTerminado
- PerdidaMaxDiaria superada → DiaTerminado
- HoraCierre → cierra posición abierta + DiaTerminado

## Session Management

- Sesiones habilitadas por toggles (OperarAsia, OperarEuropa, OperarAmerica)
- Opera continuamente durante las sesiones seleccionadas desde la apertura de cada una
- Reset por día (18:00 ET boundary como MNQ10minV2): dailyPnL=0, tradesHoy=0, estado=EsperandoCruce

## Chart Visualization

- Línea horizontal verde: resistencia del canal
- Línea horizontal roja: soporte del canal
- Triángulo arriba/abajo: punto de entrada
- Línea punteada: nivel breakeven cuando se activa
- Texto en esquina: estado actual + parámetros activos

## Logging (CSV, mismo formato que MNQ10minV2)

- **trades_log:** Timestamp, dirección, entrada, salida, PnL, stopDistance, tamañoCanal, estadoSalida
- **daily_log:** Fecha, trades, PnL diario, acumulado
- **bar_log:** Barra, OHLCV, EMA20, EMA200, ATR, estado actual

## File Structure

Un solo archivo `MNQ5P.cs` — single-file strategy como convención del proyecto. Incluye:
- Indicators: EMA(20), EMA(200), ATR(14)
- Enum EstadoMNQ5P
- Properties NinjaScript para optimización walk-forward
- Draw objects para visualización

## Timeframe

Barras de 2 minutos (igual que MNQ10minV2).
