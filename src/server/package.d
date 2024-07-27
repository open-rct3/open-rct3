/// License: AGPL 3.0
module rct3.server;

import rct3.env;
import std.stdio;
import std.typecons : Tuple, tuple;
import vibe.d;

package shared auto running = false;
package Tuple!(HTTPServerSettings, "settings", URLRouter, "router") server;

shared static this() {
  import rct3.server.routes : router;

  Env.load;

  void errorPage(HTTPServerRequest req, HTTPServerResponse res, HTTPServerErrorInfo error) {
    res.render!("error.dt", req, error);
  }

  auto settings = new HTTPServerSettings;
  settings.port = envOrDefault("PORT", "8080").to!ushort;
  settings.bindAddresses = ["::1", "127.0.0.1"];
  settings.errorPageHandler = toDelegate(&errorPage);

  server = tuple!("settings", "router")(settings, router);
}

void main() {
  if (!finalizeCommandLineOptions()) return;

  // Start the server and run the event loop
  auto listener = listenHTTP(server.settings, server.router);
  running = true;
  runEventLoop();

  // Server has exited
  listener.stopListening();
  running = false;
}
