# Region Vision
![Version 1.2.9.0](https://img.shields.io/badge/Version-1.2.9.0-blue.svg)
![API 2.1](https://img.shields.io/badge/API-2.1-green.svg)
[![Build status](https://ci.appveyor.com/api/projects/status/vwtwaqfmpxrpdfwn?svg=true)](https://ci.appveyor.com/project/jujaga/region-vision)

Introduction
--------

This is a [TShock](http://tshock.co/xf/) plugin that allows players to see region boundaries in the in-game world. The command `/rv <region name>` shows the boundary of that region using paint. Only the player using the command will see it; the tiles don't actually 'exist'. The region name can be entered in full, or only the start may be entered if this is not ambiguous.

If the region is large (larger than 256 × 256 tiles), only part of the region boundary near the player will be shown. Repeating the `/rv` command after moving can show a different part of the border. The border will also automatically update if the region is resized or deleted.

If part (or all) of the region boundary is in the air, 'phantom' magical ice tiles will mark the boundary. Players will *not* be allowed to place blocks or walls on them; attempting to do so will reset the border and give the player their item back. Hitting one of said ice tiles will also reset the borders.

Commands
--------

* `/regionview <region name>` or `/rv <region name>` – shows the given region border
* `/regionclear` or `/rc` – removes all region borders from your view
* `/regionviewnear` or `rvn` – turns on or off automatic showing of regions near you

Permissions
-----------

* `regionvision.regionview` – gives access to `/rv` and `/rc`.
* `regionvision.regionviewnear` – gives access to `/rvn`.
