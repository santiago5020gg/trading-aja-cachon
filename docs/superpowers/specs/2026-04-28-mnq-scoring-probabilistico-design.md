# MNQ En Caliente — Sistema de Scoring Probabilistico

## Problema

El bot actual (MNQEnCalienteBot.cs) usa logica binaria: `spread > 70 AND body > 60% → ENTRAR`. Esto genera dos problemas:

1. **Falsos rechazos**: spread de 68 o body de 55% descarta trades que podrian ser buenos
2. **Falsa confianza**: spread de 192 + body de 99% parece perfecto pero puede estar sobreextendido (Feb 05: -$183.50)

Claude analizo febrero 2026 "en caliente" usando razonamiento probabilistico — peso multiples factores, los compenso entre si, y tomo decisiones con gradientes. El bot debe replicar ese razonamiento.

## Objetivo

Reescribir el sistema de entrada para que use un **score 0-100** basado en 6 factores con pesos por gradiente. El score determina si entrar, con que agresividad, y como gestionar el trade. Calibrado contra los 19 dias de febrero 2026 para replicar las mismas decisiones que Claude tomo.

## Referencia

- **Estrategia completa**: `.claude/skills/mnq-probabilistic-strategy/SKILL.md`
- **Resultados a replicar**: `.claude/skills/mnq-probabilistic-strategy/resultados-febrero-2026.md`
- **Total neto febrero**: +$2,704.00 en 19 dias (12 positivos, 2 neutros, 5 negativos)

---

## Motor de Scoring — 6 Factores (0-100 cada uno)

### Factor 1: Spread SMA20-SMA200

Mide la fuerza de tendencia. Interpolacion lineal entre puntos.

```
 0 pts  →   0 score
15 pts  →  30 score
30 pts  →  50 score
60 pts  →  75 score
80+ pts → 100 score
```

### Factor 2: Body % de la vela

Mide conviccion de la vela actual.

```
20%  →  10 score
40%  →  40 score
55%  →  65 score
60%  →  75 score
80%+ → 100 score
```

### Factor 3: Alineacion precio/SMAs

Mide coherencia direccional.

Para LONG:
```
Precio ENCIMA ambas + SMA20 > SMA200 → 100 (alineacion perfecta)
Precio ENCIMA ambas + SMA20 < SMA200 →  70 (precio lidera)
Precio ENCIMA SMA20, DEBAJO SMA200   →  40 (transicion)
Precio ENTRE ambas SMAs              →  20 (zona de nadie)
```
Espejo simetrico para SHORT.

### Factor 4: Mechas (wick ratio)

Mide rechazo o indecision. "Mecha a favor" = mecha inferior en long, superior en short.

```
Mecha a favor < 10% del rango → 100 (impulso limpio)
Mecha a favor 10-25%          →  75
Mecha a favor 25-40%          →  50
Mecha a favor > 40%           →  20 (rechazo fuerte)
```

### Factor 5: Momentum

Barras consecutivas cerrando en la misma direccion.

```
3+ barras misma direccion     → 100
2 barras consecutivas         →  75
1 barra (la actual)           →  50
Barra contra direccion previa →  25
```

### Factor 6: ATR relativo

ATR(14) actual / promedio ATR de ultimas 50 barras. Mide si el mercado esta explosivo o dormido.

```
ATR ratio > 1.5   → 100 (explosivo, breakouts confirman)
ATR ratio 1.0-1.5 →  75 (volatilidad normal)
ATR ratio 0.7-1.0 →  50 (tranquilo)
ATR ratio < 0.7   →  30 (dormido, whipsaw probable)
```

---

## Pesos por Tipo de Entrada

Cada tipo de entrada pesa los 6 factores diferente:

| Factor | Tendencia | Pullback | Ruptura |
|--------|-----------|----------|---------|
| Spread | 25% | 15% | 10% |
| Body % | 20% | 15% | 25% |
| Alineacion | 25% | 20% | 20% |
| Mechas | 10% | 15% | 15% |
| Momentum | 10% | 25% | 10% |
| ATR relativo | 10% | 10% | 20% |
| **Total** | **100%** | **100%** | **100%** |

Score final = suma(factor_score * peso) para cada tipo.

---

## Flujo de Evaluacion por Barra

```
Cada barra despues de 09:30 (sesion regular):
├── Calcular los 6 factores
│
├── Aplicar pesos TENDENCIA  → scoreTendencia
├── Aplicar pesos PULLBACK   → scorePullback
├── Aplicar pesos RUPTURA    → scoreRuptura
│
├── Mejor score = max(los 3)
│
├── Si mejor score >= 60 → ENTRAR con ese tipo y esa direccion
├── Si mejor score 40-59 → MONITOREAR (no entrar, seguir evaluando)
└── Si mejor score < 40  → nada, dia tranquilo
```

No hay modos rigidos (Tendencia/Rango/PullbackWatch). Cada barra evalua los 3 tipos y el score mas alto gana. El tipo de entrada emerge del score, no de un state machine.

---

