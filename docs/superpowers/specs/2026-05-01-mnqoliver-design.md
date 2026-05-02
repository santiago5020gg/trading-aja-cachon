# MNQOliver — Diseño de Estrategia v4

**Fecha**: 2026-05-01
**Reemplaza**: MNQEnCalienteBot.cs (scoring probabilístico v3)
**Problema**: El bot v3 tiene 29 parámetros optimizados contra Feb+Abr que causan overfitting. Falla en meses no incluidos en la calibración (Mar: -$394).
**Solución**: Estrategia nueva basada en principios universales de price action (Oliver Velez) + ATR como normalizador de volatilidad + walk-forward validation.

---

## 1. Arquitectura General

### Filosofía

En vez de preguntar "¿qué score tiene esta barra?" (6 factores × pesos = overfitting), la estrategia pregunta:

1. ¿Hay condiciones de mercado? (SOH filter)
2. ¿Cuál es la dirección? (EMA20 vs SMA200)
3. ¿En qué fase del movimiento estoy? (spread/ATR)
4. ¿Es buen momento del día? (Fases del día)
5. ¿Hay vela detonante? (body% + rango/ATR)

### Indicadores (solo 4)

- **EMA(20)** — dirección rápida (reemplaza SMA20)
- **SMA(200)** — contexto macro
- **ATR(14)** — normalizador universal de volatilidad
- **SMA(ATR, 50)** — promedio del ATR para calcular ratio

### Parámetros (11 total)

**Fijos (3) — nunca optimizar:**
- MaxDailyLoss = $270
- DailyProfitTarget = $270
- MaxTradesPerDay = 3

**Walk-forward (8) — validar con protocolo de Sección 4:**
| # | Parámetro | Valor propuesto | Qué controla |
|---|-----------|----------------|--------------|
| 1 | SOH_AtrRatio | 0.80 | Umbral ATR para detectar rango |
| 2 | SOH_EmaSlopeMin | 15 | Pendiente EMA mínima (pts en 10 barras) |
| 3 | FaseMadura_SpreadATR | 3.0 | Spread/ATR donde movimiento está agotado |
| 4 | Detonante_BodyPct | 0.65 | Mínimo body% para vela detonante |
| 5 | Detonante_RangoATR | 1.0 | Mínimo rango/ATR para vela detonante |
| 6 | Stop_AtrMult | 1.5 | Multiplicador ATR para stop inicial |
| 7 | Trail_AtrBuffer | 0.3 | Buffer ATR adicional en trailing |
| 8 | Breakeven_AtrMult | 1.5 | Distancia ATR para activar breakeven |

### Lo que se elimina del bot v3

- 6 factores de scoring (Spread, Body, Align, Wick, Momentum, ATR)
- 18 pesos por tipo de entrada (WeightTend*, WeightPull*, WeightRupt*)
- 3 umbrales de score (ScoreEntryMin, ScoreHighConfidence, ScoreScalingMin)
- Spread fijo 80-170
- 3 tipos de entrada separados (Tendencia, Pullback, Ruptura)
- Scaling in
- Choppy mode reactivo
- SMA20 (reemplazado por EMA20)

### Lo que se mantiene

- $270 max loss / $270 profit target
- Trailing bar-a-bar (mejorado con buffer ATR y 2 barras)
- Sesión americana 09:32-15:50 NY
- SMA(200), ATR(14), SMA(ATR,50)
- Breathe period (2 barras)
- Breakeven (ahora ATR-based)
- 1 contrato MNQ
- Logging CSV (trades, daily, bar)
- MCPBridge + MCPBridgeIndicator (sin cambios)

---

## 2. Flujo de Decisión Barra a Barra

Cada barra de 2 minutos, el bot ejecuta este flujo en orden. Si falla cualquier paso, se detiene ahí.

### Paso 0: ¿Puedo operar hoy? (CheckDayLimits)

```
SI dailyPnL <= -$270  → STOP día (max loss)
SI dailyPnL >= +$270  → STOP día (profit target)
SI tradesHoy >= 3     → STOP día (límite trades)
SI hora < 09:32 NY    → ESPERAR
SI hora >= 15:50 NY   → cerrar posición si hay, STOP día
SI hora >= 15:30 NY   → solo gestionar trade abierto, no abrir nuevo
SI posición abierta   → IR A Paso 7 (gestión)
```

