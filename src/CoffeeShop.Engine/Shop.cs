namespace CoffeeShop.Engine;

/// <summary>
/// A tiny coffee shop that is secretly a distributed system.
/// The cashier is a web request, the shout across the shop is a message that can get lost, the barista is a worker
/// that can crash, and the paper-ticket rail is a durable outbox. Time only moves when <see cref="Tick"/> is called,
/// so a run is deterministic and easy to test.
/// </summary>
public sealed class Shop
{
    /// <summary>How long a finished order stays on screen before it disappears.</summary>
    public const double FinishedLingerSeconds = 1.1;

    private const int MaxEvents = 40;

    private readonly Random _random;
    private readonly List<Order> _orders = new();
    private readonly Queue<Order> _cashierLine = new();
    private readonly List<Order> _rail = new();
    private readonly Queue<Order> _baristasHead = new();
    private readonly Dictionary<int, Ticket> _tickets = new();
    private readonly List<ShopEvent> _events = new();

    private Order? _atCashier;
    private Order? _brewing;
    private double _crashRemaining;
    private int _nextOrderId = 1;
    private int _nextTicketId = 1;
    private int _ordersLost;
    private int _wastedDrinks;
    private double _waitTotal;
    private int _served;

    public Shop(ShopConfig? config = null)
    {
        Config = config ?? new ShopConfig();
        _random = new Random(Config.Seed);
    }

    /// <summary>Settings. They can be changed while the shop is running; the next tick uses the new values.</summary>
    public ShopConfig Config { get; }

    /// <summary>Seconds since the shop opened.</summary>
    public double Clock { get; private set; }

    /// <summary>Everything currently worth drawing, including orders that have just finished.</summary>
    public IReadOnlyList<Order> Orders => _orders;

    public bool BaristaCrashed => _crashRemaining > 0;

    /// <summary>Seconds until the barista is back on their feet. Zero when they are fine.</summary>
    public double BaristaRecoversIn => Math.Max(0, _crashRemaining);

    /// <summary>How far through the current drink the barista is, from 0 to 1. Zero when idle.</summary>
    public double BrewProgress => _brewing is null ? 0 : Math.Clamp(_brewing.StageAge / Config.BrewSeconds, 0, 1);

    /// <summary>How far through the current order the cashier is, from 0 to 1. Zero when idle.</summary>
    public double CashierProgress => _atCashier is null ? 0 : Math.Clamp(_atCashier.StageAge / Config.CashierSeconds, 0, 1);

    /// <summary>Number of paper tickets currently pinned to the rail.</summary>
    public int TicketsOnRail => _rail.Count;

    /// <summary>The latest happenings, newest first.</summary>
    public IReadOnlyList<ShopEvent> RecentEvents(int count = 6) =>
        _events.AsEnumerable().Reverse().Take(count).ToList();

    public ShopStats Stats
    {
        get
        {
            var average = _served == 0 ? 0 : _waitTotal / _served;
            return new ShopStats(_tickets.Count, _served, _ordersLost, _wastedDrinks, average);
        }
    }

    /// <summary>
    /// Customers who have finished ordering and are now waiting for their first drink, oldest first.
    /// Someone still in the cashier's line is not listed, because they have not ordered yet.
    /// </summary>
    public IReadOnlyList<PickupWaiter> PickupWaiters
    {
        get
        {
            var waiters = new List<PickupWaiter>();
            foreach (var ticket in _tickets.Values.OrderBy(t => t.Id))
            {
                if (ticket.Drinks > 0)
                {
                    continue;
                }

                var live = _orders.Where(o => o.TicketId == ticket.Id && !o.IsFinished).ToList();
                if (live.Any(o => o.Stage is OrderStage.Queueing or OrderStage.AtCashier))
                {
                    continue;
                }

                var waited = Clock - ticket.PlacedAt;
                waiters.Add(new PickupWaiter(ticket.Id, waited, waited >= Config.PatienceSeconds, live.Count == 0));
            }

            return waiters;
        }
    }

