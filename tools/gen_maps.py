#!/usr/bin/env python3
"""
Generate FarmHub.tmx and PlayerFarm.tmx for the MultiFarm mod.

All tile GIDs use firstgid=16 for spring_outdoorsTileSheet (same as Backwoods.tmx).
GID = sheet_tile_index + 16.

Verified tile references (confirmed from vanilla map tile data):
  367 (sheet 351) = dark fill outside cliff
  396 (sheet 380) = solid cliff / rock fill
  373 (sheet 357) = cliff-to-grass left edge transition
  421 (sheet 405) = grass inner (near cliff edge)
  422 (sheet 406) = grass B
  423 (sheet 407) = grass C  — used as portal patch marker
  191 (sheet 175) = main open grass
  166 (sheet 150) = spawnable grass
  316 (sheet 300) = dark grass / shadow near cliff
  320 (sheet 304) = dark grass variant
  169 (sheet 153) = dirt road main tile   (BusStop y=23 row at sheet 153)
  392 (sheet 376) = road north edge / cliff corner (BusStop y=21, sheet 376)
  342 (sheet 326) = road south edge      (BusStop y=25, sheet 326)
  370 (sheet 354) = rock bottom-left
  371 (sheet 355) = rock bottom-center
  372 (sheet 356) = rock bottom-right

Front layer (same firstgid=16 outdoor sheet):
  Cliff face tiles (top-left corner diagonal, from Backwoods):
    962(946), 956(940), 957(941), 982(966), 1008(992), 981(965)
  Tree tiles (3-wide × 4-tall):
    Row 0: 26(10), 27(11), 28(12)
    Row 1: 51(35), 52(36), 53(37)
    Row 2: 76(60), 77(61), 78(62)
    Row 3: 101(85), 102(86), 103(87)
  Small shrub (2-wide):
    Top: 104(88), 105(89)  — single-row shrub or tree top

Portal slot positions:
  Slots 1-4: y=8,  x = 8, 20, 32, 44
  Slots 5-8: y=28, x = 8, 20, 32, 44

Warp connections:
  Left  (x=-1, y=17-22) -> Farm 79 17
  Right (x=60, y=17-22) -> BusStop 11 23
  Slot portals (px,py)  -> MultiFarm_Farm_N at (40, 5)
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
def grid_to_csv(g): return "\n".join(csv_row(row)+"," for row in g)

# ---------------------------------------------------------------------------
# FarmHub.tmx  (60 wide × 40 tall)
# ---------------------------------------------------------------------------
def build_farmhub():
    W, H = 60, 40
    SLOT_POS = [
        ( 8,  8), (20,  8), (32,  8), (44,  8),   # slots 1-4
        ( 8, 28), (20, 28), (32, 28), (44, 28),    # slots 5-8
    ]
    PATH_Y1, PATH_Y2 = 17, 22   # E-W path rows (inclusive): road_N + 3 road + road_S

    # ── BACK layer ─────────────────────────────────────────────────────────
    back = make_grid(W, H, GRASS)

    # Outer cliff top/bottom (rows 0-1 and 38-39)
    for y in (0, 1, 38, 39):
        set_row(back, y, CLIFF)
        set_tile(back, 0, y, DARK)
        set_tile(back, W-1, y, DARK)

    # Cliff side walls (left x=0-2, right x=57-59), skip path opening
    for y in range(2, 38):
        if PATH_Y1 <= y <= PATH_Y2:
            # Path opening — keep grass (default)
            pass
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

    # Scatter dark grass patches in interior for variety
    for y in range(4, 36):
        for x in range(4, W-4):
            r = random.random()
            if r < 0.06:
                back[y][x] = GRASS_S
            elif r < 0.10:
                back[y][x] = G_DARK

    # E-W main path (y=PATH_Y1 to PATH_Y2, full width except cliff walls)
    # y=17: road north edge
    set_row(back, PATH_Y1, ROAD_N, 0, W)
    # y=18-20: main road
    for y in range(PATH_Y1+1, PATH_Y2):
        set_row(back, y, ROAD, 0, W)
    # y=22: road south edge
    set_row(back, PATH_Y2, ROAD_S, 0, W)
    # Re-apply cliff walls on path rows (left x=0-1 and right x=58-59 are clear)
    for y in range(PATH_Y1, PATH_Y2+1):
        set_tile(back, 0, y, GRASS)     # left path opening
        set_tile(back, 1, y, GRASS_A)
        set_tile(back, W-1, y, GRASS)   # right path opening
        set_tile(back, W-2, y, GRASS_A)

    # Vertical connector paths (single road tile wide) from portals to E-W path
    for px, py in SLOT_POS[:4]:   # upper portals: connect DOWN to path north edge
        for cy in range(py+2, PATH_Y1):
            set_tile(back, px, cy, ROAD)
    for px, py in SLOT_POS[4:]:   # lower portals: connect UP to path south edge
        for cy in range(PATH_Y2+1, py-1):
            set_tile(back, px, cy, ROAD)

    # Portal patches — 3×3 GRASS_C centred on warp tile
    for px, py in SLOT_POS:
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                nx, ny = px+dx, py+dy
                if 3 < nx < W-4 and 3 < ny < H-4:
                    back[ny][nx] = GRASS_C

    # ── BUILDINGS layer ─────────────────────────────────────────────────────
    buildings = make_grid(W, H, EMPTY)

    # ── PATHS layer (forage spawns; warps are in Warp map property) ─────────
    paths = make_grid(W, H, EMPTY)
    # Scatter forage spawn tiles (2015=forage marker from Backwoods, firstgid=16: 2016,2017)
    FORAGE = 2017
    for y in range(4, 36):
        for x in range(4, W-4):
            if back[y][x] == GRASS_S and random.random() < 0.08:
                paths[y][x] = FORAGE

    # ── FRONT layer — cliff face + trees ────────────────────────────────────
    front = make_grid(W, H, EMPTY)

    # Upper-left cliff face (diagonal, copied from Backwoods rows 0-6)
    for fy, row in enumerate(CLIFF_FACE_ROWS):
        for fx, t in enumerate(row):
            set_tile(front, fx, fy, t)

    # Upper-right cliff face (mirror: just use same cliff face tiles going right-to-left)
    for fy, row in enumerate(CLIFF_FACE_ROWS):
        for fi, t in enumerate(row):
            set_tile(front, W-1-fi, fy, t)

    # Lower-left and lower-right cliff face (flipped vertically)
    for fy, row in enumerate(CLIFF_FACE_ROWS):
        for fx, t in enumerate(row):
            set_tile(front, fx, H-1-fy, t)
            set_tile(front, W-1-fx, H-1-fy, t)

    # Tree positions (x,y = top-left of 3×4 tree).
    # Safe zone exclusions: portals ±4 tiles, path y=15-24, cliff walls x<5 or x>54
    TREES = [
        # Upper half
        (5,  3), (14, 3), (25, 3), (37, 3), (50, 3),
        (5,  7), (26, 6), (37, 5), (50, 7),
        (5, 12), (13,12), (27,13), (50,12),
        # Lower half
        (5, 23), (25,24), (37,23), (50,24),
        (5, 29), (15,29), (27,29), (50,29),
        (5, 33), (25,33), (37,34), (50,33),
    ]
    for tx, ty in TREES:
        # Skip if overlaps with path rows or portal exclusion zones
        if PATH_Y1-3 <= ty+3 and ty <= PATH_Y2+3:
            continue  # tree touches path band
        overlap = False
        for px, py in SLOT_POS:
            if abs(tx+1 - px) < 4 and abs(ty+1 - py) < 5:
                overlap = True; break
        if overlap:
            continue
        place_tree(front, tx, ty)

    # ── AlwaysFront layer (empty — no overhanging canopy needed) ────────────
    alwaysfront = make_grid(W, H, EMPTY)

    # --- Warp property string ---
    # Left exits → Farm
    farm_warps = " ".join(
        f"-1 {y} Farm 79 17" for y in range(PATH_Y1, PATH_Y2+1)
    )
    # Right exits → BusStop
    bus_warps = " ".join(
        f"60 {y} BusStop 11 23" for y in range(PATH_Y1, PATH_Y2+1)
    )
    # Slot portal warps — arrive at (40, 5) on player farm (clear of top-edge return warp)
    slot_warps = " ".join(
        f"{px} {py} MultiFarm_Farm_{i+1} 40 5"
        for i, (px, py) in enumerate(SLOT_POS)
    )
    warp_str = f"{farm_warps} {bus_warps} {slot_warps}"

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
    path = os.path.join(OUT_DIR, "FarmHub.tmx")
    with open(path, "w") as f:
        f.write(tmx.lstrip())
    print(f"Wrote {path}")
    return SLOT_POS, PATH_Y1, PATH_Y2


# ---------------------------------------------------------------------------
# PlayerFarm.tmx  (80 wide x 65 tall) — based on vanilla Farm.tmx layout
# Farm.tmx uses firstgid=129 for outdoor sheet. We'll use same tilesets.
# ---------------------------------------------------------------------------
def build_playerfarm():
    """
    Read Farm.tmx tile data for Back/Buildings/Paths/Front/AlwaysFront layers
    and write a PlayerFarm.tmx that:
      - Has the same farmland area as vanilla Farm
      - Replaces Farm's warps with a single return warp to MultiFarm_Hub
      - Removes the FarmCave warp (no cave per player farm)
    """
    FARM_TMX = os.path.join(os.path.dirname(__file__), "..",
                            "vanilla-maps", "Farm.tmx")

    with open(FARM_TMX, "r") as f:
        farm_src = f.read()

    import re

    # Extract each layer's CSV data
    def extract_layer(src, layer_name):
        pat = rf'<layer[^>]*name="{layer_name}"[^>]*>.*?<data encoding="csv">\n(.*?)\n\s*</data>'
        m = re.search(pat, src, re.DOTALL)
        if m:
            return m.group(1).strip()
        return None

    layers = {}
    for lname in ("Back", "Buildings", "Paths", "Front", "AlwaysFront", "AlwaysFront2"):
        data = extract_layer(farm_src, lname)
        if data:
            layers[lname] = data

    W, H = 80, 65

    # No static return warp in the TMX — PlayerFarmManager adds slot-specific
    # return warps at runtime (top edge y=-1, x=38-42 → hub slot portal+2).
    return_warp = ""

    # Build layer blocks
    def layer_block(lid, name, data):
        return f"""  <layer id="{lid}" name="{name}" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{data}
    </data>
  </layer>"""

    layer_ids = {"Back": 1, "Buildings": 2, "Paths": 3,
                 "Front": 4, "AlwaysFront": 5, "AlwaysFront2": 6}
    layer_blocks = []
    for lname in ("Back", "Buildings", "Paths", "Front", "AlwaysFront", "AlwaysFront2"):
        if lname in layers:
            layer_blocks.append(layer_block(layer_ids[lname], lname, layers[lname]))

    layers_xml = "\n".join(layer_blocks)

    tmx = f"""<?xml version="1.0"?>