### Paso 1: ¿Hay condiciones de mercado? (CheckSOH)

Detecta rango ANTES de perder dinero (proactivo, no reactivo).

```
atrRatio = ATR(14) / SMA(ATR, 50)
emaPendiente = |EMA20[0] - EMA20[10]|

SI atrRatio < SOH_AtrRatio AND emaPendiente < SOH_EmaSlopeMin → SOH, no operar
```

**Excepción breakout**: si estamos en SOH pero:
- Precio cierra fuera del rango reciente (high/low últimas 20 barras)
- Y ATR ratio cruza por encima de 1.0
→ Evaluar como posible entrada en Paso 2.

**Base teórica**: Oliver Velez p.98: "Cuando el Trader JR está en un mercado lateral, cruzamos los brazos (SOH) y esperamos." La 20ma plana es "el verdadero ingrediente del mercado lateral" (p.96-97).

### Paso 2: ¿Cuál es la dirección? (GetDirection)

```
SI precio > EMA20 AND EMA20 > SMA200  → SOLO LONGS (tendencia alcista fuerte)
SI precio > EMA20 AND EMA20 < SMA200  → SOLO LONGS (alcista, cautela)
SI precio < EMA20 AND EMA20 < SMA200  → SOLO SHORTS (tendencia bajista fuerte)
SI precio < EMA20 AND EMA20 > SMA200  → SOLO SHORTS (bajista, cautela)
```

Nunca operar contra la EMA20. Si precio cruza EMA20, cambiar sesgo pero esperar confirmación (vela detonante).

**Base teórica**: Velez p.29: "Los iFund Traders buscan ir en largo cuando la 20ma está por encima de la 200ma. Buscan ir en corto cuando la 20ma está por debajo de la 200ma."

### Paso 3: ¿En qué fase del movimiento estoy? (GetMovementPhase)

```
spreadEMA = |EMA20 - SMA200|
spreadNormalizado = spreadEMA / ATR(14)

SI spreadNormalizado < 1.5   → FASE TEMPRANA (post-ignición, entrar)
SI spreadNormalizado 1.5-3.0 → FASE MEDIA (aceptable)
SI spreadNormalizado > 3.0   → FASE MADURA (NO entrar, movimiento agotado)
```

Normalizar con ATR resuelve el problema de "qué es lejos" — se adapta automáticamente a la volatilidad del día.

**Base teórica**: Velez p.7: Vela Elefante de Ignición (inicio) vs Exhausta (final). Velez p.51: "Si la acción va muy lejos por debajo o por encima de la 20ma, y la 20ma llega muy lejos de la 200ma, un reverso mayor estará usualmente cercano."

### Paso 4: ¿Es buen momento del día? (CheckDayPhase)

```
SI hora 09:32 - 11:15  → FASE1 (prioridad, hasta 2 trades)
SI hora 11:15 - 14:15  → FASE2 (SOH, no abrir trades nuevos)
SI hora 14:15 - 15:30  → FASE3 (permitido, máximo 1 trade)
```

**Evidencia**: Los 5 mejores trades del bot en Feb (+$529, +$359, +$352, +$219, +$159) fueron todos en Fase1. Los trades en Fase2 fueron mayoritariamente whipsaws negativos.

**Base teórica**: Velez p.193-194: Fase 1 (apertura-11:15), Fase 2 (11:15-2:15, "zona de calma ecuatorial"), Fase 3 (2:15-cierre, "a menudo la más lucrativa").

### Paso 5: ¿Hay vela detonante? (CheckDetonante)

La barra actual debe cumplir AMBAS condiciones:

```
bodyPct = |Close - Open| / (High - Low)
rangoRelativo = (High - Low) / ATR(14)

SI bodyPct >= Detonante_BodyPct AND rangoRelativo >= Detonante_RangoATR → DETONANTE
```

- bodyPct >= 0.65: convicción (65%+ del rango es cuerpo). Velez: "buen control" o mejor.
- rangoRelativo >= 1.0: vela más grande que el promedio. Vela "elefante" relativa.

