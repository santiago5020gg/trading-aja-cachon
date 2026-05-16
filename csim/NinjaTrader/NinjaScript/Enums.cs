namespace NinjaTrader.NinjaScript
{
    public enum State
    {
        SetDefaults,
        Configure,
        DataLoaded,
        Historical,
        Realtime,
        Terminated
    }

    public enum Calculate
    {
        OnBarClose,
        OnEachTick,
        OnPriceChange
    }

    public enum MaximumBarsLookBack
    {
        TwoHundredFiftySix,
        Infinite
    }

    public enum OrderFillResolution
    {
        Standard,
        High
    }

    public enum StartBehavior
    {
        WaitUntilFlat,
        AdoptAccountPosition,
        ImmediatelySubmit
    }

    public enum TimeInForce
    {
        Gtc,
        Day
    }

    public enum RealtimeErrorHandling
    {
        StopCancelClose,
        IgnoreAllErrors
    }

    public enum StopTargetHandling
    {
        PerEntryExecution,
        ByStrategyPosition
    }

    public enum EntryHandling
    {
        AllEntries,
        UniqueEntries
    }

    public enum CalculationMode
    {
        Currency,
        Percent,
        Price,
        Ticks
    }
}

namespace NinjaTrader.Cbi
{
    public enum MarketPosition
    {
        Flat,
        Long,
        Short
    }

    public enum OrderState
    {
        Initialized,
        Submitted,
        Accepted,
        Working,
        Filled,
        Cancelled,
        Rejected
    }
}
