# StrategyGame

Godot 4.6 Mono prototype for a 2D turn-based strategy game with a generated hex map, factions, armies, agents, cities, resources, and a basic AI turn loop.

## Run

Open from a new PowerShell so the Godot PATH entry is available:

```powershell
cd V:\Repos\StrategyGame
godot --path .
```

Press `F5` in the editor. The main scene is `res://scenes/Main.tscn`.

## Controls

- Left click a player army or agent to select it.
- Left click a highlighted hex to move.
- Mouse wheel zooms the camera.
- Middle or right mouse drag pans.
- `End Turn` advances through AI factions back to the player.
- `Save Game` writes the current state to Godot's `user://strategy-save.json`.
- `Load Game` restores that saved state.
- `Detach Leader` removes a joined agent from the selected army.

## Verify

```powershell
dotnet build
dotnet run --project tests\StrategyGame.Tests\StrategyGame.Tests.csproj
godot --headless --path V:\Repos\StrategyGame --quit
```

## Current Prototype

- The map grid and tokens are drawn in code in `src/Game/Presentation/MainGame.cs`.
- Game content is authored in JSON under `data/`.
- Pure simulation code lives under `src/Game/Core/`.
- Save/load snapshots are handled in core and can be replayed deterministically for AI turns.
- The initial test harness lives under `tests/StrategyGame.Tests/`.
