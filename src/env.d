/// License: AGPL 3.0
module rct3.env;

public import dotenv;

import std.conv : to;
import std.traits : hasIndirections, isScalarType;

///
T envOrDefault(T)(string key, T default_ = T.init) if (isScalarType!T) {
  import std.algorithm : sort;

  if (Env.keys.dup.sort.contains(key)) return Env[key].to!T;
  return default_;
}
/// ditto
/// See_Also: https://forum.dlang.org/post/mailman.380.1389620489.15871.digitalmars-d-learn@puremagic.com
T envOrDefault(T)(string key, T default_ = null) if (hasIndirections!T) {
  import std.algorithm : sort;

  if (Env.keys.dup.sort.contains(key)) return Env[key].to!T;
  return default_;
}