## Score → Gestion Post-Entrada

### Stop inicial y breathe period

| Score de entrada | Stop inicial | Breathe bars | Breakeven trigger |
|-----------------|-------------|-------------|-------------------|
| 80-100 | 0.75x ATR | 2 barras | 50 pts |
| 60-79 | 1.0x ATR | 3 barras | 67.5 pts |

### Trailing adaptativo

- **Score 80+**: trailing bar-a-bar normal. Dejar correr.
- **Score 60-79**: trailing conservador — al llegar a +67.5 pts, mover stop a breakeven+10 pts en vez de solo breakeven. Proteger mas.

### Reglas de la skill que NO cambian

- Trailing bar-a-bar: HH/LL mueve stop al low/high de esa barra
- Stop nunca retrocede
- Max daily loss $270
- Regla de 2 breathe whipsaws → dia terminado
- Sesion 09:30-16:00 ET
- 1 contrato inicial, max 2

---

## Re-entrada y Scaling

### Re-entrada despues de trailing stop

```
Despues de salir por trailing:
├── Recalcular los 6 factores en la barra actual
├── Nuevo score >= 60 + spread saludable → re-entry permitido
├── Nuevo score < 60 → tendencia agotada, no re-entrar
```

### Scaling (agregar contrato)

- Trade 1 en breakeven o mejor
- Score actual del pullback >= 70 (mas exigente que entrada)
- Riesgo combinado < $270

---

## Arquitectura — Un Solo Archivo

Todo dentro de `MNQEnCalienteBot.cs`:

```
MNQEnCalienteBot.cs
│
├── Score Engine (metodos privados nuevos)
│   ├── CalcSpreadScore(sma20, sma200) → 0-100
│   ├── CalcBodyScore(open, close, high, low) → 0-100
│   ├── CalcAlignmentScore(price, sma20, sma200, direction) → 0-100
│   ├── CalcWickScore(open, close, high, low, direction) → 0-100
│   ├── CalcMomentumScore(closes recientes) → 0-100
│   ├── CalcATRScore(atr14, atrSMA50) → 0-100
│   │
│   ├── CalcWeightedScore(tipo, direction) → score total 0-100
│   └── GetBestEntry() → (tipo, score, direccion)
│
├── Trade Manager (adaptado)
│   ├── Breathe period (variable segun score)
│   ├── Trailing bar-a-bar (agresividad segun score)
│   ├── Breakeven (trigger segun score)
│   └── Re-entry (requiere nuevo score >= 60)
│
├── Risk Manager (sin cambios)
│   ├── Max daily loss $270
│   ├── Regla 2 whipsaws
│   └── Stop sizing segun score
│
└── Telemetry (ampliado)
    ├── Scores individuales por factor
    ├── Score total por tipo
    └── Tipo ganador y decision
```

---

## Parametros expuestos en NinjaTrader

Los pesos y umbrales son parametros ajustables:

```csharp
// Umbrales de score
ScoreEntryMin = 60        // minimo para entrar
ScoreHighConfidence = 80  // score alto → gestion agresiva
ScoreScalingMin = 70      // minimo para agregar contrato

// Pesos por tipo (ajustables sin recompilar)
// Se exponen como parametros de NinjaTrader
WeightTendSpread = 25
WeightTendBody = 20
WeightTendAlign = 25
WeightTendWick = 10
WeightTendMomentum = 10
WeightTendATR = 10
// ... igual para Pullback y Ruptura

// Gestion de riesgo (sin cambios)
MaxDailyLoss = 270
MaxBreatheWhipsaws = 2
BreakevenPtsHigh = 50     // score 80+
BreakevenPtsNormal = 67.5 // score 60-79
```

---

## Calibracion — Validar contra Febrero 2026

Antes de usar el bot en otros meses, verificar que para los 19 dias de febrero el scoring replica las decisiones de Claude:

| Dia | Claude decidio | El bot debe |
|-----|---------------|-------------|
| Feb 02 | LONG tendencia 09:32 | Score tendencia >= 60, direccion LONG |
| Feb 03 | SHORT ruptura | Score ruptura >= 60, direccion SHORT |
| Feb 05 | SHORT tendencia (perdio) | Score tendencia >= 60 (la perdida es correcta) |
| Feb 09 | NO 09:32, pullback 10:30 | Score < 60 en 09:32, score pullback >= 60 en 10:30 |
| Feb 11 | NO TRADE | Score < 60 todo el dia |
| Feb 19 | NO TRADE | Score < 60 todo el dia |

Las perdidas deben replicarse tambien — Feb 05, Feb 13, Feb 23, Feb 25, Feb 27 son parte de la probabilidad.

---

## Lo que NO cambia

- Trailing bar-a-bar con HH/LL
- Breathe period con deteccion de whipsaw
- Max daily loss y regla de 2 whipsaws
- Sesion 09:30-16:00 ET
- MNQ en barras de 2 minutos
- SMA20 y SMA200 como indicadores base
- Telemetria JSON a C:\temp
- Archivo unico .cs
