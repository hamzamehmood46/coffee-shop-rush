namespace CoffeeShop.Engine;

/// <summary>Where an order is right now. The web page turns each stage into a spot on the screen.</summary>
public enum OrderStage
{
    /// <summary>Standing in line, waiting for the cashier.</summary>
    Queueing,
    /// <summary>Being taken by the cashier.</summary>
    AtCashier,
    /// <summary>Written on a paper ticket and pinned to the rail (safety net on).</summary>
    OnRail,
    /// <summary>Shouted across the shop and still in the air (safety net off).</summary>
    Shouted,
    /// <summary>The barista has heard it and is holding it in their head (safety net off).</summary>
    InBaristasHead,
    /// <summary>The barista is making the drink.</summary>
    Brewing,
    /// <summary>The drink is ready for pickup.</summary>
    Ready,
    /// <summary>The order vanished: the barista forgot it.</summary>
    Lost,
    /// <summary>A repeat order that the ticket rail recognised and merged into the original.</summary>
    Merged,
}

/// <summary>One order moving through the shop. A customer who asks twice creates two orders with the same ticket number.</summary>
public sealed class Order
{
    internal Order(int id, int ticketId, bool isRepeat)
    {
        Id = id;
        TicketId = ticketId;
        IsRepeat = isRepeat;
    }

    public int Id { get; }

    /// <summary>The customer's ticket number. A repeat order has the same number as the first one.</summary>
    public int TicketId { get; }

    /// <summary>True when the customer got impatient and asked again.</summary>
    public bool IsRepeat { get; }

    public OrderStage Stage { get; internal set; } = OrderStage.Queueing;

    /// <summary>Seconds spent in the current stage.</summary>
    public double StageAge { get; internal set; }

    /// <summary>Position in the cashier's line, 0 for the front. Only meaningful while queueing.</summary>
    public int LinePosition { get; internal set; }

    /// <summary>Position on the ticket rail, 0 for the oldest ticket. Only meaningful while on the rail.</summary>
    public int RailSlot { get; internal set; }

    /// <summary>True once the order has reached an end state and is only being shown for a moment before it disappears.</summary>
    public bool IsFinished => Stage is OrderStage.Ready or OrderStage.Lost or OrderStage.Merged;

    /// <summary>True once the order has been written on a ticket, so a crash cannot make it vanish.</summary>
    internal bool Pinned { get; set; }

    internal void MoveTo(OrderStage stage)
    {
        Stage = stage;
        StageAge = 0;
    }
}
