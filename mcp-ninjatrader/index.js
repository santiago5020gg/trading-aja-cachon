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
  "Get historical bar data (OHLCV + SMA20 + SMA200) from the active NinjaTrader chart. Can retrieve up to 22000 bars (~30 days of 2-min data). Use 'from' and 'to' for date ranges, or 'count' for most recent N bars. Set sessionOnly=true to filter regular session (09:30-16:00 ET) only.",
  {
    count: z.number().min(1).max(22000).optional().describe("Number of most recent bars (1-22000). Ignored if from/to provided."),
    from: z.string().optional().describe("Start date/time: yyyy-MM-dd or yyyy-MM-dd HH:mm"),
    to: z.string().optional().describe("End date/time: yyyy-MM-dd or yyyy-MM-dd HH:mm"),
    sessionOnly: z.boolean().optional().describe("If true, only return regular session bars (09:30-16:00 ET)")
  },
  async ({ count, from, to, sessionOnly }) => {
    try {
      let url = "/history?";
      if (from && to) {
        url += `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
      } else {
        url += `count=${count || 750}`;
      }
      if (sessionOnly) url += "&session=1";

      const data = await fetchNT(url);
      if (data.error) return { content: [{ type: "text", text: data.error }] };

      const meta = await fetchNT("/data");
      const result = {
        instrument: meta.instrument || "MNQ",
        barPeriod: meta.barPeriod || "2 Min",
        totalBarsInMemory: data.totalBars,
        barsReturned: data.returnedBars,
        bars: data.bars,
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
      let hist = [];
      try { const hdata = await fetchNT("/history?count=20"); hist = hdata.bars || []; } catch {}
      const recent = hist;

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

server.tool(
  "playback_goto",
  "Fast-forward NinjaTrader Playback to a specific date/time. The playback will run at max speed and auto-pause when it reaches the target. Requires Playback mode active and playing in NinjaTrader. Time format: yyyy-MM-dd HH:mm",
  { targetTime: z.string().describe("Target date/time in format yyyy-MM-dd HH:mm (e.g., '2026-02-11 09:32')") },
  async ({ targetTime }) => {
    try {
      const res = await fetch(`${NT_URL}/playback/goto`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ targetTime }),
        signal: AbortSignal.timeout(5000),
      });
      let text = await res.text();
      const data = JSON.parse(text);

      if (data.error) return { content: [{ type: "text", text: `Error: ${data.error}` }] };

      const maxWait = 600000;
      const pollInterval = 3000;
      let elapsed = 0;

      while (elapsed < maxWait) {
        await new Promise((r) => setTimeout(r, pollInterval));
        elapsed += pollInterval;

        try {
          const statusRes = await fetch(`${NT_URL}/playback/status`, { signal: AbortSignal.timeout(3000) });
          const statusData = JSON.parse(await statusRes.text());

          if (statusData.status === "arrived") {
            const barRes = await fetch(`${NT_URL}/data`, { signal: AbortSignal.timeout(5000) });
            let barText = await barRes.text();
            barText = barText.replace(/(\d)F(\d)/g, "$1.$2");
            barText = barText.replace(/:(-?)F(\d+)/g, ":${1}0");
            barText = barText.replace(/(\d),(\d+)(?=[,\}\]\s])/g, "$1.$2");
            const barData = JSON.parse(barText);

            const summary = {
              status: "arrived",
              requestedTime: targetTime,
              actualTime: barData.timestampNY,
              instrument: barData.instrument,
              bar: barData.current,
              indicators: barData.indicators,
            };
            return { content: [{ type: "text", text: JSON.stringify(summary, null, 2) }] };
          }
        } catch (_) {}
      }

      return { content: [{ type: "text", text: `Timeout: Playback did not reach ${targetTime} within ${maxWait / 1000}s. Check if Playback is running.` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

server.tool(
  "playback_pause",
  "Pause the NinjaTrader Playback",
  {},
  async () => {
    try {
      const res = await fetch(`${NT_URL}/playback/pause`, { signal: AbortSignal.timeout(5000) });
      const data = JSON.parse(await res.text());
      return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

server.tool(
  "playback_resume",
  "Resume the NinjaTrader Playback at normal speed",
  {},
  async () => {
    try {
      const res = await fetch(`${NT_URL}/playback/resume`, { signal: AbortSignal.timeout(5000) });
      const data = JSON.parse(await res.text());
      return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

server.tool(
  "playback_status",
  "Check the current Playback status (idle, seeking, arrived, paused, playing)",
  {},
  async () => {
    try {
      const res = await fetch(`${NT_URL}/playback/status`, { signal: AbortSignal.timeout(5000) });
      const data = JSON.parse(await res.text());
      return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
