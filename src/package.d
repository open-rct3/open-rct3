/// License: AGPL 3.0
module rct3;

/// "RCT3" encoded in ASCII.
enum ubyte[4] rct3 = [72, 63, 74, 33];

/// Server constants.
struct Server {
  import std.bitmanip : bitfields;

  /// Returns: Sum of "RCT3" in ASCII, limited to MIN_PORT and MAX_PORT.
  static ushort defaultPort() @property {
    import std.algorithm : clamp, map, sort, sum;
    import std.conv : to;
    import std.range : chunks;

    // Group digits in pairs, sum the pairs, and then sum the remaining digits
    // See https://stackoverflow.com/a/43416236/1363247
    return rct3[].chunks(2).map!sum.sum.clamp(49_152, 65_535).to!ushort;
  }
}

unittest {
  import std.conv : text;
  assert(Server.defaultPort == 49_152, Server.defaultPort.text);
}
