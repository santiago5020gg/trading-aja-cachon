import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const NT_URL = "http://localhost:8500";

async function fetchNT(endpoint) {
  const res = await fetch(`${NT_URL}${endpoint}`, { signal: AbortSignal.timeout(5000) });
  if (!res.ok) throw new Error(`NinjaTrader responded ${res.status}`);
  let text = await res.text();
  // NinjaTrader locale fix: remove stray 'F' format specifiers (e.g., 25663F250 → 25663.50, F2 → 0)
  text = text.replace(/(\d)F(\d)/g, '$1.$2');
  text = text.replace(/:(-?)F(\d+)/g, ':${1}0');
  // NinjaTrader locale fix: decimal commas → dots (e.g., 25743,50 → 25743.50)
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
    return { content: [{ type: "text", text: `Connection failed: ${e.message}\n\nMake sure:\n1. NinjaTrader is running\n2. MCPBridgeIndicator is on a chart\n3. MCP Bridge is started (Tools > MCP Bridge Start)` }] };
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
