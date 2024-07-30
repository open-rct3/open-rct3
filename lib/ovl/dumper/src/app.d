/// License: GPL 2.0
import std.stdio;

import nfde;
import ovl;

void main(string[] args) {
  import std.string : format;

  string inputFile() {
    if (args.length > 1) return args[1];
    // TODO: Show an open dialog only if a tty isn't connected
    string path;
    if (openDialog(path, [FilterItem("OVL archive files"c.ptr, "*.ovl"c.ptr)]) == Result.okay)
      return path;
    write("OVL archive path: ");
    return stdin.readln;
  }

  auto file = inputFile;
  format!"Reading %s"(file).writeln;
  auto archive = Ovl.load(file);
}
