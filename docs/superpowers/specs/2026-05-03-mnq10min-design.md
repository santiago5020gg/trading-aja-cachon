# MNQ10min — Estrategia de Ruptura del Rango de 10 Minutos

## Resumen

Estrategia NinjaTrader 8 NinjaScript para MNQ (Micro E-mini Nasdaq). Opera rupturas del rango formado entre 09:30-09:40 NY en barras de 2 minutos. Entrada por ruptura con confirmación de cuerpo de vela, gestión de riesgo automática, breakeven con adición de contrato, y trailing bar-a-bar.

## Arquitectura

- **Archivo:** `MNQ10min.cs` — estrategia single-file NinjaScript (misma convención que MNQOliver.cs)
- **Timeframe:** Barras de 2 minutos
- **Instrumento:** MNQ (Micro E-mini Nasdaq), 1 punto = $0.50 por contrato
- **Sesión:** 09:30-15:50 NY

## Máquina de Estados

```
ESPERANDO_RANGO → ESPERANDO_RUPTURA → EN_TRADE → TRAILING → DIA_TERMINADO
```

### ESPERANDO_RANGO (09:30-09:40)
- Acumula barras de 2 min entre 09:30 y 09:38 (5 barras: 09:30, 09:32, 09:34, 09:36, 09:38)
- Calcula rangoHigh = max(High de las 5 barras) y rangoLow = min(Low de las 5 barras)
- A las 09:40 el rango queda definido, transición a ESPERANDO_RUPTURA

### ESPERANDO_RUPTURA
- Espera una vela **completada** (OnBarClose) después de las 09:40
- Condición de entrada:
  - Close > rangoHigh → señal LONG
  - Close < rangoLow → señal SHORT
  - Cuerpo de la vela >= PctCuerpoRuptura% del rango de la vela: `|Close - Open| / |High - Low| >= 0.60`
- Al cumplirse, calcula stop y contratos, entra al mercado, transición a EN_TRADE
- También se llega aquí después de un stop loss si quedan oportunidades

### EN_TRADE
- Posición abierta con stop fijo
- Monitorea:
  - **Stop hit** → pérdida registrada, evalúa si quedan oportunidades y riesgo disponible → ESPERANDO_RUPTURA o DIA_TERMINADO
  - **50% del stop alcanzado a favor** → mueve stop a breakeven + agrega 1 contrato market
  - **80% del movimiento a favor** → transición a TRAILING

### TRAILING
- Trailing bar-a-bar:
  - Busca la vela anterior completada más próxima a la vela actual con cuerpo >= PctCuerpoTrailing% del rango de la vela
  - LONG: trailing = Low de esa vela. SHORT: trailing = High de esa vela
  - Cada nueva vela completada: si cuerpo >= PctCuerpoTrailing%, actualiza trailing (solo si es más favorable, nunca retrocede)
- Cierre: precio cruza el nivel de trailing → cierra toda la posición (contratos originales + agregado)
- Evalúa ganancia acumulada vs meta del día

### DIA_TERMINADO
Se alcanza cuando:
- Ganancia acumulada >= meta del día (1:1 del stop del primer trade)
- Pérdida acumulada >= RiesgoMaxDia ($600)
- Se agotaron los MaxTrades
- Riesgo disponible para siguiente trade = 0 contratos
- Hora >= HoraCierre (15:50 NY) → cierre forzado de posiciones abiertas

## Cálculo del Rango

- Barras incluidas: 09:30, 09:32, 09:34, 09:36, 09:38 (5 barras de 2 min)
- rangoHigh = max(High[09:30], High[09:32], High[09:34], High[09:36], High[09:38])
- rangoLow = min(Low[09:30], Low[09:32], Low[09:34], Low[09:36], Low[09:38])
- rangoPuntos = rangoHigh - rangoLow
- La barra de 09:40 NO forma parte del rango, es la primera candidata a ruptura

## Cálculo del Stop

El stop combina el rango de 10 min + el cuerpo de la vela de ruptura + colchón:

- cuerpoVelaRuptura = |Close - Open| de la vela que genera la señal
- **LONG:**
  - stopNivel = rangoLow - cuerpoVelaRuptura - ColchonStop
  - stopPuntos = precioEntrada - stopNivel
- **SHORT:**
  - stopNivel = rangoHigh + cuerpoVelaRuptura + ColchonStop
  - stopPuntos = stopNivel - precioEntrada
- Si stopPuntos > MaxStopPuntos (200): se limita a MaxStopPuntos
  - LONG: stopNivel = precioEntrada - MaxStopPuntos
  - SHORT: stopNivel = precioEntrada + MaxStopPuntos
- El stop se coloca como orden fija y NO se mueve (excepto al breakeven al 50%)

## Cálculo de Contratos

- **Primer trade:** `contratos = floor(RiesgoMaxPrimerTrade / (stopPuntos × 0.50))`
  - Mínimo: 1 contrato
  - Ejemplo: stop 185 pts → riesgo/contrato = $92.50 → floor(400/92.50) = 4 contratos
- **Segundo trade:** `contratos = floor(riesgoDisponible / (stopPuntos × 0.50))`
  - riesgoDisponible = RiesgoMaxDia - |pérdida acumulada|
  - Si contratos = 0 → no entra, DIA_TERMINADO
- **Contrato adicional al breakeven:** siempre +1 contrato market al alcanzar 50% del stop a favor

