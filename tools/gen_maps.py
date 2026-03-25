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

import os

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "MultiFarm", "assets", "maps")
os.makedirs(OUT_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Back-layer tile constants (firstgid=16 for outdoor sheet)
# ---------------------------------------------------------------------------
CLIFF  = 396   # solid cliff fill
DARK   = 367   # dark outside-cliff fill
GRASS_C= 423   # portal patch grass
GRASS  = 191   # main interior grass
ROAD   = 169   # dirt road
EMPTY  = 0

# ---------------------------------------------------------------------------
# Front-layer tile constants (cliff face corners)
# ---------------------------------------------------------------------------
CF = [962, 962, 956, 956, 957, 982]
CF1= [962, 956, 956, 957, 982, 1008]
CF2= [962, 956, 957, 982, 1008,    0]
CF3= [956, 981, 982, 1008,   0,    0]
CF4= [957, 982, 1008,   0,   0,    0]
CF5= [982, 1008,   0,   0,   0,    0]
CF6= [1008,  0,   0,   0,   0,    0]
CLIFF_FACE_ROWS = [CF, CF1, CF2, CF3, CF4, CF5, CF6]

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

def csv_row(row): return ",".join(str(t) for t in row)
def grid_to_csv(g):
    rows = [csv_row(row) for row in g]
    return ",\n".join(rows) + "\n"

# ---------------------------------------------------------------------------
# Hub TMX generator — 44 wide × 24 tall
#
# Each hub is independent and has exactly one entrance on one side:
#   "west"  — entrance from vanilla Farm map   → FarmHub
#   "east"  — entrance from vanilla BusStop    → BusStopHub
#   "north" — entrance from vanilla Backwoods  → BackwoodsHub
#   "south" — entrance from vanilla Forest     → ForestHub
#
# All 8 portal slots at the same positions in every hub:
#   Row 1 (y=5):  x = 6, 15, 24, 33
#   Row 2 (y=16): x = 6, 15, 24, 33
#
# Hub entry coordinates (used in FarmHubManager.cs):
#   Farm Hub    west  entrance: arrive at ( 2, 10)
#   BusStop Hub east  entrance: arrive at (41, 10)
#   Backwoods Hub north entrance: arrive at (21,  2)
#   Forest Hub  south entrance: arrive at (21, 21)
# ---------------------------------------------------------------------------
HUB_W, HUB_H = 44, 24

SLOT_POS = [
    ( 4,  5), ( 9,  5), (14,  5), (19,  5),   # slots 1-4
    (24,  5), (29,  5), (34,  5), (39,  5),    # slots 5-8
]

# E-W path rows y=9-11; N-S entrance columns x=20-22 (north/south hubs)
_PATH_YS = range(9, 12)
_PATH_XS = range(20, 23)


def build_hub(hub_name, exits):
    """
    exits: dict mapping side → (dest_map, dest_x, dest_y)
    Example (Farm Hub): {"west": ("Farm", 79, 17), "east": ("BusStop", 11, 23)}
    Spine direction: horizontal if any west/east exit, vertical if north/south only.
    """
    W, H = HUB_W, HUB_H
    has_horiz = "west" in exits or "east" in exits

    # ── BACK layer ─────────────────────────────────────────────────────────
    back = make_grid(W, H, GRASS)

    # 2-tile cliff border
    set_row(back, 0,   DARK)
    set_row(back, 1,   CLIFF)
    set_row(back, H-2, CLIFF)
    set_row(back, H-1, DARK)
    set_col(back, 0,   DARK,  2, H-2)
    set_col(back, 1,   CLIFF, 2, H-2)
    set_col(back, W-2, CLIFF, 2, H-2)
    set_col(back, W-1, DARK,  2, H-2)

    # Main spine + portal connectors — direction depends on exits
    if has_horiz:
        # Horizontal spine y=9-11, vertical stems from each portal down to spine
        for y in _PATH_YS:
            set_row(back, y, ROAD, 2, W-2)
        for px, py in SLOT_POS:              # all portals at y=5 → stem y=7,8 down to spine
            for cy in range(py+2, _PATH_YS[0]):
                set_tile(back, px, cy, ROAD)
    else:
        # Vertical spine x=20-22, horizontal branches to each portal
        for x in _PATH_XS:
            set_col(back, x, ROAD, 2, H-2)
        for px, py in SLOT_POS:
            if px < _PATH_XS[0]:             # portal left of spine → road goes right
                for cx in range(px+2, _PATH_XS[0]):
                    set_tile(back, cx, py, ROAD)
            elif px > _PATH_XS[-1]:          # portal right of spine → road goes left
                for cx in range(_PATH_XS[-1]+1, px-1):
                    set_tile(back, cx, py, ROAD)

    # Portal patches (3×3 GRASS_C) — placed after connectors so they render on top
    for px, py in SLOT_POS:
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                nx, ny = px+dx, py+dy
                if 2 <= nx <= W-3 and 2 <= ny <= H-3:
                    back[ny][nx] = GRASS_C

    # Punch through cliff border for every exit side
    for side in exits:
        if side == "west":
            for y in _PATH_YS:
                back[y][0] = ROAD;  back[y][1] = ROAD
        elif side == "east":
            for y in _PATH_YS:
                back[y][W-1] = ROAD;  back[y][W-2] = ROAD
        elif side == "north":
            for x in _PATH_XS:
                back[0][x] = ROAD;  back[1][x] = ROAD
        elif side == "south":
            for x in _PATH_XS:
                back[H-1][x] = ROAD;  back[H-2][x] = ROAD

    # ── BUILDINGS layer — collision ──────────────────────────────────────────
    buildings = make_grid(W, H, EMPTY)
    for y in range(H):
        for x in range(W):
            if back[y][x] in (CLIFF, DARK):
                buildings[y][x] = 118

    # ── PATHS layer ─────────────────────────────────────────────────────────
    paths = make_grid(W, H, EMPTY)

    # ── FRONT layer — cliff face corners ────────────────────────────────────
    front = make_grid(W, H, EMPTY)
    for fy, row in enumerate(CLIFF_FACE_ROWS):
        for fx, t in enumerate(row):
            set_tile(front, fx,     fy,     t)
            set_tile(front, W-1-fx, fy,     t)
            set_tile(front, fx,     H-1-fy, t)
            set_tile(front, W-1-fx, H-1-fy, t)

    # ── AlwaysFront layer ───────────────────────────────────────────────────
    alwaysfront = make_grid(W, H, EMPTY)

    # ── Warp property string ────────────────────────────────────────────────
    warp_parts = []
    for side, (dm, dx, dy) in exits.items():
        if side == "west":
            warp_parts.append(" ".join(f"-1 {y} {dm} {dx} {dy}" for y in _PATH_YS))
        elif side == "east":
            warp_parts.append(" ".join(f"{W} {y} {dm} {dx} {dy}" for y in _PATH_YS))
        elif side == "north":
            warp_parts.append(" ".join(f"{x} -1 {dm} {dx} {dy}" for x in _PATH_XS))
        elif side == "south":
            warp_parts.append(" ".join(f"{x} {H} {dm} {dx} {dy}" for x in _PATH_XS))

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
{grid_to_csv(back)}</data>
  </layer>
  <layer id="2" name="Buildings" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(buildings)}</data>
  </layer>
  <layer id="3" name="Paths" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(paths)}</data>
  </layer>
  <layer id="4" name="Front" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(front)}</data>
  </layer>
  <layer id="5" name="AlwaysFront" width="{W}" height="{H}" opacity="1" offsetx="0" offsety="0">
    <properties />
    <data encoding="csv">
{grid_to_csv(alwaysfront)}</data>
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
    # Farm Hub — sits on the Farm↔BusStop road; exits both west (Farm) and east (BusStop).
    # Entered from Farm east edge OR BusStop west edge (both intercepted by OnWarped).
    build_hub("MultiFarm_Hub_Farm", exits={
        "west": ("Farm",    79, 17),
        "east": ("BusStop", 11, 23),
    })

    # Backwoods Hub — vertical spine; exits north (Backwoods) and south (Farm mountain trail).
    # Entered from Backwoods south edge (warp patched in map) OR Farm north edge (OnWarped).
    build_hub("MultiFarm_Hub_Backwoods", exits={
        "north": ("Backwoods", 14, 38),
        "south": ("Farm",      66, 15),
    })

    # Forest Hub — vertical spine; exits south (Forest) and north (Farm south edge).
    # Entered from Forest north edge (warp patched in map) OR Farm south edge (OnWarped).
    build_hub("MultiFarm_Hub_Forest", exits={
        "south": ("Forest", 68,  1),
        "north": ("Farm",   68, 63),
    })

    for type_id in FARM_TYPE_SOURCES:
        build_player_farm(type_id)

    build_interior_template("FarmCave.tmx",  "PlayerFarmCave.tmx")
    build_interior_template("FarmHouse.tmx", "PlayerFarmHouse.tmx")

    print("\nSlot portal positions (same in all 3 hubs, 1 row of 8 at y=5):")
    for i, (x, y) in enumerate(SLOT_POS):
        print(f"  Slot {i+1}: ({x}, {y})  arrival: ({x}, {y+2})")
    print(f"\nHub entry coordinates:")
    print(f"  Farm Hub    from Farm    (west  side): ( 2, 10)")
    print(f"  Farm Hub    from BusStop (east  side): (41, 10)")
    print(f"  Backwoods Hub from Backwoods (north): (21,  2)")
    print(f"  Backwoods Hub from Farm      (south): (21, 21)")
    print(f"  Forest Hub  from Forest  (south): (21, 21)")
    print(f"  Forest Hub  from Farm    (north): (21,  2)")
