# StrategyGame

Godot 4.6.1 Mono prototype for a 2D turn-based strategy game with a generated 32x32 hex island map, saved biome regions, resources, factions, unit groups, agent units, cities, save/load, and a deterministic AI turn loop.

## Run

Open from a new PowerShell so the Godot PATH entry is available:

```powershell
cd V:\Repos\StrategyGame
godot --path .
```

Press `F5` in the editor. The main scene is `res://scenes/Main.tscn`.

The game starts at a simple main menu. Use `New Game` to generate a fresh sandbox world, or `Load Game` to restore `user://strategy-save.json` when a save exists.

## Controls

- Left click a player group to select it.
- Left click a highlighted hex to move the selected group.
- Mouse wheel zooms the camera.
- Middle or right mouse drag pans.
- `End Turn` advances through every AI faction and returns control to the player.
- `Save` writes the current state to Godot's `user://strategy-save.json`.
- `Save and Exit` saves, clears the active game, and returns to the main menu.
- Group actions can deploy units from a settlement garrison, station units, transfer units between colocated groups, and split units into a new group.

## Verify

```powershell
dotnet build
dotnet run --project tests\StrategyGame.Tests\StrategyGame.Tests.csproj
godot --headless --path V:\Repos\StrategyGame --quit
```

## Current Prototype

- Pure simulation code lives under `src/Game/Core/` and has no Godot API dependency.
- Godot presentation code lives under `src/Game/Presentation/` and is split into startup, input, HUD/menu, drawing, and hex math partials.
- JSON catalogs in `data/` currently define units, buildings, factions, and weighted AI events.
- Terrain and resources are code-defined: land terrain is resolved from saved biome regions with temperature, moisture, and explicit terrain-variant sliders; resources are placed by map-generation rules.
- Biome resolution is documented in `docs/BIOME_README.md`.
- The sandbox generator creates an island with ocean borders, small inland lakes, biome regions, region-edge hills and mountains with a few peaks, starting cities, and starting units in settlement garrisons.
- Save/load snapshots are handled in core and preserve deterministic AI replay.
- The console test harness lives under `tests/StrategyGame.Tests/`.
