# Biome Logic

Simple reference for the current terrain resolver.

## Flow

`Temperature + moisture -> base biome -> final terrain`

- `Temperature` is latitude-based and affected by climate bias.
- `Moisture` is region noise shifted by the world wetness slider.
- Paired terrain cells use explicit worldgen bias sliders.
- Elevation is separate: hills, mountains, and peaks affect movement, defense, and visuals, but do not change moisture or terrain identity.

## Terrain Table

| Temperature \ Moisture | `Dry` | `Normal` | `Wet` |
| --- | --- | --- | --- |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |
| `Subarctic` | `Tundra` | `Tundra` | `Taiga` |
| `Temperate` | `Grassland` / `Shrubland` | `Conifer Forest` / `Broadleaf Forest` | `Swamp` |
| `Tropical` | `Desert` / `Badlands` | `Grassland` | `Jungle` |

## Variant Sliders

| Slider | `0` | `100` |
| --- | --- | --- |
| `Grassland 0 - 100 Shrubland` | Always `Grassland` | Always `Shrubland` |
| `Desert 0 - 100 Badlands` | Always `Desert` | Always `Badlands` |
| `Conifer 0 - 100 Broadleaf` | Always `Conifer Forest` | Always `Broadleaf Forest` |

Each eligible region rolls deterministically from the world seed and region identity.

## Movement Cost

Flat land starts at `1.0`.

| Rule | Cost |
| --- | --- |
| Bad terrain: `Desert`, `Tundra`, `Badlands`, `Swamp` | `+0.5` |
| Forested terrain: `Taiga`, `Conifer Forest`, `Broadleaf Forest`, `Jungle`, `Swamp` | `+0.5` |
| Land ice: `Ice Sheet` | `+1.0` |
| `Hills` | `+0.5` |
| `Mountains`, `Peaks` | `+1.0` |
| `volcano` feature | `+1.0` |

`Swamp` intentionally receives both bad-terrain and forested-terrain surcharges.
