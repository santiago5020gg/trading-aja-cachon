---
name: mnq-probabilistic-strategy
description: Estrategia probabilistica completa para operar MNQ futures en sesion americana usando barras de 2 minutos, SMA20/SMA200, y trailing bar-a-bar. Incluye entrada por tendencia, pullback a SMA20, y ruptura de rango. Usar cuando se analicen dias de trading o se tomen decisiones de entrada/salida.
---

# Estrategia Probabilistica MNQ — Operativa "En Caliente"

Esta estrategia opera MNQ (Micro E-mini Nasdaq) en barras de 2 minutos durante la sesion regular americana (09:30-16:00 ET). Todas las decisiones se toman EN CALIENTE — solo con informacion disponible en el momento, NUNCA con conocimiento de velas futuras.

**Referencia**: 1 punto MNQ = $2 (4 ticks x $0.50/tick). $270 = 135 puntos.

---

## FASE 1: Evaluacion Pre-Entrada (09:30-09:32)

Al abrir la sesion regular, evaluar 3 condiciones simultaneas:

### 1.1 Spread SMA20-SMA200
- **>80 puntos**: Tendencia establecida, hay setup potencial
- **60-80 puntos**: Zona gris, precaucion
- **<60 puntos**: Sin señal de tendencia en 09:32. Evaluar si aplica RUPTURA DE RANGO (ver FASE 3B)

### 1.2 Posicion del precio respecto a SMAs
- **Precio ENCIMA de ambas SMAs**: Setup alcista
- **Precio DEBAJO de ambas SMAs**: Setup bajista
- **Precio ENTRE las SMAs**: NO HAY SETUP de tendencia en 09:32, esperar

### 1.3 Anatomia de la vela 09:32
- **Body >60% del rango**: Alta conviccion. El color del cuerpo indica direccion
- **Body 50-60%**: Conviccion media, considerar con precaucion
- **Body <50%**: Baja conviccion / doji / rechazo. NO entrar por tendencia
- **Mecha grande**: Indica rechazo. Una vela roja con mecha inferior grande NO es completamente bajista — el mercado rechazo el downside

---

## FASE 2: Arbol de Decision de Entrada

```
09:32 llega:
├── Spread >80 + precio FUERA de ambas SMAs + body >60%
│   └── ENTRAR en direccion del cuerpo de la vela (FASE 4: trail)
│       ├── Vela VERDE + precio ENCIMA SMAs → LONG
│       └── Vela ROJA + precio DEBAJO SMAs → SHORT
│
├── Spread <30 + precio cruza AMBAS SMAs + body >60%
│   └── RUPTURA DE RANGO (ver FASE 3B). Entrar en direccion del quiebre
│
├── Spread <60 + body <50% + precio ENTRE SMAs
│   └── RANGO confirmado. Monitorear para RUPTURA (FASE 3B)
│
├── Spread 30-60 OR body <50% OR precio ENTRE SMAs (sin ruptura)
│   └── NO ENTRY en 09:32. Esperar pullback (FASE 3A)
│
└── Señales mixtas (spread 60-80, body 50-60%)
    └── NO ENTRY. Esperar pullback (FASE 3A)
```

---

## FASE 3A: Entrada por Pullback a SMA20 (post-09:32)

Cuando 09:32 no da señal de tendencia ni de ruptura, monitorear barra a barra:

### Condiciones para entrada por pullback:
1. **Las SMAs se cruzan** despues de 09:32 (SMA20 cruza SMA200)
2. **El spread CRECE** despues del cruce (se esta formando tendencia)
3. **El precio hace pullback** hasta tocar o acercarse a SMA20
4. **El precio REBOTA** en SMA20 (la barra cierra del lado correcto)

### Confirmacion del rebote:
- Para LONG: precio toca/baja a SMA20 y la barra cierra ENCIMA de SMA20
- Para SHORT: precio sube a SMA20 y la barra cierra DEBAJO de SMA20

