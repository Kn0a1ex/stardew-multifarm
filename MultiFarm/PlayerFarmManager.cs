using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiFarm
{
    /// <summary>
    /// Manages per-player farm assignments and the private GameLocation instances.
    ///
    /// Slot numbering: 1–8 (0 = unassigned).
    /// Each slot has:
    ///   - Farm:      "MultiFarm_Farm_N"      (PlayerFarm_{type}.tmx)
    ///   - Cave:      "MultiFarm_Cave_N"      (PlayerFarmCave.tmx, runtime warp patched)
    ///   - FarmHouse: "MultiFarm_FarmHouse_N" (PlayerFarmHouse.tmx, runtime warp patched)
    ///
    /// Farm type data (cave entry tile, house door tile, spawn, map height, south-warp centre X):
    ///   Type 0 Standard      80×65  cave(34,5)   house(64,14)  spawn(40,5)  southX=40
    ///   Type 1 Riverland     80×65  cave(34,5)   house(64,14)  spawn(40,5)  southX=40
    ///   Type 2 Forest        80×65  cave(34,5)   house(64,14)  spawn(40,5)  southX=40
    ///   Type 3 Hill-top      80×65  cave(34,5)   house(64,14)  spawn(40,5)  southX=40
    ///   Type 4 Wilderness    80×65  cave(34,5)   house(64,14)  spawn(40,5)  southX=40
    ///   Type 5 FourCorners   80×80  cave(30,35)  house(64,14)  spawn(40,5)  southX=40
    ///   Type 6 Meadowlands  100×75  cave(88,54)  house(64,14)  spawn(50,5)  southX=52
    /// </summary>
    public class PlayerFarmManager
    {
        // ── Name constants ───────────────────────────────────────────────────
        public  const string FarmPrefix      = "MultiFarm_Farm_";
        public  const string CavePrefix      = "MultiFarm_Cave_";
        public  const string FarmHousePrefix = "MultiFarm_FarmHouse_";
        private const string AssignmentsFile = "data/assignments.json";

        // ── Farm type data ───────────────────────────────────────────────────
        // (tmx, caveX, caveY, houseX, houseY, spawnX, spawnY, mapH, southX)
        private static readonly Dictionary<int, (string tmx, int caveX, int caveY,
                                                  int houseX, int houseY,
                                                  int spawnX, int spawnY,
                                                  int mapH, int southX)> FarmTypeData = new()
        {
            { 0, ("PlayerFarm_0.tmx", 34,  5, 64, 14, 40, 5, 65, 40) },
            { 1, ("PlayerFarm_1.tmx", 34,  5, 64, 14, 40, 5, 65, 40) },
            { 2, ("PlayerFarm_2.tmx", 34,  5, 64, 14, 40, 5, 65, 40) },
            { 3, ("PlayerFarm_3.tmx", 34,  5, 64, 14, 40, 5, 65, 40) },
            { 4, ("PlayerFarm_4.tmx", 34,  5, 64, 14, 40, 5, 65, 40) },
            { 5, ("PlayerFarm_5.tmx", 30, 35, 64, 14, 40, 5, 80, 40) },
            { 6, ("PlayerFarm_6.tmx", 88, 54, 64, 14, 50, 5, 75, 52) },
        };

        private readonly IModHelper _helper;
        private readonly IMonitor  _monitor;

        // slot → player display name (UI/commands)
        private Dictionary<int, string> _assignments  = new();

        // slot → UniqueMultiplayerID (stable, persisted)
        private Dictionary<int, long>   _assignmentIds = new();

        // UniqueMultiplayerID → chosen farm type
        private Dictionary<long, int>   _farmTypes    = new();

        // players who have already received starter items this session
        private readonly HashSet<string> _starterItemsGiven = new();

        public PlayerFarmManager(IModHelper helper, IMonitor monitor)
        {
            _helper  = helper;
            _monitor = monitor;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Assign a player to a farm slot with a chosen farm type.</summary>
        public void AssignFarm(string playerName, int slot, int farmType = 0)
        {
            if (slot < 1 || slot > ModEntry.Instance.Config.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(slot));

            long id = Game1.getAllFarmers()
                .FirstOrDefault(f => f.Name == playerName)?.UniqueMultiplayerID ?? 0;

            // Remove any previous assignment for this player (by name or ID)
            var toRemove = _assignments
                .Where(kv => kv.Value == playerName ||
                             (id != 0 && _assignmentIds.TryGetValue(kv.Key, out long eid) && eid == id))
                .Select(kv => kv.Key).ToList();
            foreach (var k in toRemove) { _assignments.Remove(k); _assignmentIds.Remove(k); }

            _assignments[slot] = playerName;
            if (id != 0) { _assignmentIds[slot] = id; _farmTypes[id] = farmType; }

            EnsurePlayerFarmsExist();
            SaveAssignments();
        }

        /// <summary>Returns the slot number for a player, or 0 if unassigned.</summary>
        public int GetSlotForPlayer(string playerName)
        {
            foreach (var kv in _assignments)
                if (kv.Value == playerName) return kv.Key;
            return 0;
        }

        /// <summary>Returns the slot number for a player by ID, or 0 if unassigned.</summary>
        public int GetSlotForPlayer(long multiplayerId)
        {
            foreach (var kv in _assignmentIds)
                if (kv.Value == multiplayerId) return kv.Key;
            return 0;
        }

        /// <summary>Returns all current (slot, playerName) assignments.</summary>
        public IEnumerable<(int slot, string name)> GetAssignments()
            => _assignments.Select(kv => (kv.Key, kv.Value));

        /// <summary>Returns the farm type ID for a player (default 0 if unknown).</summary>
        public int GetFarmTypeForPlayer(string playerName)
        {
            var farmer = Game1.getAllFarmers().FirstOrDefault(f => f.Name == playerName);
            if (farmer != null && _farmTypes.TryGetValue(farmer.UniqueMultiplayerID, out int t))
                return t;
            return 0;
        }

        /// <summary>
        /// Show the FarmSelectionMenu to the local player.
        /// Called directly or in response to a MsgNeedsFarmSelection network message.
        /// </summary>
        public void ShowFarmSelectionMenu(Farmer player)
        {
            Game1.activeClickableMenu = new FarmSelectionMenu(player,
                chosenType => OnFarmTypeChosen(player, chosenType));
        }

        /// <summary>
        /// Show farm-selection UI for a player who has no slot.
        /// If the player is the local user, shows the menu immediately.
        /// If remote, sends them a multiplayer message to show it on their end.
        /// </summary>
        public void PromptFarmSelection(Farmer player)
        {
            if (player.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
            {
                ShowFarmSelectionMenu(player);
            }
            else
            {
                _helper.Multiplayer.SendMessage(
                    message:     "show",
                    messageType: ModEntry.MsgNeedsFarmSelection,
                    modIDs:      new[] { _helper.ModRegistry.ModID },
                    playerIDs:   new[] { player.UniqueMultiplayerID }
                );
                _monitor.Log($"Sent farm-selection prompt to {player.Name}.", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Called when the local player picks a farm type from the selection menu.
        /// If we're the host, assign immediately. Otherwise send the choice to the host.
        /// </summary>
        public void OnFarmTypeChosen(Farmer player, int farmType)
        {
            if (Context.IsMainPlayer)
            {
                AssignAndWarp(player, farmType);
            }
            else
            {
                _helper.Multiplayer.SendMessage(
                    message:     new FarmChosenPayload { FarmType = farmType },
                    messageType: ModEntry.MsgFarmChosen,
                    modIDs:      new[] { _helper.ModRegistry.ModID }
                );
                _monitor.Log($"Sent farm-type choice ({farmType}) to host.", LogLevel.Debug);
            }
        }

        /// <summary>Host-side: a client sent their farm-type choice.</summary>
        public void OnRemoteFarmTypeChosen(long senderID, int farmType)
        {
            var player = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == senderID);
            if (player is null) return;

            AssignAndWarp(player, farmType);

            _helper.Multiplayer.SendMessage(
                message:     BuildSyncPayload(),
                messageType: ModEntry.MsgSyncAssignments,
                modIDs:      new[] { _helper.ModRegistry.ModID }
            );
        }

        /// <summary>Client-side: apply an assignment sync received from the host.</summary>
        public void OnSyncAssignments(SyncPayload payload)
        {
            _assignments   = payload.Assignments   ?? _assignments;
            _assignmentIds = payload.AssignmentIds ?? _assignmentIds;
            _farmTypes     = payload.FarmTypes     ?? _farmTypes;
            EnsurePlayerFarmsExist();
        }

        /// <summary>
        /// Called when a new peer connects (host-side).
        /// Sends the current assignment table so the client can register locations.
        /// NOTE: farmer data (getAllFarmers) is not yet available at PeerConnected time.
        /// The client's own SaveLoaded handler shows the selection menu when needed.
        /// </summary>
        public void OnPeerConnected(IMultiplayerPeer peer)
        {
            _helper.Multiplayer.SendMessage(
                message:    BuildSyncPayload(),
                messageType: ModEntry.MsgSyncAssignments,
                modIDs:     new[] { _helper.ModRegistry.ModID },
                playerIDs:  new[] { peer.PlayerID }
            );
        }

        /// <summary>Warp a farmer directly to a specific slot's farm location.</summary>
        public void WarpToFarm(Farmer farmer, int slot)
        {
            string locName = FarmName(slot);
            if (Game1.getLocationFromName(locName) is null)
            {
                _monitor.Log($"Farm location '{locName}' not found.", LogLevel.Warn);
                return;
            }
            var data = GetTypeDataForSlot(slot);
            Game1.warpFarmer(locName, data.spawnX, data.spawnY, 2);
        }

        /// <summary>
        /// Ensure every assigned slot has all its locations registered.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public void EnsurePlayerFarmsExist()
        {
            int maxSlots = ModEntry.Instance.Config.MaxPlayers;
            for (int slot = 1; slot <= maxSlots; slot++)
            {
                if (!_assignments.ContainsKey(slot)) continue;
                if (Game1.getLocationFromName(FarmName(slot))      is null) RegisterFarmLocation(slot);
                if (Game1.getLocationFromName(CaveName(slot))      is null) RegisterCaveLocation(slot);
                if (Game1.getLocationFromName(FarmHouseName(slot)) is null) RegisterFarmHouseLocation(slot);
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public void LoadAssignments()
        {
            try
            {
                var data = _helper.Data.ReadJsonFile<AssignmentData>(AssignmentsFile);
                if (data is not null)
                {
                    _assignments   = data.Assignments   ?? new();
                    _assignmentIds = data.AssignmentIds ?? new();
                    _farmTypes     = data.FarmTypes     ?? new();
                    _monitor.Log($"Loaded {_assignments.Count} farm assignment(s).", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not load assignments: {ex.Message}", LogLevel.Warn);
            }
        }

        public void SaveAssignments()
        {
            try
            {
                _helper.Data.WriteJsonFile(AssignmentsFile, new AssignmentData
                {
                    Assignments   = _assignments,
                    AssignmentIds = _assignmentIds,
                    FarmTypes     = _farmTypes,
                });
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not save assignments: {ex.Message}", LogLevel.Warn);
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────

        public static string FarmName(int slot)      => $"{FarmPrefix}{slot}";
        public static string CaveName(int slot)      => $"{CavePrefix}{slot}";
        public static string FarmHouseName(int slot) => $"{FarmHousePrefix}{slot}";

        // ── Private helpers ───────────────────────────────────────────────────

        private static (string tmx, int caveX, int caveY, int houseX, int houseY,
                         int spawnX, int spawnY, int mapH, int southX) GetTypeData(int farmType)
            => FarmTypeData.TryGetValue(farmType, out var d) ? d : FarmTypeData[0];

        private (string tmx, int caveX, int caveY, int houseX, int houseY,
                 int spawnX, int spawnY, int mapH, int southX) GetTypeDataForSlot(int slot)
        {
            long id = _assignmentIds.TryGetValue(slot, out long eid) ? eid : 0;
            int farmType = (id != 0 && _farmTypes.TryGetValue(id, out int t)) ? t : 0;
            return GetTypeData(farmType);
        }

        private SyncPayload BuildSyncPayload() => new()
        {
            Assignments   = _assignments,
            AssignmentIds = _assignmentIds,
            FarmTypes     = _farmTypes,
        };

        /// <summary>
        /// Assign a slot to the given farmer, warp them there, and give starter items.
        /// Finds the first open slot automatically.
        /// </summary>
        private void AssignAndWarp(Farmer farmer, int farmType)
        {
            int openSlot = 0;
            for (int i = 1; i <= ModEntry.Instance.Config.MaxPlayers; i++)
            {
                if (!_assignments.ContainsKey(i)) { openSlot = i; break; }
            }

            if (openSlot == 0)
            {
                _monitor.Log("All farm slots are full!", LogLevel.Warn);
                Game1.chatBox?.addMessage(
                    $"MultiFarm: All {ModEntry.Instance.Config.MaxPlayers} farm slots are taken.",
                    Color.Red);
                return;
            }

            // Store assignment with stable ID
            _assignments[openSlot]   = farmer.Name;
            _assignmentIds[openSlot] = farmer.UniqueMultiplayerID;
            _farmTypes[farmer.UniqueMultiplayerID] = farmType;

            EnsurePlayerFarmsExist();
            SaveAssignments();

            _monitor.Log($"Assigned {farmer.Name} to slot {openSlot} (type {farmType}).", LogLevel.Info);

            WarpToFarm(farmer, openSlot);
            if (_starterItemsGiven.Add(farmer.Name))
                GiveStarterItems(farmer);

            Game1.chatBox?.addMessage(
                $"MultiFarm: Welcome to your farm, {farmer.Name}! Head to the Farm Hub to explore.",
                Color.Green);
        }

        private void RegisterFarmLocation(int slot)
        {
            try
            {
                var typeData = GetTypeDataForSlot(slot);
                string mapPath = _helper.ModContent
                    .GetInternalAssetName($"assets/maps/{typeData.tmx}").Name;

                var farmLoc = new Farm(mapPath, FarmName(slot));
                Game1.locations.Add(farmLoc);

                var hubArrival = FarmHubManager.GetHubArrivalForSlot(slot);

                // Top-edge return warps → hub (x=38-42, y=-1)
                for (int rx = 38; rx <= 42; rx++)
                    farmLoc.warps.Add(new Warp(rx, -1,
                        FarmHubManager.HubLocationName, hubArrival.X, hubArrival.Y, false));

                // South-edge warps → hub south entry (towards Cindersap Forest)
                var hubSouth = FarmHubManager.HubEntryFromForest;
                for (int sx = typeData.southX - 2; sx <= typeData.southX + 2; sx++)
                    farmLoc.warps.Add(new Warp(sx, typeData.mapH,
                        FarmHubManager.HubLocationName, hubSouth.X, hubSouth.Y, false));

                // Cave entrance warp
                farmLoc.warps.Add(new Warp(
                    typeData.caveX, typeData.caveY, CaveName(slot), 8, 11, false));

                // Farmhouse door warp
                farmLoc.warps.Add(new Warp(
                    typeData.houseX, typeData.houseY, FarmHouseName(slot), 3, 11, false));

                _monitor.Log($"Registered farm slot {slot}: '{FarmName(slot)}'.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to register farm slot {slot}: {ex.Message}", LogLevel.Error);
            }
        }

        private void RegisterCaveLocation(int slot)
        {
            try
            {
                string mapPath = _helper.ModContent
                    .GetInternalAssetName("assets/maps/PlayerFarmCave.tmx").Name;

                var caveLoc = new GameLocation(mapPath, CaveName(slot))
                {
                    IsOutdoors   = false,
                    IsFarm       = false,
                    IsGreenhouse = false,
                };
                Game1.locations.Add(caveLoc);

                var typeData = GetTypeDataForSlot(slot);
                caveLoc.warps.Add(new Warp(
                    8, 12, FarmName(slot), typeData.caveX, typeData.caveY + 1, false));

                _monitor.Log($"Registered cave for slot {slot}: '{CaveName(slot)}'.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to register cave slot {slot}: {ex.Message}", LogLevel.Error);
            }
        }

        private void RegisterFarmHouseLocation(int slot)
        {
            try
            {
                string mapPath = _helper.ModContent
                    .GetInternalAssetName("assets/maps/PlayerFarmHouse.tmx").Name;

                // DecoratableLocation enables wallpaper, flooring, and furniture placement
                var houseLoc = new DecoratableLocation(mapPath, FarmHouseName(slot));
                Game1.locations.Add(houseLoc);

                var typeData = GetTypeDataForSlot(slot);
                houseLoc.warps.Add(new Warp(
                    3, 12, FarmName(slot), typeData.houseX, typeData.houseY + 1, false));

                _monitor.Log($"Registered farmhouse for slot {slot}: '{FarmHouseName(slot)}'.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to register farmhouse slot {slot}: {ex.Message}", LogLevel.Error);
            }
        }

        private static void GiveStarterItems(Farmer farmer)
        {
            farmer.addItemToInventoryBool(new StardewValley.Tools.Axe());
            farmer.addItemToInventoryBool(new StardewValley.Tools.Hoe());
            farmer.addItemToInventoryBool(new StardewValley.Tools.WateringCan());
            farmer.addItemToInventoryBool(new StardewValley.Tools.Pickaxe());
            farmer.addItemToInventoryBool(new StardewValley.Tools.MilkPail());
            farmer.addItemToInventoryBool(new StardewValley.Tools.Shears());
            farmer.addItemToInventoryBool(new StardewValley.Object("472", 15));  // Parsnip Seeds
            farmer.addItemToInventoryBool(new StardewValley.Object("770", 10));  // Mixed Seeds
        }

        // ── Serialisation ─────────────────────────────────────────────────────

        private class AssignmentData
        {
            public Dictionary<int, string>? Assignments   { get; set; }
            public Dictionary<int, long>?   AssignmentIds { get; set; }
            public Dictionary<long, int>?   FarmTypes     { get; set; }
        }

        // ── Multiplayer message payloads ──────────────────────────────────────

        public class FarmChosenPayload
        {
            public int FarmType { get; set; }
        }

        public class SyncPayload
        {
            public Dictionary<int, string>? Assignments   { get; set; }
            public Dictionary<int, long>?   AssignmentIds { get; set; }
            public Dictionary<long, int>?   FarmTypes     { get; set; }
        }
    }
}
