---
name: mnq-probabilistic-strategy
description: Estrategia probabilistica para operar MNQ futures en sesion americana usando barras de 2 minutos, SMA20/SMA200, y trailing bar-a-bar. Incluye entrada por tendencia 09:32, pullback a SMA20, y ruptura de rango.
when_to_use: Usar cuando se analicen dias de trading MNQ, se tomen decisiones de entrada/salida, se evaluen condiciones de mercado con SMA20/SMA200, o se simule operativa en caliente con datos de Playback.
allowed-tools:
  - mcp__ninjatrader__get_current_bar
  - mcp__ninjatrader__get_bar_history
  - mcp__ninjatrader__get_market_context
  - mcp__ninjatrader__playback_goto
  - mcp__ninjatrader__playback_pause
  - mcp__ninjatrader__playback_resume
  - mcp__ninjatrader__playback_status
---

# Estrategia Probabilistica MNQ — Operativa "En Caliente"

Opera MNQ (Micro E-mini Nasdaq) en barras de 2 minutos durante la sesion regular americana (09:30-16:00 ET). Todas las decisiones se toman EN CALIENTE — solo con informacion disponible en el momento, NUNCA con conocimiento de velas futuras.

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

### Re-convergencia: el rango puede romper MAS DE UNA VEZ en el dia

Un dia que arranca en rango y tiene whipsaw por la mañana NO esta descartado. El resorte puede re-comprimirse:

**Señales de que el rango esta madurando para romper de nuevo:**
1. **Spread vuelve a <30 pts** — las SMAs vuelven a juntarse
2. **SMAs se alinean sin re-cruzar** — SMA20 cruza SMA200 y se QUEDA del mismo lado por 10+ barras
3. **Volatilidad barra-a-barra se reduce** — las barras se hacen mas pequeñas
4. **Precio deja de cruzar SMA20 repetidamente** — los cruces se espacian o desaparecen

**Regla clave: mientras las SMAs esten convergidas, SEGUIR MONITOREANDO.** No dejar de buscar ruptura porque hubo caos previo. El rango ES el setup — toda la sesion, mañana Y tarde.

Para datos validados de rupturas de rango, ver [resultados-febrero-2026.md](resultados-febrero-2026.md).

---

## FASE 4: Gestion de Trade — Trailing Bar-a-Bar

### 4.1 Periodo de Respiracion (Breathe Period)
- **3 barras** despues de la entrada antes de mover el stop
- Stop inicial: low de la barra de entrada (LONG) o high de la barra de entrada (SHORT)
- Si en las 3 barras de respiracion hay WHIPSAW (precio cruza stop y regresa), es señal de RANGO — salir y no re-entrar por ese setup
- **Regla de 2 whipsaws**: Si en el mismo dia hay DOS trades que salen por whipsaw en breathe, el dia esta TERMINADO — no buscar un tercer breakout

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
- Si un breakout falla (whipsaw), verificar si las SMAs RE-CONVERGEN despues
- Si nunca rompe limpiamente → dia de $0 (preservar capital)

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

## PRINCIPIOS FUNDAMENTALES

1. **Probabilidad, no certeza**: Cada entrada es una apuesta probabilistica basada en la alineacion de factores
2. **En caliente siempre**: Nunca analizar con informacion futura. Solo lo que ves en la barra actual
3. **El trailing decide la salida**: No predecir targets — dejar que el trailing bar-a-bar maximice ganancia
4. **Rango no es inutil — es un resorte**: SMAs convergidas pueden dar las mejores entradas si rompen limpiamente
5. **SMA20 es la guia**: El precio respecto a SMA20 es el indicador principal de continuacion o reversal
6. **El spread cuenta la historia**: SMAs separandose = tendencia fuerte. Convergiendo = rango o ruptura inminente
7. **El cuerpo de la vela es probabilidad**: Body grande = conviccion. Mecha grande = rechazo/duda
8. **Tres caminos, no uno**: Si 09:32 no da señal, el dia no esta perdido — hay pullback y ruptura de rango

Para resultados validados de la simulacion en caliente de Febrero 2026, ver [resultados-febrero-2026.md](resultados-febrero-2026.md).
