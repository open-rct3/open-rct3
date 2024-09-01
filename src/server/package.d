/// License: AGPL 3.0
module rct3.server;

import rct3.env;
import std.json;
import std.stdio;
import std.typecons : Tuple, tuple;
import vibe.d;

package shared auto running = false;
package Tuple!(HTTPServerSettings, "settings", URLRouter, "router") server;

shared static this() {
  import std.range : take;
  import rct3 : Server;
  import rct3.server.routes : router;

  void errorPage(HTTPServerRequest req, HTTPServerResponse res, HTTPServerErrorInfo error) {
    logError("Error %d: %s\n%s", error.code, error.message, error.debugMessage);
    JSONValue response = ["error": JSONValue([
      "code": JSONValue(error.code),
      "message": JSONValue(error.message)
    ])];

    // Flash the error to the user's session
    auto session = !req.session ? res.startSession() : req.session;
    session.set("flash", response.toString);
    // TODO: Remove flash from session when reading data

    // TODO: Content type negotiation, i.e. only reply this way if the request expects HTML.
    if (error.code == 404) res.redirect("/404.html");
  }

  // Default to the external interface address on port 49152.
  auto settings = new HTTPServerSettings;
  settings.port = envOrDefault("PORT", Server.defaultPort);
  readOption("port|p", &settings.port, "Sets the port used for serving HTTP.");
  settings.bindAddresses = ["0.0.0.0"];
  readOption("bind-address|bind", &settings.bindAddresses[0], "Sets the address used for serving HTTP.");
  settings.errorPageHandler = toDelegate(&errorPage);

  server = tuple!("settings", "router")(settings, router);
}

void main() {
  if (!finalizeCommandLineOptions()) return;

  // Start the server and run the event loop
  auto listener = listenHTTP(server.settings, server.router);
  running = true;
  runEventLoop();
  running = false;

  // Server has exited
  listener.stopListening();
}
