# MultiFarm — Stardew Valley Mod

Each player gets their own private farm, connected via a shared **Farm Hub** between the main farm and the bus stop. Supports up to 8 players.

## How it works

```
[BusStop] ──── [Farm Hub] ──── [Vanilla Farm]
                   │
         ┌─────┬──┴──┬─────┐
       [P1]  [P2] … [P7] [P8]
```

- A new outdoor map (**Farm Hub**) replaces the direct Farm ↔ BusStop warp
- 8 paths radiate out from the hub, each leading to one player's private farm
- When a new player joins, they're auto-assigned the next open slot and prompted to choose a farm type
- Each private farm has a warp back to the hub

## Building

### Prerequisites
- [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [SMAPI](https://smapi.io/) installed into your Stardew Valley folder
- Stardew Valley (Steam or GOG)

### Build steps

```bash
# Point GamePath at your Stardew Valley install
export GamePath="$HOME/.steam/steam/steamapps/common/Stardew Valley"

cd MultiFarm
dotnet build
```

The built mod ends up in `MultiFarm/bin/Debug/net6.0/`.
Copy the contents to `~/.config/StardewValley/Mods/MultiFarm/`.

## Map editing

Both maps live in `MultiFarm/assets/maps/`:

| File | Purpose |
|------|---------|
| `FarmHub.tmx` | The shared hub area (build in Tiled) |
| `PlayerFarm.tmx` | Base map cloned for every player farm |

Use [Tiled](https://www.mapeditor.org/) to edit `.tmx` files.
Match the warp tile positions to the constants in `FarmHubManager.cs`.

## Config (`config.json`)

| Key | Default | Description |
|-----|---------|-------------|
| `MaxPlayers` | `8` | Number of farm slots (1–8) |
| `AllowedFarmTypes` | `[0..6]` | Farm types players can choose |
| `ReplaceVanillaWarps` | `true` | Route Farm ↔ BusStop through hub |

## Console commands (in-game)

| Command | Description |
|---------|-------------|
| `mf_status` | Show all slot assignments |
| `mf_assign <name> <slot>` | Manually assign a player to a slot |
| `mf_goto <slot>` | Warp yourself to a farm slot |

## Roadmap

- [ ] Custom FarmHub.tmx map (build in Tiled)
- [ ] PlayerFarm.tmx base map per farm type
- [ ] In-game farm selection UI (replace auto-assign)
- [ ] Per-player inventory/chest isolation option
- [ ] Signs at hub paths showing player name
- [ ] Multiplayer sync for private farm NPC schedules
