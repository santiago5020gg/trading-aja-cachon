# MCP Bridge: Claude Code + NinjaTrader 8 — Guia completa de instalacion

Guia paso a paso para que Claude Code pueda ver en tiempo real el precio, indicadores (SMA20, SMA200, ATR, RSI, etc.) y el historial de barras de cualquier chart abierto en NinjaTrader 8.

## Arquitectura

```
NinjaTrader 8 Chart
       |
  MCPBridgeIndicator.cs  (Indicator — recolecta OHLCV + indicadores cada barra)
       |
  MCPBridge.cs           (AddOn — HTTP server en localhost:8500)
       |
  HTTP GET /data, /ping
       |
  mcp-ninjatrader/index.js  (MCP server — Node.js, transporte stdio)
       |
  Claude Code             (consume MCP tools: ping, get_current_bar, get_bar_history, get_market_context)
```

Flujo: El indicador calcula datos en cada barra cerrada y los pasa al AddOn. El AddOn sirve JSON via HTTP. El MCP server de Node.js consulta ese HTTP y expone tools MCP que Claude Code puede usar.

---

## Requisitos previos

- NinjaTrader 8 instalado y funcionando
- Node.js 18+ instalado
- Claude Code CLI instalado

---

## Paso 1: Instalar el AddOn MCPBridge.cs en NinjaTrader

Este archivo crea un servidor HTTP dentro de NinjaTrader que escucha en `localhost:8500`.

### Ubicacion del archivo

```
C:\Users\<TU_USUARIO>\Documents\NinjaTrader 8\bin\Custom\AddOns\MCPBridge.cs
```

### Codigo completo: MCPBridge.cs