### Importante:
- No hay hora especifica para esta entrada. Puede ser 10:00, 11:00, o 14:00
- Lo que importa es que las condiciones se cumplan
- Si las SMAs nunca cruzan o el spread no crece, NO HAY TRADE

---

## FASE 3B: Ruptura de Rango (Range Breakout)

Cuando las SMAs estan CONVERGIDAS (spread <30 pts), actuan como una sola linea de soporte/resistencia. El rango es un resorte comprimido — cuando rompe, el movimiento es explosivo.

### Deteccion del rango:
- Spread SMA <30 pts (SMAs casi pegadas)
- Precio cruzando SMA20 repetidamente (5+ veces)
- SMAs cruzan y re-cruzan entre si
- Volumen sin direccion clara

### Condiciones para entrada por ruptura:
1. **SMAs convergidas** (spread <30 pts) — el rango esta establecido
2. **Precio cruza AMBAS SMAs** en la misma direccion en 1-2 barras
3. **La barra de cruce tiene body grande** (no doji) y volumen creciente
4. **La siguiente barra CONFIRMA** — cierra del mismo lado (DEBAJO o ENCIMA de ambas)

### Ejecucion:
```
TRIGGER: Close cruza DEBAJO (o ENCIMA) de AMBAS SMAs convergidas
CONFIRMACION: Siguiente barra cierra del mismo lado
ENTRADA: En la barra de confirmacion (o en la de cruce si body es >60%)
STOP: High de la barra de cruce (SHORT) o Low de la barra de cruce (LONG)
TRAIL: Bar-a-bar normal (FASE 4)
```

### Cuando aplica:
- Puede activarse en 09:32 si la primera barra rompe ambas SMAs convergidas con body grande
- Puede activarse en cualquier momento de la sesion si el precio sale del rango
- Feb 10 a las 13:06: SMAs convergidas en ~25585, precio rompio ambas → SHORT captura 60 pts
- Feb 17 a las 13:22: SMAs re-convergidas a 26 pts despues de whipsaw matutino → LONG captura 131 pts

### Re-convergencia: el rango puede romper MAS DE UNA VEZ en el dia

Un dia que arranca en rango y tiene whipsaw por la mañana NO esta descartado. El resorte puede re-comprimirse:

**Señales de que el rango esta madurando para romper de nuevo:**
1. **Spread vuelve a <30 pts** — despues de haberse separado, las SMAs vuelven a juntarse. El resorte se re-comprime
2. **SMAs se alinean sin re-cruzar** — si SMA20 cruza SMA200 y se QUEDA del mismo lado por 10+ barras, una direccion esta ganando
3. **Volatilidad barra-a-barra se reduce** — las barras se hacen mas pequeñas vs el caos previo. El mercado esta decidiendo
4. **Precio deja de cruzar SMA20 repetidamente** — en rango cruza 5+ veces en minutos; antes de ruptura, los cruces se espacian o desaparecen

**Regla clave: mientras las SMAs esten convergidas, SEGUIR MONITOREANDO.** No dejar de buscar ruptura porque hubo caos previo. El rango ES el setup — toda la sesion, mañana Y tarde.

### Datos validados — Dias con spread <30 que rompieron:

| Dia | Spread | Movimiento post-ruptura | Direccion | Resultado |
|-----|--------|------------------------|-----------|-----------|
| Feb 03 | 17 pts | 261 pts ($522) | SHORT | +$617 |
| Feb 10 | 18 pts | 88 pts ($176) | SHORT | +$119.50 |
| Feb 12 | 23 pts | 305 pts ($610) | SHORT | +$248 |
| Feb 13 | 21 pts | whipsaw | SHORT | -$112 |
| Feb 17 | 26 pts (re-convergencia) | 131 pts ($262) | LONG | +$262 |
| Feb 23 | 3 pts | whipsaw | SHORT | -$97 |
| Feb 24 | 8 pts | 210 pts ($420) | LONG | +$338.50 |
| Feb 26 | 6 pts | 461 pts ($922) | SHORT | +$110.50 |
| Feb 27 | 15 pts | whipsaw x2 (volatilidad extrema) | SHORT+LONG | -$129.50 |

