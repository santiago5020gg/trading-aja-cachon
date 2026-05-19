using System.Collections.Generic;

namespace NinjaTrader.Cbi
{
    public class Account
    {
        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