```csharp
#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.AddOns
{
    public class MCPBridge : AddOnBase
    {
        private HttpListener listener;
        private Thread serverThread;
        private volatile bool isRunning;

        private static readonly object DataLock = new object();
        private static MCPBridgeData latestData = null;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MCP Bridge — HTTP server for Claude Code integration";
                Name = "MCPBridge";
            }
        }

        protected override void OnWindowCreated(Window window)
        {
            if (window is ControlCenter && !isRunning)
            {
                StartServer();
                NinjaTrader.Code.Output.Process("MCP Bridge: Server auto-started on port 8500.", PrintTo.OutputTab1);
            }
        }

        protected override void OnWindowDestroyed(Window window)
        {
            if (window is ControlCenter && isRunning)
            {
                StopServer();
            }
        }

        private void StartServer()
        {
            if (isRunning) return;
            isRunning = true;

            serverThread = new Thread(RunServer)
            {
                IsBackground = true,
                Name = "MCPBridgeHTTP"
            };
            serverThread.Start();
        }

        private void StopServer()
        {
            isRunning = false;
            try { listener?.Stop(); } catch { }
        }

        private void RunServer()
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:8500/");
                listener.Start();

                while (isRunning)
                {
                    try
                    {
                        var ctx = listener.GetContext();
                        ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                    }
                    catch (HttpListenerException)
                    {
                        if (!isRunning) break;
                    }
                }
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("MCP Bridge Error: " + ex.Message, PrintTo.OutputTab1);
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath.ToLower();
                string response;

                switch (path)
                {
                    case "/ping":
                        response = "{\"status\":\"ok\",\"service\":\"NinjaTrader MCP Bridge\"}";
                        break;

                    case "/data":
                        MCPBridgeData data;
                        lock (DataLock) { data = latestData; }
                        response = data != null ? data.ToJson() : "{\"error\":\"No data available. Add MCPBridgeIndicator to a chart.\"}";
                        break;

                    default:
                        ctx.Response.StatusCode = 404;
                        response = "{\"error\":\"Unknown endpoint. Use /ping or /data\"}";
                        break;
                }

                byte[] buf = Encoding.UTF8.GetBytes(response);
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        public static void UpdateData(MCPBridgeData data)
        {
            lock (DataLock) { latestData = data; }
        }
    }

    public class MCPBridgeData
    {
        public string Instrument;
        public string BarPeriod;
        public DateTime Timestamp;
        public string TimestampNY;

        public double CurrentOpen;
        public double CurrentHigh;
        public double CurrentLow;
        public double CurrentClose;
        public double CurrentVolume;

        public double SMA20;
        public double SMA200;
        public double ATR14;
        public double RSI7;
        public double StdDev20;
        public double VolumeSMA20;

        public double SMA20Slope;
        public double SMA200Slope;
        public double SMASpread;
        public double ZScore;

        public List<BarData> History;

        public string ToJson()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder(4096);
            sb.Append("{");
            sb.AppendFormat(inv, "\"instrument\":\"{0}\",", Esc(Instrument));
            sb.AppendFormat(inv, "\"barPeriod\":\"{0}\",", Esc(BarPeriod));
            sb.AppendFormat(inv, "\"timestamp\":\"{0:yyyy-MM-dd HH:mm:ss}\",", Timestamp);
            sb.AppendFormat(inv, "\"timestampNY\":\"{0}\",", Esc(TimestampNY));
            sb.AppendFormat(inv, "\"current\":{{\"open\":{0},\"high\":{1},\"low\":{2},\"close\":{3},\"volume\":{4}}},",
                CurrentOpen.ToString("F2", inv), CurrentHigh.ToString("F2", inv), CurrentLow.ToString("F2", inv), CurrentClose.ToString("F2", inv), CurrentVolume.ToString("F0", inv));
            sb.AppendFormat(inv, "\"indicators\":{{\"sma20\":{0},\"sma200\":{1},\"atr14\":{2},\"rsi7\":{3},\"stdDev20\":{4},\"volumeSma20\":{5},\"sma20Slope\":{6},\"sma200Slope\":{7},\"smaSpread\":{8},\"zScore\":{9}}},",
                SMA20.ToString("F2", inv), SMA200.ToString("F2", inv), ATR14.ToString("F2", inv), RSI7.ToString("F2", inv), StdDev20.ToString("F2", inv), VolumeSMA20.ToString("F0", inv), SMA20Slope.ToString("F6", inv), SMA200Slope.ToString("F6", inv), SMASpread.ToString("F2", inv), ZScore.ToString("F4", inv));

            sb.Append("\"history\":[");
            if (History != null)
            {
                for (int i = 0; i < History.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var b = History[i];
                    sb.AppendFormat(inv, "{{\"time\":\"{0:yyyy-MM-dd HH:mm}\",\"o\":{1},\"h\":{2},\"l\":{3},\"c\":{4},\"v\":{5},\"sma20\":{6},\"sma200\":{7}}}",
                        b.Time.ToString("yyyy-MM-dd HH:mm"), b.Open.ToString("F2", inv), b.High.ToString("F2", inv), b.Low.ToString("F2", inv), b.Close.ToString("F2", inv), b.Volume.ToString("F0", inv), b.SMA20.ToString("F2", inv), b.SMA200.ToString("F2", inv));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public class BarData
    {
        public DateTime Time;
        public double Open, High, Low, Close, Volume;
        public double SMA20, SMA200;
    }
}
```

### Como instalar

1. Copia el archivo `MCPBridge.cs` a la ruta indicada arriba
2. En NinjaTrader, ve a **New > NinjaScript Editor**
3. Presiona **F5** (compilar) — debe compilar sin errores
4. El server se auto-inicia cuando se abre el Control Center de NinjaTrader
5. Verifica en la pestana **Output** el mensaje: `MCP Bridge: Server auto-started on port 8500.`

---

## Paso 2: Instalar el Indicator MCPBridgeIndicator.cs en NinjaTrader

Este indicador recolecta los datos del chart (OHLCV + SMA20 + SMA200 + ATR + RSI + StdDev + Z-Score) y los envia al AddOn MCPBridge.

### Ubicacion del archivo

```
C:\Users\<TU_USUARIO>\Documents\NinjaTrader 8\bin\Custom\Indicators\MCPBridgeIndicator.cs
```

### Codigo completo: MCPBridgeIndicator.cs