    /// <summary>A new customer walks up and orders.</summary>
    public Order PlaceOrder()
    {
        var ticket = new Ticket(_nextTicketId++, Clock);
        _tickets[ticket.Id] = ticket;
        var order = new Order(_nextOrderId++, ticket.Id, isRepeat: false);
        Enqueue(order);
        return order;
    }

    /// <summary>The barista drops the ball: everything they were holding in their head is at risk.</summary>
    public void CrashBarista()
    {
        if (BaristaCrashed)
        {
            return;
        }

        _crashRemaining = Config.RestartSeconds;
        var lost = 0;
        var saved = 0;

        if (_brewing is not null)
        {
            if (_brewing.Pinned)
            {
                _brewing.MoveTo(OrderStage.OnRail);
                _rail.Insert(0, _brewing);
                saved++;
            }
            else
            {
                Lose(_brewing);
                lost++;
            }

            _brewing = null;
        }

        while (_baristasHead.Count > 0)
        {
            Lose(_baristasHead.Dequeue());
            lost++;
        }

        Reindex();

        if (lost > 0 && saved == 0 && _rail.Count == 0)
        {
            AddEvent(EventKind.Bad, $"The barista crashed and forgot {Plural(lost, "order")}. Those customers will wait for nothing.");
        }
        else if (lost > 0)
        {
            AddEvent(EventKind.Bad, $"The barista crashed and forgot {Plural(lost, "order")}, but the pinned tickets survived.");
        }
        else if (saved > 0 || _rail.Count > 0)
        {
            AddEvent(EventKind.Save, "The barista crashed, but every order is on a paper ticket. Nothing is lost.");
        }
        else
        {
            AddEvent(EventKind.Info, "The barista crashed, but there was nothing to forget.");
        }
    }

    /// <summary>Moves the shop forward by <paramref name="seconds"/>.</summary>
    public void Tick(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        Clock += seconds;
        foreach (var order in _orders)
        {
            order.StageAge += seconds;
        }

        Arrivals(seconds);
        ImpatientCustomers();
        Cashier();
        Shouts();
        Barista(seconds);
        RemoveFinished();
        Reindex();
    }

    private void Arrivals(double seconds)
    {
        var rate = Config.OrdersPerMinute;
        if (rate <= 0)
        {
            return;
        }

        var chance = Math.Min(1.0, rate * seconds / 60.0);
        if (_random.NextDouble() < chance)
        {
            PlaceOrder();
        }
    }

    private void ImpatientCustomers()
    {
        foreach (var ticket in _tickets.Values)
        {
            if (ticket.Drinks > 0 || ticket.AskedAgain || Clock - ticket.PlacedAt < Config.PatienceSeconds)
            {
                continue;
            }

            ticket.AskedAgain = true;
            AddEvent(EventKind.Info, $"Customer #{ticket.Id} got impatient and asked again.");
            Enqueue(new Order(_nextOrderId++, ticket.Id, isRepeat: true));
        }
    }

    private void Cashier()
    {
        if (_atCashier is null && _cashierLine.Count > 0)
        {
            _atCashier = _cashierLine.Dequeue();
            _atCashier.MoveTo(OrderStage.AtCashier);
        }

        if (_atCashier is null || _atCashier.StageAge < Config.CashierSeconds)
        {
            return;
        }

        var order = _atCashier;
        _atCashier = null;

        if (!Config.SafetyNet)
        {
            order.MoveTo(OrderStage.Shouted);
            return;
        }

        if (order.IsRepeat && IsAlreadyHandled(order))
        {
            order.MoveTo(OrderStage.Merged);
            AddEvent(EventKind.Save, $"Customer #{order.TicketId} asked again, but their ticket is already on the rail. No second drink.");
            return;
        }

        order.Pinned = true;
        order.MoveTo(OrderStage.OnRail);
        _rail.Add(order);
    }

