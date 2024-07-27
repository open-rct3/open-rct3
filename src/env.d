/// License: AGPL 3.0
module rct3.env;

public import dotenv;

string envOrDefault(string key, string default_ = null) {
  import std.algorithm : sort;

  if (Env.keys.dup.sort.contains(key)) return Env[key];
  return default_;
}
