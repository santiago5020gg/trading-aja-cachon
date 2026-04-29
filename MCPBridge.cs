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

        private static readonly object HistoryLock = new object();
        private static List<BarData> historyBuffer = new List<BarData>();
        private static int historyMaxBars = 22000;

        private static readonly object PlaybackLock = new object();
        private static DateTime? playbackTargetTime = null;
        private static string playbackStatus = "idle";

        private static readonly object OrderLock = new object();
        private static List<OrderCommand> orderQueue = new List<OrderCommand>();

        private static readonly object PositionLock = new object();
        private static PositionState currentPosition = new PositionState();

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

                    case "/history":
                        response = HandleHistoryRequest(ctx);
                        break;

                    case "/history/count":
                        response = string.Format("{{\"totalBars\":{0}}}", GetHistoryCount());
                        break;

                    case "/playback/goto":
                        response = HandlePlaybackGoto(ctx);
                        break;

                    case "/playback/status":
                        lock (PlaybackLock)
                        {
                            response = string.Format("{{\"status\":\"{0}\",\"targetTime\":\"{1}\"}}", playbackStatus,
                                playbackTargetTime.HasValue ? playbackTargetTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "none");
                        }
                        break;

                    case "/order":
                        response = HandleOrder(ctx);
                        break;

                    case "/position":
                        lock (PositionLock)
                        {
                            response = currentPosition.ToJson();
                        }
                        break;

                    case "/playback/pause":
                        try
                        {
                            NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 0;
                            lock (PlaybackLock) { playbackStatus = "paused"; playbackTargetTime = null; }
                            response = "{\"status\":\"paused\"}";
                        }
                        catch (Exception ex)
                        {
                            response = string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "\\\""));
                        }
                        break;

                    case "/playback/resume":
                        try
                        {
                            NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 4;
                            lock (PlaybackLock) { playbackStatus = "playing"; }
                            response = "{\"status\":\"playing\",\"speed\":4}";
                        }
                        catch (Exception ex)
                        {
                            response = string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "\\\""));
                        }
                        break;

                    default:
                        ctx.Response.StatusCode = 404;
                        response = "{\"error\":\"Unknown endpoint. Use /ping, /data, /playback/goto, /playback/status, /playback/pause, /playback/resume\"}";
                        break;
                }

                byte[] buf = Encoding.UTF8.GetBytes(response);
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Cache-Control", "no-store, no-cache");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        private string HandleHistoryRequest(HttpListenerContext ctx)
        {
            try
            {
                string query = ctx.Request.Url.Query;
                var inv = System.Globalization.CultureInfo.InvariantCulture;

                string fromStr = GetQueryParam(query, "from");
                string toStr = GetQueryParam(query, "to");
                string countStr = GetQueryParam(query, "count");
                string sessionOnly = GetQueryParam(query, "session");

                List<BarData> bars;

                if (!string.IsNullOrEmpty(fromStr) && !string.IsNullOrEmpty(toStr))
                {
                    DateTime from, to;
                    if (!DateTime.TryParse(fromStr, inv, System.Globalization.DateTimeStyles.None, out from) ||
                        !DateTime.TryParse(toStr, inv, System.Globalization.DateTimeStyles.None, out to))
                        return "{\"error\":\"Invalid date format. Use yyyy-MM-dd or yyyy-MM-dd HH:mm\"}";
                    bars = GetHistoryByDate(from, to);
                }
                else
                {
                    int count = 750;
                    if (!string.IsNullOrEmpty(countStr))
                        int.TryParse(countStr, out count);
                    if (count > historyMaxBars) count = historyMaxBars;
                    bars = GetHistory(count);
                }

                if (!string.IsNullOrEmpty(sessionOnly) && sessionOnly == "1")
                {
                    var eastZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    bars = bars.Where(b =>
                    {
                        var ny = TimeZoneInfo.ConvertTime(b.Time, eastZone);
                        return ny.Hour >= 9 && ny.Hour < 16 && !(ny.Hour == 9 && ny.Minute < 30);
                    }).ToList();
                }

                var sb = new StringBuilder(bars.Count * 120 + 100);
                sb.AppendFormat("{{\"totalBars\":{0},\"returnedBars\":{1},\"bars\":[", GetHistoryCount(), bars.Count);
                for (int i = 0; i < bars.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var b = bars[i];
                    sb.AppendFormat(inv, "{{\"time\":\"{0:yyyy-MM-dd HH:mm}\",\"o\":{1},\"h\":{2},\"l\":{3},\"c\":{4},\"v\":{5},\"sma20\":{6},\"sma200\":{7}}}",
                        b.Time, b.Open.ToString("F2", inv), b.High.ToString("F2", inv), b.Low.ToString("F2", inv),
                        b.Close.ToString("F2", inv), b.Volume.ToString("F0", inv), b.SMA20.ToString("F2", inv), b.SMA200.ToString("F2", inv));
                }
                sb.Append("]}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "\\\""));
            }
        }

        private string GetQueryParam(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;
            string search = key + "=";
            int idx = query.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = query.IndexOf('&', start);
            if (end < 0) end = query.Length;
            return Uri.UnescapeDataString(query.Substring(start, end - start));
        }

        private string HandlePlaybackGoto(HttpListenerContext ctx)
        {
            if (ctx.Request.HttpMethod != "POST")
                return "{\"error\":\"Use POST with JSON body: {\\\"targetTime\\\":\\\"yyyy-MM-dd HH:mm\\\"}\"}";

            try
            {
                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                string targetStr = null;
                int idx = body.IndexOf("\"targetTime\"");
                if (idx >= 0)
                {
                    int colon = body.IndexOf(':', idx);
                    int quote1 = body.IndexOf('"', colon);
                    int quote2 = body.IndexOf('"', quote1 + 1);
                    if (quote1 >= 0 && quote2 > quote1)
                        targetStr = body.Substring(quote1 + 1, quote2 - quote1 - 1);
                }

                if (string.IsNullOrEmpty(targetStr))
                    return "{\"error\":\"Missing targetTime in JSON body\"}";

                DateTime target;
                if (!DateTime.TryParse(targetStr, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out target))
                    return "{\"error\":\"Invalid date format. Use yyyy-MM-dd HH:mm\"}";

                if (NinjaTrader.Cbi.Connection.PlaybackConnection == null)
                    return "{\"error\":\"Not in Playback mode. Open Market Replay in NinjaTrader first.\"}";

                lock (PlaybackLock)
                {
                    playbackTargetTime = target;
                    playbackStatus = "seeking";
                }

                NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = 1000;

                return string.Format("{{\"status\":\"seeking\",\"targetTime\":\"{0:yyyy-MM-dd HH:mm:ss}\"}}", target);
            }
            catch (Exception ex)
            {
                return string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "\\\""));
            }
        }

        public static void UpdateData(MCPBridgeData data)
        {
            lock (DataLock) { latestData = data; }
        }

        public static void AppendBar(BarData bar, int maxBars)
        {
            lock (HistoryLock)
            {
                if (historyBuffer.Count > 0 && historyBuffer[historyBuffer.Count - 1].Time == bar.Time)
                    historyBuffer[historyBuffer.Count - 1] = bar;
                else
                    historyBuffer.Add(bar);

                historyMaxBars = maxBars;
                while (historyBuffer.Count > historyMaxBars)
                    historyBuffer.RemoveAt(0);
            }
        }

        public static List<BarData> GetHistory(int count)
        {
            lock (HistoryLock)
            {
                int n = Math.Min(count, historyBuffer.Count);
                return historyBuffer.GetRange(historyBuffer.Count - n, n);
            }
        }

        public static List<BarData> GetHistoryByDate(DateTime from, DateTime to)
        {
            lock (HistoryLock)
            {
                var result = new List<BarData>();
                for (int i = 0; i < historyBuffer.Count; i++)
                {
                    if (historyBuffer[i].Time >= from && historyBuffer[i].Time <= to)
                        result.Add(historyBuffer[i]);
                }
                return result;
            }
        }

        public static int GetHistoryCount()
        {
            lock (HistoryLock) { return historyBuffer.Count; }
        }

        public static DateTime? GetPlaybackTarget()
        {
            lock (PlaybackLock) { return playbackTargetTime; }
        }

        public static void PlaybackTargetReached()
        {
            lock (PlaybackLock)
            {
                playbackStatus = "arrived";
                playbackTargetTime = null;
            }
        }

        public static string GetPlaybackStatus()
        {
            lock (PlaybackLock) { return playbackStatus; }
        }

        private string HandleOrder(HttpListenerContext ctx)
        {
            if (ctx.Request.HttpMethod != "POST")
                return "{\"error\":\"Use POST with JSON body: {\\\"action\\\":\\\"enter_long|enter_short|exit|set_stop\\\", \\\"quantity\\\":1, \\\"stopPrice\\\":0}\"}";

            try
            {
                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                string action = ExtractJsonString(body, "action");
                if (string.IsNullOrEmpty(action))
                    return "{\"error\":\"Missing action in JSON body\"}";

                action = action.ToLower();
                if (action != "enter_long" && action != "enter_short" && action != "exit" && action != "set_stop")
                    return "{\"error\":\"Invalid action. Use: enter_long, enter_short, exit, set_stop\"}";

                int quantity = 1;
                string qtyStr = ExtractJsonNumber(body, "quantity");
                if (!string.IsNullOrEmpty(qtyStr))
                    int.TryParse(qtyStr, out quantity);

                double stopPrice = 0;
                string stopStr = ExtractJsonNumber(body, "stopPrice");
                if (!string.IsNullOrEmpty(stopStr))
                    double.TryParse(stopStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out stopPrice);

                var cmd = new OrderCommand
                {
                    Action = action,
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    Timestamp = DateTime.UtcNow
                };

                lock (OrderLock) { orderQueue.Add(cmd); }

                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return string.Format(inv, "{{\"status\":\"queued\",\"action\":\"{0}\",\"quantity\":{1},\"stopPrice\":{2}}}",
                    action, quantity, stopPrice.ToString("F2", inv));
            }
            catch (Exception ex)
            {
                return string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "\\\""));
            }
        }

        private string ExtractJsonString(string json, string key)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx);
            int q1 = json.IndexOf('"', colon);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 >= 0 && q2 > q1)
                return json.Substring(q1 + 1, q2 - q1 - 1);
            return null;
        }

        private string ExtractJsonNumber(string json, string key)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < json.Length && json[start] == ' ') start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;
            if (end > start)
                return json.Substring(start, end - start);
            return null;
        }

        public static List<OrderCommand> ConsumeOrders()
        {
            lock (OrderLock)
            {
                if (orderQueue.Count == 0) return null;
                var cmds = new List<OrderCommand>(orderQueue);
                orderQueue.Clear();
                return cmds;
            }
        }

        public static void UpdatePosition(PositionState state)
        {
            lock (PositionLock) { currentPosition = state; }
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

    public class OrderCommand
    {
        public string Action;
        public int Quantity;
        public double StopPrice;
        public DateTime Timestamp;
    }

    public class PositionState
    {
        public string Direction = "flat";
        public double EntryPrice;
        public int Quantity;
        public double StopPrice;
        public double UnrealizedPnL;
        public string TimestampNY = "";

        public string ToJson()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(inv,
                "{{\"position\":\"{0}\",\"entryPrice\":{1},\"quantity\":{2},\"stopPrice\":{3},\"unrealizedPnL\":{4},\"timestamp\":\"{5}\"}}",
                Direction, EntryPrice.ToString("F2", inv), Quantity, StopPrice.ToString("F2", inv),
                UnrealizedPnL.ToString("F2", inv), TimestampNY);
        }
    }
}
