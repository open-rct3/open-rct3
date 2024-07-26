/// License: AGPL 3.0
module rct3.paths;

/// See_Also: $(UL
///  $(LI $(ANCHOR https://www.pcgamingwiki.com/wiki/RollerCoaster_Tycoon_3#Game_data "RCT3: Game Data") (PCGamingWiki))
///  $(LI $(ANCHOR https://steamdb.info/app/1368820 "RCT3 Complete Edition") (SteamDB))
///  $(LI $(ANCHOR https://www.protondb.com/app/1368820 "RCT3 Complete Edition on Steam Play") (ProtonDB))
/// )
struct Data {
  /// Installation data location.
  version (DDoc) static const string[] installData = [];
  else version (Windows) static const auto installData = [
    "C:\\Program Files\\RollerCoaster Tycoon 3 Platinum"
  ];
  else version (OSX) static const auto installData = [
    "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets"
  ];
  else version (Linux) static const auto installData = [];

  ///
  version(DDoc) static const string[] exe = [];
  else version(Windows) static const auto exe = [
    "C:\\Program Files\\RollerCoaster Tycoon 3 Platinum\\RCT3.exe"
  ];
  else version(OSX) static const auto exe = [
    "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/MacOS/RCT3_Exe"
  ];
  else version (Linux) static const auto exe = [];

  /// Configuration data location.
  version (DDoc) static const string[] appData = [];
  else version (Windows) static const auto appData = [
    "%APPDATA%\\Atari\\RCT3",
    "%APPDATA%\\Frontier\\RCT3"
  ];
  else version (OSX) static const auto appData = [
    // FIXME: This path may vary based on version
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum/AppData",
    // Contains Options.txt, MusicGenre.txt, and user save games, coasters, parks, structures, etc.
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Complete Edition"
  ];
  else version (Linux) static const auto appData = [
    // Steam Play (Proton on Linux) See https://www.protondb.com/app/1368820
    // FIXME: There may be differing IDs for other versions
    "$STEAM_IBRARY/steamapps/compatdata/1368820/pfx"
  ];

  /// Saved game data location.
  version (DDoc) static const string[] userData = [];
  else version (Windows) static const auto userData = [
    "%USERPROFILE%\\Documents\\RCT3"
  ];
  else version (OSX) static const auto userData = [
    // FIXME: This path may vary based on version
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum",
    // Contains Options.txt and (user?) save games, coasters, parks, structures, etc.
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Complete Edition"
  ];
  else version (Linux) static const auto userData = [
    // Steam Play (Proton on Linux) See https://www.protondb.com/app/1368820
    // FIXME: There may be differing IDs for other versions
    "$STEAM_IBRARY/steamapps/compatdata/1368820/pfx"
  ];
}

unittest {
  import std.algorithm : equal;

  assert(Data.installData.length);
  assert(Data.exe.length);
  assert(Data.appData.length);
  assert(Data.userData.length);
}