**Rupturas exitosas**: 6/9 (66.7%) — promedio +$282.58
**Rupturas fallidas (whipsaw)**: 3/9 (33.3%) — promedio -$112.83
**Net expectancy por ruptura**: +$150.89

Las SMAs convergidas son un resorte — menor spread = mayor explosion potencial. Pero 1 de cada 3 rupturas falla por whipsaw, asi que el riesgo debe ser manejable.

---

## FASE 4: Gestion de Trade — Trailing Bar-a-Bar

### 4.1 Periodo de Respiracion (Breathe Period)
- **3 barras** despues de la entrada antes de mover el stop
- Stop inicial: low de la barra de entrada (LONG) o high de la barra de entrada (SHORT)
- Si en las 3 barras de respiracion hay WHIPSAW (precio cruza stop y regresa), es señal de RANGO — salir y no re-entrar por ese setup
- **Regla de 2 whipsaws**: Si en el mismo dia hay DOS trades que salen por whipsaw en breathe, el dia esta TERMINADO — no buscar un tercer breakout. La volatilidad intra-barra es demasiado alta para que el trailing funcione. Feb 27: dos breakouts validos (SHORT 10:52, LONG 14:04), ambos whipsaw en breathe → -$129.50. El tercer breakout (15:12) hubiera funcionado, pero la disciplina de preservar capital despues de 2 breathe-whipsaws es correcta a largo plazo

### 4.2 Trailing Bar-a-Bar
Despues del breathe period:

**Para LONG:**
- Cada vez que se forma un nuevo Higher High (HH), mover stop al LOW de esa barra
- Si no hay nuevo HH, mantener stop donde esta
- El stop NUNCA retrocede — solo avanza

**Para SHORT:**
- Cada vez que se forma un nuevo Lower Low (LL), mover stop al HIGH de esa barra
- Si no hay nuevo LL, mantener stop donde esta

### 4.3 Breakeven
- Cuando el trade acumula +$135 (67.5 pts) o mas, mover stop a breakeven (precio de entrada)
- Esto protege capital y habilita la posibilidad de agregar contratos

---

## FASE 5: Deteccion de Rango — Monitorear para Ruptura

### Señales de rango (cualquiera activa la alerta):
1. **Whipsaw en breathe bars**: El precio activa el stop y regresa en las primeras 3 barras
2. **SMAs cruzan y RE-cruzan**: SMA20 cruza SMA200, luego vuelve a cruzar en direccion opuesta
3. **Precio cruza SMA20 repetidamente**: 5+ veces en pocas barras
4. **Spread SMAs converge a <20 puntos**: Las medias se juntan, no hay tendencia
5. **Doji en 09:32 + spread <30**: Indecision total desde el inicio

### Que hacer:
- Si detectas rango: NO entrar por tendencia ni por pullback
- **MONITOREAR para RUPTURA (FASE 3B) TODA LA SESION** — mañana Y tarde
- Si el precio rompe AMBAS SMAs convergidas con confirmacion → entrar por ruptura
- Si un breakout falla (whipsaw), verificar si las SMAs RE-CONVERGEN despues. Si vuelven a <30 pts → hay un SEGUNDO setup de ruptura valido
- Si nunca rompe limpiamente → dia de $0 (preservar capital)
- Un dia de $0 es un buen dia — preserva capital para dias con tendencia clara

---

## FASE 6: Agregar Contratos (Scaling In)

### Pre-condiciones (TODAS deben cumplirse):
1. Trade 1 ya esta en breakeven o mejor (stop >= precio entrada)
2. El riesgo del contrato adicional es <$135 (mitad del riesgo maximo)
3. El pullback es LIMPIO: 1-3 barras bajando a SMA20, sin whipsaw