**Entrada alternativa — RBI/GBI (Red/Green Bar Ignored):**

```
SI dirección = LONG:
   SI Close[0] > High[1] AND Close[1] < Open[1]  → RBI, entrar
SI dirección = SHORT:
   SI Close[0] < Low[1] AND Close[1] > Open[1]   → GBI, entrar
```

**Base teórica**: Velez p.125: "Una vez identificas una vela detonante, el momentum de compra está hecho cuando el máximo de la vela detonante es borrado." Velez p.210-217: RBI/GBI — "Cuando una vela roja es ignorada, tiende a seguir movimientos explosivos."

### Paso 6: Entrar al trade (EnterTrade)

```
SI dirección = LONG  → BUY 1 MNQ a Market
SI dirección = SHORT → SELL 1 MNQ a Market

stopInicial = entryPrice ∓ (Stop_AtrMult × ATR14)  // 1.5 × ATR
tradesHoy++
```

Stop inicial ATR-based: más amplio en días volátiles, más apretado en días tranquilos.

### Paso 7: Gestión del trade abierto (ManageTrade)

**Breathe period (primeras 2 barras):**
```
SI barrasEnTrade <= 2 → mantener stop inicial, no mover
```

**Trailing bar-a-bar mejorado (barra 3+):**

```
SI dirección = LONG:
   nuevoTrail = MIN(Low[0], Low[1]) - (Trail_AtrBuffer × ATR14)
   SI nuevoTrail > trailActual → mover trail (nunca retrocede)

SI dirección = SHORT:
   nuevoTrail = MAX(High[0], High[1]) + (Trail_AtrBuffer × ATR14)
   SI nuevoTrail < trailActual → mover trail (nunca retrocede)
```

Diferencias vs bot v3:
- Usa mínimo de **2 barras** (no 1) → evita salidas por mechas de 1 barra
- Buffer de **0.3 × ATR** → espacio para ruido normal de MNQ
- Trail nunca retrocede

**Breakeven adaptativo:**
```
SI ganancia no-realizada > Breakeven_AtrMult × ATR14:
   mover stop a entry + (0.3 × ATR14)
```

**Salida por límites diarios:**
```
SI dailyPnL + ganancia_no_realizada >= $270 → cerrar trade
SI precio toca stop → cerrar trade
```

### Flujo visual

```
Cada barra 2 min:
  ├─ Paso 0: ¿Puedo operar? ────── NO → esperar/stop día
  ├─ Paso 1: ¿SOH? ─────────────── ATR bajo + EMA plana → no operar
  ├─ Paso 2: ¿Dirección? ───────── Precio vs EMA20 → LONG o SHORT
  ├─ Paso 3: ¿Fase movimiento? ─── Spread/ATR > 3.0 → maduro, NO entrar
  ├─ Paso 4: ¿Momento del día? ─── Fase2 → no operar
  ├─ Paso 5: ¿Vela detonante? ──── body% + rango/ATR → SÍ/NO (o RBI/GBI)
  ├─ Paso 6: ENTRAR ─────────────── Stop = 1.5 × ATR
  └─ Paso 7: GESTIÓN ────────────── Trail 2-barras + buffer ATR
```

---

## 3. Manejo del Rango

### Detección proactiva (SOH)

La combinación ATR ratio + EMA pendiente detecta rango ANTES de entrar:

- **ATR ratio < 0.80**: volatilidad comprimida
- **EMA20 plana** (pendiente < 15 pts en 10 barras): sin dirección

Cuando AMBAS coinciden → no operar.

### Breakout del rango

Si estamos en SOH y el precio rompe el rango, se evalúa como entrada SOLO si:
1. Precio cierra fuera del high/low de las últimas 20 barras
2. ATR ratio cruza por encima de 1.0 (volatilidad expandiéndose)

Si el precio rompe pero ATR no expande → breakout falso, seguir en SOH.

### Evidencia

- Bot v3: días con 3+ whipsaws = negativos (Feb 18: -$161, Feb 24: -$87)
- Choppy mode actual es reactivo (detecta DESPUÉS de perder)
- SOH proactivo evita esas pérdidas antes de que ocurran

---

## 4. Walk-Forward Validation

### Protocolo