## Meta del Día

- meta = stopPuntos del primer trade × $0.50 × contratos del primer trade
- Es decir, el 1:1 del riesgo del primer trade
- Si ganancia acumulada >= meta → DIA_TERMINADO
- Esta referencia es fija durante todo el día, no cambia con trades posteriores

## Breakeven + Adición de Contrato

- Se activa cuando el movimiento a favor >= PctBreakeven% del stop en puntos
  - LONG: precio >= precioEntrada + (stopPuntos × PctBreakeven / 100)
  - SHORT: precio <= precioEntrada - (stopPuntos × PctBreakeven / 100)
- Acción:
  1. Mover stop a precioEntrada (breakeven)
  2. Agregar 1 contrato market al precio actual
- Solo se ejecuta una vez por trade

## Trailing Bar-a-Bar

- **Activación:** movimiento a favor >= PctTrailing% del stop en puntos
  - LONG: precio >= precioEntrada + (stopPuntos × PctTrailing / 100)
  - SHORT: precio <= precioEntrada - (stopPuntos × PctTrailing / 100)
- **Lógica:**
  1. Al activarse, busca la vela anterior completada más próxima con cuerpo >= PctCuerpoTrailing%
  2. LONG: trailing = Low de esa vela. SHORT: trailing = High de esa vela
  3. Cada nueva vela completada con cuerpo >= PctCuerpoTrailing% actualiza el trailing
  4. Solo se mueve a favor, nunca retrocede (LONG: nuevo Low > trailing anterior. SHORT: nuevo High < trailing anterior)
- **Cierre:** precio cruza trailing → cierra toda la posición (contratos originales + agregado)

## Trades Siguientes

- Se activa si el trade anterior terminó (stop loss, trailing, o breakeven) Y quedan oportunidades Y ganancia acumulada < meta del día
- Misma regla de entrada: espera vela completada que supere rango con cuerpo >=60%
- Puede ser en la misma dirección o la contraria, lo que el mercado dé primero
- Contratos:
  - Si hay pérdida acumulada: `contratos = floor(riesgoDisponible / (stopPuntos × 0.50))` donde riesgoDisponible = RiesgoMaxDia - |pérdida acumulada|
  - Si no hay pérdida: `contratos = floor(RiesgoMaxPrimerTrade / (stopPuntos × 0.50))`
- Mismas reglas de breakeven, adición de contrato, y trailing

## Cierre Forzado 15:50

- A las 15:50 NY se cierran todas las posiciones abiertas al mercado
- Sin importar si está ganando, perdiendo, o en trailing
- Transición a DIA_TERMINADO

## Parámetros NinjaTrader UI

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| MaxTrades | int | 2 | Oportunidades máximas por día |
| RiesgoMaxDia | double | 600 | USD máximo de pérdida diaria |
| RiesgoMaxPrimerTrade | double | 400 | USD máximo primer trade |
| ColchonStop | int | 5 | Puntos extra de colchón al stop |
| MaxStopPuntos | int | 200 | Máximo stop en puntos |
| HoraCierre | string | "15:50" | Hora NY cierre forzado (HH:mm) |
| PctCuerpoRuptura | int | 60 | % mínimo cuerpo vela de ruptura |
| PctBreakeven | int | 50 | % del stop para mover a breakeven |
| PctTrailing | int | 80 | % del movimiento para activar trailing |
| PctCuerpoTrailing | int | 50 | % mínimo cuerpo vela para trailing |

## Ejemplo de Flujo Completo

```
09:30-09:38  Rango: High=25935, Low=25737 (198 pts)
09:40        Rango listo. Esperando ruptura.
09:44        Vela cierra 25950, cuerpo=28pts, rango=40pts → 70% >=60% OK
             Supera rangoHigh → LONG
             Stop = 25737 - 28 - 5 = 25704 → stopPuntos = 246 > 200 → limita a 200
             Stop final = 25750
             Contratos = floor(400/(200×0.50)) = 4
             Meta día = 200 × 0.50 × 4 = $400
             Entry: 4 LONG @ 25950, Stop @ 25750

10:02        Precio 26050 → mov=100pts = 50% de 200 OK
             Stop → breakeven @ 25950
             +1 contrato market → total 5 contratos

10:18        Precio 26110 → mov=160pts = 80% de 200 OK
             Activa TRAILING
             Vela 10:16: Low=26090, cuerpo 75% → trailing @ 26090

10:20        Vela completa cuerpo 60%, Low=26100 > 26090 → trailing sube a 26100
10:22        Vela completa cuerpo 40% → no actualiza
10:24        Precio 26098 < 26100 → cierra 5 contratos
             P&L = (26100-25950) × 0.50 × 5 = $375
             $375 < $400 meta → queda 1 oportunidad

10:30        Vela cierra 25720, cuerpo=22pts, rango=35pts → 63% >=60% OK
             Supera rangoLow → SHORT
             Stop = 25935 + 22 + 5 = 25962 → stopPuntos = 242 > 200 → limita a 200
             Stop final = 25920
             riesgoDisponible = 600 - 0 = 600 (primer trade ganó)
             Contratos = floor(400/(200×0.50)) = 4
             Entry: 4 SHORT @ 25720, Stop @ 25920

10:50        Stop hit @ 25920, pérdida = 200 × 0.50 × 4 = $400
             Trades usados: 2 de 2 → DIA_TERMINADO
```
