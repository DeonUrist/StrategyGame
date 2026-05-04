# Foundation Notes

## Goal

Build the base for a Godot 2D turn-based strategy game that can grow toward hex terrain, resources, multi-unit stacking, cities with building chains, factions, AI directors, armies, and hero/agent units.

## Implemented Foundation

- Godot 4.6 Mono project using C#.
- JSON-authored catalogs for terrain, features, resources, units, buildings, factions, and weighted AI events.
- Seeded generated hex map using axial coordinates.
- Ground and water terrain, terrain movement costs, features, resources, cities, armies, and agents.
- Multi-unit army stacks per tile.
- Agent units that can move independently, join a friendly army as leader, and detach.
- City state with a building chain: Shelter -> Camp -> Townsquare -> Town Center.
- Turn manager behavior through `GameRules.AdvanceTurn`.
- Player controls one faction with an army and agent.
- AI factions use `FactionDirector` to choose weighted actions from world state and `data/events.json`.
- Strategic auto-resolve combat.
- Immediate-mode prototype visuals and simple UI in `MainGame`.
- Console smoke tests for core systems.

## Architecture

- `src/Game/Core`: pure simulation models and rules. Keep Godot-specific APIs out of this layer.
- `src/Game/Presentation`: Godot scene scripts, input, drawing, UI, and camera.
- `data`: JSON game definitions. Add new content here before hardcoding new game data.
- `tests/StrategyGame.Tests`: simple executable test harness that compiles core files directly.

## Deliberate Placeholders

- Map generation is simple and deterministic, not final worldgen.
- Visuals are drawn shapes and text, not tileset art.
- Combat is one-step auto-resolve with simple strength math.
- City economy, production, ownership pressure, diplomacy, fog of war, save/load, and tactical battles are not implemented yet.
- AI is a weighted director prototype, not a full planner.

## Recommended Next Milestones

1. Add save/load for `GameState` and deterministic replay of turns.
2. Split presentation into map view, selection controller, and UI panel classes.
3. Add explicit action objects/results so player and AI use the same command pipeline.
4. Add city production and building upgrade actions with costs.
5. Add fog of war and faction knowledge state.
6. Replace placeholder drawing with a tileset or sprite layer after rules stabilize.
