# Coffee Shop Rush

[![Test and deploy](https://github.com/hamzamehmood46/coffee-shop-rush/actions/workflows/pages.yml/badge.svg)](https://github.com/hamzamehmood46/coffee-shop-rush/actions/workflows/pages.yml)

**Live: [hamzamehmood46.github.io/coffee-shop-rush](https://hamzamehmood46.github.io/coffee-shop-rush/)**

An animated coffee shop that explains, without a single line of jargon, how real software keeps your orders safe when something crashes.

Crash the barista and watch orders vanish. Then switch on the paper-ticket safety net and crash it again. Nothing is lost.

It is built for people who do not write code: a story, one big red button, and a scoreboard. Underneath, it is a small, honest model of the patterns that protect payments, bookings and orders in production systems.

## The idea

| In the cafe | In real software |
|---|---|
| The cashier takes an order | A request arrives from your phone or browser |
| Shouting the order across the shop | A message sent without a receipt. If nobody is listening, it is gone |
| The barista | A background worker. Workers crash and restart all the time |
| The paper-ticket rail | A durable queue: the **transactional outbox** pattern. Write it down first, then do the work |
| The barista skipping tickets already made | An **idempotent consumer**: doing the same thing twice has the same effect as doing it once |
| The impatient customer who asks again | A client that retries. Retries are normal, so the system must expect them |

With the safety net **off**, a crash makes the barista forget everything they were holding, and customers who ask again can get a second drink nobody needed.
With it **on**, every order is pinned to a ticket before anything else happens, the barista resumes from the rail after a restart, and a repeat order is recognised and merged into the original.

## How it is built

```
src/CoffeeShop.Engine   plain C# simulation: no UI, no clock, no randomness you can't replay
src/CoffeeShop.Web      Blazor WebAssembly front end: SVG scene, CSS animation, no server
tests/                  xUnit tests for the engine
```

- **The engine is deterministic.** Time only moves when `Shop.Tick(seconds)` is called, and arrivals use a seeded random generator, so any run can be replayed exactly. That is what makes the behaviour testable.
- **The page is static.** Blazor WebAssembly runs the same C# engine inside the visitor's browser, so it hosts for free on GitHub Pages with no backend.
- **Everything animated is accessible.** The scene has a text description, the scoreboard and event feed are live regions, and `prefers-reduced-motion` switches the animation off.
- **It works on a phone,** with sticky controls so the Crash button is always within reach.

## Tests

```bash
dotnet test tests/CoffeeShop.Engine.Tests
```

The 23 tests cover the behaviour a visitor actually sees, including:

- a crash without the safety net loses the drink being made and everything the barista was remembering;
- a crash with the safety net loses nothing, and the drink is made after the restart;
- tickets pinned before the safety net was switched off stay safe;
- an impatient customer gets a wasted second drink without the safety net, and does not with it;
- a customer asks again only once;
- the same seed replays exactly.

## Run it locally

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/CoffeeShop.Web
```

## Deploying

Every push to `main` runs the tests and, if they pass, publishes the site to GitHub Pages (see `.github/workflows/pages.yml`).

## Related projects

The real, production-style versions of these patterns:

- [Transactional Outbox and Idempotent Inbox](https://github.com/hamzamehmood46/outbox-pattern-dotnet)
- [OrderFlow: idempotent order processing with retries and dead-letter handling](https://github.com/hamzamehmood46/orderflow-dotnet)

## License

MIT
