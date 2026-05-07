# MNQ10min-v2 — Ruptura de Rango con TP/SL 1:1

## Concepto

Estrategia para MNQ futures que captura el rango de los primeros 10 minutos de la sesión americana (09:30-09:40 ET), coloca órdenes stop market en ambos extremos, y opera con target/stop fijo 1:1. Sin gestión interna (sin breakeven, sin trailing, sin add-on).

## Instrumento

- MNQ (Micro E-mini Nasdaq 100)
- Valor por punto: $2
- Barras: cualquier timeframe (la lógica usa ticks/tiempo, no depende del periodo de barra)

## Flujo de Estados

```
EsperandoRango → OrdenesPuestas → EnTrade → DiaTerminado
```

### 1. EsperandoRango (09:30–09:40 ET)

- Antes de 09:30: no hace nada.
- Entre 09:30 y 09:39:59: acumula el High más alto y Low más bajo de cada tick/barra.
- A las 09:40: el rango queda fijado. Si High == Low (sin movimiento), día cancelado.

### 2. OrdenesPuestas

Al fijar el rango, coloca dos órdenes stop market simultáneas:

| Orden | Nivel | Dirección |
|-------|-------|-----------|
| BUY STOP | RangoHigh | Long |
| SELL STOP | RangoLow | Short |

Cuando una se llena, cancela la otra (OCO manual).

### 3. EnTrade

**Entry Long (fill en RangoHigh):**
- Stop Loss = RangoLow − ColchonStop(10 pts)
- Stop Distance = RangoHigh − (RangoLow − 10) = RangoPuntos + 10
- Profit Target = RangoHigh + StopDistance

**Entry Short (fill en RangoLow):**
- Stop Loss = RangoHigh + ColchonStop(10 pts)
- Stop Distance = (RangoHigh + 10) − RangoLow = RangoPuntos + 10
- Profit Target = RangoLow − StopDistance

El trade se resuelve solo: TP o SL, sin intervención.

### 4. DiaTerminado

El día termina cuando se cumple cualquiera de:
- Se completaron MaxTrades (2) trades
- Se alcanza HoraCierre (15:50 ET) — cierra posición abierta a mercado
- Ambas órdenes pendientes fueron canceladas sin fill antes de hora cierre

### Re-entrada tras pérdida

Si el primer trade pierde (hit SL) y tradesToday < MaxTrades:
- Vuelve a colocar la orden stop market del lado que NO se activó previamente
- Si ambos lados ya se intentaron, día terminado

## Parámetros

| Parámetro | Default | Tipo | Descripción |
|-----------|---------|------|-------------|
| ColchonStop | 10 | int (pts) | Puntos extra más allá del rango para el stop |
| MaxTrades | 2 | int | Máximo de trades por día |
| HoraCierre | "15:50" | string | Hora de cierre forzado (ET) |

## Sizing

- 1 contrato fijo siempre.
- Sin cálculo de riesgo/presupuesto.

## Configuración NinjaScript

- `Calculate = Calculate.OnEachTick` — necesario para órdenes stop market intrabar
- `EntriesPerDirection = 1`
- `EntryHandling = EntryHandling.AllEntries`
- Stop y Target manejados con `SetStopLoss()` + `SetProfitTarget()` nativos
- `IsExitOnSessionCloseStrategy = false` (lo maneja la lógica de HoraCierre)

## Ejecución de Órdenes

La entrada usa `EnterLongStopMarket(1, RangoHigh, "RangoLong")` y `EnterShortStopMarket(1, RangoLow, "RangoShort")`. Al recibir fill en OnExecutionUpdate:
1. Identifica cuál orden se llenó
2. Cancela la orden opuesta pendiente
3. SetStopLoss y SetProfitTarget ya están configurados por señal

## Logging

Misma estructura que v1 (3 CSVs):
- `mnq10minv2_trades_log.csv` — fills y exits
- `mnq10minv2_daily_log.csv` — resumen diario
- `mnq10minv2_bar_log.csv` — estado por barra

Telemetría JSON en `C:\temp\mnq_10minv2_status.json`.

## Diferencias vs v1

| Aspecto | v1 | v2 |
|---------|----|----|
| Entry trigger | Cierre barra + cuerpo ≥60% | Toque intrabar (stop market) |
| Stop | Rango + cuerpo vela + 5pts | Lado opuesto rango + 10pts |
| Target | Sin TP (trailing bar-a-bar) | TP fijo 1:1 |
| Gestión | Breakeven 50%, add-on, trailing 80% | Ninguna |
| Sizing | Calculado por presupuesto/$500 | 1 contrato fijo |
| Complejidad | ~950 líneas | ~300 líneas |
| Re-entrada | Misma dirección, recalcula stop | Lado opuesto del rango |

## Ejemplo Numérico

- Rango 09:30-09:40: High = 21,500 / Low = 21,400 → RangoPuntos = 100
- Se colocan: BUY STOP @ 21,500 y SELL STOP @ 21,400
- Precio sube y toca 21,500 → fill LONG
  - Stop = 21,400 − 10 = 21,390 (distancia = 110 pts)
  - TP = 21,500 + 110 = 21,610
  - Riesgo = 110 pts × $2 = $220
  - Reward = 110 pts × $2 = $220 (1:1)
- Si pierde → coloca SELL STOP @ 21,400 (segundo intento)
  - Stop = 21,500 + 10 = 21,510
  - TP = 21,400 − 110 = 21,290
