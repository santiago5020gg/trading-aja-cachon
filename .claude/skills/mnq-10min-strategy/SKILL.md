---
name: mnq-10min-strategy
description: Estrategia de ruptura del rango de 10 minutos (09:30-09:40) para MNQ futures. Entrada por ruptura con cuerpo >=60%, stop basado en rango + vela de ruptura, breakeven al 50%, add-on al breakeven, trailing bar-a-bar al 80%.
when_to_use: Usar cuando se analicen dias de trading MNQ con la estrategia de 10 minutos, se evaluen rupturas del rango 09:30-09:40, se simule operativa con datos de Playback, o se tomen decisiones de entrada/salida basadas en el rango de apertura.
allowed-tools:
  - mcp__ninjatrader__get_current_bar
  - mcp__ninjatrader__get_bar_history
  - mcp__ninjatrader__get_market_context
  - mcp__ninjatrader__playback_goto
  - mcp__ninjatrader__playback_pause
  - mcp__ninjatrader__playback_resume
  - mcp__ninjatrader__playback_status
---

# Estrategia MNQ10min — Ruptura del Rango de 10 Minutos

Opera MNQ (Micro E-mini Nasdaq) en barras de 2 minutos. Espera que se forme el rango de los primeros 10 minutos de sesion regular (09:30-09:40 ET), luego opera rupturas de ese rango con confirmacion de cuerpo de vela.

**Referencia**: 1 punto MNQ = $0.50 por contrato (1 tick). Riesgo maximo dia = $600.

**Spec completo**: `docs/superpowers/specs/2026-05-03-mnq10min-design.md`

---

## FASE 1: Calculo del Rango (09:30-09:40)

### Barras incluidas
5 barras de 2 minutos: 09:30, 09:32, 09:34, 09:36, 09:38

### Calculo
- **rangoHigh** = max(High de las 5 barras)
- **rangoLow** = min(Low de las 5 barras)
- **rangoPuntos** = rangoHigh - rangoLow

### Importante
- La barra de 09:40 NO forma parte del rango — es la primera candidata a ruptura
- Si no hay datos validos para el rango, no se opera ese dia

---

## FASE 2: Deteccion de Ruptura (despues de 09:40)

### Condiciones de entrada
Esperar una vela **completada** (no la actual) que cumpla:
1. **Close > rangoHigh** → señal LONG, o **Close < rangoLow** → señal SHORT
2. **Cuerpo de la vela >= 60%** del rango de la vela: `|Close - Open| / |High - Low| >= 0.60`

### Ambas direcciones
El bot opera tanto long como short, lo que el mercado de primero.

---

## FASE 3: Calculo del Stop

El stop combina tres componentes:

```
Stop = Rango de 10 min + Cuerpo de la vela de ruptura + Colchon
```

### Para LONG:
- stopNivel = rangoLow - cuerpoVelaRuptura - colchon (default 5 pts)
- stopPuntos = precioEntrada - stopNivel

### Para SHORT:
- stopNivel = rangoHigh + cuerpoVelaRuptura + colchon
- stopPuntos = stopNivel - precioEntrada

### Limite
- Si stopPuntos > 200 → se limita a 200 puntos
- El stop se coloca FIJO y NO se mueve (excepto al breakeven)

---

## FASE 4: Calculo de Contratos

### Automatico basado en riesgo
```
contratos = floor(riesgoMax / (stopPuntos x $0.50))
```

### Primer trade
- riesgoMax = $400
- Ejemplo: stop 185 pts → $92.50/contrato → floor(400/92.50) = 4 contratos

### Trades siguientes (si el primero pierde)
- riesgoDisponible = $600 - perdidaAcumulada
- contratos = floor(riesgoDisponible / (stopPuntos x $0.50))
- Si contratos = 0 → dia terminado

---

## FASE 5: Gestion del Trade

### 5.1 Breakeven (al 50% del stop)
- Cuando movimiento a favor >= 50% del stop en puntos
- Accion: mover stop a precio de entrada (breakeven)
- Agregar 1 contrato market al precio actual
- Solo se ejecuta una vez por trade

### 5.2 Trailing Bar-a-Bar (al 80% del stop)
- Se activa cuando movimiento a favor >= 80% del stop
- Busca la vela anterior completada mas proxima con cuerpo >= 50% del rango
- LONG: trailing = Low de esa vela
- SHORT: trailing = High de esa vela
- Cada nueva vela completada con cuerpo >= 50% actualiza el trailing
- Solo se mueve a favor, nunca retrocede
- Cierre: precio cruza nivel de trailing → cierra TODA la posicion (originales + add-on)

---

## FASE 6: Meta del Dia y Cierre

### Meta del dia
- meta = stopPuntos del primer trade x $0.50 x contratos del primer trade
- Es el 1:1 del riesgo del primer trade
- Si ganancia acumulada >= meta → dia terminado
- Esta referencia es FIJA, no cambia con trades posteriores

### Dia terminado cuando:
- Ganancia >= meta del dia
- Perdida >= $600
- Se agotaron los trades (default 2)
- Sin contratos disponibles para siguiente trade
- Hora >= 15:50 NY → cierre forzado de todo

---

## FASE 7: Segundo Trade

- Se activa si el primero termino y quedan oportunidades y no se alcanzo la meta
- **Misma regla de entrada**: espera vela completada que supere rango con cuerpo >= 60%
- Puede ser misma direccion o contraria
- Contratos basados en riesgo disponible restante
- Mismas reglas de breakeven, add-on, y trailing

---

## PARAMETROS CONFIGURABLES

| Parametro | Default | Descripcion |
|-----------|---------|-------------|
| MaxTrades | 2 | Oportunidades por dia |
| RiesgoMaxDia | $600 | Perdida maxima diaria |
| RiesgoMaxPrimerTrade | $400 | Riesgo maximo primer trade |
| ColchonStop | 5 pts | Puntos extra de colchon al stop |
| MaxStopPuntos | 200 pts | Maximo stop en puntos |
| HoraCierre | 15:50 | Hora NY cierre forzado |
| PctCuerpoRuptura | 60% | Minimo cuerpo vela de ruptura |
| PctBreakeven | 50% | % del stop para breakeven |
| PctTrailing | 80% | % del movimiento para trailing |
| PctCuerpoTrailing | 50% | Minimo cuerpo vela para trailing |

---

## CHECKLIST RAPIDO — Analisis de un Dia

1. Cual fue el rango 09:30-09:40? (rangoHigh, rangoLow, rangoPuntos)
2. Hubo ruptura despues de 09:40? Que vela la genero?
3. El cuerpo de la vela de ruptura fue >= 60%?
4. Cuantos puntos de stop dio? (rango + cuerpo + colchon)
5. Cuantos contratos calcularia? (riesgo / (stop x $0.50))
6. Se alcanzo el 50% para breakeven?
7. Se alcanzo el 80% para trailing?
8. Como termino el trade? (trailing, stop, cierre forzado)
9. Se alcanzo la meta del dia?

---

## EJEMPLO DE ANALISIS

```
Dia: 2 de febrero 2026
Rango 09:30-09:40: High=25935.25, Low=25737.25 → 198 puntos
Ruptura: vela 09:44 cierra 25950, cuerpo=28pts, rango=40pts → 70% OK
Direccion: LONG (supera rangoHigh)
Stop: 25737.25 - 28 - 5 = 25704.25 → 245.75 pts > 200 → limita a 200
Stop final: 25750
Contratos: floor(400/(200x0.50)) = 4
Meta dia: 200 x 0.50 x 4 = $400
```
