namespace NinjaTrader.Cbi
{
    public class MasterInstrument
    {
        public double PointValue { get; set; } = 5.0;
    }

    public class Instrument
    {
        public string FullName { get; set; } = "MNQ 06-26";
        public MasterInstrument MasterInstrument { get; set; } = new MasterInstrument();
    }
}
