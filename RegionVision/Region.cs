using System;

using Terraria;
using TShockAPI;

namespace RegionVision {
    /// <summary>Represents a TShock region and stores some data about how it's being shown to a player.</summary>
    public class Region {
        /// <summary>Returns the name of the region.</summary>
        public string name { get; private set; }
        /// <summary>The area bounded by the region.</summary>
        public Rectangle area;
        /// <summary>The part of the region border that will be shown to players.</summary>
        /// <remarks>This will be a small part of the region if the region is very large.</remarks>
        public Rectangle showArea;
        /// <summary>Returns the colour of paint used on phantom tiles for this region's border.</summary>
        public byte colour { get; private set; }
        /// <summary>True if the region was selected using a command; false if it's visible only because it's near the player.</summary>
        public bool command { get; set; }

        private Tile[] realTile;   // Holds the real tiles of the region while the phantom ones are created.

        /// <summary>The maximum width or height of a region border that will be shown.</summary>
        public const int MaximumSize = 256;

        /// <summary>Creates a new instance of the Region class.</summary>
        /// <param name="name">The name of the region</param>
        /// <param name="area">The area bounded by the region</param>
        /// <param name="command">Set to false if the region was automatically shown.</param>
        public Region(string name, Rectangle area, bool command = true) {
            this.name = name;
            this.area = area;
            this.showArea = area;
            this.command = command;

            int total = 0;
            for (int i = 0; i < name.Length; i++) total += (int) name[i];
            colour = (byte) (total % 12 + 13);
        }

        /// <summary>Calculates what part of the region border to show for a large region.</summary>
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
            if (this.showArea.Left < 1) this.showArea.X = 1;
            else if (this.showArea.Left >= Main.maxTilesX - 1) this.showArea.X = Main.maxTilesX - 1;

            if (this.showArea.Top < 1) this.showArea.Y = 1;
            else if (this.showArea.Top >= Main.maxTilesY - 1) this.showArea.Y = Main.maxTilesY - 1;

            if (this.showArea.Right >= Main.maxTilesX - 1) this.showArea.Width = Main.maxTilesX - this.showArea.X - 2;
            if (this.showArea.Bottom >= Main.maxTilesY - 1) this.showArea.Height = Main.maxTilesY - this.showArea.Y - 2;
        }

        /// <summary>Spawns fake tiles for the region border.</summary>
        /// <exception cref="InvalidOperationException">Fake tiles have already been set, which would cause a desync.</exception>
        public void setFakeTiles() {
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
                for (d = 0; d <= this.showArea.Width; d++) setFakeTile(index++, this.showArea.Left + d, this.showArea.Top);
            // East boundary
            if (this.showArea.Right == this.area.Right)
                for (d = 1; d <= this.showArea.Height; d++) setFakeTile(index++, this.showArea.Right, this.showArea.Top + d);
            // West boundary
            if (this.showArea.Width > 0 && this.showArea.Left == this.area.Left)
                for (d = 1; d <= this.showArea.Height; d++) setFakeTile(index++, this.showArea.Left, this.showArea.Top + d);
            // Bottom boundary
            if (this.showArea.Height > 0 && this.showArea.Bottom == this.area.Bottom)
                for (d = 1; d < this.showArea.Width; d++) setFakeTile(index++, this.showArea.Left + d, this.showArea.Bottom);
        }

        /// <summary>Removes fake tiles for the region, reverting to the real tiles.</summary>
        /// <exception cref="InvalidOperationException">Fake tiles have not been set.</exception>
        public void unsetFakeTiles() {
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
        public void setFakeTile(int index, int x, int y) {
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

        /// <summary>Removes a single fake tile, reverting to the real tile.</summary>
        /// <param name="index">The index in the realTile array from which to retrieve the existing tile</param>
        /// <param name="x">The x coordinate of the tile position</param>
        /// <param name="y">The y coordinate of the tile position</param>
        public void unsetFakeTile(int index, int x, int y) {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
            Main.tile[x, y] = this.realTile[index];
        }

        /// <summary>Sends tile updates for a region border to a client.</summary>
        /// <param name="player">The player to send to.</param>
        public void refresh(TShockAPI.TSPlayer player) {
            // Due to the way the Rectangle class works, the Width and Height values are one tile less than the actual dimensions of the region.
            if (this.showArea.Width <= 3 || this.showArea.Height <= 3) {
                player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left - 1, this.showArea.Top - 1, this.showArea.Width + 3, this.showArea.Height + 3, 0);
            } else {
                if (this.showArea.Top == this.area.Top)
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left - 1, this.showArea.Top - 1, this.showArea.Width + 3, 3, 0);
                if (this.showArea.Left == this.area.Left)
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left - 1, this.showArea.Top + 2, 3, this.showArea.Height, 0);
                if (this.showArea.Right == this.area.Right)
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Right - 1, this.showArea.Top + 2, 3, this.showArea.Height, 0);
                if (this.showArea.Bottom == this.area.Bottom)
                    player.SendData(PacketTypes.TileSendSection, "", this.showArea.Left + 2, this.showArea.Bottom - 1, this.showArea.Width - 3, 3, 0);
            }

            player.SendData(PacketTypes.TileFrameSection, "", (int) (this.showArea.Left / 200), (int) (this.showArea.Top / 150), (int) (this.showArea.Right / 200), (int) (this.showArea.Bottom / 150), 0);
        }
    }
}
