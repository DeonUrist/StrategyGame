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
- `WorldModels.cs`: runtime game objects such as tiles, saved biome regions, worldgen settings, armies, agents, cities, factions, and log entries.
- `HexCoord.cs`: axial hex coordinate math, neighbors, and distance.
- `MapGenerator.cs`: top-level deterministic 32x32 sandbox generation flow and base tile creation.
- `MapGenerator.Geometry.cs`: axial/row-column conversion, coastline shape, and map-edge helpers.
- `MapGenerator.Regions.cs`: saved biome region creation, moisture/water-retention assignment, north/south temperature bands, and vegetation rolls.
- `MapGenerator.TerrainFeatures.cs`: inland lakes, region-edge hill/mountain/peak generation, elevation drying, volcanoes, and resource placement.
- `MapGenerator.StartingPieces.cs`: faction starting cities, army stacks, agents, and start-location fallback.
- `TerrainResolver.cs`: public terrain resolution entry points for water and saved region biomes.
- `TerrainResolver.Biomes.cs`: moisture/water-retention base biome table and base-biome/temperature/vegetation final biome table.
- `TerrainResolver.Stats.cs`: water terrain names, terrain colors, movement costs, and defense modifiers.
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
- `MainGame.Ui.cs`: menu and HUD control construction.
- `MainGame.Flow.cs`: screen switching, new/load/save game flow, end-turn handling, and leader detach command.
- `MainGame.InfoPanel.cs`: selected-unit, tile, and log text formatting for the HUD.
- `MainGame.Drawing.cs`: draw-pass order plus map, city, army, and agent drawing.
- `MainGame.Drawing.Terrain.cs`: terrain ornaments such as trees, hills, mountains, volcanoes, and resource markers.
- `MainGame.HexMath.cs`: convert between hex coordinates and Godot pixel positions.

## Data

Authored catalogs in `data/` are:

- `units.json`: militia, spearmen, scout, and captain definitions.
- `buildings.json`: Campsite -> Shelter -> Encampment -> Village Square -> Town Square -> City Square upgrade chain.
- `factions.json`: one player faction and two AI factions.
- `events.json`: weighted AI actions: defend city, claim resource, attack enemy, scout, and upgrade city.

There are no `terrain.json`, `resources.json`, or `features.json` catalogs in the current design. Land terrain is resolved from saved biome regions; resources and special features are defined and placed from code because their behavior is tied to generated map properties.

## Tests

The test harness in `tests/StrategyGame.Tests` compiles the core game logic directly. It covers hex math, database loading, terrain resolution, deterministic map generation, region/elevation constraints, movement, agent attach/detach, city upgrades, combat, AI validity, save/load round trips, and deterministic loaded AI turns.
