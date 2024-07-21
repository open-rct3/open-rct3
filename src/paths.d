/// See_Also: <a href="https://www.pcgamingwiki.com/wiki/RollerCoaster_Tycoon_3#Game_data">RCT3: Game Data</a> (PCGamingWiki)

///
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
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum/AppData"
  ];
  else version (Linux) static const auto appData = [
    // Steam Play (Prton on Linux) See https://www.protondb.com/app/1368820
    // QUESTION: Is this correct for _every_ Steam version?
    "$STEAM_IBRARY/steamapps/compatdata/1368820/pfx"
  ];

  /// Saved game data location.
  version (DDoc) static const string[] userData = [];
  else version (Windows) static const auto userData = [
    "%USERPROFILE%\\Documents\\RCT3"
  ];
  else version (OSX) static const auto userData = [
    // FIXME: This path may vary based on version
    "$HOME/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum"
  ];
  else version (Linux) static const auto userData = [
    // Steam Play (Proton on Linux) See https://www.protondb.com/app/1368820
    // QUESTION: Is this correct for _every_ Steam version?
    "$STEAM_IBRARY/steamapps/compatdata/1368820/pfx"
  ];
}
