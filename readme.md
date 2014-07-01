Region Vision
=============

This is a [TShock](http://tshock.co/xf/) plugin that allows players to see region boundaries in the in-game world. The command `/rv <region name>` shows the boundary of that region using paint. Only the player using the command will see it; the tiles don't actually 'exist'. The region name can be entered in full, or only the start may be entered if this is not ambiguous.

If the region is large (larger than 256 × 256 tiles), only part of the region boundary near the player will be shown. Repeating the `/rv` command after moving can show a different part of the border.

If part (or all) of the region boundary is in the air, 'phantom' magical ice tiles will mark the boundary. Players will *not* be allowed to place blocks or walls on them; attempting to do so will reset the border and give the player their item back. Hitting one of said ice tiles will also reset the borders.

Commands
--------

* `/regionview <region name` or `/rv <region name>` – shows the given region border
* `/regionclear` or `/rc` – removes all region borders from your view

Permissions
-----------

* `regionview` – gives access to both commands listed above