#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace RegionVision; 

[ApiVersion(2, 1)]
public class RegionVisionPlugin : TerrariaPlugin {
	/// <summary>The list of players being tracked by this plugin.</summary>
	public List<Player> Players { get; } = new();

	public override Version Version => new(1, 3, 0, 0);
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

	private readonly Timer refreshTimer = new(5000);

	public RegionVisionPlugin(Main game) : base(game) => this.Order = 1;

	public override void Initialize() {
		var viewCommand = new Command(new List<string>(new string[] { "regionvision.regionview", "regionview" }),
			this.CommandView, new string[] { "regionview", "rv" }) {
			AllowServer = false,
			HelpDesc = new string[] { "Usage: /rv <region name>", "Shows you the boundary of the specified region" }
		};
		Commands.ChatCommands.Add(viewCommand);

		var clearCommand = new Command(new List<string>(new string[] { "regionvision.regionview", "regionview" }),
								 this.CommandClear,
								 new string[] { "regionclear", "rc" }) {
			AllowServer = false,
			HelpDesc = new string[] { "Usage: /rc", "Removes all region borders from your view" }
		};
		Commands.ChatCommands.Add(clearCommand);

		var viewNearbyCommand = new Command(new List<string>(new string[] { "regionvision.regionviewnear", "regionviewnear" }),
			this.CommandViewNearby, new string[] { "regionviewnear", "rvn" }) {
			AllowServer = false,
			HelpDesc = new string[] { "Usage: /rvn", "Turns on or off automatic showing of regions near you" }
		};
		Commands.ChatCommands.Add(viewNearbyCommand);

		GetDataHandlers.TileEdit += TShockAPI.HandlerList<GetDataHandlers.TileEditEventArgs>.Create(this.OnTileEdit, HandlerPriority.High, false);
		TShockAPI.Hooks.RegionHooks.RegionCreated += this.RegionHooks_RegionCreated;
		TShockAPI.Hooks.RegionHooks.RegionDeleted += this.RegionHooks_RegionDeleted;
		ServerApi.Hooks.ServerJoin.Register(this, this.OnPlayerJoin);
		ServerApi.Hooks.ServerLeave.Register(this, this.OnPlayerLeave);
		TShockAPI.Hooks.PlayerHooks.PlayerCommand += this.OnPlayerCommand;

		this.refreshTimer.AutoReset = false;
		this.refreshTimer.Elapsed += this.RefreshTimer_Elapsed;
	}

	void RegionHooks_RegionDeleted(RegionHooks.RegionDeletedEventArgs args) {
		if (args.Region.WorldID != Main.worldID.ToString()) return;

		// If any players were viewing this region, clear its border.
		lock (this.Players) {
			foreach (var player in this.Players) {
				for (var i = 0; i < player.Regions.Count; i++) {
					var region = player.Regions[i];
					if (region.Name.Equals(args.Region.Name)) {
						player.TSPlayer.SendMessage("Region " + region.Name + " has been deleted.", textColour[region.colour - 13]);
						region.refresh(player.TSPlayer);
						player.Regions.RemoveAt(i);

						foreach (var region2 in player.Regions)
							region2.SetFakeTiles();
						foreach (var region2 in player.Regions)
							region2.refresh(player.TSPlayer);
						foreach (var region2 in player.Regions.Reverse<Region>())
							region2.UnsetFakeTiles();

						break;
					}
				}
			}
		}
	}
	void RegionHooks_RegionCreated(RegionHooks.RegionCreatedEventArgs args) {
		this.refreshTimer.Stop();
		this.RefreshRegions();
	}

	/// <summary>Returns the <see cref="Player"/> instance for the player with a given index number.</summary>
	public Player? FindPlayer(int index) => this.Players.FirstOrDefault(p => p.Index == index);

