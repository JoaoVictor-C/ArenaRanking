# ArenaRanking

A competitive ranking system for **League of Legends Arena** — the 8-team mode Riot ships without a visible ladder. This builds one: it polls the Riot API for players' matches, scores each placement with an Elo-style rating, and serves a ranked leaderboard.

---

## The problem

Arena has no public rating. You finish 1st through 8th and the game records the match, but nothing accumulates across matches into a number that says how good you are. This project supplies that number — call it **PDL** — and keeps it current without anyone pressing refresh.

## The rating

PDL starts at **1000** and moves after every match by placement, scaled by a confidence factor:

```
Δ = factor × placementMultiplier[place]
```

| Place | 1st | 2nd | 3rd | 4th | 5th | 6th | 7th | 8th |
|---|---|---|---|---|---|---|---|---|
| Multiplier | +1.3 | +1.1 | +0.8 | +0.6 | −0.4 | −0.6 | −1.0 | −1.7 |

Two properties are deliberate:

- **Top half gains, bottom half loses, and 8th costs more than 1st pays** (−1.7 against +1.3). Arena is 8-way, so a symmetric curve would inflate the whole ladder over time; the asymmetry keeps the mean anchored.
- **New players move faster.** The factor is **80** for a new account and settles to a base of **50** after `MinMatchesStable` (10) matches, capped at **140**. A new player reaches roughly their true rating in a handful of games instead of grinding there; an established one stops thrashing on a single bad night. This is the same idea as a chess K-factor, tuned for an 8-way mode.

All of it lives in [`Configs/PdlCalculationSettings.cs`](api/Configs/PdlCalculationSettings.cs) — the curve is configuration, not constants buried in a loop, because tuning a ladder means changing these numbers repeatedly.

## Architecture

Two independent .NET 8 services against one MongoDB, split by workload rather than by layer:

```
api/              read path  — serves rankings and player data, cached
pdl_update_api/   write path — background workers polling Riot, recalculating PDL
```

The read path must stay fast and must not stall behind a rate-limited third-party API; the write path is slow, periodic, and allowed to fail and retry. They scale differently, so they deploy separately.

### `api/` — read path

- `PlayerController`, `RiotApiController` — leaderboard, player lookup, match history
- `RankingCacheService` + `RankingCacheUpdateHostedService` — the leaderboard is recomputed on a timer into a cache rather than sorted per request
- Repository factory over MongoDB, JWT-protected admin routes

### `pdl_update_api/` — write path

Three `IHostedService` background workers:

| Worker | Job |
|---|---|
| `PdlUpdateHostedService` | Poll tracked players for new Arena matches, apply the rating change |
| `RiotIdUpdateHostedService` | Riot IDs are mutable — players rename. Reconcile `gameName#tagLine` against the immutable `puuid` |
| `DatabaseCloneService` / `DatabaseMigrationService` | Snapshot and migrate between database states |

Plus `PdlRecalculationService`, which replays a player's whole match history from scratch. When the curve changes, existing ratings computed under the old one are wrong — so recalculation from source is a first-class operation, not a migration script.

`RiotApiKeyManager` centralises key handling so the Riot rate limit is managed in one place rather than at every call site.

## Stack

.NET 8 · ASP.NET Core · MongoDB · Riot Games API · JWT Bearer · Docker · `IHostedService` background workers

## Running it

Both services are containerised:

```bash
docker build -t arena-api ./api
docker build -t arena-pdl ./pdl_update_api
```

Configuration comes from environment variables (see `Configs/EnvironmentConfigProvider.cs` and `DotEnvLoader.cs`) — a Riot API key and a MongoDB connection string are required. Locally:

```bash
cd api && dotnet run
```

## Related

[ArenaRank](https://github.com/JoaoVictor-C/ArenaRank) is the earlier, static prototype of the same idea, kept for reference.

## Status

Personal project, built 2025. Riot API keys for personal use expire every 24 hours, so a public deployment needs a production key.
