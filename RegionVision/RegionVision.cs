using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

using Terraria;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace RegionVision {
    [ApiVersion(1, 26)]
    public class RegionVisionPlugin : TerrariaPlugin {
        /// <summary>The list of players being tracked by this plugin.</summary>
        public List<Player> players { get; }

        public override Version Version => new Version(1, 2, 7, 2);
        public override string Name => "Region Vision";
        public override string Author => "Andrio Celos";
        public override string Description => "See your regions.";

        /// <summary>The range, in tiles, within which region borders may be automatically shown.</summary>
        public const int NearRange = 100;

        /// <summary>The array from which the colour of the 'You are now viewing...' message is retrieved, to match the colour of the border.</summary>
        public readonly static Color[] textColour = {new Color(244,  93,  93),
                                                     new Color(244, 169,  93),
                                                     new Color(244, 244,  93),
                                                     new Color(169, 244,  93),
                                                     new Color( 93, 244,  93),
                                                     new Color( 93, 244, 169),
                                                     new Color( 93, 244, 244),
                                                     new Color( 93, 169, 244),
                                                     new Color( 93,  93, 244),
                                                     new Color(169,  93, 244),
                                                     new Color(244,  93, 244),
                                                     new Color(244,  93, 169)};

        private Timer refreshTimer = new Timer(5000);

        public RegionVisionPlugin(Main game) : base(game) {
            players = new List<Player>();
            Order = 1;
        }

        public override void Initialize() {
            Command viewCommand = new Command(new List<string>(new string[] { "regionvision.regionview", "regionview" }),
                commandView, new string[] { "regionview", "rv" });
            viewCommand.AllowServer = false;
            viewCommand.HelpDesc = new string[] { "Usage: /rv <region name>", "Shows you the boundary of the specified region" };
            Commands.ChatCommands.Add(viewCommand);

            Command clearCommand = new Command(new List<string>(new string[] { "regionvision.regionview", "regionview" }),
                commandClear, new string[] { "regionclear", "rc" });
            clearCommand.AllowServer = false;
            clearCommand.HelpDesc = new string[] { "Usage: /rc", "Removes all region borders from your view" };
            Commands.ChatCommands.Add(clearCommand);

            Command viewNearbyCommand = new Command(new List<string>(new string[] { "regionvision.regionviewnear", "regionviewnear" }),
                commandViewNearby, new string[] { "regionviewnear", "rvn" });
            viewNearbyCommand.AllowServer = false;
            viewNearbyCommand.HelpDesc = new string[] { "Usage: /rvn", "Turns on or off automatic showing of regions near you" };
            Commands.ChatCommands.Add(viewNearbyCommand);

            GetDataHandlers.TileEdit += TShockAPI.HandlerList<GetDataHandlers.TileEditEventArgs>.Create(OnTileEdit, HandlerPriority.High, false);
            TShockAPI.Hooks.RegionHooks.RegionCreated += RegionHooks_RegionCreated;
            TShockAPI.Hooks.RegionHooks.RegionDeleted += RegionHooks_RegionDeleted;
            ServerApi.Hooks.ServerJoin.Register(this, onPlayerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, onPlayerLeave);
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += onPlayerCommand;

            refreshTimer.AutoReset = false;
            refreshTimer.Elapsed += refreshTimer_Elapsed;
        }

        void RegionHooks_RegionDeleted(RegionHooks.RegionDeletedEventArgs args) {
            if (args.Region.WorldID != Main.worldID.ToString()) return;

            // If any players were viewing this region, clear its border.
            lock (players) {
                foreach (Player player in players) {
                    for (int i = 0; i < player.regions.Count; i++) {
                        Region region = player.regions[i];
                        if (region.name.Equals(args.Region.Name)) {
                            player.TSPlayer.SendMessage("Region " + region.name + " has been deleted.", textColour[region.colour - 13]);
                            region.refresh(player.TSPlayer);
                            player.regions.RemoveAt(i);

                            foreach (Region region2 in player.regions)
                                region2.setFakeTiles();
                            foreach (Region region2 in player.regions)
                                region2.refresh(player.TSPlayer);
                            foreach (Region region2 in player.regions.Reverse<Region>())
                                region2.unsetFakeTiles();

                            break;
                        }
                    }
                }
            }
        }
        void RegionHooks_RegionCreated(RegionHooks.RegionCreatedEventArgs args) {
            refreshTimer.Stop();
            refreshTimer_Elapsed(this, null);
        }

        /// <summary>Returns the <see cref="Player"/> instance for the player with a given index number.</summary>
        public Player findPlayer(int index) {
            foreach (Player player in players)
                if (player.index == index) return player;
            return null;
        }

        private void commandView(CommandArgs args) {
            TShockAPI.DB.Region tRegion = null;
            List<TShockAPI.DB.Region> matches = new List<TShockAPI.DB.Region>();

            if (args.Parameters.Count < 1) {
                args.Player.SendErrorMessage("Usage: /regionview <region name>");
                return;
            } 

            // Find the specified region.
            for (int pass = 1; pass <= 3 && tRegion == null && matches.Count == 0; pass++) {
                foreach (TShockAPI.DB.Region _tRegion in TShock.Regions.Regions) {
                    switch (pass) {
                        case 1:  // Pass 1: exact match
                            if (_tRegion.Name == args.Parameters[0]) {
                                tRegion = _tRegion;
                                break;
                            } else if (_tRegion.Name.Equals(args.Parameters[0], StringComparison.OrdinalIgnoreCase))
                                matches.Add(_tRegion);
                            break;
                        case 2:  // Pass 2: case-sensitive partial match
                            if (_tRegion.Name.StartsWith(args.Parameters[0]))
                                matches.Add(_tRegion);
                            break;
                        case 3:  // Pass 3: case-insensitive partial match
                            if (_tRegion.Name.StartsWith(args.Parameters[0], StringComparison.OrdinalIgnoreCase))
                                matches.Add(_tRegion);
                            break;
                    }
                    if (tRegion != null) break;
                }
            }

            if (tRegion == null) {
                if (matches.Count == 1) {
                    tRegion = matches[0];
                } else if (matches.Count == 0) {
                    args.Player.SendErrorMessage("No such region exists.");
                    return;
                } else if (matches.Count > 5) {
                    args.Player.SendErrorMessage("Multiple matching regions were found: {0} and {1} more. Please be more specific.", string.Join(", ", matches.Take(5).Select(r => r.Name)), matches.Count - 5);
                    return;
                } else if (matches.Count > 1) {
                    args.Player.SendErrorMessage("Multiple matching regions were found: {0}. Please be more specific.", string.Join(", ", matches.Select(r => r.Name)));
                    return;
                }
            }

            if (tRegion.Area.Width < 0 || tRegion.Area.Height < 0) {
                args.Player.SendErrorMessage("Region {0} contains no tiles. (Found dimensions: {1} × {2})\nUse [c/FF8080:/region resize] to fix it.", tRegion.Name, tRegion.Area.Width, tRegion.Area.Height);
                return;
            }

            lock (players) {
                Player player = findPlayer(args.Player.Index);
                if (player == null) return;

                // Register this region.
                Region region = player.regions.FirstOrDefault(r => r.name == tRegion.Name);
                if (region == null)
                    region = new Region(tRegion.Name, tRegion.Area);
                else
                    player.regions.Remove(region);

                foreach (Region _region in player.regions)
                    _region.setFakeTiles();
                if (region.showArea != region.area) region.refresh(player.TSPlayer);
                player.regions.Add(region);
                region.calculateArea(args.Player);
                region.setFakeTiles();
                region.refresh(player.TSPlayer);

                foreach (Region _region in player.regions.Reverse<Region>())
                    _region.unsetFakeTiles();

                string message = "You are now viewing " + region.name + ".";
                // Show how large the region is if it's large.
                if (tRegion.Area.Width >= Region.MaximumSize || tRegion.Area.Height >= Region.MaximumSize) {
                    int num; int num2;
                    if (tRegion.Area.Bottom < args.Player.TileY) {
                        num = args.Player.TileY - tRegion.Area.Bottom;
                        message += " Borders are " + num + (num == 1 ? " tile" : " tiles") + " above you";
                    } else if (tRegion.Area.Top > args.Player.TileY) {
                        num = tRegion.Area.Top - args.Player.TileY;
                        message += " Borders are " + (tRegion.Area.Top - args.Player.TileY) + (num == 1 ? " tile" : " tiles") + " below you";
                    } else {
                        num = args.Player.TileY - tRegion.Area.Top;
                        num2 = tRegion.Area.Bottom - args.Player.TileY;
                        message += " Borders are " + (args.Player.TileY - tRegion.Area.Top) + (num == 1 ? " tile" : " tiles") + " above, " +
                            (tRegion.Area.Bottom - args.Player.TileY) + (num2 == 1 ? " tile" : " tiles") + " below you";
                    }
                    if (tRegion.Area.Right < args.Player.TileX) {
                        num = args.Player.TileX - tRegion.Area.Right;
                        message += ", " + (args.Player.TileX - tRegion.Area.Right) + (num == 1 ? " tile" : " tiles") + " west of you.";
                    } else if (tRegion.Area.Left > args.Player.TileX) {
                        num = tRegion.Area.Left - args.Player.TileX;
                        message += ", " + (tRegion.Area.Left - args.Player.TileX) + (num == 1 ? " tile" : " tiles") + " east of you.";
                    } else {
                        num = args.Player.TileX - tRegion.Area.Left;
                        num2 = tRegion.Area.Right - args.Player.TileX;
                        message += ", " + (args.Player.TileX - tRegion.Area.Left) + (num == 1 ? " tile" : " tiles") + " west, " +
                            (tRegion.Area.Right - args.Player.TileX) + (num2 == 1 ? " tile" : " tiles") + " east of you.";
                    }
                }
                args.Player.SendMessage(message, textColour[region.colour - 13]);

                refreshTimer.Interval = 7000;
                refreshTimer.Enabled = true;
            }
        }

        private void commandClear(CommandArgs args) {
            lock (players) {
                Player player = findPlayer(args.Player.Index);
                if (player == null) return;
                player.viewingNearby = false;
                clearRegions(player);
            }
        }

        private void commandViewNearby(CommandArgs args) {
            lock (players) {
                Player player = findPlayer(args.Player.Index);
                if (player == null) return;

                if (player.viewingNearby) {
                    player.viewingNearby = false;
                    args.Player.SendInfoMessage("You are no longer viewing regions near you.");
                } else {
                    player.viewingNearby = true;
                    args.Player.SendInfoMessage("You are now viewing regions near you.");
                    refreshTimer.Interval = 1500;
                    refreshTimer.Enabled = true;
                }
            }
        }

        /// <summary>Removes all region borders from a player's view.</summary>
        /// <param name="player">The player to reset</param>
        public void clearRegions(Player player) {
            foreach (Region region in player.regions)
                region.refresh(player.TSPlayer);
            player.regions.Clear();
        }

        private void OnTileEdit(object sender, GetDataHandlers.TileEditEventArgs e)
        {
            if (e.Action > GetDataHandlers.EditAction.KillTileNoItem ||
                e.Action == GetDataHandlers.EditAction.KillWall) return;
            if (e.Action == GetDataHandlers.EditAction.PlaceTile && e.EditData == Terraria.ID.TileID.MagicalIceBlock) return;

            lock (players) {
                Player player = findPlayer(e.Player.Index);
                if (player == null) return;
                if (player.regions.Count == 0) return;

                // Stop the edit if a phantom tile is the only thing making it possible.
                foreach (Region region in player.regions) {
                    // Clear the region borders if they break one of the phantom ice blocks.
                    if ((e.Action == GetDataHandlers.EditAction.KillTile || e.Action == GetDataHandlers.EditAction.KillTileNoItem) && (Main.tile[e.X, e.Y] == null || !Main.tile[e.X, e.Y].active()) &&
                        e.X >= region.showArea.Left - 1 && e.X <= region.showArea.Right + 1 && e.Y >= region.showArea.Top - 1 && e.Y <= region.showArea.Bottom + 1 &&
                        !(e.X >= region.showArea.Left + 2 && e.X <= region.showArea.Right - 2 && e.Y >= region.showArea.Top + 2 && e.Y <= region.showArea.Bottom - 2)) {
                        e.Handled = true;
                        //clearRegions(player);
                        break;
                    }
                    if ((e.Action == GetDataHandlers.EditAction.PlaceTile || e.Action == GetDataHandlers.EditAction.PlaceWall) && !tileValidityCheck(region, e.X, e.Y, e.Action)) {
                        e.Handled = true;
                        player.TSPlayer.SendData(PacketTypes.TileSendSquare, "", 1, e.X, e.Y, 0, 0);
                        if (e.Action == GetDataHandlers.EditAction.PlaceTile) giveTile(player, e);
                        if (e.Action == GetDataHandlers.EditAction.PlaceWall) giveWall(player, e);
                        break;
                    }
                }
                if (e.Handled) clearRegions(player);
            }
        }

        /// <summary>Checks whether a player's attempted tile edit is valid. A tile edit is considered invalid if it's only possible due to the presence of phantom tiles.</summary>
        /// <param name="region">The region to check.</param>
        /// <param name="x">The x coordinate of the edited tile.</param>
        /// <param name="y">The y coordinate of the edited tile.</param>
        /// <param name="editType">The type of the edit.</param>
        /// <returns>true if the edit was valid; false if it wasn't.</returns>
        public bool tileValidityCheck(Region region, int x, int y, GetDataHandlers.EditAction editType) {
            // Check if there's a wall or another tile next to this tile.
            if (editType == GetDataHandlers.EditAction.PlaceWall) {
                if (Main.tile[x, y] != null && Main.tile[x, y].active()) return true;
                if (Main.tile[x - 1, y] != null && ((Main.tile[x - 1, y].active() && !Main.tileNoAttach[Main.tile[x - 1, y].type]) || Main.tile[x - 1, y].wall > 0)) return true;
                if (Main.tile[x + 1, y] != null && ((Main.tile[x + 1, y].active() && !Main.tileNoAttach[Main.tile[x + 1, y].type]) || Main.tile[x + 1, y].wall > 0)) return true;
                if (Main.tile[x, y - 1] != null && ((Main.tile[x, y - 1].active() && !Main.tileNoAttach[Main.tile[x, y - 1].type]) || Main.tile[x, y - 1].wall > 0)) return true;
                if (Main.tile[x, y + 1] != null && ((Main.tile[x, y + 1].active() && !Main.tileNoAttach[Main.tile[x, y + 1].type]) || Main.tile[x, y + 1].wall > 0)) return true;
            } else {
                if (Main.tile[x, y] != null && Main.tile[x, y].wall > 0) return true;
                if (Main.tile[x - 1, y] != null && Main.tile[x - 1, y].wall > 0) return true;
                if (Main.tile[x + 1, y] != null && Main.tile[x + 1, y].wall > 0) return true;
                if (Main.tile[x, y - 1] != null && Main.tile[x, y - 1].wall > 0) return true;
                if (Main.tile[x, y + 1] != null && Main.tile[x, y + 1].wall > 0) return true;
                if (Main.tile[x - 1, y] != null && Main.tile[x - 1, y].active() && !Main.tileNoAttach[Main.tile[x - 1, y].type]) return true;
                if (Main.tile[x + 1, y] != null && Main.tile[x + 1, y].active() && !Main.tileNoAttach[Main.tile[x + 1, y].type]) return true;
                if (Main.tile[x, y - 1] != null && Main.tile[x, y - 1].active() && !Main.tileNoAttach[Main.tile[x, y - 1].type]) return true;
                if (Main.tile[x, y - 1] != null && Main.tile[x, y + 1].active() && !Main.tileNoAttach[Main.tile[x, y + 1].type]) return true;
            }

            // Check if this tile is next to a region boundary.
            if (x >= region.showArea.Left - 1 && x <= region.showArea.Right + 1 && y >= region.showArea.Top - 1 && y <= region.showArea.Bottom + 1 &&
                !(x >= region.showArea.Left + 2 && x <= region.showArea.Right - 2 && y >= region.showArea.Top + 2 && y <= region.showArea.Bottom - 2))
                    return false;
            return true;
        }

        private void onPlayerJoin(JoinEventArgs e) {
            lock (players)
                players.Add(new Player(e.Who));
        }

        private void onPlayerLeave(LeaveEventArgs e) {
            lock (players)
                for (int i = 0; i < players.Count; i++) {
                    if (players[i].index == e.Who) {
                        players.RemoveAt(i);
                        break;
                    }
                }
        }

        /// <summary>Returns the item used to attempt a rejected foreground tile edit to the player.</summary>
        /// <param name="player">The player attempting the edit</param>
        /// <param name="e">The data from the edit event</param>
        public void giveTile(Player player, GetDataHandlers.TileEditEventArgs e) {
            Item item = new Item(); bool found = false;
            for (int i = 1; i <= Terraria.ID.ItemID.Count; i++) {
                item.SetDefaults(i, true);
                if (item.createTile == e.EditData && item.placeStyle == e.Style) {
                    if (item.tileWand != -1)  item.SetDefaults(item.tileWand, true);
                    found = true;
                    break;
                }
            }
            if (found) giveItem(player, item);
        }

        /// <summary>Returns the item used to attempt a rejected background wall edit to the player.</summary>
        /// <param name="player">The player attempting the edit</param>
        /// <param name="e">The data from the edit event</param>
        public void giveWall(Player player, GetDataHandlers.TileEditEventArgs e) {
            Item item = new Item(); bool found = false;
            for (int i = 1; i <= Terraria.ID.ItemID.Count; i++) {
                item.SetDefaults(i, true);
                if (item.createWall == e.EditData) {
                    found = true;
                    break;
                }
            }
            if (found) giveItem(player, item);
        }

        /// <summary>Gives an item to a player.</summary>
        /// <param name="player">The player to receive the item</param>
        /// <param name="item">The item to give</param>
        public void giveItem(Player player, Item item) {
            int itemID = Item.NewItem((int) player.TSPlayer.X, (int) player.TSPlayer.Y, item.width, item.height, item.type, 1, true, 0, true);
            Main.item[itemID].owner = player.index;
            NetMessage.SendData((int) PacketTypes.ItemDrop, -1, -1, "", itemID, 0f, 0f, 0f);
            NetMessage.SendData((int) PacketTypes.ItemOwner, -1, -1, "", itemID, 0f, 0f, 0f);
        }

        private void onPlayerCommand(PlayerCommandEventArgs e) {
            if (e.Parameters.Count >= 2 && e.CommandName.ToLower() == "region" && new string[] {"delete", "resize", "expand"}.Contains(e.Parameters[0].ToLower())) {
                if (Commands.ChatCommands.Any(c => c.HasAlias("region") && c.CanRun(e.Player)))
                    refreshTimer.Interval = 1500;
            }
        }

        private void refreshTimer_Elapsed(object sender, ElapsedEventArgs e) {
            bool anyRegions = false;

            // Check for regions that have changed.
            lock (players) {
                foreach (Player player in players) {
                    bool refreshFlag = false;

                    for (int i = 0; i < player.regions.Count; i++) {
                        Region region = player.regions[i];
                        TShockAPI.DB.Region tRegion = TShock.Regions.GetRegionByName(region.name);

                        if (tRegion == null) {
                            // The region was removed.
                            refreshFlag = true;
                            region.refresh(player.TSPlayer);
                            player.regions.RemoveAt(i--);
                        } else {
                            Rectangle newArea = tRegion.Area;
                            if (!region.command && (!player.viewingNearby || !isPlayerNearby(player.TSPlayer, region.area))) {
                                // The player is no longer near the region.
                                refreshFlag = true;
                                region.refresh(player.TSPlayer);
                                player.regions.RemoveAt(i--);
                            } else
                            if (newArea != region.area) {
                                // The region was resized.
                                if (newArea.Width < 0 || newArea.Height < 0) {
                                    refreshFlag = true;
                                    region.refresh(player.TSPlayer);
                                    player.regions.RemoveAt(i--);
                                } else {
                                    anyRegions = true;
                                    refreshFlag = true;
                                    region.refresh(player.TSPlayer);
                                    region.area = newArea;
                                    region.calculateArea(player.TSPlayer);
                                }
                            } else {
                                anyRegions = true;
                            }
                        }
                    }

                    if (player.viewingNearby) {
                        anyRegions = true;

                        // Search for nearby regions
                        foreach (TShockAPI.DB.Region tRegion in TShock.Regions.Regions) {
                            if (tRegion.WorldID == Main.worldID.ToString() && tRegion.Area.Width >= 0 && tRegion.Area.Height >= 0) {
                                if (isPlayerNearby(player.TSPlayer, tRegion.Area)) {
                                    if (!player.regions.Any(r => r.name == tRegion.Name)) {
                                        refreshFlag = true;
                                        Region region = new Region(tRegion.Name, tRegion.Area, false);
                                        region.calculateArea(player.TSPlayer);
                                        player.regions.Add(region);
                                        player.TSPlayer.SendMessage("You see region " + region.name + ".", textColour[region.colour - 13]);
                                    }
                                }
                            }
                        }
                    }

                    if (refreshFlag) {
                        foreach (Region region in player.regions)
                            region.setFakeTiles();
                        foreach (Region region in player.regions)
                            region.refresh(player.TSPlayer);
                        foreach (Region region in player.regions.Reverse<Region>())
                            region.unsetFakeTiles();
                    }
                }
            }

            if (anyRegions) {
                refreshTimer.Interval = 7000;
                refreshTimer.Enabled = true;
            }
        }

        /// <summary>Checks whether a given player is near a given region.</summary>
        /// <param name="tPlayer">The player to check</param>
        /// <param name="area">The region to check</param>
        /// <returns>true if the player is within 100 tiles of the region; false otherwise</returns>
        public static bool isPlayerNearby(TSPlayer tPlayer, Rectangle area) {
            int playerX = (int) (tPlayer.X / 16);
            int playerY = (int) (tPlayer.Y / 16);

            return playerX >= area.Left   - NearRange &&
                   playerX <= area.Right  + NearRange &&
                   playerY >= area.Top    - NearRange &&
                   playerY <= area.Bottom + NearRange;
        }
    }
}
