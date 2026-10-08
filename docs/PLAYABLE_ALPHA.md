# Playable Alpha: First Settlement

This branch preserves every existing game system. The immediate goal is to make a new player's first session easy to run, understand, test, and measure before adding more systems.

## The first-session promise

A new tester should be able to complete this loop in 10 to 15 minutes without another human player:

1. Name a commander and enter the world.
2. Place the guided home-base buildings.
3. Collect the first production.
4. Claim one nearby parcel.
5. Build something suited to that parcel.
6. Trade or deliver what the settlement produces.
7. Complete one short battle against a bot.
8. Understand what became stronger and what to do next.

Politics, alliances, the Sentinel, the Ashlands, and long-form progression remain in the game. They are later goals, not requirements for evaluating the first session.

## Playtest questions

Every test should answer these questions:

- Can the tester begin without help?
- Do they understand how home-base buildings differ from claimed-land buildings?
- Can they find the next action without being told verbally?
- Do production, terrain suitability, and collection feel connected?
- Does the first trade or delivery have a clear benefit?
- Is the first battle understandable and short enough to retry?
- Where does the tester hesitate, become confused, or stop?

## Definition of done

- The full loop can be completed from a fresh local database.
- Waiting timers used by the loop are compressed in the playtest profile.
- No step requires another human player.
- A smoke test verifies guest creation and authenticated state retrieval.
- Automated builds reject code that no longer compiles.
- Gameplay changes are made in small commits and keep the original `main` branch untouched.

## Current playtest profile

Run `scripts/dev.ps1 -Playtest` to enable a local-only profile with:

- more starting cash, fuel, and gold;
- accelerated research;
- shorter election, law, and Sentinel timing;
- a weaker Sentinel suitable for small test groups;
- developer practice tools;
- two bots per open sector.

These settings are process-local. They do not alter production defaults or the deployed database.
