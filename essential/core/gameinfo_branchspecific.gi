"GameInfo"
{
    //
    // Branch-varying info, such as the game/title and app IDs, is in gameinfo_branchspecific.gi.
    // gameinfo.gi is the non-branch-varying content and can be integrated between branches.
    //

    game         "Dota 2"
    title        "Dota 2"

    FileSystem
	{
		SteamAppId				570
		BreakpadAppId			373300	// Report crashes under beta DLC, not the S1 game.  Delete this when all clients are switched to S2
		BreakpadAppId_Tools		375360  // Use a separate bucket of buckets for "-tools" crashes so that they don't get drowned out by game crashes. Falls back to BreakpadAppId/SteamAppId if missing
		//
		// The code that loads this file automatically does a few things here:
		//
		// 1. For each "Game" search path, it adds a "GameBin" path, in <dir>\bin
		// 2. For each "Game" search path, it adds another "Game" path in front of it with _<langage> at the end.
		//    For example: c:\hl2\cstrike on a french machine would get a c:\hl2\cstrike_french path added to it.
		// 3. For the first "Game" search path, it adds a search path called "MOD".
		// 4. For the first "Game" search path, it adds a search path called "DEFAULT_WRITE_PATH".
		//

		//
		// Search paths are relative to the exe directory\..\
		//
		SearchPaths
		{
			// These are optional language paths. They must be mounted first, which is why there are first in the list.
			// *LANGUAGE* will be replaced with the actual language name. If not running a specific language, these paths will not be mounted
			Game_Language		dota_*LANGUAGE*

			// These are optional low-violence paths. They will only get mounted if you're in a low-violence mode.
			Game_LowViolence	dota_lv

			Game				_ArdysaMods
			Game				dota
			Game				core

			Mod					_ArdysaMods
			Mod					dota

			Write				dota

			// These are optional language paths. They must be mounted first, which is why there are first in the list.
			// *LANGUAGE* will be replaced with the actual language name. If not running a specific language, these paths will not be mounted
			AddonRoot_Language	dota_*LANGUAGE*_addons

			AddonRoot			dota_addons

			// Note: addon content is included in publiccontent by default.
			PublicContent		dota_core
			PublicContent		core
		}
		
		"UserSettingsPathID"	"USRLOCAL"
		"LegacyUserSettingsPathID"	"MOD"
				
		AddonsChangeDefaultWritePath 0
	}
}