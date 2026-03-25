#!/usr/bin/env python3
"""
Generate hub and player farm TMX maps for the MultiFarm mod.

Three hub maps, each 60×40:
  MultiFarm_Hub_East   — between Farm (west) and BusStop (east); N/S exits to North/South hubs
  MultiFarm_Hub_North  — between Backwoods (north) and East Hub (south)
  MultiFarm_Hub_South  — between East Hub (north) and Forest (south)

Each hub contains all 8 player portal slots at the same positions:
  Slots 1-4: y=8,  x = 8, 20, 32, 44
  Slots 5-8: y=28, x = 8, 20, 32, 44

Hub entry coordinates (used in FarmHubManager.cs):
  East Hub from Farm/west:      ( 2, 20)
  East Hub from BusStop/east:   (57, 20)
  East Hub from North Hub:      (30,  2)
  East Hub from South Hub:      (30, 37)
  North Hub from Backwoods:     (30,  2)
  North Hub from East Hub:      (30, 37)
  South Hub from East Hub:      (30,  2)
  South Hub from Forest:        (30, 37)

All tile GIDs use firstgid=16 for spring_outdoorsTileSheet (same as Backwoods.tmx).
"""

import os, random

random.seed(42)   # deterministic output

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "MultiFarm", "assets", "maps")
os.makedirs(OUT_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Back-layer tile constants (firstgid=16 for outdoor sheet)
# ---------------------------------------------------------------------------
CLIFF    = 396   # solid cliff fill
DARK     = 367   # dark outside-cliff fill
CLIFF_L  = 373   # cliff→grass left-edge
GRASS_A  = 421   # grass near cliff
GRASS_B  = 422   # grass B
GRASS_C  = 423   # grass C  (portal patch)
GRASS    = 191   # main interior grass
GRASS_S  = 166   # spawnable grass
G_DARK   = 320   # darker grass / shadow
G_SHADE  = 316   # shadow grass near cliff
ROAD     = 169   # dirt road main tile
ROAD_N   = 392   # road north edge (grass→road)
ROAD_S   = 342   # road south edge (road→grass)
EMPTY    = 0

# ---------------------------------------------------------------------------
# Front-layer tile constants
# ---------------------------------------------------------------------------
CF = [962, 962, 956, 956, 957, 982]   # cliff face top row
CF1= [962, 956, 956, 957, 982, 1008]
CF2= [962, 956, 957, 982, 1008,    0]
CF3= [956, 981, 982, 1008,   0,    0]
CF4= [957, 982, 1008,   0,   0,    0]
CF5= [982, 1008,   0,   0,   0,    0]
CF6= [1008,  0,   0,   0,   0,    0]
CLIFF_FACE_ROWS = [CF, CF1, CF2, CF3, CF4, CF5, CF6]   # 7 rows

# Tree (3-wide × 4-tall): call place_tree(front, x, y) to place
TREE_ROWS = [[26,27,28],[51,52,53],[76,77,78],[101,102,103]]

# ---------------------------------------------------------------------------
# Grid helpers
# ---------------------------------------------------------------------------
def make_grid(w, h, default=EMPTY):
    return [[default]*w for _ in range(h)]

def set_tile(g, x, y, t):
    if 0 <= x < len(g[0]) and 0 <= y < len(g):
        g[y][x] = t

def set_row(g, y, t, x0=0, x1=None):
    if x1 is None: x1 = len(g[0])
    for x in range(x0, x1): set_tile(g, x, y, t)

def set_col(g, x, t, y0=0, y1=None):
    if y1 is None: y1 = len(g)
    for y in range(y0, y1): set_tile(g, x, y, t)

def set_rect(g, x0, y0, x1, y1, t):
    for y in range(y0, y1):
        for x in range(x0, x1): set_tile(g, x, y, t)

def place_tree(g, x, y):
    """Place a 3×4 tree at (x,y) top-left in the Front layer."""
    for dy, row in enumerate(TREE_ROWS):
        for dx, t in enumerate(row):
            set_tile(g, x+dx, y+dy, t)

def csv_row(row): return ",".join(str(t) for t in row)
def grid_to_csv(g): return "\n".join(csv_row(row) for row in g)

# ---------------------------------------------------------------------------
# Hub TMX generator (60 wide × 40 tall) — used for all three hub maps.
#
# Each hub has:
#   - 8 portal slots (same positions in every hub)
#   - N-S path at x=29-31, exits at top/bottom
#   - E-W path at y=17-22 with optional left/right exits (East Hub only)
#
# Call with:
#   north_warp / south_warp: (dest_map, dest_x, dest_y) for N-S path exits
#   west_warp  / east_warp:  (dest_map, dest_x, dest_y) or None
# ---------------------------------------------------------------------------
SLOT_POS = [
    ( 8,  8), (20,  8), (32,  8), (44,  8),   # slots 1-4
    ( 8, 28), (20, 28), (32, 28), (44, 28),    # slots 5-8
]
PATH_Y1, PATH_Y2 = 17, 22   # E-W path rows (inclusive)
NS_XS = range(29, 32)       # N-S path columns x=29,30,31


def build_hub(hub_name, north_warp, south_warp, west_warp=None, east_warp=None):
    W, H = 60, 40
    has_west = west_warp is not None
    has_east = east_warp is not None

    # ── BACK layer ─────────────────────────────────────────────────────────
    back = make_grid(W, H, GRASS)

    # Outer cliff top/bottom (rows 0-1 and 38-39)
    for y in (0, 1, 38, 39):
        set_row(back, y, CLIFF)
        set_tile(back, 0, y, DARK)
        set_tile(back, W-1, y, DARK)

    # Cliff side walls (left x=0-2, right x=57-59)
    # Skip path-row opening only for hubs that have west/east exits.
    for y in range(2, 38):
        if (has_west or has_east) and PATH_Y1 <= y <= PATH_Y2:
            pass  # E-W path opening — tiles set below
        else:
            set_tile(back, 0, y, DARK)
            set_tile(back, 1, y, CLIFF)
            set_tile(back, 2, y, CLIFF_L)
            set_tile(back, 3, y, GRASS_A)
            set_tile(back, W-1, y, DARK)
            set_tile(back, W-2, y, CLIFF)
            set_tile(back, W-3, y, CLIFF_L)
            set_tile(back, W-4, y, GRASS_A)

    # Cliff inner transition row (y=2, y=37)
    for x in range(4, W-4):
        set_tile(back, x, 2, GRASS_A)
        set_tile(back, x, 37, GRASS_A)

    # Shadow grass band just inside cliff (y=3, y=36)
    for x in range(4, W-4):
        set_tile(back, x, 3, G_SHADE)
        set_tile(back, x, 36, G_SHADE)

    # Random grass variety
    for y in range(4, 36):
        for x in range(4, W-4):
            r = random.random()
            if r < 0.06:
                back[y][x] = GRASS_S
            elif r < 0.10:
                back[y][x] = G_DARK

    # E-W main path (full width)
    set_row(back, PATH_Y1, ROAD_N, 0, W)
    for y in range(PATH_Y1+1, PATH_Y2):
        set_row(back, y, ROAD, 0, W)
    set_row(back, PATH_Y2, ROAD_S, 0, W)

    # E-W path openings — only punch through cliff if there's a warp on that side
    if has_west:
        for y in range(PATH_Y1, PATH_Y2+1):
            set_tile(back, 0, y, GRASS)
            set_tile(back, 1, y, GRASS_A)
    if has_east:
        for y in range(PATH_Y1, PATH_Y2+1):
            set_tile(back, W-1, y, GRASS)
            set_tile(back, W-2, y, GRASS_A)

    # Vertical connector paths from portals to E-W path
    for px, py in SLOT_POS[:4]:   # upper portals → down to path north edge
        for cy in range(py+2, PATH_Y1):
            set_tile(back, px, cy, ROAD)
    for px, py in SLOT_POS[4:]:   # lower portals → up to path south edge
        for cy in range(PATH_Y2+1, py-1):
            set_tile(back, px, cy, ROAD)

    # Portal patches — 3×3 GRASS_C centred on warp tile
    for px, py in SLOT_POS:
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                nx, ny = px+dx, py+dy
                if 3 < nx < W-4 and 3 < ny < H-4:
                    back[ny][nx] = GRASS_C

    # N-S connector path (x=29-31), skip E-W road rows (already full-width road)
    for py in range(0, H):
        if PATH_Y1 <= py <= PATH_Y2:
            continue
        for px in NS_XS:
            set_tile(back, px, py, ROAD)

    # ── BUILDINGS layer ─────────────────────────────────────────────────────
    buildings = make_grid(W, H, EMPTY)

    # ── PATHS layer (forage spawns) ─────────────────────────────────────────
    paths = make_grid(W, H, EMPTY)
    FORAGE = 2017
    for y in range(4, 36):
        for x in range(4, W-4):
            if back[y][x] == GRASS_S and random.random() < 0.08:
                paths[y][x] = FORAGE

    # ── FRONT layer — cliff face + trees ────────────────────────────────────
    front = make_grid(W, H, EMPTY)

    for fy, row in enumerate(CLIFF_FACE_ROWS):   # upper-left
        for fx, t in enumerate(row):
            set_tile(front, fx, fy, t)
    for fy, row in enumerate(CLIFF_FACE_ROWS):   # upper-right (mirror)
        for fi, t in enumerate(row):
            set_tile(front, W-1-fi, fy, t)
    for fy, row in enumerate(CLIFF_FACE_ROWS):   # lower corners
        for fx, t in enumerate(row):
            set_tile(front, fx, H-1-fy, t)
            set_tile(front, W-1-fx, H-1-fy, t)

    TREES = [
        (5,  3), (14, 3), (24, 3), (37, 3), (50, 3),
        (5,  7), (23, 6), (37, 5), (50, 7),
        (5, 12), (13,12), (24,13), (50,12),
        (5, 23), (24,24), (37,23), (50,24),
        (5, 29), (15,29), (24,29), (50,29),
        (5, 33), (24,33), (37,34), (50,33),
    ]
    NS_CENTER = 30
    for tx, ty in TREES:
        if PATH_Y1-3 <= ty+3 and ty <= PATH_Y2+3:
            continue
        if abs(tx+1 - NS_CENTER) < 3:
            continue
        overlap = False
        for px, py in SLOT_POS:
            if abs(tx+1 - px) < 4 and abs(ty+1 - py) < 5:
                overlap = True; break
        if overlap:
            continue
        place_tree(front, tx, ty)

    # ── AlwaysFront layer ───────────────────────────────────────────────────
    alwaysfront = make_grid(W, H, EMPTY)

    # ── Warp property string ────────────────────────────────────────────────
    warp_parts = []
    if west_warp:
        warp_parts.append(" ".join(
            f"-1 {y} {west_warp[0]} {west_warp[1]} {west_warp[2]}"
            for y in range(PATH_Y1, PATH_Y2+1)
        ))
    if east_warp:
        warp_parts.append(" ".join(
            f"{W} {y} {east_warp[0]} {east_warp[1]} {east_warp[2]}"
            for y in range(PATH_Y1, PATH_Y2+1)
        ))
    nd, nx, ny_ = north_warp
    sd, sx, sy = south_warp
    warp_parts.append(" ".join(f"{px} -1 {nd} {nx} {ny_}" for px in NS_XS))
    warp_parts.append(" ".join(f"{px} {H} {sd} {sx} {sy}" for px in NS_XS))
    warp_parts.append(" ".join(
        f"{px} {py} MultiFarm_Farm_{i+1} 40 5"
        for i, (px, py) in enumerate(SLOT_POS)
    ))
    warp_str = " ".join(warp_parts)

    tmx = f"""<?xml version="1.0"?>
<map version="1.4" tiledversion="1.4.2" orientation="orthogonal" renderorder="right-down" compressionlevel="0" width="{W}" height="{H}" tilewidth="16" tileheight="16" infinite="0" nextlayerid="9" nextobjectid="1">
  <properties>
    <property name="Outdoors" type="bool" value="True" />
    <property name="Fall_Objects" type="string" value="T" />
    <property name="Spring_Objects" type="string" value="T" />
    <property name="Summer_Objects" type="string" value="T" />
    <property name="Winter_Objects" type="string" value="T" />
    <property name="Warp" type="string" value="{warp_str}" />
  </properties>
  <tileset firstgid="1" name="monsterGraveTiles" tilewidth="16" tileheight="16" tilecount="15" columns="3">
    <image source="spring_monsterGraveTiles" width="48" height="80" />
  </tileset>
  <tileset firstgid="16" name="outdoor" tilewidth="16" tileheight="16" tilecount="1975" columns="25">
    <image source="spring_outdoorsTileSheet" width="400" height="1264" />
    <tile id="150">
      <properties>
        <property name="Spawnable" type="string" value="T" />
        <property name="Type" type="string" value="Grass" />
      </properties>
    </tile>
    <tile id="175">
      <properties>
        <property name="Type" type="string" value="Grass" />
      </properties>
    </tile>
  </tileset>
  <layer id="1" name="Back" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(back)}
    </data>
  </layer>
  <layer id="2" name="Buildings" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(buildings)}
    </data>
  </layer>
  <layer id="3" name="Paths" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(paths)}
    </data>
  </layer>
  <layer id="4" name="Front" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(front)}
    </data>
  </layer>
  <layer id="5" name="AlwaysFront" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(alwaysfront)}
    </data>
  </layer>
  <objectgroup id="6" name="Back" visible="false" locked="false" />
  <objectgroup id="7" name="Buildings" visible="false" locked="false" />
  <objectgroup id="8" name="Paths" visible="false" locked="false" />
</map>
"""
    out_path = os.path.join(OUT_DIR, f"{hub_name}.tmx")
    with open(out_path, "w") as f:
        f.write(tmx.lstrip())
    print(f"Wrote {out_path}")


# ---------------------------------------------------------------------------
# Per-type PlayerFarm TMX generation
#
# Farm type IDs match Stardew Valley's internal values:
#   0=Standard, 1=Riverland, 2=Forest, 3=Hill-top, 4=Wilderness,
#   5=FourCorners, 6=Meadowlands/Ranching
#
# Each type gets its own TMX: PlayerFarm_0.tmx … PlayerFarm_6.tmx
# The Warp property is left empty — PlayerFarmManager adds slot-specific
# runtime warps (top-edge return to hub, cave entrance, farmhouse entrance).
# ---------------------------------------------------------------------------

FARM_TYPE_SOURCES = {
    0: "Farm.tmx",
    1: "Farm_Fishing.tmx",
    2: "Farm_Foraging.tmx",
    3: "Farm_Mining.tmx",
    4: "Farm_Combat.tmx",
    5: "Farm_FourCorners.tmx",
    6: "Farm_Ranching.tmx",
}


def _extract_map_attr(src, attr):
    import re
    m = re.search(rf'{attr}="(\d+)"', src)
    return int(m.group(1)) if m else None


def _extract_layer(src, layer_name):
    import re
    pat = rf'<layer[^>]*name="{layer_name}"[^>]*>.*?<data encoding="csv">\n(.*?)\n\s*</data>'
    m = re.search(pat, src, re.DOTALL)
    return m.group(1).strip() if m else None


def _extract_tilesets(src):
    """Return the raw tileset XML blocks from a TMX source."""
    import re
    return re.findall(r'<tileset\b.*?</tileset>', src, re.DOTALL)


def build_player_farm(type_id):
    """Generate PlayerFarm_{type_id}.tmx from the corresponding vanilla Farm TMX."""
    import re

    src_name = FARM_TYPE_SOURCES[type_id]
    src_path = os.path.join(os.path.dirname(__file__), "..", "vanilla-maps", src_name)
    with open(src_path, "r") as f:
        src = f.read()

    W = _extract_map_attr(src, "width")
    H = _extract_map_attr(src, "height")

    layers = {}
    for lname in ("Back", "Buildings", "Paths", "Front", "AlwaysFront", "AlwaysFront2"):
        data = _extract_layer(src, lname)
        if data:
            layers[lname] = data

    tilesets_xml = "\n".join(_extract_tilesets(src))

    def layer_block(lid, name, data):
        return (
            f'  <layer id="{lid}" name="{name}" width="{W}" height="{H}" '
            f'opacity="1" offsetx="0" offsety="0">\n'
            f'    <properties />\n'
            f'    <data encoding="csv">\n'
            f'{data}\n'
            f'    </data>\n'
            f'  </layer>'
        )

    layer_ids = {"Back": 1, "Buildings": 2, "Paths": 3,
                 "Front": 4, "AlwaysFront": 5, "AlwaysFront2": 6}
    layer_blocks = [
        layer_block(layer_ids[n], n, layers[n])
        for n in ("Back", "Buildings", "Paths", "Front", "AlwaysFront", "AlwaysFront2")
        if n in layers
    ]
    layers_xml = "\n".join(layer_blocks)
    next_layer_id = max(layer_ids[n] for n in layers) + 1

    tmx = (
        f'<?xml version="1.0"?>\n'
        f'<map version="1.4" tiledversion="1.4.2" orientation="orthogonal" renderorder="right-down" '
        f'compressionlevel="0" width="{W}" height="{H}" tilewidth="16" tileheight="16" '
        f'infinite="0" nextlayerid="{next_layer_id + 5}" nextobjectid="1">\n'
        f'  <properties>\n'
        f'    <property name="AllowGrassSurviveInWinter" type="string" value="T" />\n'
        f'    <property name="Outdoors" type="string" value="T" />\n'
        f'    <property name="Warp" type="string" value="" />\n'
        f'  </properties>\n'
        f'{tilesets_xml}\n'
        f'{layers_xml}\n'
        f'  <objectgroup id="{next_layer_id}"   name="Back"         visible="false" locked="false" />\n'
        f'  <objectgroup id="{next_layer_id+1}" name="Buildings"    visible="false" locked="false" />\n'
        f'  <objectgroup id="{next_layer_id+2}" name="Paths"        visible="false" locked="false" />\n'
        f'  <objectgroup id="{next_layer_id+3}" name="Front"        visible="false" locked="false" />\n'
        f'  <objectgroup id="{next_layer_id+4}" name="AlwaysFront"  visible="false" locked="false" />\n'
        f'</map>\n'
    )

    out_path = os.path.join(OUT_DIR, f"PlayerFarm_{type_id}.tmx")
    with open(out_path, "w") as f:
        f.write(tmx)
    print(f"Wrote {out_path}  ({W}x{H})")


def build_interior_template(vanilla_name, output_name):
    """
    Copy a vanilla interior TMX (FarmCave, FarmHouse) clearing its Warp property.
    PlayerFarmManager patches Warp at runtime with slot-specific destinations.
    """
    import re

    src_path = os.path.join(os.path.dirname(__file__), "..", "vanilla-maps", vanilla_name)
    with open(src_path, "r") as f:
        src = f.read()

    # Clear Warp value so the runtime patch is the sole warp source
    out = re.sub(
        r'(<property name="Warp"[^>]*value=")[^"]*(")',
        r'\g<1>\g<2>',
        src
    )

    out_path = os.path.join(OUT_DIR, output_name)
    with open(out_path, "w") as f:
        f.write(out)
    print(f"Wrote {out_path}  (template from {vanilla_name})")


if __name__ == "__main__":
    # East Hub — main hub between Farm (west) and BusStop (east)
    # N-S path leads to North Hub (above) and South Hub (below)
    build_hub("MultiFarm_Hub_East",
        north_warp=("MultiFarm_Hub_North", 30, 37),  # arrive at bottom of North Hub
        south_warp=("MultiFarm_Hub_South", 30,  2),  # arrive at top of South Hub
        west_warp =("Farm",    79, 17),
        east_warp =("BusStop", 11, 23),
    )

    # North Hub — between Backwoods (north) and East Hub (south)
    build_hub("MultiFarm_Hub_North",
        north_warp=("Backwoods",         14, 38),  # arrive near south edge of Backwoods
        south_warp=("MultiFarm_Hub_East", 30,  2), # arrive at top of East Hub
    )

    # South Hub — between East Hub (north) and Forest (south)
    build_hub("MultiFarm_Hub_South",
        north_warp=("MultiFarm_Hub_East", 30, 37), # arrive at bottom of East Hub
        south_warp=("Forest", 68, 1),
    )

    for type_id in FARM_TYPE_SOURCES:
        build_player_farm(type_id)

    build_interior_template("FarmCave.tmx",  "PlayerFarmCave.tmx")
    build_interior_template("FarmHouse.tmx", "PlayerFarmHouse.tmx")

    print("\nSlot portal positions (same in all 3 hubs):")
    for i, (x, y) in enumerate(SLOT_POS):
        print(f"  Slot {i+1}: ({x}, {y})")
    print(f"\nE-W path rows: y={PATH_Y1}–{PATH_Y2}  (East Hub only has west/east exits)")
    print(f"N-S path cols: x=29-31  (all 3 hubs)")
    print(f"\nHub entry coordinates:")
    print(f"  East Hub from Farm/west:     ( 2, 20)")
    print(f"  East Hub from BusStop/east:  (57, 20)")
    print(f"  East Hub from North Hub:     (30,  2)")
    print(f"  East Hub from South Hub:     (30, 37)")
    print(f"  North Hub from Backwoods:    (30,  2)")
    print(f"  North Hub from East Hub:     (30, 37)")
    print(f"  South Hub from East Hub:     (30,  2)")
    print(f"  South Hub from Forest:       (30, 37)")