	private void CommandView(CommandArgs args) {
		TShockAPI.DB.Region? tRegion = null;
		var matches = new List<TShockAPI.DB.Region>();

		if (args.Parameters.Count < 1) {
			args.Player.SendErrorMessage("Usage: /regionview <region name>");
			return;
		}

		// Find the specified region.
		for (var pass = 1; pass <= 3 && tRegion == null && matches.Count == 0; pass++) {
			foreach (var _tRegion in TShock.Regions.Regions) {
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

		if (tRegion!.Area.Width < 0 || tRegion.Area.Height < 0) {
			args.Player.SendErrorMessage("Region {0} contains no tiles. (Found dimensions: {1} × {2})\nUse [c/FF8080:/region resize] to fix it.", tRegion.Name, tRegion.Area.Width, tRegion.Area.Height);
			return;
		}

		lock (this.Players) {
			var player = this.FindPlayer(args.Player.Index);
			if (player == null) return;

			// Register this region.
			var region = player.Regions.FirstOrDefault(r => r.Name == tRegion.Name);
			if (region == null)
				region = new Region(tRegion.Name, tRegion.Area);
			else
				player.Regions.Remove(region);

			foreach (var _region in player.Regions)
				_region.SetFakeTiles();
			if (region.showArea != region.area) region.refresh(player.TSPlayer);
			player.Regions.Add(region);
			region.CalculateArea(args.Player);
			region.SetFakeTiles();
			region.refresh(player.TSPlayer);

			foreach (var _region in player.Regions.Reverse<Region>())
				_region.UnsetFakeTiles();

			var message = "You are now viewing " + region.Name + ".";
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

			this.refreshTimer.Interval = 7000;
			this.refreshTimer.Enabled = true;
		}
	}

	private void CommandClear(CommandArgs args) {
		lock (this.Players) {
			var player = this.FindPlayer(args.Player.Index);
			if (player == null) return;
			player.IsViewingNearby = false;
			this.ClearRegions(player);
		}
	}

	private void CommandViewNearby(CommandArgs args) {
		lock (this.Players) {
			var player = this.FindPlayer(args.Player.Index);
			if (player == null) return;

			if (player.IsViewingNearby) {
				player.IsViewingNearby = false;
				args.Player.SendInfoMessage("You are no longer viewing regions near you.");
			} else {
				player.IsViewingNearby = true;
				args.Player.SendInfoMessage("You are now viewing regions near you.");
				this.refreshTimer.Interval = 1500;
				this.refreshTimer.Enabled = true;
			}
		}
	}

	/// <summary>Removes all region borders from a player's view.</summary>
	/// <param name="player">The player to reset</param>
	public void ClearRegions(Player player) {
		foreach (var region in player.Regions)
			region.refresh(player.TSPlayer);
		player.Regions.Clear();
	}

	private void OnTileEdit(object sender, GetDataHandlers.TileEditEventArgs e)
	{
		if (e.Action is > GetDataHandlers.EditAction.KillTileNoItem or GetDataHandlers.EditAction.KillWall) return;
		if (e.Action == GetDataHandlers.EditAction.PlaceTile && e.EditData == Terraria.ID.TileID.MagicalIceBlock) return;

		lock (this.Players) {
			var player = this.FindPlayer(e.Player.Index);
			if (player == null) return;
			if (player.Regions.Count == 0) return;

			// Stop the edit if a phantom tile is the only thing making it possible.
			foreach (var region in player.Regions) {
				// Clear the region borders if they break one of the phantom ice blocks.
				if ((e.Action == GetDataHandlers.EditAction.KillTile || e.Action == GetDataHandlers.EditAction.KillTileNoItem) && (Main.tile[e.X, e.Y] == null || !Main.tile[e.X, e.Y].active()) &&
					e.X >= region.showArea.Left - 1 && e.X <= region.showArea.Right + 1 && e.Y >= region.showArea.Top - 1 && e.Y <= region.showArea.Bottom + 1 &&
					!(e.X >= region.showArea.Left + 2 && e.X <= region.showArea.Right - 2 && e.Y >= region.showArea.Top + 2 && e.Y <= region.showArea.Bottom - 2)) {
					e.Handled = true;
					//clearRegions(player);
					break;
				}
				if ((e.Action == GetDataHandlers.EditAction.PlaceTile || e.Action == GetDataHandlers.EditAction.PlaceWall) && !this.TileValidityCheck(region, e.X, e.Y, e.Action)) {
					e.Handled = true;
					player.TSPlayer.SendData(PacketTypes.TileSendSquare, "", 1, e.X, e.Y, 0, 0);
					if (e.Action == GetDataHandlers.EditAction.PlaceTile) this.GiveTile(player, e);
					if (e.Action == GetDataHandlers.EditAction.PlaceWall) this.GiveWall(player, e);
					break;
				}
			}
			if (e.Handled) this.ClearRegions(player);
		}
	}

	/// <summary>Checks whether a player's attempted tile edit is valid. A tile edit is considered invalid if it's only possible due to the presence of phantom tiles.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="x">The x coordinate of the edited tile.</param>
	/// <param name="y">The y coordinate of the edited tile.</param>
	/// <param name="editType">The type of the edit.</param>
	/// <returns>true if the edit was valid; false if it wasn't.</returns>
	public bool TileValidityCheck(Region region, int x, int y, GetDataHandlers.EditAction editType) {
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
		return x < region.showArea.Left - 1 || x > region.showArea.Right + 1 || y < region.showArea.Top - 1 || y > region.showArea.Bottom + 1 ||
			x >= region.showArea.Left + 2 && x <= region.showArea.Right - 2 && y >= region.showArea.Top + 2 && y <= region.showArea.Bottom - 2;
	}

	private void OnPlayerJoin(JoinEventArgs e) {
		lock (this.Players)
			this.Players.Add(new Player(e.Who));
	}

	private void OnPlayerLeave(LeaveEventArgs e) {
		lock (this.Players)
			for (var i = 0; i < this.Players.Count; i++) {
				if (this.Players[i].Index == e.Who) {
					this.Players.RemoveAt(i);
					break;
				}
			}
	}

	/// <summary>Returns the item used to attempt a rejected foreground tile edit to the player.</summary>
	/// <param name="player">The player attempting the edit</param>
	/// <param name="e">The data from the edit event</param>
	public void GiveTile(Player player, GetDataHandlers.TileEditEventArgs e) {
		var item = new Item(); var found = false;
		for (var i = 1; i <= Terraria.ID.ItemID.Count; i++) {
			item.SetDefaults(i, true);
			if (item.createTile == e.EditData && item.placeStyle == e.Style) {
				if (item.tileWand != -1)  item.SetDefaults(item.tileWand, true);
				found = true;
				break;
			}
		}
		if (found) this.GiveItem(player, item);
	}

	/// <summary>Returns the item used to attempt a rejected background wall edit to the player.</summary>
	/// <param name="player">The player attempting the edit</param>
	/// <param name="e">The data from the edit event</param>
	public void GiveWall(Player player, GetDataHandlers.TileEditEventArgs e) {
		var item = new Item(); var found = false;
		for (var i = 1; i <= Terraria.ID.ItemID.Count; i++) {
			item.SetDefaults(i, true);
			if (item.createWall == e.EditData) {
				found = true;
				break;
			}
		}
		if (found) {
			item.stack = 1;
			this.GiveItem(player, item);
		}
	}

	/// <summary>Gives an item to a player.</summary>
	/// <param name="player">The player to receive the item</param>
	/// <param name="item">The item to give</param>
	public void GiveItem(Player player, Item item) => player.TSPlayer.GiveItem(item.type, 1);

	private void OnPlayerCommand(PlayerCommandEventArgs e) {
		if (e.Parameters.Count >= 2 && e.CommandName.ToLower() == "region" && new string[] {"delete", "resize", "expand"}.Contains(e.Parameters[0].ToLower())) {
			if (Commands.ChatCommands.Any(c => c.HasAlias("region") && c.CanRun(e.Player)))
				this.refreshTimer.Interval = 1500;
		}
	}

	private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e) => this.RefreshRegions();

	private void RefreshRegions() {
		var anyRegions = false;

		// Check for regions that have changed.
		lock (this.Players) {
			foreach (var player in this.Players) {
				var refreshFlag = false;

				for (var i = 0; i < player.Regions.Count; i++) {
					var region = player.Regions[i];
					var tRegion = TShock.Regions.GetRegionByName(region.Name);

					if (tRegion == null) {
						// The region was removed.
						refreshFlag = true;
						region.refresh(player.TSPlayer);
						player.Regions.RemoveAt(i--);
					} else {
						var newArea = tRegion.Area;
						if (!region.command && (!player.IsViewingNearby || !IsPlayerNearby(player.TSPlayer, region.area))) {
							// The player is no longer near the region.
							refreshFlag = true;
							region.refresh(player.TSPlayer);
							player.Regions.RemoveAt(i--);
						} else
						if (newArea != region.area) {
							// The region was resized.
							if (newArea.Width < 0 || newArea.Height < 0) {
								refreshFlag = true;
								region.refresh(player.TSPlayer);
								player.Regions.RemoveAt(i--);
							} else {
								anyRegions = true;
								refreshFlag = true;
								region.refresh(player.TSPlayer);
								region.area = newArea;
								region.CalculateArea(player.TSPlayer);
							}
						} else {
							anyRegions = true;
						}
					}
				}

				if (player.IsViewingNearby) {
					anyRegions = true;

					// Search for nearby regions
					foreach (var tRegion in TShock.Regions.Regions) {
						if (tRegion.WorldID == Main.worldID.ToString() && tRegion.Area.Width >= 0 && tRegion.Area.Height >= 0) {
							if (IsPlayerNearby(player.TSPlayer, tRegion.Area)) {
								if (!player.Regions.Any(r => r.Name == tRegion.Name)) {
									refreshFlag = true;
									var region = new Region(tRegion.Name, tRegion.Area, false);
									region.CalculateArea(player.TSPlayer);
									player.Regions.Add(region);
#if !SILENT
									player.TSPlayer.SendMessage("You see region " + region.Name + ".", textColour[region.colour - 13]);
#endif
								}
							}
						}
					}
				}

				if (refreshFlag) {
					foreach (var region in player.Regions)
						region.SetFakeTiles();
					foreach (var region in player.Regions)
						region.refresh(player.TSPlayer);
					foreach (var region in player.Regions.Reverse<Region>())
						region.UnsetFakeTiles();
				}
			}
		}

		if (anyRegions) {
			this.refreshTimer.Interval = 7000;
			this.refreshTimer.Enabled = true;
		}
	}

	/// <summary>Checks whether a given player is near a given region.</summary>
	/// <param name="tPlayer">The player to check</param>
	/// <param name="area">The region to check</param>
	/// <returns>true if the player is within 100 tiles of the region; false otherwise</returns>
	public static bool IsPlayerNearby(TSPlayer tPlayer, Rectangle area) {
		var playerX = (int) (tPlayer.X / 16);
		var playerY = (int) (tPlayer.Y / 16);

		return playerX >= area.Left   - NearRange &&
				playerX <= area.Right  + NearRange &&
				playerY >= area.Top    - NearRange &&
				playerY <= area.Bottom + NearRange;
	}
}