```csharp
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class MCPBridgeIndicator : Indicator
    {
        private SMA sma20;
        private SMA sma200;
        private ATR atr14;
        private RSI rsi7;
        private StdDev stdDev20;
        private SMA volSma20;
        private TimeZoneInfo easternZone;

        [NinjaScriptProperty]
        [Display(Name = "History Bars", Description = "Number of historical bars to send", GroupName = "MCP Bridge", Order = 1)]
        public int HistoryBars { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Feeds chart data to MCP Bridge for Claude Code integration";
                Name = "MCPBridgeIndicator";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                IsAutoScale = false;
                HistoryBars = 200;
            }
            else if (State == State.DataLoaded)
            {
                sma20 = SMA(20);
                sma200 = SMA(200);
                atr14 = ATR(14);
                rsi7 = RSI(7, 3);
                stdDev20 = StdDev(20);
                volSma20 = SMA(Volume, 20);
                easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 200) return;

            DateTime nyTime = TimeZoneInfo.ConvertTime(Time[0], easternZone);

            var data = new MCPBridgeData
            {
                Instrument = Instrument.FullName,
                BarPeriod = BarsPeriod.ToString(),
                Timestamp = Time[0],
                TimestampNY = nyTime.ToString("yyyy-MM-dd HH:mm:ss"),
                CurrentOpen = Open[0],
                CurrentHigh = High[0],
                CurrentLow = Low[0],
                CurrentClose = Close[0],
                CurrentVolume = Volume[0],
                SMA20 = sma20[0],
                SMA200 = sma200[0],
                ATR14 = atr14[0],
                RSI7 = rsi7[0],
                StdDev20 = stdDev20[0],
                VolumeSMA20 = volSma20[0],
                SMA20Slope = CurrentBar >= 5 ? (sma20[0] - sma20[5]) / 5.0 : 0,
                SMA200Slope = CurrentBar >= 5 ? (sma200[0] - sma200[5]) / 5.0 : 0,
                SMASpread = atr14[0] > 0 ? Math.Abs(sma20[0] - sma200[0]) / atr14[0] : 0,
                ZScore = stdDev20[0] > 0 ? (Close[0] - sma20[0]) / stdDev20[0] : 0
            };

            int barsToSend = Math.Min(HistoryBars, CurrentBar);
            data.History = new List<BarData>(barsToSend);
            for (int i = barsToSend - 1; i >= 0; i--)
            {
                data.History.Add(new BarData
                {
                    Time = Time[i],
                    Open = Open[i],
                    High = High[i],
                    Low = Low[i],
                    Close = Close[i],
                    Volume = Volume[i],
                    SMA20 = sma20[i],
                    SMA200 = sma200[i]
                });
            }

            MCPBridge.UpdateData(data);
        }
    }
}
```

### Como instalar

1. Copia el archivo `MCPBridgeIndicator.cs` a la ruta indicada arriba
2. En NinjaTrader, compila con **F5** en el NinjaScript Editor
3. Abre un chart (ej. MNQ, 2 minutos)
4. Click derecho en el chart > **Indicators** > busca **MCPBridgeIndicator** > **Add**
5. Parametro `History Bars`: 200 (default, maximo 500 barras de historial)
6. Click **OK**

**Importante:** El indicador necesita al menos 200 barras cargadas antes de empezar a enviar datos. En timeframes bajos (1-2 min) esto es inmediato. En timeframes altos (diario) necesitas suficiente historial.

---

## Paso 3: Crear el MCP server en Node.js

Este es el puente entre el HTTP de NinjaTrader y Claude Code via protocolo MCP (stdio).

### Estructura de archivos

```
tu-proyecto/
  mcp-ninjatrader/
    package.json
    index.js
```

### package.json

```json
{
  "name": "mcp-ninjatrader",
  "version": "1.0.0",
  "description": "MCP server for NinjaTrader 8 — Claude Code integration",
  "main": "index.js",
  "type": "module",
  "dependencies": {
    "@modelcontextprotocol/sdk": "^1.12.1"
  }
}
```

### index.js

