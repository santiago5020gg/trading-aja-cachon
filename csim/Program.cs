using System;
using System.Globalization;
using System.IO;
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
            var engine = new OrderEngine(strategy, 0.25, 1);
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

            // Override output directory if specified
            string outputDir = config.OutputDir;
            if (outputDir != null)
            {
                var field = typeof(MNQ10minV2).GetField("botHistoryDir",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Directory.CreateDirectory(outputDir);
                field.SetValue(strategy, outputDir);
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

            // Determine actual output directory for display
            if (outputDir == null)
            {
                var field = typeof(MNQ10minV2).GetField("botHistoryDir",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                outputDir = (string)field.GetValue(strategy);
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== RESULTADO ===");
            Console.WriteLine($"  Ticks procesados: {tickCount:N0}");
            Console.WriteLine($"  Archivos generados en: {outputDir}");
        }

        // ───────────────────────────────────────────────
        // Main simulation loop
        // ───────────────────────────────────────────────

        static long RunSimulation(MNQ10minV2 strategy, OrderEngine engine, string tickFile)
        {
            long tickCount = 0;
            int currentBarMinute = -1;

            using (var reader = new StreamReader(tickFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Parse tick
                    if (!TryParseTick(line, out Tick tick))
                        continue;

                    tickCount++;

                    // Step 1: Fill pending entries from previous tick
                    engine.FillPendingEntries(tick.Last, tick.Time);

                    // Step 2: Determine if new 1-minute bar
                    int tickMinute = tick.Time.Hour * 60 + tick.Time.Minute;
                    if (tickMinute != currentBarMinute)
                    {
                        // New bar
                        currentBarMinute = tickMinute;
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

                    // Step 3: Set Volume and Time
                    strategy.Volume[0] = tick.Volume;
                    strategy.Time[0] = tick.Time;

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
                tick.Time = new DateTime(year, month, day, hour, minute, second).AddTicks(ticks);
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
