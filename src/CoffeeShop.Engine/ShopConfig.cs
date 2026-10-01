namespace CoffeeShop.Engine;

/// <summary>Tunable settings for one run of the coffee shop.</summary>
public sealed class ShopConfig
{
    /// <summary>Average customers walking in per minute. Zero means nobody arrives on their own.</summary>
    public double OrdersPerMinute { get; set; } = 14;

    /// <summary>Seconds the cashier needs to take one order.</summary>
    public double CashierSeconds { get; set; } = 1.2;

    /// <summary>Seconds the barista needs to make one drink.</summary>
    public double BrewSeconds { get; set; } = 3.2;

    /// <summary>Seconds it takes the cashier's shout to reach the barista (only used without the safety net).</summary>
    public double ShoutSeconds { get; set; } = 0.7;

    /// <summary>How long a customer waits before asking again.</summary>
    public double PatienceSeconds { get; set; } = 13;

    /// <summary>How long the barista is out of action after a crash.</summary>
    public double RestartSeconds { get; set; } = 4;

    /// <summary>
    /// The safety net: every order is written on a paper ticket and pinned to a rail before anything else happens,
    /// and the barista skips tickets that were already made. Off, the cashier just shouts the order across the shop.
    /// </summary>
    public bool SafetyNet { get; set; }

    /// <summary>Seed for the random arrivals, so a run can be replayed exactly.</summary>
    public int Seed { get; set; } = 7;
}
