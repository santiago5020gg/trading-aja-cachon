---
name: mnq-probabilistic-strategy
description: Estrategia probabilistica completa para operar MNQ futures en sesion americana usando barras de 2 minutos, SMA20/SMA200, y trailing bar-a-bar. Usar cuando se analicen dias de trading o se tomen decisiones de entrada/salida.
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
- **<60 puntos**: Sin señal clara, NO entrar en 09:32. Esperar patron de pullback

### 1.2 Posicion del precio respecto a SMAs
- **Precio ENCIMA de ambas SMAs**: Setup alcista
- **Precio DEBAJO de ambas SMAs**: Setup bajista
- **Precio ENTRE las SMAs**: NO HAY SETUP en 09:32, esperar

### 1.3 Anatomia de la vela 09:32
- **Body >60% del rango**: Alta conviccion. El color del cuerpo indica direccion
- **Body 50-60%**: Conviccion media, considerar con precaucion
- **Body <50%**: Baja conviccion / doji / rechazo. NO entrar
- **Mecha grande**: Indica rechazo. Una vela roja con mecha inferior grande NO es completamente bajista — el mercado rechazo el downside

---

## FASE 2: Arbol de Decision de Entrada

```
09:32 llega:
├── Spread >80 + precio FUERA de ambas SMAs + body >60%
│   └── ENTRAR en direccion del cuerpo de la vela
│       ├── Vela VERDE + precio ENCIMA SMAs → LONG
│       └── Vela ROJA + precio DEBAJO SMAs → SHORT
│
├── Spread <60 OR body <50% OR precio ENTRE SMAs
│   └── NO ENTRY en 09:32. Pasar a FASE 3 (esperar pullback)
│
└── Señales mixtas (spread 60-80, body 50-60%)
    └── NO ENTRY. Pasar a FASE 3
```

---

## FASE 3: Entrada por Pullback a SMA20 (post-09:32)

Cuando 09:32 no da señal, monitorear barra a barra:

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

## FASE 4: Gestion de Trade — Trailing Bar-a-Bar

### 4.1 Periodo de Respiracion (Breathe Period)
- **3 barras** despues de la entrada antes de mover el stop
- Stop inicial: low de la barra de entrada (LONG) o high de la barra de entrada (SHORT)
- Si en las 3 barras de respiracion hay WHIPSAW (precio cruza stop y regresa), es señal de RANGO

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

## FASE 5: Deteccion de Rango — NO OPERAR

### Señales de rango (cualquiera activa la alerta):
1. **Whipsaw en breathe bars**: El precio activa el stop y regresa en las primeras 3 barras
2. **SMAs cruzan y RE-cruzan**: SMA20 cruza SMA200, luego vuelve a cruzar en direccion opuesta
3. **Precio cruza SMA20 repetidamente**: 5+ veces en pocas barras
4. **Spread SMAs converge a <20 puntos**: Las medias se juntan, no hay tendencia
5. **Doji en 09:32 + spread <30**: Indecision total desde el inicio

### Que hacer:
- Si detectas rango ANTES de entrar: NO ENTRAR. Dia de $0.
- Si detectas rango DESPUES de entrar: El trailing te sacara con una perdida controlada. NO re-entrar.
- Un dia de $0 es un buen dia — preserva capital para dias con tendencia clara.

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
2. La tendencia principal sigue intacta (SMAs alineadas, spread creciendo)
3. Se forma un nuevo pullback limpio a SMA20
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
- [ ] Spread actual entre SMAs? (creciendo/decreciendo?)
- [ ] Hay nuevo HH o LL? (mover trailing?)
- [ ] Alguna señal de rango? (whipsaw, cruces repetidos?)
- [ ] Si tengo trade: esta en breathe period o ya en trailing?
- [ ] P&L actual del dia vs limites?

---

## RESULTADOS VALIDADOS (En Caliente, Feb 2026)

| Dia | Tipo Señal | Setup | P&L |
|-----|-----------|-------|-----|
| Feb 02 | LONG (body 94.5%, spread 160) | 09:32 + re-entry | +$427.50 |
| Feb 03 | SHORT (body 64.2%, spread 140) | 09:32 trail | +$655.50 |
| Feb 04 | SHORT (body 89.3%, spread 80) | 09:32 + 2 re-entries | +$430.00 |
| Feb 05 | SHORT → whipsaw detectado | Stop + re-entry opcional | -$183.50 a +$227 |
| Feb 06 | NO 09:32 (doji, spread 51) | Pullback SMA20 11:02 | +$18.00 |
| Feb 09 | NO 09:32 (mecha, spread 28) | Pullback SMA20 10:30 | +$293.50 |
| Feb 10 | RANGO (doji, spread 18) | Sin trade | $0 |

**Total conservador 7 dias: +$1,641**
**Promedio diario: +$234.43**
**Win rate: 6/7 dias positivos o neutros (85.7%)**
**Unico dia negativo: Feb 05 (rango detectado tarde)**

---

## PRINCIPIOS FUNDAMENTALES

1. **Probabilidad, no certeza**: Cada entrada es una apuesta probabilistica basada en la alineacion de factores
2. **En caliente siempre**: Nunca analizar con informacion futura. Solo lo que ves en la barra actual
3. **El trailing decide la salida**: No predecir targets — dejar que el trailing bar-a-bar maximice ganancia
4. **Rango = no operar**: Un dia de $0 es mejor que un dia de -$270
5. **SMA20 es la guia**: El precio respecto a SMA20 es el indicador principal de continuacion o reversal
6. **El spread cuenta la historia**: SMAs separandose = tendencia fuerte. Convergiendo = precaucion
7. **El cuerpo de la vela es probabilidad**: Body grande = conviccion. Mecha grande = rechazo/duda
