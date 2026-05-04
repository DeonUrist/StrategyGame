# Code Guide

This project is split so each file has one clear job.

## Core Game Logic

Core files live in `src/Game/Core`. They do not use Godot APIs, so they are easier to test and reason about.

- `Definitions.cs`: data shapes loaded from JSON catalogs.
- `GameDatabase.cs`: loads terrain, unit, faction, building, resource, and event data from `data/`.
- `WorldModels.cs`: runtime game objects such as tiles, armies, agents, cities, factions, and log entries.
- `HexCoord.cs`: axial hex coordinate math, neighbors, and distance.
- `MapGenerator.cs`: creates the deterministic sandbox map and starting faction pieces.
- `GameRules.Movement.cs`: movement cost and pathfinding range.
- `GameRules.Stacks.cs`: army stack movement and combat entry.
- `GameRules.Agents.cs`: agent movement, joining armies, and detaching leaders.
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

Game content lives in `data/`. Add or tune content there before hardcoding new values in C#.

## Tests

The test harness in `tests/StrategyGame.Tests` compiles the core game logic directly. Use it to check that rules still work after changes.