```javascript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const NT_URL = "http://localhost:8500";

async function fetchNT(endpoint) {
  const res = await fetch(`${NT_URL}${endpoint}`, { signal: AbortSignal.timeout(5000) });
  if (!res.ok) throw new Error(`NinjaTrader responded ${res.status}`);
  let text = await res.text();
  // NinjaTrader locale fix: remove stray 'F' format specifiers (e.g., 25663F250 -> 25663.50)
  text = text.replace(/(\d)F(\d)/g, '$1.$2');
  text = text.replace(/:(-?)F(\d+)/g, ':${1}0');
  // NinjaTrader locale fix: decimal commas -> dots (e.g., 25743,50 -> 25743.50)
  text = text.replace(/(\d),(\d+)(?=[,\}\]\s])/g, '$1.$2');
  return JSON.parse(text);
}

const server = new McpServer({
  name: "mcp-ninjatrader",
  version: "1.0.0",
});

server.tool("ping", "Check if NinjaTrader MCP Bridge is running and responding", {}, async () => {
  try {
    const data = await fetchNT("/ping");
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  } catch (e) {
    return { content: [{ type: "text", text: `Connection failed: ${e.message}\n\nMake sure:\n1. NinjaTrader is running\n2. MCPBridgeIndicator is on a chart\n3. MCP Bridge is started` }] };
  }
});

server.tool(
  "get_current_bar",
  "Get the current bar data (OHLCV) and all indicator values (SMA20, SMA200, ATR14, RSI7, StdDev, Z-Score, SMA Spread, slopes) from the active NinjaTrader chart",
  {},
  async () => {
    try {
      const data = await fetchNT("/data");
      if (data.error) return { content: [{ type: "text", text: data.error }] };

      const summary = {
        instrument: data.instrument,
        barPeriod: data.barPeriod,
        timestampNY: data.timestampNY,
        bar: data.current,
        indicators: data.indicators,
      };
      return { content: [{ type: "text", text: JSON.stringify(summary, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

server.tool(
  "get_bar_history",
  "Get historical bar data (OHLCV + SMA20 + SMA200) from the active NinjaTrader chart. Returns the most recent N bars.",
  { count: z.number().min(1).max(500).default(50).describe("Number of bars to retrieve (1-500, default 50)") },
  async ({ count }) => {
    try {
      const data = await fetchNT("/data");
      if (data.error) return { content: [{ type: "text", text: data.error }] };

      const n = Math.min(count || 50, data.history ? data.history.length : 0);
      const bars = data.history ? data.history.slice(-n) : [];

      const result = {
        instrument: data.instrument,
        barPeriod: data.barPeriod,
        barsReturned: bars.length,
        bars: bars,
      };
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

server.tool(
  "get_market_context",
  "Get a complete market context snapshot: current price, all indicators, trend analysis, and recent price action summary from the active NinjaTrader chart",
  {},
  async () => {
    try {
      const data = await fetchNT("/data");
      if (data.error) return { content: [{ type: "text", text: data.error }] };

      const ind = data.indicators;
      const cur = data.current;
      const hist = data.history || [];
      const recent = hist.slice(-20);

      let trend = "RANGE";
      if (ind.sma20 > ind.sma200 && ind.smaSpread >= 1.0 && ind.sma20Slope > 0) trend = "BULLISH";
      else if (ind.sma20 < ind.sma200 && ind.smaSpread >= 1.0 && ind.sma20Slope < 0) trend = "BEARISH";
      else if (ind.sma20 > ind.sma200) trend = "WEAK BULLISH";
      else if (ind.sma20 < ind.sma200) trend = "WEAK BEARISH";

      let highs = recent.map((b) => b.h);
      let lows = recent.map((b) => b.l);
      let recentHigh = Math.max(...highs);
      let recentLow = Math.min(...lows);

      const context = {
        instrument: data.instrument,
        timestampNY: data.timestampNY,
        price: {
          current: cur.close,
          open: cur.open,
          high: cur.high,
          low: cur.low,
          volume: cur.volume,
        },
        indicators: ind,
        analysis: {
          trend: trend,
          priceVsSMA20: cur.close > ind.sma20 ? "ABOVE" : "BELOW",
          priceVsSMA200: cur.close > ind.sma200 ? "ABOVE" : "BELOW",
          smaAlignment: ind.sma20 > ind.sma200 ? "BULLISH (SMA20 > SMA200)" : "BEARISH (SMA20 < SMA200)",
          volatility: ind.atr14,
          momentum: ind.rsi7 > 70 ? "OVERBOUGHT" : ind.rsi7 < 30 ? "OVERSOLD" : "NEUTRAL",
        },
        recentRange: {
          last20High: recentHigh,
          last20Low: recentLow,
          rangePoints: recentHigh - recentLow,
        },
      };
      return { content: [{ type: "text", text: JSON.stringify(context, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
```

### Instalar dependencias

```bash
cd mcp-ninjatrader
npm install
```

---

## Paso 4: Configurar Claude Code para usar el MCP server

Crear archivo `.mcp.json` en la raiz del proyecto:

```json
{
  "mcpServers": {
    "ninjatrader": {
      "command": "node",
      "args": ["mcp-ninjatrader/index.js"],
      "env": {}
    }
  }
}
```