**Ronda 1**: Calibrar en Feb → Validar en Mar (sin tocar)
**Ronda 2**: Calibrar en Feb+Mar → Validar en Abr (sin tocar)
**Ronda 3**: Calibrar en Feb+Mar+Abr → Validar en Mayo (demo, sin tocar)

### Criterios de aprobación por ronda

| Criterio | Umbral |
|----------|--------|
| P&L mes de validación | > -$150 |
| Días verdes | > 50% |
| Max drawdown intraday | < $400 |
| Profit Factor | > 1.0 |
| Win rate | > 35% |

### Reglas anti-overfitting

1. Máximo 3 rondas de ajuste por mes de calibración
2. Solo ajustar 1-2 parámetros por ronda
3. Cada cambio debe tener razón lógica documentada
4. Parámetros fijos nunca se tocan
5. Test de sensibilidad: ±20% en cada parámetro no debe destruir resultados

### Si falla validación

- P&L > -$150: aceptable, continuar
- P&L -$150 a -$400: revisar trades, ajustar 1 parámetro, re-validar
- P&L < -$400: volver al diseño, la estrategia no es robusta

---

## 5. Estructura del Archivo MNQOliver.cs

```
MNQOliver : Strategy
│
├── Properties (11 NinjaScriptProperty)
├── Indicators (ema20, sma200, atr14, atrSma50)
│
├── OnStateChange()          — inicializar indicadores
├── OnBarUpdate()            — punto de entrada principal
│   ├── CheckDayLimits()     — Paso 0: límites diarios y horarios
│   ├── CheckSOH()           — Paso 1: filtro rango (ATR + EMA slope)
│   ├── GetDirection()       — Paso 2: precio vs EMA20 vs SMA200
│   ├── GetMovementPhase()   — Paso 3: spread normalizado por ATR
│   ├── CheckDayPhase()      — Paso 4: Fase1/2/3 del día
│   ├── CheckDetonante()     — Paso 5: vela detonante o RBI/GBI
│   ├── EnterTrade()         — Paso 6: entrada + stop ATR-based
│   └── ManageTrade()        — Paso 7: breathe + trail + breakeven
│
├── ResetDaily()             — reset contadores a las 09:30
├── LogTrade()               — CSV: fecha, hora, acción, dirección, P&L, ATR, fase
└── LogDaily()               — CSV: fecha, P&L, trades, whipsaws, razón cierre
```

---

## 6. Principios de Oliver Velez Incorporados

| Principio | Cómo se implementa | Referencia libro |
|-----------|-------------------|-----------------|
| 20MA como soporte/resistencia | EMA20 define dirección (Paso 2) | p.29, 36, 40 |
| Vela Elefante / Detonante | Paso 5: bodyPct + rango/ATR | p.4, 7, 125 |
| RBI/GBI (barra ignorada) | Paso 5: entrada alternativa | p.210-217 |
| SOH en mercado lateral | Paso 1: ATR + EMA plana | p.96-98 |
| No tomar 3ro+ pullback | MaxTradesPerDay = 3 | p.98 |
| Fases del día | Paso 4: Fase1/2/3 | p.193-194 |
| Niveles de control (body%) | Paso 5: bodyPct como filtro | p.8-14 |
| Trailing vela-a-vela | Paso 7: mejorado con 2 barras + ATR | p.76-78 |
| 200MA como contexto | Paso 2 + Paso 3: spread EMA20-SMA200 | p.51 |
| Ignición vs Exhausta | Paso 3: fase temprana vs madura | p.7 |

---

## 7. Principios de Velez NO Incorporados (y por qué)

| Principio | Por qué NO | Evidencia |
|-----------|-----------|-----------|
| Regla 3-5-8 barras máx | MNQ tiene tendencias de 15-30+ barras | Datos Feb: movimientos de 20-43 barras |
| Horas de Reacción (10:00, 11:15) | ~35% coincidencia en MNQ, no mejor que azar | Análisis cruzado 8 días Feb |
| 8MA como trailing | EMA8 genera más salidas prematuras que 2-bar trail | MNQ tiene mechas más grandes que acciones |
| Fade del rango (bid/offer) | Velez dice "no debe ser ejecutado" por Trader JR | p.98: es movimiento avanzado |
