using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

using Terraria;
using TerrariaApi;
using TerrariaApi.Server;

using TShockAPI;

namespace RegionVision
{
    /// <summary>
    /// Associates data with an online player
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The player's index number
        /// </summary>
        public int index { get; private set; }
        /// <summary>
        /// Returns this player's TSPlayer instance
        /// </summary>
        public TSPlayer TSPlayer { get { return TShock.Players[index]; } }
        /// <summary>
        /// The list of regions this player is viewing
        /// </summary>
        public List<Region> regions { get; private set; }

        /// <summary>
        /// Creates a new instance of the Player class
        /// </summary>
        /// <param name="index">The player's index number</param>
        public Player(int index)
        {
            this.index = index;
            this.regions = new List<Region>();
        }
    }

    /// <summary>
    /// Represents a TShock region and stores some data about how it's being shown to a player
    /// </summary>
    public class Region
    {
        /// <summary>
        /// Returns the name of the region
        /// </summary>
        public string name { get; private set; }
        /// <summary>
        /// The area bounded by the region
        /// </summary>
        public Rectangle area;
        /// <summary>
        /// The part of the region border that will be shown to players
        /// </summary>
        public Rectangle showArea;
        /// <summary>
        /// Returns the colour of paint used on phantom tiles for this region's border
        /// </summary>
        public byte colour { get; private set; }

        private Tile[] realTile;

        /// <summary>
        /// The maximum width or height of a region border that will be shown
        /// </summary>
        public const int MaximumSize = 256;

        /// <summary>
        /// Creates a new instance of the Region class.
        /// </summary>
        /// <param name="name">The name of the region</param>
        /// <param name="area">The area bounded by the region</param>
        public Region(string name, Rectangle area) {
            this.name = name;
            this.area = area;
            this.showArea = area;

            int total = 0;
            for (int i = 0; i < name.Length; i++) total += (int) name[i];
            colour = (byte) (total % 12);
        }

        /// <summary>
        /// Calculates what part of the region border to show for a large region.
        /// </summary>
        public void calculateArea(TSPlayer tPlayer) {
            this.showArea = this.area;

            // If the region is large, only part of its border will be shown.
            if (this.showArea.Width >= MaximumSize) {
                this.showArea.X = (int) (tPlayer.X / 16) - MaximumSize / 2;
                this.showArea.Width = MaximumSize - 1;
                if (this.showArea.Left < this.area.Left) this.showArea.X = this.area.Left;
                else if (this.showArea.Right > this.area.Right) this.showArea.X = this.area.Right - (MaximumSize - 1);
            }
            if (this.showArea.Height >= MaximumSize) {
                this.showArea.Y = (int) (tPlayer.Y / 16) - MaximumSize / 2;
                this.showArea.Height = MaximumSize - 1;
                if (this.showArea.Top < this.area.Top) this.showArea.Y = this.area.Top;
                else if (this.showArea.Bottom > this.area.Bottom) this.showArea.Y = this.area.Bottom - (MaximumSize - 1);
            }

            // Ensure the region boundary is within the world.
            if      (this.showArea.Left < 1) this.showArea.X = 1;
            else if (this.showArea.Left >= Main.maxTilesX - 1) this.showArea.X = Main.maxTilesX - 1;

            if      (this.showArea.Top  < 1) this.showArea.Y = 1;
            else if (this.showArea.Top  >= Main.maxTilesY - 1) this.showArea.Y = Main.maxTilesY - 1;

            if (this.showArea.Right  >= Main.maxTilesX - 1) this.showArea.Width  = Main.maxTilesX - this.showArea.X - 2;
            if (this.showArea.Bottom >= Main.maxTilesY - 1) this.showArea.Height = Main.maxTilesY - this.showArea.Y - 2;
       }

        /// <summary>
        /// Spawns fake tiles for the region border
        /// </summary>
        /// <exception cref="InvalidOperationException">Fake tiles have already been set, which would cause a desync.</exception>
        public void setFakeTiles()
        {
            int d; int index = 0;

            if (this.realTile != null) throw new InvalidOperationException("Fake tiles have already been set for the region.");

            // Initialise the temporary tile array.
            if (this.showArea.Width == 0)
                this.realTile = new Tile[this.showArea.Height + 1];
            else if (this.showArea.Height == 0)
                this.realTile = new Tile[this.showArea.Width + 1];
            else
                this.realTile = new Tile[(this.showArea.Width + this.showArea.Height) * 2];

            // Top boundary
            if (this.showArea.Top == this.area.Top)
                for (d = 0; d <= this.showArea.Width ; d++) setFakeTile(index++, this.showArea.Left  + d, this.showArea.Top       );
            // East boundary
            if (this.showArea.Right == this.area.Right)
                for (d = 1; d <= this.showArea.Height; d++) setFakeTile(index++, this.showArea.Right    , this.showArea.Top + d   );
            // West boundary
            if (this.showArea.Width > 0 && this.showArea.Left == this.area.Left)
                for (d = 1; d <= this.showArea.Height; d++) setFakeTile(index++, this.showArea.Left     , this.showArea.Top    + d);
            // Bottom boundary
            if (this.showArea.Height > 0 && this.showArea.Bottom == this.area.Bottom)
                for (d = 1; d <  this.showArea.Width ; d++) setFakeTile(index++, this.showArea.Left  + d, this.showArea.Bottom    );
        }

        /// <summary>
        /// Removes fake tiles for the region, reverting to the real tiles.
        /// </summary>
        /// <exception cref="InvalidOperationException">Fake tiles have not been set.</exception>
        public void unsetFakeTiles()
        {
            int d; int index = 0;

            if (this.realTile == null) throw new InvalidOperationException("Fake tiles have not been set for the region.");

            // Top boundary
            if (this.showArea.Top == this.area.Top)
                for (d = 0; d <= this.showArea.Width; d++) unsetFakeTile(index++, this.showArea.Left + d, this.showArea.Top);
            // East boundary
            if (this.showArea.Right == this.area.Right)
                for (d = 1; d <= this.showArea.Height; d++) unsetFakeTile(index++, this.showArea.Right, this.showArea.Top + d);
            // West boundary
            if (this.showArea.Width > 0 && this.showArea.Left == this.area.Left)
                for (d = 1; d <= this.showArea.Height; d++) unsetFakeTile(index++, this.showArea.Left, this.showArea.Top + d);
            // Bottom boundary
            if (this.showArea.Height > 0 && this.showArea.Bottom == this.area.Bottom)
                for (d = 1; d < this.showArea.Width; d++) unsetFakeTile(index++, this.showArea.Left + d, this.showArea.Bottom);

            this.realTile = null;
        }

        /// <summary>Adds a single fake tile. If a tile exists, this will replace it with a painted clone. Otherwise, this will place an inactive magical ice tile with the same paint.</summary>
        /// <param name="index">The index in the realTile array into which to store the existing tile</param>
        /// <param name="x">The x coordinate of the tile position</param>
        /// <param name="y">The y coordinate of the tile position</param>
        public void setFakeTile(int index, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;

            this.realTile[index] = Main.tile[x, y];
            Tile fakeTile;
            if (this.realTile[index] == null) fakeTile = new Tile();
            else fakeTile = new Tile(this.realTile[index]);
            if (this.realTile[index] != null && this.realTile[index].active()) {
                if (this.realTile[index].type == Terraria.ID.TileID.RainbowBrick) fakeTile.type = Terraria.ID.TileID.GrayBrick;
                fakeTile.color(this.colour);
            } else {
                if (Main.rand == null) Main.rand = new Random();
                fakeTile.active(true);
                fakeTile.inActive(true);
                fakeTile.type = Terraria.ID.TileID.MagicalIceBlock;
                fakeTile.frameX = (short) (162 + Main.rand.Next(0, 2) * 18);
                fakeTile.frameY = 54;
                fakeTile.color(this.colour);
            }
            Main.tile[x, y] = fakeTile;
        }

        /// <summary>Removes a single fake tile, reverting to the real tile</summary>
        /// <param name="index">The index in the realTile array from which to retrieve the existing tile</param>
        /// <param name="x">The x coordinate of the tile position</param>
        /// <param name="y">The y coordinate of the tile position</param>
        public void unsetFakeTile(int index, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
            Main.tile[x, y] = this.realTile[index];
        }

        /// <summary>Sends tile updates for a region border to a client.</summary>
        /// <param name="player">The player to send to</param>
        public void refresh(TShockAPI.TSPlayer player) {
            // Due to the way the Rectangle class works, the Width and Height values are one tile less than the actual dimensions of the region.
            if (this.showArea.Width <= 3 || this.showArea.Height <= 3) {
                player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left - 1, this.showArea.Top - 1, this.showArea.Width + 3, this.showArea.Height + 3, 0);
            } else {
                if (this.showArea.Top    == this.area.Top   )
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left  - 1, this.showArea.Top    - 1, this.showArea.Width + 3,                    3, 0);
                if (this.showArea.Left   == this.area.Left  )
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left  - 1, this.showArea.Top    + 2,                       3, this.showArea.Height, 0);
                if (this.showArea.Right  == this.area.Right )
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Right - 1, this.showArea.Top    + 2,                       3, this.showArea.Height, 0);
                if (this.showArea.Bottom == this.area.Bottom)
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left  + 2, this.showArea.Bottom - 1, this.showArea.Width - 3,                    3, 0);
            }

            player.SendData(PacketTypes.TileFrameSection, "", (int) (this.showArea.Left / 200), (int) (this.showArea.Top / 150), (int) (this.showArea.Right / 200), (int) (this.showArea.Bottom / 150), 0);
        }
    }

    [ApiVersion(1, 16)]
    public class RegionVisionPlugin : TerrariaPlugin
    {
        public List<Player> players { get; private set; }
        private Timer refreshTimer = new Timer(30000);

        public override Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public override string Name
        {
            get { return "Region Vision"; }
        }

        public override string Author
        {
            get { return "Andrio Celos"; }
        }

        public override string Description
        {
            get { return "See your regions"; }
        }

        /// <summary>
        /// The array from which the colour of the 'You are now viewing...' message is retrieved, to match the colour of the border
        /// </summary>
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

        public RegionVisionPlugin(Main game)
            : base(game)
        {
            players = new List<Player>();
            Order = 1;
        }

        public override void Initialize()
        {
            Command viewCommand = new Command("regionview", commandView, new string[] { "regionview", "rv" });
            viewCommand.AllowServer = false;
            Commands.ChatCommands.Add(viewCommand);

            Command clearCommand = new Command("regionview", commandClear, new string[] { "regionclear", "rc" });
            clearCommand.AllowServer = false;
            Commands.ChatCommands.Add(clearCommand);

            GetDataHandlers.TileEdit += TShockAPI.HandlerList<GetDataHandlers.TileEditEventArgs>.Create(OnTileEdit, HandlerPriority.High, false);
            ServerApi.Hooks.ServerJoin.Register(this, onPlayerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, onPlayerLeave);
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += onPlayerCommand;

            refreshTimer.AutoReset = false;
            refreshTimer.Elapsed += refreshTimer_Elapsed;
        }

        /// <summary>
        /// Returns the Player instance for a given index number
        /// </summary>
        public Player findPlayer(int index) {
            foreach (Player player in players)
                if (player.index == index) return player;
            return null;
        }

        private void commandView(CommandArgs args)
        {
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
                args.Player.SendErrorMessage("Region {0} contains no tiles. (Found dimensions: {1} × {2}) Use  /region resize  to fix it.", tRegion.Name, tRegion.Area.Width, tRegion.Area.Height);
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

                args.Player.SendMessage("You are now viewing " + region.name + ".", textColour[region.colour]);
                refreshTimer.Interval = 30000;
                refreshTimer.Enabled = true;
            }
        }

        private void commandClear(CommandArgs args) {
            lock (players) {
                Player player = findPlayer(args.Player.Index);
                if (player == null) return;
                clearRegions(player);
            }
        }

        /// <summary>
        /// Removes all region borders from a player's view
        /// </summary>
        /// <param name="player">The player to reset</param>
        public void clearRegions(Player player) {
            foreach (Region region in player.regions)
                region.refresh(player.TSPlayer);
            player.regions.Clear();
        }

        private void OnTileEdit(object sender, TShockAPI.GetDataHandlers.TileEditEventArgs e)
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
                        clearRegions(player);
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

        /// <summary>
        /// Checks whether a player's attempted tile edit is valid. A tile edit is considered invalid if it's only possible due to the presence of phantom tiles.
        /// </summary>
        /// <param name="region">The region to check</param>
        /// <param name="x">The x coordinate of the edited tile</param>
        /// <param name="y">The y coordinate of the edited tile</param>
        /// <param name="editType">The type of the edit</param>
        /// <returns>true if the edit was valid; false if it wasn't</returns>
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

        private void onPlayerJoin(TerrariaApi.Server.JoinEventArgs e) {
            lock (players)
                players.Add(new Player(e.Who));
        }

        private void onPlayerLeave(TerrariaApi.Server.LeaveEventArgs e) {
            lock (players)
                for (int i = 0; i < players.Count; i++) {
                    if (players[i].index == e.Who) {
                        players.RemoveAt(i);
                        break;
                    }
                }
        }

        /// <summary>
        /// Returns the item used to attempt a rejected foreground tile edit to the player
        /// </summary>
        /// <param name="player">The player attempting the edit</param>
        /// <param name="e">The data from the edit event</param>
        void giveTile(Player player, TShockAPI.GetDataHandlers.TileEditEventArgs e) {
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

        /// <summary>
        /// Returns the item used to attempt a rejected background wall edit to the player
        /// </summary>
        /// <param name="player">The player attempting the edit</param>
        /// <param name="e">The data from the edit event</param>
        void giveWall(Player player, TShockAPI.GetDataHandlers.TileEditEventArgs e) {
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

        /// <summary>
        /// Gives an item to a player
        /// </summary>
        /// <param name="player">The player to receive the item</param>
        /// <param name="item">The item to give</param>
        void giveItem(Player player, Item item) {
            int itemID = Item.NewItem((int) player.TSPlayer.X, (int) player.TSPlayer.Y, item.width, item.height, item.type, 1, true, 0, true);
            Terraria.Main.item[itemID].owner = player.index;
            NetMessage.SendData((int) PacketTypes.ItemDrop, -1, -1, "", itemID, 0f, 0f, 0f);
            NetMessage.SendData((int) PacketTypes.ItemOwner, -1, -1, "", itemID, 0f, 0f, 0f);
        }

        private void onPlayerCommand(TShockAPI.Hooks.PlayerCommandEventArgs e) {
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
                refreshTimer.Interval = 30000;
                refreshTimer.Enabled = true;
            }
        }

    }
}