### Ejecucion:
- Entrar contrato adicional en el rebote de SMA20
- Stop del contrato adicional: low/high de la barra de rebote
- El trailing continua igual para ambos contratos
- Riesgo total combinado NUNCA debe exceder $270

### Cuando NO agregar:
- Si el pullback tiene mas de 3 barras (momentum perdido)
- Si el precio cruza SMA20 (ya no es pullback, es reversal)
- Si el riesgo combinado excederia $270

---

## FASE 7: Re-entrada en el Mismo Dia

### Cuando considerar re-entrada:
1. El trade original salio por trailing (no por stop loss maximo)
2. La tendencia principal sigue intacta (SMAs alineadas, spread creciendo o estable)
3. Se forma un nuevo pullback limpio a SMA20 (1-3 barras)
4. El P&L del dia permite otro trade sin exceder $270 de perdida maxima

### Cuando NO re-entrar:
- Si el stop fue por whipsaw (señal de rango)
- Si las SMAs empiezan a converger
- Si ya perdiste $135+ en el dia (conservar capital)

---

## GESTION DE RIESGO

| Parametro | Valor |
|-----------|-------|
| Perdida maxima diaria | $270 (135 pts) |
| Objetivo minimo diario | $270 (135 pts) |
| Stop inicial maximo | 67.5 pts ($135) por contrato |
| Riesgo por contrato adicional | <$135 |
| Sesion operativa | 09:30 - 16:00 ET |
| Instrumento | MNQ (Micro E-mini Nasdaq) |
| Timeframe | 2 minutos |
| Contratos iniciales | 1 |
| Contratos maximos | 2 |

---

## CHECKLIST RAPIDO POR BARRA

Al ver cada nueva barra de 2 minutos:

- [ ] Precio ENCIMA o DEBAJO de SMA20?
- [ ] Precio ENCIMA o DEBAJO de SMA200?
- [ ] Spread actual entre SMAs? (creciendo/decreciendo/convergiendo?)
- [ ] SMAs convergidas (<30 pts)? → Monitorear para ruptura
- [ ] Hay nuevo HH o LL? (mover trailing?)
- [ ] Alguna señal de rango? (whipsaw, cruces repetidos?)
- [ ] Si hay rango: precio cruzo AMBAS SMAs? → Ruptura?
- [ ] Si tengo trade: esta en breathe period o ya en trailing?
- [ ] P&L actual del dia vs limites?

---

## TIPOS DE ENTRADA — RESUMEN

| Tipo | Condicion | Cuando |
|------|-----------|--------|
| **Tendencia 09:32** | Spread >80 + body >60% + precio fuera ambas SMAs | 09:32 |
| **Pullback SMA20** | SMAs cruzaron + spread crece + rebote en SMA20 | Cualquier hora |
| **Ruptura de Rango** | SMAs convergidas (<30) + precio cruza AMBAS + confirmacion | Cualquier hora |

---

## RESULTADOS VALIDADOS (En Caliente, Febrero 2026 Completo — 19 dias)