    private void Shouts()
    {
        foreach (var order in _orders.Where(o => o.Stage == OrderStage.Shouted && o.StageAge >= Config.ShoutSeconds).ToList())
        {
            if (BaristaCrashed)
            {
                Lose(order);
                AddEvent(EventKind.Bad, $"Order #{order.TicketId} was shouted while the barista was down. Nobody heard it.");
            }
            else
            {
                order.MoveTo(OrderStage.InBaristasHead);
                _baristasHead.Enqueue(order);
            }
        }
    }

    private void Barista(double seconds)
    {
        if (BaristaCrashed)
        {
            _crashRemaining -= seconds;
            if (_crashRemaining <= 0)
            {
                _crashRemaining = 0;
                AddEvent(EventKind.Good, "The barista is back at the machine.");
            }

            return;
        }

        if (_brewing is null)
        {
            var next = TakeNext();
            if (next is null)
            {
                return;
            }

            next.MoveTo(OrderStage.Brewing);
            _brewing = next;
            return;
        }

        if (_brewing.StageAge < Config.BrewSeconds)
        {
            return;
        }

        FinishDrink(_brewing);
        _brewing = null;
    }

    private Order? TakeNext()
    {
        while (_rail.Count > 0)
        {
            var candidate = _rail[0];
            _rail.RemoveAt(0);

            if (_tickets[candidate.TicketId].Drinks > 0)
            {
                candidate.MoveTo(OrderStage.Merged);
                AddEvent(EventKind.Save, $"Ticket #{candidate.TicketId} was already made. The barista skipped it.");
                continue;
            }

            return candidate;
        }

        return _baristasHead.Count > 0 ? _baristasHead.Dequeue() : null;
    }

    private void FinishDrink(Order order)
    {
        var ticket = _tickets[order.TicketId];
        ticket.Drinks++;
        order.MoveTo(OrderStage.Ready);

        if (ticket.Drinks == 1)
        {
            var wait = Clock - ticket.PlacedAt;
            _served++;
            _waitTotal += wait;
            AddEvent(EventKind.Good, $"Drink for customer #{ticket.Id} is ready after {wait:0.#} seconds.");
        }
        else
        {
            _wastedDrinks++;
            AddEvent(EventKind.Bad, $"Wasted drink: customer #{ticket.Id} already had one, and the barista made another.");
        }
    }

    private bool IsAlreadyHandled(Order repeat)
    {
        var ticket = _tickets[repeat.TicketId];
        if (ticket.Drinks > 0)
        {
            return true;
        }

        return _orders.Any(o =>
            o != repeat
            && o.TicketId == repeat.TicketId
            && o.Stage is OrderStage.OnRail or OrderStage.Brewing or OrderStage.InBaristasHead or OrderStage.Shouted);
    }

    private void Enqueue(Order order)
    {
        _orders.Add(order);
        _cashierLine.Enqueue(order);
    }

    private void Lose(Order order)
    {
        order.MoveTo(OrderStage.Lost);
        _ordersLost++;
    }

    private void RemoveFinished() =>
        _orders.RemoveAll(o => o.IsFinished && o.StageAge >= FinishedLingerSeconds);

    private void Reindex()
    {
        var line = 0;
        foreach (var order in _cashierLine)
        {
            order.LinePosition = line++;
        }

        for (var i = 0; i < _rail.Count; i++)
        {
            _rail[i].RailSlot = i;
        }
    }

    private void AddEvent(EventKind kind, string message)
    {
        _events.Add(new ShopEvent(Clock, kind, message));
        if (_events.Count > MaxEvents)
        {
            _events.RemoveAt(0);
        }
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private sealed class Ticket(int id, double placedAt)
    {
        public int Id { get; } = id;
        public double PlacedAt { get; } = placedAt;
        public int Drinks { get; set; }
        public bool AskedAgain { get; set; }
    }
}
