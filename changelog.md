Version 1.2.9.0 - 22 April 2017
----------------------------------

* Updated for TShock Mintaka 4.3.23 and _Terraria_ 1.3.5.3 (thanks [Jujaga](https://github.com/jujaga)!)

Version 1.2.8.0 – 29 December 2016
----------------------------------

* Updated for TShock Mintaka 4.3.22 and _Terraria_ 1.3.4.4 (thanks [Ruby Rose](https://github.com/deadsurgeon42)!)

Version 1.2.7.2 – 29 November 2016
----------------------------------

* Updated for API development version 1.26 and _Terraria_ 1.3.4.3.

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
