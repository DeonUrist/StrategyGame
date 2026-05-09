# Foundation Notes

## Goal

Build the base for a Godot 2D turn-based strategy game that can grow toward hex terrain, resources, multi-unit stacking, cities with building chains, factions, AI directors, armies, and hero/agent units.

## Implemented Foundation

- Godot 4.6.1 Mono project using C# and `net10.0`.
- JSON-authored catalogs for units, buildings, factions, and weighted AI events.
- Code-defined terrain resolver for saved biome regions, elevation, coastline, and special feature combinations.
- Seeded 32x32 generated hex island map using axial coordinates.
- Ocean border, small inland lakes, region-edge hills, mountains, peaks, volcano features, saved biome regions, north/south temperature bands, moisture, and explicit terrain-variant rolls.
- Ground and water terrain, terrain movement costs, terrain defense modifiers, resources, cities, armies, and agents.
- Saved worldgen settings for wetness, terrain variants, elevation variance, and climate bias.
- Multi-unit army stacks per tile.
- Agent units that can move independently, join a friendly army as leader, and detach.
- City state with a building chain: Campsite -> Shelter -> Encampment -> Village Square -> Town Square -> City Square.
- City building upgrades replace the previous chain level instead of adding every level beside it.
- Turn manager behavior through `GameRules.AdvanceTurn`.
- Player controls one faction with an army and agent.
- AI factions use `FactionDirector` to choose weighted actions from world state and `data/events.json`.
- AI faction turns use deterministic turn/faction seeds so loaded states can replay the same director result.
- Strategic auto-resolve combat.
- Versioned JSON save/load snapshots for `GameState`.
- Immediate-mode prototype visuals, a main menu, save/load menu entry, and in-game HUD in `MainGame`.
- Console smoke tests for core systems.

## Architecture

- `src/Game/Core`: pure simulation models and rules. Keep Godot-specific APIs out of this layer.
- `src/Game/Presentation`: Godot scene scripts, input, drawing, UI, and camera.
- `data`: JSON game definitions for units, buildings, factions, and events. Terrain regions, resources, and map features currently live in code because they are generated from composable map properties.
- `tests/StrategyGame.Tests`: simple executable test harness that compiles core files directly.

## Deliberate Placeholders

- Map generation is simple and deterministic, not final worldgen.
- Visuals are rendered with sprite layers plus lightweight grid and selection overlays.
- Combat is one-step auto-resolve with simple strength math.
- City economy, production costs, ownership pressure, diplomacy, fog of war, and tactical battles are not implemented yet.
- AI is a weighted director prototype, not a full planner.
- Save/load exists for the core `GameState`, but there is only one fixed save slot exposed through the prototype UI.

## Recommended Next Milestones

1. Split presentation into map view, selection controller, and UI panel classes.
2. Add explicit action objects/results so player and AI use the same command pipeline.
3. Add city production and building upgrade costs.
4. Add fog of war and faction knowledge state.
5. Split presentation into smaller map-view, selection-controller, and HUD classes as the sprite layers grow.
