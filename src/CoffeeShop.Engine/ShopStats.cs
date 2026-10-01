namespace CoffeeShop.Engine;

/// <summary>The scoreboard.</summary>
/// <param name="CustomersServed">Customers who got at least one drink.</param>
/// <param name="CustomersPlaced">Different customers who have ordered so far.</param>
/// <param name="OrdersLost">Orders the barista forgot, so the customer waited for nothing.</param>
/// <param name="WastedDrinks">Extra drinks made because the same customer was served twice.</param>
/// <param name="AverageWaitSeconds">Average time from ordering to the first drink, for customers who were served.</param>
public sealed record ShopStats(
    int CustomersPlaced,
    int CustomersServed,
    int OrdersLost,
    int WastedDrinks,
    double AverageWaitSeconds)
{
    /// <summary>Customers still waiting for their first drink.</summary>
    public int StillWaiting => CustomersPlaced - CustomersServed;
}

/// <summary>A customer who has ordered and is standing at the pickup counter waiting for a drink.</summary>
/// <param name="TicketId">The customer's ticket number.</param>
/// <param name="WaitSeconds">How long they have been waiting since they first ordered.</param>
/// <param name="Impatient">True once they have waited longer than their patience allows.</param>
/// <param name="Forgotten">True when no order of theirs is in progress any more: the barista forgot it.</param>
public sealed record PickupWaiter(int TicketId, double WaitSeconds, bool Impatient, bool Forgotten);

/// <summary>A plain-English line for the "what just happened" feed.</summary>
/// <param name="Time">Shop clock, in seconds, when it happened.</param>
/// <param name="Kind">Used by the page to pick a colour.</param>
/// <param name="Message">The sentence to show.</param>
public sealed record ShopEvent(double Time, EventKind Kind, string Message);

public enum EventKind
{
    Info,
    Good,
    Bad,
    Save,
}