Claude Code detecta este archivo automaticamente al iniciar una sesion en el directorio del proyecto.

---

## Paso 5: Verificar la conexion

1. Asegurate de que NinjaTrader esta abierto con un chart activo
2. El chart debe tener el indicador **MCPBridgeIndicator** agregado
3. Inicia Claude Code en el directorio del proyecto
4. Usa el tool MCP `ping` — debe responder `{"status":"ok","service":"NinjaTrader MCP Bridge"}`
5. Usa `get_current_bar` — debe mostrar el precio actual, SMA20, SMA200, ATR, RSI, etc.
6. Usa `get_bar_history` con count=50 — debe mostrar las ultimas 50 barras con OHLCV + SMA20 + SMA200

---

## Tools MCP disponibles

| Tool | Descripcion | Parametros |
|------|-------------|------------|
| `ping` | Verifica que NinjaTrader esta conectado | ninguno |
| `get_current_bar` | Barra actual: OHLCV + SMA20, SMA200, ATR14, RSI7, StdDev20, Z-Score, SMA slopes, SMA spread | ninguno |
| `get_bar_history` | Historial de barras con OHLCV + SMA20 + SMA200 | `count` (1-500, default 50) |
| `get_market_context` | Snapshot completo: precio, indicadores, analisis de tendencia, rango reciente | ninguno |

---

## Datos que recibe Claude Code

### Barra actual (get_current_bar)
- **Precio:** open, high, low, close, volume
- **SMA20:** media movil simple de 20 periodos
- **SMA200:** media movil simple de 200 periodos
- **ATR14:** average true range de 14 periodos (volatilidad)
- **RSI7:** relative strength index de 7 periodos (momentum)
- **StdDev20:** desviacion estandar de 20 periodos
- **VolumeSMA20:** media de volumen de 20 periodos
- **SMA20 Slope:** pendiente de SMA20 (ultimas 5 barras)
- **SMA200 Slope:** pendiente de SMA200 (ultimas 5 barras)
- **SMA Spread:** distancia SMA20-SMA200 normalizada por ATR
- **Z-Score:** desviacion del precio respecto a SMA20 en unidades de StdDev

### Historial (get_bar_history)
- Por cada barra: timestamp, open, high, low, close, volume, SMA20, SMA200
- Maximo 500 barras (limitado por el parametro HistoryBars del indicador, default 200)

---

## Troubleshooting

### "Connection failed" al hacer ping
- NinjaTrader no esta abierto, o el Control Center no se ha cargado aun
- El AddOn MCPBridge.cs no compilo correctamente — revisa el NinjaScript Editor
- El puerto 8500 esta bloqueado por firewall

### "No data available. Add MCPBridgeIndicator to a chart."
- No hay ningun chart con MCPBridgeIndicator activo
- El chart no tiene suficientes barras cargadas (necesita minimo 200)

### Numeros con formato incorrecto (comas en vez de puntos)
- Esto pasa en sistemas con locale espanol. El MCPBridge.cs ya usa `InvariantCulture` para formatear numeros
- El index.js tiene regex de sanitizacion adicional como respaldo
- Si persiste, verificar que MCPBridge.cs usa `.ToString("F2", inv)` con `inv = CultureInfo.InvariantCulture`

### Los datos no se actualizan
- El indicador usa `Calculate = Calculate.OnBarClose` — solo actualiza cuando cierra una barra
- En un chart de 2 minutos, los datos se actualizan cada 2 minutos
- Para datos tick-by-tick, cambiar a `Calculate = Calculate.OnEachTick` (genera mas carga)

---

## Notas importantes

- **Locale:** NinjaTrader en sistemas Windows en espanol puede generar decimales con coma (25743,50) en vez de punto. El AddOn ya maneja esto con `InvariantCulture`, y el MCP server tiene regex de respaldo.
- **Un chart a la vez:** El bridge solo envia datos del ultimo chart que actualizo el indicador. Si tienes multiples charts con MCPBridgeIndicator, el `/data` devuelve el que actualizo mas recientemente.
- **Timezone:** Todos los timestamps se convierten a Eastern Time (New York) para consistencia con el horario del mercado.
- **Playback/Simulacion:** El bridge funciona igual con datos en vivo o con el Playback de NinjaTrader. Pero Claude Code no puede controlar el Playback — el usuario debe avanzar/retroceder manualmente.
