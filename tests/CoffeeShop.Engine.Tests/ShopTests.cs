using CoffeeShop.Engine;

namespace CoffeeShop.Engine.Tests;

public class ShopTests
{
    private const double Step = 0.05;

    private static Shop Quiet(Action<ShopConfig>? tweak = null)
    {
        var config = new ShopConfig { OrdersPerMinute = 0 };
        tweak?.Invoke(config);
        return new Shop(config);
    }

    private static void Run(Shop shop, double seconds)
    {
        var steps = (int)Math.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            shop.Tick(Step);
        }
    }

    [Fact]
    public void An_order_becomes_a_drink_without_the_safety_net()
    {
        var shop = Quiet();
        shop.PlaceOrder();

        Run(shop, 7);

        Assert.Equal(1, shop.Stats.CustomersServed);
        Assert.Equal(0, shop.Stats.OrdersLost);
        Assert.InRange(shop.Stats.AverageWaitSeconds, 4.8, 5.6); // 1.2 cashier + 0.7 shout + 3.2 brew
    }

    [Fact]
    public void An_order_becomes_a_drink_with_the_safety_net_and_skips_the_shout()
    {
        var shop = Quiet(c => c.SafetyNet = true);
        shop.PlaceOrder();

        Run(shop, 7);

        Assert.Equal(1, shop.Stats.CustomersServed);
        Assert.InRange(shop.Stats.AverageWaitSeconds, 4.2, 4.9); // 1.2 cashier + 3.2 brew
    }

    [Fact]
    public void Without_the_safety_net_a_crash_loses_the_drink_being_made()
    {
        var shop = Quiet();
        shop.PlaceOrder();
        Run(shop, 3); // cashier done, shouted, barista is brewing

        shop.CrashBarista();

        Assert.Equal(1, shop.Stats.OrdersLost);
        Assert.Equal(OrderStage.Lost, shop.Orders[0].Stage);
        Run(shop, 3);
        Assert.Equal(0, shop.Stats.CustomersServed);
    }

    [Fact]
    public void Without_the_safety_net_a_crash_wipes_everything_the_barista_was_holding()
    {
        var shop = Quiet();
        shop.PlaceOrder();
        shop.PlaceOrder();
        shop.PlaceOrder();
        Run(shop, 3.4); // one brewing, one remembered, one still at the cashier

        shop.CrashBarista();

        Assert.Equal(2, shop.Stats.OrdersLost);

        Run(shop, 2.6); // the third order is shouted while the barista is down
        Assert.Equal(3, shop.Stats.OrdersLost);
    }

    [Fact]
    public void With_the_safety_net_a_crash_loses_nothing_and_the_drink_is_made_after_the_restart()
    {
        var shop = Quiet(c => c.SafetyNet = true);
        shop.PlaceOrder();
        Run(shop, 2); // pinned to the rail and already brewing

        shop.CrashBarista();

        Assert.Equal(0, shop.Stats.OrdersLost);
        Assert.Equal(1, shop.TicketsOnRail);

        Run(shop, 12);
        Assert.Equal(1, shop.Stats.CustomersServed);
        Assert.Equal(0, shop.Stats.WastedDrinks);
        Assert.Equal(0, shop.Stats.OrdersLost);
    }

    [Fact]
    public void Tickets_that_were_pinned_stay_safe_even_if_the_safety_net_is_switched_off_later()
    {
        var shop = Quiet(c => c.SafetyNet = true);
        shop.PlaceOrder();
        Run(shop, 2);

        shop.Config.SafetyNet = false;
        shop.CrashBarista();

        Assert.Equal(0, shop.Stats.OrdersLost);
    }

    [Fact]
    public void An_impatient_customer_gets_a_second_drink_without_the_safety_net()
    {
        var shop = Quiet(c =>
        {
            c.PatienceSeconds = 3;
            c.BrewSeconds = 4;
        });
        shop.PlaceOrder();

        Run(shop, 14);

        Assert.Equal(1, shop.Stats.CustomersPlaced);
        Assert.Equal(1, shop.Stats.CustomersServed);
        Assert.Equal(1, shop.Stats.WastedDrinks);
    }

    [Fact]
    public void An_impatient_customer_does_not_get_a_second_drink_with_the_safety_net()
    {
        var shop = Quiet(c =>
        {
            c.SafetyNet = true;
            c.PatienceSeconds = 3;
            c.BrewSeconds = 4;
        });
        shop.PlaceOrder();

        Run(shop, 14);

        Assert.Equal(1, shop.Stats.CustomersServed);
        Assert.Equal(0, shop.Stats.WastedDrinks);
        Assert.Contains(shop.RecentEvents(10), e => e.Kind == EventKind.Save && e.Message.Contains("asked again"));
    }

    [Fact]
    public void A_customer_only_asks_again_once()
    {
        var shop = Quiet(c =>
        {
            c.PatienceSeconds = 2;
            c.BrewSeconds = 30;
        });
        shop.PlaceOrder();

        Run(shop, 25);

        Assert.Equal(1, shop.Events("asked again"));
    }

    [Fact]
    public void Nobody_arrives_when_the_rate_is_zero()
    {
        var shop = Quiet();

        Run(shop, 60);

        Assert.Equal(0, shop.Stats.CustomersPlaced);
        Assert.Empty(shop.Orders);
    }

    [Fact]
    public void A_busier_shop_gets_more_customers()
    {
        var calm = new Shop(new ShopConfig { OrdersPerMinute = 6, Seed = 3 });
        var rush = new Shop(new ShopConfig { OrdersPerMinute = 40, Seed = 3 });

        Run(calm, 120);
        Run(rush, 120);

        Assert.True(rush.Stats.CustomersPlaced > calm.Stats.CustomersPlaced * 2);
    }

    [Fact]
    public void The_same_seed_replays_exactly()
    {
        var a = new Shop(new ShopConfig { OrdersPerMinute = 30, Seed = 11 });
        var b = new Shop(new ShopConfig { OrdersPerMinute = 30, Seed = 11 });

        Run(a, 90);
        Run(b, 90);

        Assert.Equal(a.Stats, b.Stats);
    }

    [Fact]
    public void Crashing_a_barista_who_is_already_down_changes_nothing()
    {
        var shop = Quiet();
        shop.CrashBarista();
        var remaining = shop.BaristaRecoversIn;

        Run(shop, 1);
        shop.CrashBarista();

        Assert.True(shop.BaristaRecoversIn < remaining);
    }

    [Fact]
    public void The_barista_comes_back_after_the_restart_time()
    {
        var shop = Quiet(c => c.RestartSeconds = 2);
        shop.CrashBarista();
        Assert.True(shop.BaristaCrashed);

        Run(shop, 2.2);

        Assert.False(shop.BaristaCrashed);
        Assert.Equal(EventKind.Good, shop.RecentEvents(1)[0].Kind);
    }

    [Fact]
    public void A_crash_with_nothing_in_flight_is_harmless_and_says_so()
    {
        var shop = Quiet();

        shop.CrashBarista();

        Assert.Equal(0, shop.Stats.OrdersLost);
        Assert.Contains("nothing to forget", shop.RecentEvents(1)[0].Message);
    }

    [Fact]
    public void Finished_orders_leave_the_screen_after_a_moment()
    {
        var shop = Quiet();
        shop.PlaceOrder();

        Run(shop, 5.5); // the drink came out at about 5.1 seconds
        Assert.Contains(shop.Orders, o => o.Stage == OrderStage.Ready);

        Run(shop, Shop.FinishedLingerSeconds + 0.5);
        Assert.Empty(shop.Orders);
    }

    [Fact]
    public void Customers_in_the_line_know_their_place()
    {
        var shop = Quiet();
        shop.PlaceOrder();
        shop.PlaceOrder();
        shop.PlaceOrder();

        Run(shop, 0.5);

        var queueing = shop.Orders.Where(o => o.Stage == OrderStage.Queueing).OrderBy(o => o.LinePosition).ToList();
        Assert.Equal(new[] { 0, 1 }, queueing.Select(o => o.LinePosition));
    }

    [Fact]
    public void Wait_times_stay_reasonable_under_a_rush_with_the_safety_net_and_a_crash()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 16, SafetyNet = true, Seed = 5 });

        Run(shop, 30);
        shop.CrashBarista();
        Run(shop, 90);

        Assert.Equal(0, shop.Stats.OrdersLost);
        Assert.Equal(0, shop.Stats.WastedDrinks);
        Assert.True(shop.Stats.CustomersServed >= shop.Stats.CustomersPlaced - 6);
    }
}

