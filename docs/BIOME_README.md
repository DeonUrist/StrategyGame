# Biome Logic

Simple reference for the current biome resolver in code.

## Flow

`Moisture + retention -> base biome -> vegetation clamp -> final biome`

- `~` Elevation drying can lower tile moisture and vegetation on hills, mountains, and peaks.
- `*` Temperature can override or flavor the base biome result.
- `->` Final names are gameplay-facing terrain labels.

## Base Biome Table

| Moisture \\ Retention | `Draining` | `Normal` | `Holding` |
| --- | --- | --- | --- |
| `Dry` | `Desert` | `Wasteland` | `Badlands` |
| `Normal` | `Dryland` | `Plain` | `Floodplain` |
| `Wet` | `Barrens` | `Wetland` | `Swamp` |

### Temperature adjustment

| Rule | Result |
| --- | --- |
| `Desert` + `Temperate` | `Dryland` |
| `Desert` + `Subarctic` | `Dryland` |
| everything else | unchanged |

## Max Vegetation

| Base biome | Max vegetation |
| --- | --- |
| `Desert` | `None` |
| `Wasteland` | `None` |
| `Badlands` | `Sparse` |
| `Dryland` | `Sparse` |
| `Plain` | `Lush` |
| `Floodplain` | `Lush` |
| `Barrens` | `Sparse` |
| `Wetland` | `Lush` |
| `Swamp` | `Lush` |

Special rule: `Arctic` always clamps vegetation to `None`.

## Final Biome Table

### `Desert`

| Temperature | `None` / `Sparse` / `Lush` |
| --- | --- |
| `Tropical` | `Desert` |
| `Subtropical` | `Desert` |
| `Temperate` | `Desert` |
| `Subarctic` | `Ice Sheet` |
| `Arctic` | `Ice Sheet` |

### `Wasteland`

| Temperature | Final biome |
| --- | --- |
| `Tropical` | `Wasteland` |
| `Subtropical` | `Wasteland` |
| `Temperate` | `Steppe` |
| `Subarctic` | `Tundra` |
| `Arctic` | `Ice Sheet` |

### `Badlands`

| Temperature | Final biome |
| --- | --- |
| `Tropical` | `Badlands` |
| `Subtropical` | `Badlands` |
| `Temperate` | `Badlands` |
| `Subarctic` | `Badlands` |
| `Arctic` | `Ice Sheet` |

### `Dryland`

| Temperature | `None` | `Sparse` | `Lush`* |
| --- | --- | --- | --- |
| `Tropical` | `Dryland` | `Savanna` | `Dryland` |
| `Subtropical` | `Steppe` | `Prairie` | `Steppe` |
| `Temperate` | `Grassland` | `Shrubland` | `Grassland` |
| `Subarctic` | `Tundra` | `Tundra` | `Tundra` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

### `Plain`

| Temperature | `None` | `Sparse` | `Lush` |
| --- | --- | --- | --- |
| `Tropical` | `Plain` | `Savanna` | `Jungle` |
| `Subtropical` | `Steppe` | `Prairie` | `Rainforest` |
| `Temperate` | `Grassland` | `Shrubland` | `Forest` |
| `Subarctic` | `Tundra` | `Tundra` | `Taiga` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

### `Floodplain`

| Temperature | `None` | `Sparse` | `Lush` |
| --- | --- | --- | --- |
| `Tropical` | `Floodplain` | `Savanna` | `Jungle` |
| `Subtropical` | `Floodplain` | `Prairie` | `Rainforest` |
| `Temperate` | `Grassland` | `Grassland` | `Forest` |
| `Subarctic` | `Tundra` | `Tundra` | `Taiga` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

### `Barrens`

| Temperature | `None` | `Sparse` | `Lush`* |
| --- | --- | --- | --- |
| `Tropical` | `Wasteland` | `Shrubland` | `Wasteland` |
| `Subtropical` | `Wasteland` | `Shrubland` | `Wasteland` |
| `Temperate` | `Wasteland` | `Shrubland` | `Wasteland` |
| `Subarctic` | `Tundra` | `Tundra` | `Tundra` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

### `Wetland`

| Temperature | `None` | `Sparse` | `Lush` |
| --- | --- | --- | --- |
| `Tropical` | `Wetland` | `Wetland` | `Jungle` |
| `Subtropical` | `Wetland` | `Wetland` | `Rainforest` |
| `Temperate` | `Wetland` | `Wetland` | `Swamp` |
| `Subarctic` | `Tundra` | `Tundra` | `Taiga` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

### `Swamp`

| Temperature | `None` | `Sparse` | `Lush` |
| --- | --- | --- | --- |
| `Tropical` | `Swamp` | `Swamp` | `Jungle` |
| `Subtropical` | `Swamp` | `Swamp` | `Rainforest` |
| `Temperate` | `Swamp` | `Swamp` | `Forest` |
| `Subarctic` | `Wetland` | `Wetland` | `Taiga` |
| `Arctic` | `Ice Sheet` | `Ice Sheet` | `Ice Sheet` |

`*` These cells are unreachable after vegetation clamping, but they document the effective resolved output if they are ever passed in.

## Elevation Effect

| Elevation | Moisture change | Vegetation change |
| --- | --- | --- |
| `Hills` | `-1` | `-0..1` |
| `Mountains` | `-1..2` | `-0..2` |
| `Peaks` | `-1..2` | `-1..2` |

Meaning:

- Higher terrain keeps the same region and temperature.
- Only tile-local moisture and vegetation are dried.
- Temperate elevated floodplains now fall to `Grassland`, not `Bog`.
