# Code Guide

This project is split so each file has one clear job.

## Project Shape

- `StrategyGame.csproj`: Godot 4.6.1 C# project targeting `net10.0`; excludes test source files from the game assembly.
- `project.godot`: project metadata and `res://scenes/Main.tscn` as the main scene.
- `data/`: JSON catalogs for authored definitions. The current catalogs are `units.json`, `buildings.json`, `factions.json`, and `events.json`.

## Core Game Logic

Core files live in `src/Game/Core`. They do not use Godot APIs, so they are easier to test and reason about.

- `Definitions.cs`: shared game data shapes and terrain enums.
- `GameDatabase.cs`: loads unit, building, faction, and event catalogs from `data/`, and defines resources in code.
- `GameState.cs`: top-level runtime state, faction helpers, and game log helper.
- `WorldModels.cs`: runtime game objects such as tiles, armies, agents, cities, factions, and log entries.
- `HexCoord.cs`: axial hex coordinate math, neighbors, and distance.
- `MapGenerator.cs`: creates the deterministic 32x32 sandbox island, inland lakes, mountain chains, vegetation clusters, resources, cities, armies, and agents.
- `TerrainResolver.cs`: turns climate, rainfall, elevation, vegetation, and special features into final tile names, colors, movement, and defense values.
- `GameRules.Movement.cs`: movement cost and pathfinding range.
- `GameRules.Stacks.cs`: army stack movement and combat entry.
- `GameRules.Agents.cs`: agent movement, joining armies, and detaching leaders.
- `GameRules.Cities.cs`: city building-chain upgrades.
- `GameRules.Turns.cs`: faction turn order and movement reset.
- `CombatResolver.cs`: simple strategic autoresolve combat.
- `FactionDirector.cs`: the high-level AI turn flow.
- `FactionDirector.Weights.cs`: how AI chooses a broad action.
- `FactionDirector.Targets.cs`: how AI chooses map targets and city upgrades.
- `GameStateSerializer.cs`: versioned save/load snapshots.

## Godot Presentation

Presentation files live in `src/Game/Presentation`. They connect player input and drawing to the core game logic.

- `MainGame.cs`: scene startup, database load, sandbox creation, shared state fields.
- `MainGame.Input.cs`: mouse clicks, camera zoom, camera pan, selection, and move commands.
- `MainGame.Ui.cs`: buttons, info panel, save/load, end-turn flow.
- `MainGame.Drawing.cs`: map, city, army, and agent drawing.
- `MainGame.HexMath.cs`: convert between hex coordinates and Godot pixel positions.

## Data

Authored catalogs in `data/` are:

- `units.json`: militia, spearmen, scout, and captain definitions.
- `buildings.json`: Shelter -> Camp -> Townsquare -> Town Center upgrade chain.
- `factions.json`: one player faction and two AI factions.
- `events.json`: weighted AI actions: defend city, claim resource, attack enemy, scout, and upgrade city.

There are no `terrain.json`, `resources.json`, or `features.json` catalogs in the current design. Terrain is resolved in `TerrainResolver.cs`; resources and special features are defined and placed from code because their behavior is tied to generated map properties.

## Tests

The test harness in `tests/StrategyGame.Tests` compiles the core game logic directly. It covers hex math, database loading, terrain resolution, deterministic map generation, movement, agent attach/detach, city upgrades, combat, AI validity, save/load round trips, and deterministic loaded AI turns.