public class PickupWaiterTests
{
    private const double Step = 0.05;

    private static void Run(Shop shop, double seconds)
    {
        for (var i = 0; i < (int)Math.Round(seconds / Step); i++)
        {
            shop.Tick(Step);
        }
    }

    [Fact]
    public void A_customer_in_the_cashiers_line_is_not_yet_waiting_at_pickup()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 0 });
        shop.PlaceOrder();
        Run(shop, 0.5);

        Assert.Empty(shop.PickupWaiters);
    }

    [Fact]
    public void A_customer_who_has_ordered_waits_at_pickup_until_the_drink_is_ready()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 0 });
        shop.PlaceOrder();

        Run(shop, 3);
        var waiter = Assert.Single(shop.PickupWaiters);
        Assert.False(waiter.Forgotten);
        Assert.False(waiter.Impatient);

        Run(shop, 3);
        Assert.Empty(shop.PickupWaiters);
    }

    [Fact]
    public void Without_the_safety_net_a_crash_leaves_customers_waiting_for_an_order_that_no_longer_exists()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 0, PatienceSeconds = 100 });
        shop.PlaceOrder();
        Run(shop, 3);

        shop.CrashBarista();
        Run(shop, 1.3); // the lost order clears off the screen

        var waiter = Assert.Single(shop.PickupWaiters);
        Assert.True(waiter.Forgotten);
    }

    [Fact]
    public void With_the_safety_net_a_crash_does_not_make_anyone_forgotten()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 0, SafetyNet = true, PatienceSeconds = 100 });
        shop.PlaceOrder();
        Run(shop, 3);

        shop.CrashBarista();
        Run(shop, 1.3);

        Assert.All(shop.PickupWaiters, w => Assert.False(w.Forgotten));
    }

    [Fact]
    public void A_waiting_customer_becomes_impatient_after_their_patience_runs_out()
    {
        var shop = new Shop(new ShopConfig { OrdersPerMinute = 0, PatienceSeconds = 2, BrewSeconds = 30 });
        shop.PlaceOrder();

        Run(shop, 2.5); // they ask again, so they are back in the cashier's line
        Assert.Empty(shop.PickupWaiters);

        Run(shop, 2); // the second order has gone through, and they wait at pickup again
        Assert.True(Assert.Single(shop.PickupWaiters).Impatient);
    }
}

internal static class ShopTestExtensions
{
    public static int Events(this Shop shop, string fragment) =>
        shop.RecentEvents(100).Count(e => e.Message.Contains(fragment));
}
