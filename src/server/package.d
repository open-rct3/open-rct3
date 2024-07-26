/// License: AGPL 3.0
module rct3.server;

import std.stdio;
import std.typecons : Tuple, tuple;
import vibe.d;

package shared auto running = false;
package Tuple!(HTTPServerSettings, "settings", URLRouter, "router") server;

shared static this() {
  import rct3.server.routes : router, Server;

  // TODO: Read server PORT and other configs from .env
  const port = 8080;

  void errorPage(HTTPServerRequest req, HTTPServerResponse res, HTTPServerErrorInfo error) {
    res.render!("error.dt", req, error);
  }

  auto settings = new HTTPServerSettings;
  settings.port = port;
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
