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
- **Parametros de riesgo:** ColchonStop, ColchonBreakeven, BreakevenPct, MaxTrades, PerdidaMaxDiaria, ModoTP, HoraCierre
- **Motor de riesgo (position sizing dinamico):** calcula contratosCalculados y tradesPermitidosHoy basado en PerdidaMaxDiaria, rango del dia, y MaxTrades. Tres casos: presupuesto >= riesgo (N contratos, MaxTrades), riesgo <= perdidaMax (1 contrato, trades reducidos), riesgo > perdidaMax (FUERA_PRESUPUESTO, no opera)
- **Derivados:** maxStopsPuros, maxTakeProfits, maxBreakevens (round(MaxTrades/2)), qtyTP1, qtyTP2 (calculados dinamicamente tras rango)
- **Estados:** EsperandoRango, OrdenesPuestas, EnTrade, DiaTerminado
- **Ventana de rango:** minutos inicio/fin (actualmente 09:32-09:40 en C#, mapea a ticks 09:30-09:39 en Python)
- **Logica de entrada:** breakout, flip, re-entry, cooldown
- **Trailing TP1:** CantTrailTP1 escalones parametrizables (TP1Act1-4, TP1Stp1-4). Defaults: 75%->45%, 85%->60%, 95%->80%, 99%->95%
- **Trailing TP2:** CantTrailTP2 escalones parametrizables (TP2Act1-6, TP2Stp1-6). Defaults: 50%->18%, 70%->50%, 85%->60%, 90%->70%, 95%->84%, 98%->94%
- **Breakeven:** umbral parametrizable (BreakevenPct, default 60%) y offset (ColchonBreakeven, default 5 pts)
- **Gestion de riesgo:** maxStops, maxTPs, maxBEs, transiciones post-trade
- **PnL:** formula de calculo (POINT_VALUE = $2 por punto por micro contrato)

### 2. Comparar con el Python actual

Leer `tick_simulator_mnq10minv2.py` y comparar cada seccion:
- Parametros CLI y constructor (--colchon, --colchon-be, --breakeven-pct, --trades, --perdida-max, --modo, --cierre, --cant-trail-tp1/tp2, --tp1-actN/stpN, --tp2-actN/stpN)
- Motor de riesgo: position sizing dinamico (presupuesto ideal, contratos calculados, trades permitidos, FUERA_PRESUPUESTO)
- Ventana de rango
- Logica de entrada (breakout levels, flip, cooldown duration)
- Trailing parametrizable (escalones respetan cant_trail_tp1/tp2, porcentajes desde listas tp1_acts/tp1_stps)
- Breakeven (umbral desde breakeven_pct, offset desde colchon_be)
- Post-trade transitions (usa trades_permitidos_hoy, no max_trades fijo)
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

## Parametros actuales del simulador Python

```
--colchon          ColchonStop (pts, default 5)
--colchon-be       ColchonBreakeven (pts, default 5)
--breakeven-pct    Breakeven activacion % (default 60)
--trades           MaxTrades/dia (default 2)
--perdida-max      PerdidaMaxDiaria $ (default 400)
--modo             1a1 o 1a2 (default 1a2)
--cierre           Hora cierre HH:MM ET (default 15:50)
--cant-trail-tp1   Escalones TP1 1-4 (default 4)
--tp1-act1..4      Activacion % TP1 (defaults: 75, 85, 95, 99)
--tp1-stp1..4      Stop % TP1 (defaults: 45, 60, 80, 95)
--cant-trail-tp2   Escalones TP2 1-6 (default 6)
--tp2-act1..6      Activacion % TP2 (defaults: 50, 70, 85, 90, 95, 98)
--tp2-stp1..6      Stop % TP2 (defaults: 18, 50, 60, 70, 84, 94)
-v                 Verbose
```

## Arquitectura del position sizing

El Python replica exactamente el motor de riesgo del C#:
1. `riesgo_1_micro = (rango_pts + colchon_stop) * POINT_VALUE`
2. `presupuesto_ideal = perdida_max / max_trades`
3. Si `presupuesto >= riesgo`: `contratos = floor(presupuesto / riesgo)`, `trades_permitidos = max_trades`
4. Si `riesgo <= perdida_max`: `contratos = 1`, `trades_permitidos = floor(perdida_max / riesgo)`
5. Si `riesgo > perdida_max`: no opera (FUERA_PRESUPUESTO)

Los contratos y qty_tp1/qty_tp2 se recalculan cada dia tras formarse el rango.

## Parametros nuevos

Si el C# agrega un nuevo parametro en `#region Parameters`:
1. Agregar al CLI de Python (`parse_args()`)
2. Agregar al constructor de `TickSimulator`
3. Usarlo en la logica correspondiente
4. Agregar al print de parametros en `run()`
5. Si afecta position sizing, actualizar la seccion del motor de riesgo en la transicion de rango
