/// License: AGPL 3.0
module rct3.paths;

/// See_Also: $(UL
///  $(LI $(ANCHOR https://www.pcgamingwiki.com/wiki/RollerCoaster_Tycoon_3#Game_data "RCT3: Game Data") (PCGamingWiki))
///  $(LI $(ANCHOR https://steamdb.info/app/1368820 "RCT3 Complete Edition") (SteamDB))
///  $(LI $(ANCHOR https://www.protondb.com/app/1368820 "RCT3 Complete Edition on Steam Play") (ProtonDB))
/// )
struct Paths {
  /// Installation data location.
  version (DDoc) static const string[] installData = [];
  else version (Windows) static const auto installData = [
    "C:\\Program Files\\RollerCoaster Tycoon 3 Platinum"
  ];
  else version (OSX) static const auto installData = [
    "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets"
  ];
  else version (Linux) static const auto installData = [
    // FIXME: What is the path to game OVLs with Proton for Linux?
  ];

  ///
  version(DDoc) static const string[] exe = [];
  else version(Windows) static const auto exe = [
    "C:\\Program Files\\RollerCoaster Tycoon 3 Platinum\\RCT3.exe"
  ];
  else version(OSX) static const auto exe = [
    "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/MacOS/RCT3",
    "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/MacOS/RCT3_Exe"
  ];
  else version (Linux) static const auto exe = [
    // FIXME: What is the path to RCT3.exe with Proton for Linux?
  ];

  /// Configuration data location.
  version (DDoc) static const string[] appData = [];
  else version (Windows) static const auto appData = [
    "$APPDATA\\Atari\\RCT3",
    "$APPDATA\\Frontier\\RCT3"
  ];
  else version (OSX) static const auto appData = [
    // FIXME: This path may vary based on version
    "~/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum/AppData",
    // Contains Options.txt, MusicGenre.txt, and user save games, coasters, parks, structures, etc.
    "~/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Complete Edition"
  ];
  else version (Linux) static const auto appData = [
    // Steam Play (Proton on Linux) See https://www.protondb.com/app/1368820
    // FIXME: There may be differing IDs for other versions
    "$STEAM_LIBRARY/steamapps/compatdata/1368820/pfx"
  ];

  /// Saved game data location, including coasters, parks, structures, etc.
  version (DDoc) static const string[] userData = [];
  else version (Windows) static const auto userData = [
    "$USERPROFILE\\Documents\\RCT3"
  ];
  else version (OSX) static const auto userData = [
    // FIXME: This path may vary based on version
    "~/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Platinum",
    "~/Library/Containers/com.aspyr.rct3.appstore/Data/Library/Application Support/RollerCoaster Tycoon 3 Complete Edition"
  ];
  else version (Linux) static const auto userData = [
    // Steam Play (Proton on Linux) See https://www.protondb.com/app/1368820
    // FIXME: There may be differing IDs for other versions
    "$STEAM_LIBRARY/steamapps/compatdata/1368820/pfx"
  ];
}

/// Thrown when RCT3 game data could not be found.
class GameNotFoundException : Exception {
  import std.exception : basicExceptionCtors;

  ///
  mixin basicExceptionCtors;
}

///
struct Data {
  import std.algorithm : any, find, map;
  import std.exception : enforce;
  import std.file : dirEntries, exists, SpanMode;
  import std.path : expandTilde;

  /// Throws: When RCT3 installation data could not be found.
  static auto installData(string glob = null) @property {
    auto paths = Paths.installData.map!(path => path.expandTilde.expandEnvironmentVars);
    enum notFoundMsg = "Could not find RCT3 installation data.";
    enforce!GameNotFoundException(paths.any!exists, notFoundMsg);

    foreach (path; paths) if (path.exists)
      return path.dirEntries(glob is null ? "*" : glob, SpanMode.depth);
    throw new GameNotFoundException(notFoundMsg);
  }

  /// Throws: When RCT3's executable could not be found.
  static auto exe() @property {
    import std.algorithm : filter;
    import std.file : attrIsFile, getAttributes;

    auto paths = Paths.exe.map!(path => path.expandTilde.expandEnvironmentVars);
    enum notFoundMsg = "Could not find RCT3 executable.";
    enforce!GameNotFoundException(paths.any!exists, notFoundMsg);

    foreach (path; paths) if (path.exists) return path;
    throw new GameNotFoundException(notFoundMsg);
  }

  /// Throws: When application data, e.g. `Options.txt`, could not be found.
  static auto appData(string glob = null) @property {
    auto paths = Paths.appData.map!(path => path.expandTilde.expandEnvironmentVars);
    enum notFoundMsg = "Could not find RCT3 application data.";
    enforce!GameNotFoundException(paths.any!exists, notFoundMsg);

    foreach (path; paths) if (path.exists)
      return path.dirEntries(glob is null ? "*" : glob, SpanMode.depth);
    throw new GameNotFoundException(notFoundMsg);
  }

  /// Throws: When user data, e.g. parks and structures, could not be found.
  static auto userData(string glob = null) @property {
    auto paths = Paths.userData.map!(path => path.expandTilde.expandEnvironmentVars);
    enum notFoundMsg = "Could not find RCT3 user data.";
    enforce!GameNotFoundException(paths.any!exists, notFoundMsg);

    foreach (path; paths) if (path.exists)
      return path.dirEntries(glob is null ? "*" : glob, SpanMode.depth);
    throw new GameNotFoundException(notFoundMsg);
  }
}

unittest {
  import std.algorithm : equal;

  assert(Paths.installData.length);
  assert(Paths.exe.length);
  assert(Paths.appData.length);
  assert(Paths.userData.length);
}

version (FindRCT3) unittest {
  import std.exception : assertNotThrown;

  assertNotThrown(Data.installData);
  assertNotThrown(Data.exe);
  assertNotThrown(Data.appData);
  assertNotThrown(Data.userData);
}

private string[string] env;

private string expandEnvironmentVars(string value) {
  import std.algorithm : canFind;
  import std.process : environment;
  import std.string : replace;

  auto vars = env.length == 0 ? (env = environment.toAA) : env;
  foreach (var; vars.keys) if (value.canFind("$" ~ var)) value = value.replace("$" ~ var, vars[var]);
  return value;
}
