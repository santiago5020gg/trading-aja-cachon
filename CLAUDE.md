# MNQ Probabilistic Trading Bot

## Project overview

NinjaTrader 8 automated strategy for MNQ (Micro E-mini Nasdaq) futures. The bot uses a probabilistic 7-factor entry scoring system with regime detection, dynamic position sizing (Kelly criterion), and full risk management.

## Architecture

- **MNQProbabilisticBot.cs** (~1400 lines) — The complete NinjaTrader strategy. C# targeting NinjaTrader 8 NinjaScript API. Includes: regime detection (SMA20/SMA200), 7-factor entry score, Kelly-based position sizing, daily P&L controls, chart HUD panel, and CSV statistics logging.
- **mcp-ninjatrader/** — Node.js MCP server that bridges Claude Code to NinjaTrader via HTTP. Connects to MCPBridgeIndicator running on a NinjaTrader chart (localhost:8500). Provides tools: `ping`, `get_current_bar`, `get_bar_history`, `get_market_context`.

## Key conventions

- The bot is a single-file NinjaScript strategy — do not split into multiple files
- NinjaTrader uses C# with its own NinjaScript base classes (Strategy, Indicator)
- All times are New York (Eastern) timezone
- The MCP bridge requires MCPBridgeIndicator added to an active NinjaTrader chart
- NinjaTrader may output JSON with Spanish locale (decimal commas instead of dots) — the MCP bridge sanitizes this in `fetchNT()`

## Known issues

- SMA200 value in bar history is truncated to single digit (shows "2" instead of full value like 25663). Needs fix in the MCPBridgeIndicator C# code.
- Bar history is capped at 200 bars. For monthly analysis, the chart must use a higher timeframe (daily/hourly).

## Development workflow

- Edit C# in this repo, then copy/reload in NinjaTrader
- MCP bridge runs via `node mcp-ninjatrader/index.js` (stdio transport, launched by Claude Code)
- Test connection: use `ping` MCP tool, then `get_current_bar`

## Language

The user communicates in Spanish. Respond in Spanish.
