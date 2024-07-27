/// License: GPL 2.0
import std.stdio;

import ovl;

void main(string[] args) {
  import std.string : format;

  string inputFile() {
    if (args.length > 1) return args[1];
    write("OVL archive path: ");
    return stdin.readln;
  }

  auto file = inputFile;
  format!"Reading %s"(file);
  auto archive = Ovl.load(file);
}
