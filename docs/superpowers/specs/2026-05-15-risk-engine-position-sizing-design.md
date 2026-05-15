# Risk Engine + Position Sizing Dinamico — MNQ10minV2

**Fecha:** 2026-05-15  
**Archivo objetivo:** MNQ10minV2.cs

## Objetivo

Reemplazar el parametro manual `MicroContratos` por un motor de riesgo que calcula automaticamente la cantidad de contratos y trades permitidos basandose en la Perdida Maxima Diaria y el riesgo real del rango del dia.

## Cambios

### 1. Eliminacion de parametro `MicroContratos`

Se elimina el parametro `MicroContratos` (input manual 1-20). El sizing ahora es dinamico.

### 2. Nuevo parametro: `PerdidaMaxDiaria`

- Tipo: `double`
- Default: `400`
- Grupo: "1. Risk", Order 4
- Proposito: limitar la perdida maxima del dia en dolares

### 3. Motor de Riesgo (se ejecuta al cerrar el rango 09:32-09:40)

```
Riesgo1Micro ($) = (rangoPuntos + ColchonStop) * 2.0
PresupuestoIdeal ($) = PerdidaMaxDiaria / MaxTrades
```

**Escenario A** (Presupuesto Holgado): `PresupuestoIdeal >= Riesgo1Micro`
- contratosCalculados = Floor(PresupuestoIdeal / Riesgo1Micro)
- tradesPermitidosHoy = MaxTrades

**Escenario B** (Presupuesto Ajustado): `PresupuestoIdeal < Riesgo1Micro` Y `Riesgo1Micro <= PerdidaMaxDiaria`
- contratosCalculados = 1
- tradesPermitidosHoy = Floor(PerdidaMaxDiaria / Riesgo1Micro)

**Escenario C** (Fuera de Presupuesto): `Riesgo1Micro > PerdidaMaxDiaria`
- contratosCalculados = 0, tradesPermitidosHoy = 0
- Estado -> DiaTerminado

### 4. Distribucion TP1/TP2

Despues del calculo de contratos:

| Contratos | Modo 1a1 (Solo1a1) | Modo 1a2 (Con1a2) |
|-----------|---------------------|---------------------|
| >= 3      | qtyTP1 = N-1, qtyTP2 = 1 | qtyTP1 = N-1, qtyTP2 = 1 |
| 2         | qtyTP1 = 2, qtyTP2 = 0   | qtyTP1 = 1, qtyTP2 = 1   |
| 1         | qtyTP1 = 1, qtyTP2 = 0   | qtyTP1 = 0, qtyTP2 = 1   |
| 0         | No se opera               | No se opera               |

Regla especial: 1 contrato en modo 1a2 -> va completo a TP2 (trailing hacia 1:2).

### 5. Trailing Daily Drawdown

Se evalua al cierre de cada trade (cuando position == Flat):

```
peakDailyPnL = Max(peakDailyPnL, dailyPnL)
drawdownDesdeElPeak = peakDailyPnL - dailyPnL
if (drawdownDesdeElPeak >= PerdidaMaxDiaria) -> DiaTerminado
```

Variables nuevas: `peakDailyPnL`, `tradesPermitidosHoy`.

### 6. Mejora visual: puntos del rango

Despues de dibujar el rectangulo del rango, se agrega Draw.Text con "{N} pts" centrado dentro del rectangulo. Visible en ambos modos (Operar y Visualizar).

```csharp
double midPrice = (rangoHigh + rangoLow) / 2.0;
int midBar = (CurrentBar - rangoStartBar) / 2;
Draw.Text(this, "RangoPts" + tag, string.Format("{0:F0} pts", rangoPuntos), midBar, midPrice, Brushes.DodgerBlue);
```

## Zonas del codigo afectadas

| Zona | Cambio |
|------|--------|
| Parametros (#region Parameters) | Eliminar MicroContratos, agregar PerdidaMaxDiaria |
| State.Configure | Eliminar logica de split TP1/TP2 (se mueve a ProcesarRango) |
| State.SetDefaults | Eliminar default MicroContratos, agregar default PerdidaMaxDiaria |
| ProcesarRango() | Motor de riesgo + split TP1/TP2 + Draw.Text puntos |
| OnExecutionUpdate | Track peakDailyPnL, check drawdown al flat |
| ProcesarFinTrade() | Usar tradesPermitidosHoy en vez de MaxTrades |
| ResetDaily() | Reset peakDailyPnL, tradesPermitidosHoy |

## Validacion del escenario ejemplo

PerdidaMaxDiaria=$300, Rango=100pts, ColchonStop=5, MaxTrades=7:
- Riesgo1Micro = (100+5)*2 = $210
- PresupuestoIdeal = 300/7 = $42.86
- $42.86 < $210 -> No es A
- $210 <= $300 -> Es B
- Contratos = 1, TradesPermitidosHoy = Floor(300/210) = 1

## Restricciones

- NO se elimina la logica de Stop Loss puro
- NO se elimina ningun estado ni trailing existente
- La logica de maxStopsPuros/maxTakeProfits/maxBreakevens sigue intacta pero limitada por tradesPermitidosHoy
