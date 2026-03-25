#!/usr/bin/env python3
"""
Generate hub and player farm TMX maps for the MultiFarm mod.

Three hub maps, each 44×24:
  MultiFarm_Hub_Farm      — between Farm (west) and BusStop (east)
  MultiFarm_Hub_Backwoods — Backwoods (north) / vanilla Farm mountain trail (south)
  MultiFarm_Hub_Forest    — Forest (south) / vanilla Farm south edge (north)

Portal positions per hub (slot 1 → vanilla "Farm", slots 2-8 → MultiFarm_Farm_N):
  Farm Hub      — west side, y=5, x=3,6,9,12,15,18,21,24 (spacing 3)
  Backwoods Hub — south side, y=18, x=4,9,14,19,24,29,34,39 (spacing 5)
  Forest Hub    — north side, y=5,  x=4,9,14,19,24,29,34,39 (spacing 5)

Farm arrival from hub (OnWarped re-warps to correct position per hub source):
  From Backwoods Hub → near farm top:   (spawnX, 5)   placeholder (40,  5)
  From Forest Hub    → near farm south: (southX, H-10) placeholder (40, 55)
  From Farm Hub      → near farm east:  (W-5, 17)      placeholder (75, 15)
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
# Per-hub portal slot positions
# ---------------------------------------------------------------------------
# Farm Hub (horizontal spine): portals on west side at y=5, spacing 3
FARM_HUB_SLOTS = [
    ( 3, 5), ( 6, 5), ( 9, 5), (12, 5),
    (15, 5), (18, 5), (21, 5), (24, 5),
]

# Backwoods Hub (vertical spine): portals on south side at y=18, spacing 5
BACKWOODS_HUB_SLOTS = [
    ( 4, 18), ( 9, 18), (14, 18), (19, 18),
    (24, 18), (29, 18), (34, 18), (39, 18),
]

# Forest Hub (vertical spine): portals on north side at y=5, spacing 5
FOREST_HUB_SLOTS = [
    ( 4, 5), ( 9, 5), (14, 5), (19, 5),
    (24, 5), (29, 5), (34, 5), (39, 5),
]

HUB_W, HUB_H = 44, 24

# E-W path rows y=9-11 (Farm Hub horizontal spine)
_PATH_YS = range(9, 12)
# N-S path columns x=20-22 (Backwoods / Forest Hub vertical spine)
_PATH_XS = range(20, 23)


# ---------------------------------------------------------------------------
# Hub TMX generator — 44 wide × 24 tall
#
# exits: dict mapping side → (dest_map, dest_x, dest_y)
# slot_pos: list of 8 (x, y) portal tile positions for this hub
# farm_arrival: (x, y) tile on player farm where hub portal teleports to
#   (placeholder — OnWarped immediately re-warps to the correct spot per hub)
#
# Spine direction: horizontal if any west/east exit, vertical otherwise.
#
# Hub entry coordinates (FarmHubManager.cs):
#   Farm Hub      west  entrance: ( 2, 10)   east entrance: (41, 10)
#   Backwoods Hub north entrance: (21,  2)   south entrance: (21, 21)
#   Forest Hub    south entrance: (21, 21)   north entrance: (21,  2)
# ---------------------------------------------------------------------------
def build_hub(hub_name, exits, slot_pos, farm_arrival=(40, 5)):
    """
    Build and write a hub TMX map.

    exits:        dict { "west"|"east"|"north"|"south" → (dest_map, dest_x, dest_y) }
    slot_pos:     list of 8 (tile_x, tile_y) portal positions
    farm_arrival: (x, y) destination tile on farm when stepping into a portal
                  (OnWarped in C# overrides this to the correct hub-direction spawn)
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

    # Main spine + portal connectors
    if has_horiz:
        # Farm Hub: horizontal spine y=9-11; vertical stems from portals down to spine
        for y in _PATH_YS:
            set_row(back, y, ROAD, 2, W-2)
        for px, py in slot_pos:
            for cy in range(py+2, _PATH_YS[0]):   # y=7,8 for portals at y=5
                set_tile(back, px, cy, ROAD)
    else:
        # Backwoods/Forest Hub: vertical spine x=20-22; horizontal branches from portals
        for x in _PATH_XS:
            set_col(back, x, ROAD, 2, H-2)
        for px, py in slot_pos:
            if px < _PATH_XS[0]:                   # left of spine → branch east
                for cx in range(px+2, _PATH_XS[0]):
                    set_tile(back, cx, py, ROAD)
            elif px > _PATH_XS[-1]:                # right of spine → branch west
                for cx in range(_PATH_XS[-1]+1, px-1):
                    set_tile(back, cx, py, ROAD)

    # Portal patches (3×3 GRASS_C) — drawn after connectors
    for px, py in slot_pos:
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                nx, ny = px+dx, py+dy
                if 2 <= nx <= W-3 and 2 <= ny <= H-3:
                    back[ny][nx] = GRASS_C

    # Punch cliff border for every exit side
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

    # Slot 1 uses the vanilla "Farm" location; slots 2-8 use "MultiFarm_Farm_N".
    # The farm_arrival coords are a placeholder — OnWarped in C# re-warps based on hub.
    def _farm_dest(i):
        name = "Farm" if i == 0 else f"MultiFarm_Farm_{i+1}"
        return f"{name} {farm_arrival[0]} {farm_arrival[1]}"

    warp_parts.append(" ".join(
        f"{px} {py} {_farm_dest(i)}"
        for i, (px, py) in enumerate(slot_pos)
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
    import re
    return re.findall(r'<tileset\b.*?</tileset>', src, re.DOTALL)


def build_player_farm(type_id):
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
    import re

    src_path = os.path.join(os.path.dirname(__file__), "..", "vanilla-maps", vanilla_name)
    with open(src_path, "r") as f:
        src = f.read()

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
    # Farm Hub — between vanilla Farm (west) and BusStop (east).
    # Portals on west side (y=5, x=3–24) so players see them immediately on entry from Farm.
    # Farm portals → placeholder farm arrival (75, 15); OnWarped redirects to east side of farm.
    build_hub("MultiFarm_Hub_Farm", exits={
        "west": ("Farm",    79, 17),
        "east": ("BusStop", 11, 23),
    }, slot_pos=FARM_HUB_SLOTS, farm_arrival=(75, 15))

    # Backwoods Hub — vertical spine; portals on south side (y=18) so farms appear below.
    # South exit: vanilla Farm mountain trail at (40, 5) — matches vanilla Backwoods→Farm warp.
    build_hub("MultiFarm_Hub_Backwoods", exits={
        "north": ("Backwoods", 14, 38),
        "south": ("Farm",      40,  5),
    }, slot_pos=BACKWOODS_HUB_SLOTS, farm_arrival=(40, 5))

    # Forest Hub — vertical spine; portals on north side (y=5) so farms appear above.
    # North exit: vanilla Farm south edge at (68, 63) — matches vanilla Forest→Farm warp.
    build_hub("MultiFarm_Hub_Forest", exits={
        "south": ("Forest", 68,  1),
        "north": ("Farm",   68, 63),
    }, slot_pos=FOREST_HUB_SLOTS, farm_arrival=(40, 55))

    for type_id in FARM_TYPE_SOURCES:
        build_player_farm(type_id)

    build_interior_template("FarmCave.tmx",  "PlayerFarmCave.tmx")
    build_interior_template("FarmHouse.tmx", "PlayerFarmHouse.tmx")

    print("\nPortal positions per hub:")
    print("  Farm Hub (west side, y=5, spacing 3):")
    for i, (x, y) in enumerate(FARM_HUB_SLOTS):
        print(f"    Slot {i+1}: ({x},{y})  hub-arrival:({x},{y+2})")
    print("  Backwoods Hub (south side, y=18, spacing 5):")
    for i, (x, y) in enumerate(BACKWOODS_HUB_SLOTS):
        print(f"    Slot {i+1}: ({x},{y})  hub-arrival:({x},{y+2})")
    print("  Forest Hub (north side, y=5, spacing 5):")
    for i, (x, y) in enumerate(FOREST_HUB_SLOTS):
        print(f"    Slot {i+1}: ({x},{y})  hub-arrival:({x},{y+2})")
