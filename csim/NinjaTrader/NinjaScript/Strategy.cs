using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using CSimulator;

namespace NinjaTrader.NinjaScript.Strategies
{
    // ───────────────────────────────────────────────
    // Series types for [0]-indexed price/volume/time
    // ───────────────────────────────────────────────

    public class PriceSeries
    {
        private double _current;
        public double this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(double val) => _current = val;
    }

    public class VolumeSeries
    {
        private long _current;
        public long this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(long val) => _current = val;
    }

    public class TimeSeries
    {
        private DateTime _current;
        public DateTime this[int index]
        {
            get => _current;
            set => _current = value;
        }
        public void Set(DateTime val) => _current = val;
    }

    // ───────────────────────────────────────────────
    // Strategy base class
    // ───────────────────────────────────────────────

    public abstract class Strategy
    {
        // ── State ──
        public State State { get; set; }

        // ── Data Series ──
        public PriceSeries Close { get; } = new PriceSeries();
        public PriceSeries High { get; } = new PriceSeries();
        public PriceSeries Low { get; } = new PriceSeries();
        public PriceSeries Open { get; } = new PriceSeries();
        public VolumeSeries Volume { get; } = new VolumeSeries();
        public TimeSeries Time { get; } = new TimeSeries();

        // ── Bar state ──
        public int CurrentBar { get; set; }
        public bool IsFirstTickOfBar { get; set; }
        public double TickSize { get; set; } = 0.25;

        // ── Bid/Ask (for fill simulation) ──
        public double CurrentBid { get; set; }
        public double CurrentAsk { get; set; }

        protected double GetCurrentAsk() => CurrentAsk;
        protected double GetCurrentBid() => CurrentBid;

        // ── Position / Account / Instrument ──
        public Position Position { get; set; } = new Position();
        public Account Account { get; set; } = new Account();
        public Instrument Instrument { get; set; } = new Instrument();

        // ── Configuration properties (set in OnStateChange SetDefaults) ──
        public string Name { get; set; }
        public string Description { get; set; }
        public Calculate Calculate { get; set; }
        public int EntriesPerDirection { get; set; }
        public EntryHandling EntryHandling { get; set; }
        public bool IsExitOnSessionCloseStrategy { get; set; }
        public int ExitOnSessionCloseSeconds { get; set; }
        public bool IsFillLimitOnTouch { get; set; }
        public MaximumBarsLookBack MaximumBarsLookBack { get; set; }
        public OrderFillResolution OrderFillResolution { get; set; }
        public int Slippage { get; set; }
        public StartBehavior StartBehavior { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public bool TraceOrders { get; set; }
        public RealtimeErrorHandling RealtimeErrorHandling { get; set; }
        public StopTargetHandling StopTargetHandling { get; set; }
        public int BarsRequiredToTrade { get; set; }
        public bool IsInstantiatedOnEachOptimizationIteration { get; set; }
        public bool IsOverlay { get; set; }

        // ── Order engine (injected by harness) ──
        internal IOrderEngine _orderEngine;

        // ───────────────────────────────────────────
        // Virtual lifecycle methods
        // ───────────────────────────────────────────

        protected virtual void OnStateChange() { }
        protected virtual void OnBarUpdate() { }
        protected virtual void OnExecutionUpdate(Execution execution, string executionId,
            double price, int quantity, MarketPosition marketPosition,
            string orderId, DateTime time) { }

        // ───────────────────────────────────────────
        // Order submission methods
        // ───────────────────────────────────────────

        protected void EnterLong(int quantity, string signalName)
        {
            _orderEngine?.SubmitEntry(signalName, MarketPosition.Long, quantity);
        }

        protected void EnterShort(int quantity, string signalName)
        {
            _orderEngine?.SubmitEntry(signalName, MarketPosition.Short, quantity);
        }

        protected void ExitLong(string exitSignalName, string fromEntrySignalName)
        {
            _orderEngine?.SubmitMarketExit(fromEntrySignalName, MarketPosition.Long, exitSignalName);
        }

        protected void ExitShort(string exitSignalName, string fromEntrySignalName)
        {
            _orderEngine?.SubmitMarketExit(fromEntrySignalName, MarketPosition.Short, exitSignalName);
        }

        protected void SetStopLoss(string fromEntry, CalculationMode mode, double value, bool isSimulated)
        {
            // Convert to price if needed (harness always works in price)
            _orderEngine?.SetStop(fromEntry, value);
        }

        protected void SetProfitTarget(string fromEntry, CalculationMode mode, double value)
        {
            _orderEngine?.SetTarget(fromEntry, value, mode);
        }

        protected void CancelOrder(Order order)
        {
            _orderEngine?.CancelOrder(order);
        }

        // ───────────────────────────────────────────
        // Drawing (no-op in simulation)
        // ───────────────────────────────────────────

        protected void RemoveDrawObject(string tag) { /* no-op */ }

        // ───────────────────────────────────────────
        // Harness API — called by the simulator loop
        // ───────────────────────────────────────────

        public void Initialize()
        {
            State = State.SetDefaults;
            OnStateChange();

            State = State.Configure;
            OnStateChange();

            State = State.DataLoaded;
            OnStateChange();
        }

        public void Reconfigure()
        {
            State = State.Configure;
            OnStateChange();
            State = State.DataLoaded;
            OnStateChange();
        }

        public void TriggerOnBarUpdate()
        {
            OnBarUpdate();
        }

        public void TriggerOnExecutionUpdate(Execution execution, string executionId,
            double price, int quantity, MarketPosition marketPosition,
            string orderId, DateTime time)
        {
            OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
        }
    }
}
