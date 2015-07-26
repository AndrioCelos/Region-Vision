Version 1.2.4 – 26 July 2015
----------------------------

* Updated for API version 1.20 and _Terraria_ 1.3.0.7.

Version 1.2.3 – 17 July 2015
----------------------------

* Updated for API version 1.19 and _Terraria_ 1.3.
* Repainted one of the messages.

Version 1.2.2 – 29 June 2015
----------------------------

* Now targets [TShock 4.3.0 GM1](https://tshock.co/xf/index.php?threads/tshock-4-3-0-gm-prerelease.3759/) and .NET Framework 4.5.

Version 1.2.1 – 9 May 2015
--------------------------

* Changed the permissions to `regionvision.regionview` and `regionvision.regionviewnear` to be standards compliant. The old permissions still work.

Version 1.2 – 9 March 2015
--------------------------

* Updated for the Terraria Server API version 1.17.
* I now use the RegionDeleted hook to inform users immediately and descriptively when a region they are viewing is deleted. It turns out I can't use RegionEntered and RegionLeft, because they don't work when regions overlap.

Version 1.1 – 21 July 2014
--------------------------

* Added the `/rvn` command
* Added help text to all commands
* Fix: deep paint is now used instead of normal paint, which shows on more tiles.

Version 1.0 – 1 July 2014
-------------------------
Initial release
