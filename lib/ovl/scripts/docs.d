import std.process;
import std.stdio;

void main() {
  import std.algorithm : filter, map;
  import std.file : copy, dirEntries, readText, SpanMode, write;
  import std.string : endsWith, replace, strip;

  auto gitRevCmd = execute(["git", "describe", "--tags", "--abbrev=0"]);
  const DUB_VERSION = gitRevCmd.status != 0 ? "v0.1.0" : gitRevCmd.output.strip;

  auto index = readText("views/index.hbs");
  write("docs/index.html", index.replace("{{ DUB_VERSION }}", DUB_VERSION));

  auto documents = dirEntries("docs", SpanMode.depth).filter!(
    entry => entry.isFile && entry.name.endsWith(".html")
  ).map!(entry => entry.name);

  foreach (string document; documents) {
    write(document, document.readText.replace("{{ DUB_VERSION }}", DUB_VERSION));
  }
}
