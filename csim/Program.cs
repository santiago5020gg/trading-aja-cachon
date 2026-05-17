using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace CSimulator
{
    // ───────────────────────────────────────────────
    // CLI configuration
    // ───────────────────────────────────────────────

    internal class SimConfig
    {
        public int ColchonStop = 5;
        public int ColchonBreakeven = 5;
        public int BreakevenPct = 60;
        public int MaxTrades = 2;
        public double PerdidaMaxDiaria = 400;
        public string ModoTP = "1a2";
        public string HoraCierre = "15:50";
        public string OutputDir = null;
        public bool NoTelemetry = false;
        public string TickFile = null;
    }

    // ───────────────────────────────────────────────
    // Tick data structure
    // ───────────────────────────────────────────────

    internal struct Tick
    {
        public DateTime Time;
        public double Last;
        public long Volume;
    }

    // ───────────────────────────────────────────────
    // Program entry point
    // ───────────────────────────────────────────────

    class Program
    {
        static readonly TimeZoneInfo EasternZone =
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        static void Main(string[] args)
        {
            var config = ParseArgs(args);

            if (config.TickFile == null)
            {
                PrintUsage();
                return;
            }

            if (!File.Exists(config.TickFile))
            {
                Console.Error.WriteLine($"Error: archivo no encontrado: {config.TickFile}");
                return;
            }

            // Print parameters
            Console.WriteLine("=== PARAMETROS ===");
            Console.WriteLine($"  ColchonStop: {config.ColchonStop} | ColchonBE: {config.ColchonBreakeven} | BreakevenPct: {config.BreakevenPct}%");
            Console.WriteLine($"  Max Trades/Dia: {config.MaxTrades} | PerdidaMaxDiaria: ${config.PerdidaMaxDiaria}");
            Console.WriteLine($"  Modo TP: {(config.ModoTP == "1a1" ? "Solo1a1" : "Con1a2")}");
            Console.WriteLine($"  Hora Cierre: {config.HoraCierre} ET");
            Console.WriteLine($"  Archivo: {config.TickFile}");
            Console.WriteLine();

            // Create strategy and engine
            var strategy = new MNQ10minV2();
            var engine = new OrderEngine(strategy, 0.25);
            strategy._orderEngine = engine;

            // Initialize (triggers OnStateChange: SetDefaults → Configure → DataLoaded)
            strategy.Initialize();

            // Set parameters AFTER Initialize (overriding defaults from OnStateChange)
            strategy.ColchonStop = config.ColchonStop;
            strategy.ColchonBreakeven = config.ColchonBreakeven;
            strategy.BreakevenPct = config.BreakevenPct;
            strategy.MaxTrades = config.MaxTrades;
            strategy.PerdidaMaxDiaria = config.PerdidaMaxDiaria;
            strategy.HoraCierre = config.HoraCierre;

            // Recalculate derived fields that depend on MaxTrades
            var flags2 = BindingFlags.NonPublic | BindingFlags.Instance;
            int maxHalf = (int)Math.Round((double)config.MaxTrades / 2, MidpointRounding.AwayFromZero);
            typeof(MNQ10minV2).GetField("maxStopsPuros", flags2).SetValue(strategy, maxHalf);
            typeof(MNQ10minV2).GetField("maxTakeProfits", flags2).SetValue(strategy, maxHalf);
            typeof(MNQ10minV2).GetField("maxBreakevens", flags2).SetValue(strategy, maxHalf);
            // Recalculate horaCierreH/M
            string[] hcParts = config.HoraCierre.Split(':');
            typeof(MNQ10minV2).GetField("horaCierreH", flags2).SetValue(strategy, int.Parse(hcParts[0]));
            typeof(MNQ10minV2).GetField("horaCierreM", flags2).SetValue(strategy, int.Parse(hcParts[1]));

            // Set ModoTP via reflection (nested enum)
            var modoProp = typeof(MNQ10minV2).GetProperty("ModoTP");
            var modoEnumType = modoProp.PropertyType;
            modoProp.SetValue(strategy, Enum.Parse(modoEnumType, config.ModoTP == "1a1" ? "Solo1a1" : "Con1a2"));

            // Set ModoOperacion to Operar
            var opProp = typeof(MNQ10minV2).GetProperty("ModoOperacion");
            opProp.SetValue(strategy, Enum.Parse(opProp.PropertyType, "Operar"));

            // Set ModoLog to Month
            var logProp = typeof(MNQ10minV2).GetProperty("ModoLog");
            logProp.SetValue(strategy, Enum.Parse(logProp.PropertyType, "Month"));

            // Build output directory: csim/output/<months>-<filters>
            string outputDir = config.OutputDir;
            if (outputDir == null)
            {
                string dirName = BuildOutputDirName(config);
                string csimDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                // Go up from bin/Debug/net9.0 to csim/
                string csimRoot = Path.GetFullPath(Path.Combine(csimDir, "..", "..", ".."));
                outputDir = Path.Combine(csimRoot, "output", dirName);
            }

            Directory.CreateDirectory(outputDir);
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(MNQ10minV2).GetField("botHistoryDir", flags).SetValue(strategy, outputDir);
                typeof(MNQ10minV2).GetField("csvLogPath", flags)
                    .SetValue(strategy, Path.Combine(outputDir, "mnq10minv2_trades_log.csv"));
                typeof(MNQ10minV2).GetField("csvDailyPath", flags)
                    .SetValue(strategy, Path.Combine(outputDir, "mnq10minv2_daily_log.csv"));
                typeof(MNQ10minV2).GetField("csvBarLogPath", flags)
                    .SetValue(strategy, Path.Combine(outputDir, "mnq10minv2_bar_log.csv"));
                typeof(MNQ10minV2).GetMethod("InitCsvLogs", flags)
                    .Invoke(strategy, null);
            }

            // Disable telemetry if requested
            if (config.NoTelemetry)
            {
                var telField = typeof(MNQ10minV2).GetField("telemetryPath",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                telField.SetValue(strategy, Path.Combine(Path.GetTempPath(), "mnq_noop.json"));
            }

            // Run simulation
            long tickCount = RunSimulation(strategy, engine, config.TickFile);


            // Summary
            Console.WriteLine();
            Console.WriteLine("=== RESULTADO ===");
            Console.WriteLine($"  Ticks procesados: {tickCount:N0}");
            Console.WriteLine($"  Archivos generados en: {outputDir}");
            Console.WriteLine();

            // Print daily summary table
            string dailyPath = Path.Combine(outputDir, "mnq10minv2_daily_log.csv");
            if (File.Exists(dailyPath))
                PrintDailySummary(dailyPath);
        }

        // ───────────────────────────────────────────────
        // Auto-generate output dir name from tick file months + params
        // ───────────────────────────────────────────────

        static string BuildOutputDirName(SimConfig config)
        {
            string[] monthAbbrevs = { "ene", "feb", "mar", "abr", "may", "jun",
                "jul", "ago", "sep", "oct", "nov", "dic" };

            // Detect months from tick file name
            string fileName = Path.GetFileNameWithoutExtension(config.TickFile).ToLower();
            var months = new List<string>();
            string[] spanishMonths = { "enero", "febrero", "marzo", "abril", "mayo", "junio",
                "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
            for (int i = 0; i < spanishMonths.Length; i++)
            {
                if (fileName.Contains(spanishMonths[i]))
                    months.Add(monthAbbrevs[i]);
            }

            // If no months found in name, scan first few lines to detect date range
            if (months.Count == 0)
            {
                try
                {
                    var detectedMonths = new HashSet<int>();
                    using (var reader = new StreamReader(config.TickFile))
                    {
                        // Read first line for start month
                        string first = reader.ReadLine();
                        if (first != null && first.Length >= 6)
                        {
                            int m = int.Parse(first.Substring(4, 2));
                            detectedMonths.Add(m);
                        }
                        // Seek to end for last month (read last 500 bytes)
                    }
                    // Read last line
                    var allBytes = new FileStream(config.TickFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    long seekPos = Math.Max(0, allBytes.Length - 200);
                    allBytes.Seek(seekPos, SeekOrigin.Begin);
                    var tail = new StreamReader(allBytes);
                    string lastLine = null;
                    string l;
                    while ((l = tail.ReadLine()) != null)
                    {
                        if (l.Length > 10) lastLine = l;
                    }
                    tail.Close();
                    if (lastLine != null && lastLine.Length >= 6)
                    {
                        int m = int.Parse(lastLine.Substring(4, 2));
                        detectedMonths.Add(m);
                    }

                    foreach (int m in detectedMonths.OrderBy(x => x))
                        months.Add(monthAbbrevs[m - 1]);
                }
                catch { }
            }

            string monthPart = months.Count > 0 ? string.Join("-", months) : "sim";

            string filters = $"cs{config.ColchonStop}-cbe{config.ColchonBreakeven}-bk{config.BreakevenPct}" +
                             $"-mt{config.MaxTrades}-pmd{config.PerdidaMaxDiaria:0}-{config.ModoTP}";

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

            return $"{timestamp}_{monthPart}-{filters}";
        }

        // ───────────────────────────────────────────────
        // Daily summary table
        // ───────────────────────────────────────────────

        static void PrintDailySummary(string dailyPath)
        {
            var lines = File.ReadAllLines(dailyPath);
            if (lines.Length < 2) return;

            string[] monthNames = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            // Header: Date,DailyPnL,TotalPnL,Trades,RangoPts,...
            // CSV uses comma as decimal separator (Spanish locale from strategy)
            // Example: 2026-01-02,-343,00,-343,00,3,83,25
            // Strategy: date is always 10 chars, then parse fields knowing decimals use ,XX pattern

            var days = new List<(DateTime date, double pnl, int trades)>();

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = ParseDailyCsvLine(lines[i]);
                if (fields == null || fields.Count < 4) continue;

                if (!DateTime.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    continue;
                string pnlStr = fields[1].Replace(',', '.');
                if (!double.TryParse(pnlStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double pnl))
                    continue;
                if (!int.TryParse(fields[3], out int trades))
                    continue;
                if (trades == 0) continue;

                days.Add((date, pnl, trades));
            }

            if (days.Count == 0) return;

            Console.WriteLine("================================================================================");
            Console.WriteLine("RESUMEN");
            Console.WriteLine("================================================================================");

            double grandTotal = 0;
            int grandTrades = 0;
            int daysOperated = 0;

            var grouped = days.GroupBy(d => new { d.date.Year, d.date.Month });

            foreach (var month in grouped)
            {
                Console.WriteLine();
                Console.WriteLine($"{monthNames[month.Key.Month]} {month.Key.Year}");
                Console.WriteLine();
                Console.WriteLine("  dia   PnL          #trades   acumulado");

                double monthAccum = 0;
                int monthTrades = 0;

                foreach (var day in month)
                {
                    monthAccum += day.pnl;
                    monthTrades += day.trades;
                    daysOperated++;
                    grandTotal += day.pnl;
                    grandTrades += day.trades;

                    string pnlFmt = FormatMoney(day.pnl);
                    string accumFmt = FormatMoney(monthAccum);

                    Console.WriteLine($"  {day.date.Day,-5} {pnlFmt,-12} {day.trades,-9} {accumFmt}");
                }

                Console.WriteLine("  ---   ---          ---       ---");
                Console.WriteLine($"  MES   {FormatMoney(monthAccum),-12} {monthTrades,-9}");
            }

            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine($"  Dias operados: {daysOperated}");
            Console.WriteLine($"  Total Trades: {grandTrades}");
            Console.WriteLine($"  PnL Total: {FormatMoney(grandTotal)}");
            double avg = daysOperated > 0 ? grandTotal / daysOperated : 0;
            Console.WriteLine($"  Promedio diario: {FormatMoney(avg)}");
            Console.WriteLine();
        }

        // Parse daily CSV with comma as decimal separator (Spanish locale).
        // Header: Date,DailyPnL,TotalPnL,Trades,RangoPts,...
        // Example: "2026-01-02,-343,00,-598,50,3,83,25"
        // We know field positions: 0=Date, 1=DailyPnL(float), 2=TotalPnL(float), 3=Trades(int), 4=RangoPts(float)
        static List<string> ParseDailyCsvLine(string line)
        {
            if (line.Length < 10) return null;
            string date = line.Substring(0, 10);
            if (line.Length < 11 || line[10] != ',') return null;

            string rest = line.Substring(11);
            var parts = rest.Split(',');
            var tokens = new List<string> { date };

            int idx = 0;
            while (idx < parts.Length)
            {
                // tokens.Count == 3 means we're about to parse Trades (integer field)
                if (tokens.Count == 3)
                {
                    tokens.Add(parts[idx]);
                    idx++;
                }
                else if (idx + 1 < parts.Length && parts[idx + 1].Length == 2 &&
                         int.TryParse(parts[idx + 1], out _))
                {
                    tokens.Add(parts[idx] + "," + parts[idx + 1]);
                    idx += 2;
                }
                else
                {
                    tokens.Add(parts[idx]);
                    idx++;
                }
            }
            return tokens;
        }

        static string FormatMoney(double value)
        {
            if (value >= 0)
                return $"${value.ToString("F2", CultureInfo.InvariantCulture)}";
            return $"$-{Math.Abs(value).ToString("F2", CultureInfo.InvariantCulture)}";
        }

        // ───────────────────────────────────────────────
        // Main simulation loop
        // ───────────────────────────────────────────────

        static long RunSimulation(MNQ10minV2 strategy, OrderEngine engine, string tickFile)
        {
            long tickCount = 0;
            // 2-minute bars: bar period defined by even-minute boundary
            // Bar closing at minute M contains ticks from M-2 to M-1 (e.g., bar "09:32" has ticks 09:30:00–09:31:59)
            int currentBarSlot = -1;  // floor(totalMinutes / 2)

            using (var reader = new StreamReader(tickFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (!TryParseTick(line, out Tick tick))
                        continue;

                    tickCount++;

                    // Step 1: Fill pending entries from previous tick
                    engine.FillPendingEntries(tick.Last, tick.Time);

                    // Step 2: Determine bar slot (2-minute periods)
                    // Convert tick UTC time to ET for bar alignment
                    DateTime tickET = TimeZoneInfo.ConvertTimeFromUtc(tick.Time, EasternZone);
                    int totalMinutes = tickET.Hour * 60 + tickET.Minute;
                    int barSlot = totalMinutes / 2;

                    if (barSlot != currentBarSlot)
                    {
                        // New bar
                        currentBarSlot = barSlot;
                        strategy.CurrentBar++;
                        strategy.IsFirstTickOfBar = true;
                        strategy.Open[0] = tick.Last;
                        strategy.High[0] = tick.Last;
                        strategy.Low[0] = tick.Last;
                        strategy.Close[0] = tick.Last;
                    }
                    else
                    {
                        // Same bar: update OHLC
                        strategy.IsFirstTickOfBar = false;
                        if (tick.Last > strategy.High[0])
                            strategy.High[0] = tick.Last;
                        if (tick.Last < strategy.Low[0])
                            strategy.Low[0] = tick.Last;
                        strategy.Close[0] = tick.Last;
                    }

                    // Step 3: Set Volume and Time[0] = bar close time (even minute boundary)
                    strategy.Volume[0] = tick.Volume;
                    // NinjaTrader Time[0] for a 2-min bar is the period close (next even minute)
                    int closeMinute = (barSlot + 1) * 2;
                    int closeH = closeMinute / 60;
                    int closeM = closeMinute % 60;
                    DateTime barCloseET = tickET.Date.AddHours(closeH).AddMinutes(closeM);
                    strategy.Time[0] = TimeZoneInfo.ConvertTimeToUtc(barCloseET, EasternZone);

                    // Step 4: Call OnBarUpdate
                    strategy.TriggerOnBarUpdate();

                    // Step 5: Process market exits then evaluate stops/targets
                    engine.ProcessMarketExits(tick.Last, tick.Time);
                    engine.EvaluateStopsAndTargets(tick.Last, tick.Time);

                }
            }

            return tickCount;
        }

        // ───────────────────────────────────────────────
        // Tick parser
        // Format: "yyyyMMdd HHmmss fffffff;last;bid;ask;volume"
        // ───────────────────────────────────────────────

        static bool TryParseTick(string line, out Tick tick)
        {
            tick = default;

            // Split by semicolons
            // The timestamp part contains spaces, so it's the content before the first semicolon
            int firstSemi = line.IndexOf(';');
            if (firstSemi < 0) return false;

            string timestampStr = line.Substring(0, firstSemi);
            string remaining = line.Substring(firstSemi + 1);

            // Parse remaining fields: last;bid;ask;volume
            string[] fields = remaining.Split(';');
            if (fields.Length < 4) return false;

            // Parse last price
            if (!double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double last))
                return false;

            // Parse volume
            if (!long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long volume))
                return false;

            // Parse timestamp: "20260401 050000 1480000"
            // Format: yyyyMMdd HHmmss fffffff
            // The sub-second part is 7 digits (ticks, i.e., 100-nanosecond units)
            string[] tsParts = timestampStr.Split(' ');
            if (tsParts.Length < 3) return false;

            string datePart = tsParts[0]; // "20260401"
            string timePart = tsParts[1]; // "050000"
            string fracPart = tsParts[2]; // "1480000"

            if (datePart.Length != 8 || timePart.Length != 6) return false;

            int year = int.Parse(datePart.Substring(0, 4));
            int month = int.Parse(datePart.Substring(4, 2));
            int day = int.Parse(datePart.Substring(6, 2));
            int hour = int.Parse(timePart.Substring(0, 2));
            int minute = int.Parse(timePart.Substring(2, 2));
            int second = int.Parse(timePart.Substring(4, 2));

            // Parse fractional seconds (7-digit ticks)
            long ticks = 0;
            if (fracPart.Length <= 7)
            {
                ticks = long.Parse(fracPart) * (long)Math.Pow(10, 7 - fracPart.Length);
            }

            try
            {
                // NinjaTrader tick exports use UTC timestamps.
                // Strategy does ConvertTime(Time[0], easternZone) expecting UTC input.
                tick.Time = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).AddTicks(ticks);
            }
            catch
            {
                return false;
            }

            tick.Last = last;
            tick.Volume = volume;
            return true;
        }

        // ───────────────────────────────────────────────
        // CLI argument parser
        // ───────────────────────────────────────────────

        static SimConfig ParseArgs(string[] args)
        {
            var config = new SimConfig();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--colchon":
                        if (++i < args.Length) config.ColchonStop = int.Parse(args[i]);
                        break;
                    case "--colchon-be":
                        if (++i < args.Length) config.ColchonBreakeven = int.Parse(args[i]);
                        break;
                    case "--breakeven-pct":
                        if (++i < args.Length) config.BreakevenPct = int.Parse(args[i]);
                        break;
                    case "--trades":
                        if (++i < args.Length) config.MaxTrades = int.Parse(args[i]);
                        break;
                    case "--perdida-max":
                        if (++i < args.Length) config.PerdidaMaxDiaria = double.Parse(args[i], CultureInfo.InvariantCulture);
                        break;
                    case "--modo":
                        if (++i < args.Length) config.ModoTP = args[i];
                        break;
                    case "--cierre":
                        if (++i < args.Length) config.HoraCierre = args[i];
                        break;
                    case "--output-dir":
                        if (++i < args.Length) config.OutputDir = args[i];
                        break;
                    case "--no-telemetry":
                        config.NoTelemetry = true;
                        break;
                    default:
                        // Positional argument = tick file
                        if (!args[i].StartsWith("--"))
                            config.TickFile = args[i];
                        break;
                }
            }

            return config;
        }

        // ───────────────────────────────────────────────
        // Usage help
        // ───────────────────────────────────────────────

        static void PrintUsage()
        {
            Console.WriteLine("Uso: dotnet run --project csim -- [opciones] <tick_file>");
            Console.WriteLine();
            Console.WriteLine("Opciones:");
            Console.WriteLine("  --colchon <int>       ColchonStop (default: 5)");
            Console.WriteLine("  --colchon-be <int>    ColchonBreakeven (default: 5)");
            Console.WriteLine("  --breakeven-pct <int> BreakevenPct (default: 60)");
            Console.WriteLine("  --trades <int>        MaxTrades (default: 2)");
            Console.WriteLine("  --perdida-max <float> PerdidaMaxDiaria (default: 400)");
            Console.WriteLine("  --modo <1a1|1a2>      ModoTP (default: 1a2)");
            Console.WriteLine("  --cierre <HH:mm>      HoraCierre (default: 15:50)");
            Console.WriteLine("  --output-dir <path>   Override CSV output directory");
            Console.WriteLine("  --no-telemetry        Disable telemetry JSON writes");
        }
    }
}
