---
name: sync-cs-to-simulator
description: Sincroniza la logica de MNQ10minV2.cs (NinjaTrader C#) al simulador Python tick_simulator_mnq10minv2.py. Lee el C# actual, detecta cambios vs el Python, y actualiza el simulador para que replique exactamente el comportamiento del bot.
when_to_use: Usar cuando se modifique MNQ10minV2.cs (cambios en trailing, estados, gestion de riesgo, parametros) y se necesite que el simulador Python refleje esos cambios. Tambien usar para verificar que ambos estan sincronizados.
---

# Sync C# to Simulator

Convierte la logica de `MNQ10minV2.cs` al simulador Python `tick_simulator_mnq10minv2.py`.

## Archivos

- **Fuente (C#):** `MNQ10minV2.cs` (raiz del repo)
- **Destino (Python):** `tick_simulator_mnq10minv2.py` (raiz del repo)
- **Validacion:** `historicos test/MNQ 06-26-tickyaaa.Last.txt` (ticks del 6 de mayo)

## Proceso

### 1. Leer el C# actual

Leer `MNQ10minV2.cs` completo y extraer:
- **Parametros:** ColchonStop, MaxTrades, MicroContratos, ModoTP, HoraCierre
- **Derivados:** maxStopsPuros, maxTakeProfits, maxBreakevens, qtyTP1, qtyTP2
- **Estados:** EsperandoRango, OrdenesPuestas, EnTrade, DiaTerminado
- **Ventana de rango:** minutos inicio/fin (actualmente 09:32-09:40 en C#, mapea a ticks 09:30-09:39 en Python)
- **Logica de entrada:** breakout, flip, re-entry, cooldown
- **Trailing TP1:** escalones y porcentajes (actualmente 75%->45%, 85%->60%, 95%->80%, 99%->95%)
- **Trailing TP2:** escalones y porcentajes (actualmente 50%->18%, 70%->50%, 85%->60%, 90%->70%, 95%->84%, 98%->94%)
- **Breakeven:** umbral (actualmente 60%) y offset (actualmente +5 pts)
- **Gestion de riesgo:** maxStops, maxTPs, maxBEs, transiciones post-trade
- **PnL:** formula de calculo (POINT_VALUE = $2 por punto por micro contrato)

### 2. Comparar con el Python actual

Leer `tick_simulator_mnq10minv2.py` y comparar cada seccion:
- Parametros y derivados
- Ventana de rango
- Logica de entrada (breakout levels, flip, cooldown duration)
- Trailing (escalones, porcentajes, orden de evaluacion)
- Breakeven (umbral, offset)
- Post-trade transitions
- PnL formulas

### 3. Actualizar el Python

Para cada diferencia encontrada:
- Modificar la seccion correspondiente en el Python
- Mantener la estructura del simulador (clase TickSimulator, metodos _process_tick, _check_ordenes, _monitor_trade, _enter_trade, _post_trade_transition)
- No cambiar: parsing de ticks, BarBuilder, CSV writers, CLI args (a menos que haya nuevos parametros en el C#)

### 4. Validar

Ejecutar:
```bash
python tick_simulator_mnq10minv2.py -v "historicos test/MNQ 06-26-tickyaaa.Last.txt"
```

Verificar que los resultados del 6 de mayo siguen siendo coherentes (el PnL puede cambiar si la logica cambio, pero la ejecucion debe ser limpia sin errores).

## Mapeo C# -> Python

| C# (NinjaTrader) | Python (Simulador) | Nota |
|---|---|---|
| `Time[0]` | tick timestamp directo | C# usa bar close time; Python usa tick time con offset -2min en ventana |
| `Close[0]` (OnEachTick) | `price` (tick actual) | Identicos en modo OnEachTick |
| `High[0]`, `Low[0]` | Acumulados en BarBuilder | Solo para logging, no para logica |
| `SetStopLoss(signal, price)` | `self.tp1_stop = price` | Directo |
| `SetProfitTarget(signal, ticks)` | `self.tp1_target = entry + pts` | Convertir ticks a puntos |
| `EnterLong(qty, signal)` | `self._enter_trade(1, price)` | Market order al tick actual |
| `Position.MarketPosition` | `self.trade_direction` | 1=long, -1=short, 0=flat |
| `Slippage = 1` | No aplicado | Causa 1 tick de diferencia en precios, PnL identico |
| Decimales formato espanol | `fmt_price()` usa `.replace('.', ',')` | Para CSVs |

## Reglas de la ventana de rango

El C# usa `nyNow.Minute >= 32 && nyNow.Minute <= 40` con `Time[0]` (bar close time en chart de 2min).
En el simulador Python se traduce a ticks con `minute >= 30 and minute <= 39` porque:
- Bar con Time[0]=09:32 abre a las 09:30 (ticks 09:30:00-09:31:59)
- Bar con Time[0]=09:40 abre a las 09:38 (ticks 09:38:00-09:39:59)
- Primera barra fuera: Time[0]=09:42, ticks desde 09:40:00

Si el C# cambia la ventana, ajustar restando 2 al minuto inicio y fin.

## Parametros nuevos

Si el C# agrega un nuevo parametro en `#region Parameters`:
1. Agregar al CLI de Python (`parse_args()`)
2. Agregar al constructor de `TickSimulator`
3. Usarlo en la logica correspondiente
4. Agregar al print de parametros en `run()`
