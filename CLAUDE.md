# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Build
dotnet build

# Run all tests
dotnet run --project tests\StrategyGame.Tests\StrategyGame.Tests.csproj

# Headless engine verification (no window)
godot --headless --path V:\Repos\StrategyGame --quit

# Launch game
godot --path .
```

There is no solution file — `dotnet build` from the repo root builds the main project. The test project links Core sources directly (see below).

## Architecture

### Core / Presentation split

`src/Game/Core/` contains **pure C# with zero Godot API imports** — all game logic, map generation, rules, AI, and serialization. It is compiled into both the Godot game and the test harness.

`src/Game/Presentation/` contains Godot-dependent code (imports `Godot`). It reads Core state for rendering and calls Core methods to apply changes. Never put Godot types in Core.

### Data flow

```
Godot input event
  → MainGame.Input._UnhandledInput()
  → GameRules.TryMoveGroup() / group action rules (Core)
  → GameState mutated in place
  → MainGame drawing/panel update reads _state
```

### Key Core types

| Type | Role |
|------|------|
| `HexCoord(Q, R)` | Axial coordinate record; `Neighbors()` and `Distance()` use cube coords |
| `HexMap` | `Dictionary<HexCoord, HexTile>` with neighbor queries |
| `HexTile` | Per-cell data: elevation, moisture, region ID, resource, city, deployed group IDs |
| `RegionState` | Biome identity (base biome, temperature, moisture) for a set of tiles |
| `GroupState` | Movable or stationed set of units; settlements start with one garrison group |
| `UnitInstance` | One concrete unit in a group, with stable identity for split/transfer |
| `CityState` | Settlement: coord, generated name, owner, TownCenter level |
| `GameState` | Central mutable container holding all of the above plus `GameDatabase` and turn tracking |
| `GameDatabase` | Loads `data/*.json` catalogs (units, buildings, factions, events); resources defined in code |

### Important design decisions

- **Deterministic seeded generation**: `MapGenerator` and `FactionDirector` use a seeded `Random` so saves replay AI turns identically. Do not introduce `System.Random` without threading it through the seed.
- **Terrain resolved from region properties**: Final biome names come from `TerrainResolver` reading `RegionState` fields — they are never stored per-tile. Changing resolution logic affects all tiles in a region.
- **City upgrades replace**: Each upgrade removes the previous building (not additive). The `GameRules.Cities` partial enforces this chain.
- **Agents are units**: a solo agent is a one-unit `GroupState`; transfer/split handles combining or separating agent units.
- **Stationed groups leave the map**: stationed groups are removed from `HexTile.GroupIds` and tracked through `CityState.StationedGroupIds` until deployed.
- **Save version gating**: `GameStateSerializer` rejects saves with a version number other than the current constant. Bump the version whenever the snapshot shape changes.

### Partial-class layout

Large subsystems are split into partial classes:

- `MapGenerator` + 4 partials (base tiles, regions, seas/lakes, elevation, starting pieces)
- `TerrainResolver` + 2 partials (movement costs, biome name resolution)
- `GameRules` split into partials: Movement, Groups, Cities, Turns
- `FactionDirector` + 2 partials (weighted event selection, action execution)
- `MainGame` split into: Input, Ui, Flow, InfoPanel, Layers, HexMath

### Test harness

`tests/StrategyGame.Tests/StrategyGame.Tests.csproj` compiles `src/Game/Core/**/*.cs` directly (no project reference) so it runs without Godot. `Program.cs` contains hand-written assertion tests covering hex math, generation determinism, movement, group split/transfer, garrison station/deploy, agents as units, cities, combat, save/load round-trips, and AI replay. Add new tests as plain methods following the existing pattern.
