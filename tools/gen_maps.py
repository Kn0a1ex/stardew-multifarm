#!/usr/bin/env python3
"""
Generate FarmHub.tmx and PlayerFarm.tmx for the MultiFarm mod.

Tile GID reference (firstgid=16 for spring_outdoorsTileSheet, same as Backwoods):
  367 = dark forest/cliff fill (tile 351)
  396 = cliff border fill (tile 380)
  373 = cliff-to-grass left edge (tile 357)
  421 = grass/open ground (tile 405)
  422 = grass variant (tile 406)
  423 = grass variant (tile 407)
  191 = open grass interior (tile 175)
  166 = spawnable grass (tile 150)
  272 = water/path horizontal (tile 256)
  270 = water channel (tile 254)

Portal slot positions (hub-relative):
  Slots 1-4: y=8,  x = 8, 20, 32, 44
  Slots 5-8: y=28, x = 8, 20, 32, 44

Farm entry  (left):  x=-1, y=18-21  -> Farm 79 17
BusStop exit (right): x=60, y=18-21 -> BusStop 11 23
"""

import os

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "MultiFarm", "assets", "maps")
os.makedirs(OUT_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Tile GID constants (firstgid=16 for outdoor sheet, like Backwoods)
# ---------------------------------------------------------------------------
CLIFF     = 396   # solid cliff border
CLIFF_NW  = 367   # dark corner/forest fill (outside cliff)
CLIFF_L   = 373   # cliff left edge transition to grass
GRASS_A   = 421   # open grass A
GRASS_B   = 422   # open grass B
GRASS_C   = 423   # open grass C
GRASS     = 191   # interior open grass
GRASS_S   = 166   # spawnable grass (slightly different visual)
DIRT      = 304   # dirt path (from Backwoods row 23 area: 304-16=288 in sheet)
EMPTY     = 0


def make_grid(w, h, default=GRASS):
    return [[default] * w for _ in range(h)]


def set_row(grid, y, tile):
    for x in range(len(grid[y])):
        grid[y][x] = tile


def set_col(grid, x, tile, y_start=0, y_end=None):
    h = len(grid)
    if y_end is None:
        y_end = h
    for y in range(y_start, y_end):
        grid[y][x] = tile


def set_rect(grid, x0, y0, x1, y1, tile):
    for y in range(y0, y1):
        for x in range(x0, x1):
            grid[y][x] = tile


def csv_row(row):
    return ",".join(str(t) for t in row)


def grid_to_csv(grid):
    return "\n".join(csv_row(row) + "," for row in grid)


# ---------------------------------------------------------------------------
# FarmHub.tmx  (60 wide x 40 tall)
# ---------------------------------------------------------------------------
def build_farmhub():
    W, H = 60, 40

    # --- BACK layer ---
    back = make_grid(W, H, GRASS)

    # Top border rows 0-1
    set_row(back, 0, CLIFF)
    for x in range(W):
        back[1][x] = CLIFF_L if x == 0 else (CLIFF if x >= W-2 else GRASS_A)
    back[1][0]   = CLIFF_L
    back[1][W-1] = CLIFF
    back[1][W-2] = CLIFF

    # Bottom border rows 38-39
    set_row(back, 39, CLIFF)
    for x in range(W):
        back[38][x] = CLIFF_L if x == 0 else (CLIFF if x >= W-2 else GRASS_A)
    back[38][0]   = CLIFF_L
    back[38][W-1] = CLIFF
    back[38][W-2] = CLIFF

    # Left border cols 0-1 (cliff except at path y=18-21)
    PATH_Y1, PATH_Y2 = 18, 22   # path rows (inclusive range)
    for y in range(2, 38):
        if PATH_Y1 <= y <= PATH_Y2:
            back[y][0] = GRASS_A  # open for path
            back[y][1] = GRASS
        else:
            back[y][0] = CLIFF_NW
            back[y][1] = CLIFF

    # Right border cols 58-59 (cliff except at path y=18-21)
    for y in range(2, 38):
        if PATH_Y1 <= y <= PATH_Y2:
            back[y][W-1] = GRASS_A
            back[y][W-2] = GRASS
        else:
            back[y][W-1] = CLIFF
            back[y][W-2] = CLIFF_NW

    # Add corner fills
    for y in range(2, 38):
        if not (PATH_Y1 <= y <= PATH_Y2):
            back[y][2] = GRASS_A   # inner transition from cliff

    # Main E-W path (y=18-21): slightly different grass to hint at path
    for y in range(PATH_Y1, PATH_Y2+1):
        for x in range(2, W-2):
            back[y][x] = GRASS_S  # use spawnable grass for path area

    # Portal areas — 3x3 dirt patches at each slot entrance
    SLOT_POS = [
        (8, 8), (20, 8), (32, 8), (44, 8),   # slots 1-4
        (8, 28), (20, 28), (32, 28), (44, 28) # slots 5-8
    ]
    for (px, py) in SLOT_POS:
        # 3x3 dirt patch centered on portal tile
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                nx, ny = px+dx, py+dy
                if 2 <= nx < W-2 and 2 <= ny < H-2:
                    back[ny][nx] = GRASS_C  # distinct tile for portal entry

    # Paths leading from main E-W path to portals (vertical connectors)
    for (px, py) in SLOT_POS[:4]:  # upper portals connect down to path
        for y in range(py+2, PATH_Y1):
            if 2 <= px < W-2:
                back[y][px] = GRASS_C
    for (px, py) in SLOT_POS[4:]:  # lower portals connect up to path
        for y in range(PATH_Y2+1, py-1):
            if 2 <= px < W-2:
                back[y][px] = GRASS_C

    # --- BUILDINGS layer (mostly empty, portal markers in Front instead) ---
    buildings = make_grid(W, H, EMPTY)

    # --- PATHS layer (empty — warps handled via Warp map property) ---
    paths = make_grid(W, H, EMPTY)

    # --- FRONT layer (empty) ---
    front = make_grid(W, H, EMPTY)

    # --- AlwaysFront layer (empty) ---
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