<map version="1.4" tiledversion="1.4.2" orientation="orthogonal" renderorder="right-down" compressionlevel="0" width="{W}" height="{H}" tilewidth="16" tileheight="16" infinite="0" nextlayerid="13" nextobjectid="1">
  <properties>
    <property name="AllowGrassSurviveInWinter" type="string" value="T" />
    <property name="Outdoors" type="string" value="T" />
    <property name="Warp" type="string" value="{return_warp}" />
  </properties>
  <tileset firstgid="1" name="extra_tiles" tilewidth="16" tileheight="16" tilecount="64" columns="8">
    <image source="spring_outdoorTileSheet_extra" width="128" height="128" />
    <tile id="0">
      <properties>
        <property name="Buildable" type="bool" value="True" />
        <property name="CanPlantTrees" type="string" value="T" />
        <property name="NoSpawn" type="string" value="T" />
        <property name="Type" type="string" value="Grass" />
      </properties>
    </tile>
    <tile id="1">
      <properties>
        <property name="Buildable" type="bool" value="True" />
        <property name="NoSpawn" type="string" value="T" />
        <property name="Type" type="string" value="Dirt" />
      </properties>
    </tile>
    <tile id="2">
      <properties>
        <property name="Buildable" type="bool" value="True" />
        <property name="CanPlantTrees" type="string" value="T" />
        <property name="Diggable" type="string" value="T" />
        <property name="NoSpawn" type="string" value="T" />
        <property name="Type" type="string" value="Dirt" />
      </properties>
    </tile>
  </tileset>
  <tileset firstgid="65" name="Paths" tilewidth="16" tileheight="16" tilecount="64" columns="4">
    <image source="paths" width="64" height="256" />
  </tileset>
  <tileset firstgid="129" name="untitled tile sheet" tilewidth="16" tileheight="16" tilecount="1975" columns="25">
    <image source="spring_outdoorsTileSheet" width="400" height="1264" />
    <tile id="150">
      <properties>
        <property name="Buildable" type="bool" value="True" />
        <property name="Type" type="string" value="Grass" />
      </properties>
    </tile>
    <tile id="175">
      <properties>
        <property name="Buildable" type="bool" value="True" />
        <property name="Type" type="string" value="Grass" />
      </properties>
    </tile>
  </tileset>
{layers_xml}
  <objectgroup id="7" name="Back" visible="false" locked="false" />
  <objectgroup id="8" name="Buildings" visible="false" locked="false" />
  <objectgroup id="9" name="Paths" visible="false" locked="false" />
  <objectgroup id="10" name="Front" visible="false" locked="false" />
  <objectgroup id="11" name="AlwaysFront" visible="false" locked="false" />
  <objectgroup id="12" name="AlwaysFront2" visible="false" locked="false" />
</map>
"""
    path = os.path.join(OUT_DIR, "PlayerFarm.tmx")
    with open(path, "w") as f:
        f.write(tmx.lstrip())
    print(f"Wrote {path}")


if __name__ == "__main__":
    slot_pos, path_y1, path_y2 = build_farmhub()
    build_playerfarm()
    print("\nSlot portal positions (for FarmHubManager.cs):")
    for i, (x, y) in enumerate(slot_pos):
        print(f"  Slot {i+1}: ({x}, {y})")
    print(f"\nFarm/BusStop path rows: y={path_y1} to y={path_y2}")
    print(f"Farm left exit:    x=-1, y={path_y1}-{path_y2} -> Farm 79 17")
    print(f"BusStop right exit: x=60, y={path_y1}-{path_y2} -> BusStop 11 23")