| Dia | Tipo Entrada | Setup | P&L | Categoria |
|-----|-------------|-------|-----|-----------|
| Feb 02 | Tendencia 09:32 | LONG (body 94%, spread 80) trail | +$514.50 | ✅ GANADOR |
| Feb 03 | Ruptura de Rango | SHORT (body 64%, spread 17) breakout + re-entry | +$617.00 | ✅ GANADOR |
| Feb 04 | Tendencia 09:32 | SHORT (body 89%, spread 80) + re-entries | +$430.00 | ✅ GANADOR |
| Feb 05 | Tendencia 09:32 | SHORT (body 99%, spread 192) → whipsaw | -$183.50 | ❌ PERDEDOR |
| Feb 06 | Pullback SMA20 | NO 09:32 (doji, spread 51) → pullback 11:02 | +$18.00 | ⚠️ MEDIO |
| Feb 09 | Pullback SMA20 | NO 09:32 (mecha, spread 28) → pullback 10:30 | +$293.50 | ✅ GANADOR |
| Feb 10 | Ruptura de Rango | RANGO (doji, spread 18) → breakout 13:06 | +$119.50 | ⚠️ MEDIO |
| Feb 11 | No trade | Spread 42, doji, precio ENTRE SMAs → sin setup | $0 | ⬜ PROTEGIDO |
| Feb 12 | Ruptura de Rango | SHORT (spread 23, breakout 09:32) | +$248.00 | ✅ GANADOR |
| Feb 13 | Ruptura de Rango | SHORT (spread 21) → whipsaw | -$112.00 | ❌ PERDEDOR |
| Feb 17 | Ruptura de Rango | LONG (spread 26, re-convergencia 13:22) | +$262.00 | ✅ GANADOR |
| Feb 18 | Pullback SMA20 | LONG → pullback limpio a SMA20 | +$30.50 | ⚠️ MEDIO |
| Feb 19 | No trade | RANGO sin convergencia ni ruptura | $0 | ⬜ PROTEGIDO |
| Feb 20 | Pullback SMA20 | LONG → pullback limpio a SMA20 | +$245.50 | ✅ GANADOR |
| Feb 23 | Ruptura de Rango | SHORT (spread 3) → whipsaw | -$97.00 | ❌ PERDEDOR |
| Feb 24 | Ruptura de Rango | LONG (spread 8) + pullback re-entry | +$338.50 | ✅ GANADOR |
| Feb 25 | Pullback SMA20 | LONG → trailing corto, ~breakeven | -$1.50 | ❌ PERDEDOR |
| Feb 26 | Ruptura de Rango | SHORT (spread 6) + 2 re-entries | +$110.50 | ⚠️ MEDIO |
| Feb 27 | Ruptura de Rango | SHORT + LONG (re-conv) → 2 breathe whipsaws | -$129.50 | ❌ PERDEDOR |

### Metricas

| Metrica | Valor |
|---------|-------|
| **Total neto 19 dias** | **+$2,704.00** |
| **Promedio diario** | **+$142.32** |
| Dias positivos (>$0) | 12 (63.2%) |
| Dias neutros ($0) | 2 (10.5%) |
| Dias negativos | 5 (26.3%) |
| Mejor dia | Feb 03: +$617.00 |
| Peor dia | Feb 05: -$183.50 |
| Ganancias totales | +$3,297.50 |
| Perdidas totales | -$593.50 |
| **Profit factor** | **5.56:1** |

### Distribucion por tipo de entrada

| Tipo | Dias | Ganancia total | Promedio |
|------|------|---------------|----------|
| Tendencia 09:32 | 3 | +$761.00 | +$253.67 |
| Pullback SMA20 | 4 | +$586.50 | +$146.63 |
| Ruptura de Rango | 10 | +$1,356.50 | +$135.65 |
| No trade | 2 | $0 | $0 |

---

## PRINCIPIOS FUNDAMENTALES

1. **Probabilidad, no certeza**: Cada entrada es una apuesta probabilistica basada en la alineacion de factores
2. **En caliente siempre**: Nunca analizar con informacion futura. Solo lo que ves en la barra actual
3. **El trailing decide la salida**: No predecir targets — dejar que el trailing bar-a-bar maximice ganancia
4. **Rango no es inutil — es un resorte**: SMAs convergidas pueden dar las mejores entradas si rompen limpiamente
5. **SMA20 es la guia**: El precio respecto a SMA20 es el indicador principal de continuacion o reversal
6. **El spread cuenta la historia**: SMAs separandose = tendencia fuerte. Convergiendo = rango o ruptura inminente
7. **El cuerpo de la vela es probabilidad**: Body grande = conviccion. Mecha grande = rechazo/duda
8. **Tres caminos, no uno**: Si 09:32 no da señal, el dia no esta perdido — hay pullback y ruptura de rango
