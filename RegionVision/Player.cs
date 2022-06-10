#nullable enable

using System.Collections.Generic;
using TShockAPI;

namespace RegionVision;

/// <summary>
/// Associates data with an online player
/// </summary>
public class Player {
	/// <summary>The player's index number.</summary>
	public int Index { get; }
	/// <summary>Returns this player's TSPlayer instance.</summary>
	public TSPlayer TSPlayer => TShock.Players[this.Index];
	/// <summary>The list of regions this player is viewing.</summary>
	public List<Region> Regions { get; } = new();
	/// <summary>True if the player has elected to see regions near them.</summary>
	public bool IsViewingNearby { get; set; }

	/// <summary>Creates a new instance of the Player class.</summary>
	/// <param name="index">The player's index number.</param>
	public Player(int index) => this.Index = index;
}
