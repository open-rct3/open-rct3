/// License: AGPL 3.0
module rct3.server;

import std.conv : to;
import std.stdio;
import std.traits : Unconst, Unqual;
import std.typecons : Tuple, tuple;
import vibe.d;

package shared auto running = false;
package Tuple!(HTTPServerSettings, "settings", URLRouter, "router") server;
// See https://wiki.dlang.org/User:Csmith1991/Vibe.d_Documentation/websocket
package WebSocket[] sockets;

shared static this() {
  import std.array : array;
  import std.path : asAbsolutePath, dirSeparator, expandTilde, isRooted;
  import std.file : exists;

  // TODO: Read public folder path, server PORT and other configs from .env
  const publicDir = "public/".expandTilde.asAbsolutePath.array.to!string;
  const port = 8080;

  /// Use `/ws` to identify WebSocket requests, otherwise, serve files out of the public folder.
  auto router = new URLRouter;
  /// Section: Routes
  {
    router.get("/", &index);
    // WebSocket entry point
    router.get("/ws", handleWebSockets(&connectWebSocket));
    if (publicDir.isRooted && publicDir.exists)
      router.get("*", serveStaticFiles(publicDir));
  }

  auto settings = new HTTPServerSettings;
  settings.port = port;
  settings.bindAddresses = ["::1", "127.0.0.1"];
  settings.errorPageHandler = toDelegate(&errorPage);

  server = tuple!("settings", "router")(settings, router);
}

/// Returns: Mutable reference to the given `object`.
Unconst!T mut(T)(T object) if (is(Unqual!T : Object)) {
  import std.conv : castFrom;
  return castFrom!T.to!(Unconst!T)(object);
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

package:

void errorPage(HTTPServerRequest req, HTTPServerResponse res, HTTPServerErrorInfo error) {
  res.render!("error.dt", req, error);
}

void index(HTTPServerRequest req, HTTPServerResponse res) {
  res.render!("index.dt", req);
}

void connectWebSocket(scope WebSocket socket) {
  import std.algorithm : canFind, countUntil, remove;

  // Note: *No* other headers are assignable in standard WebSocket clients.
  // See https://stackoverflow.com/a/4361358
  auto protocol = socket.request.headers.get("Sec-WebSocket-Protocol", "UNDEFINED");
  logInfo("Connecting to WS peer @ %s...", socket.request.mut.peer);
  logDebug("Protocol: %s", protocol);
  // TODO: Handle auth tokens from requested protocol

  // Add socket to sockets list
  sockets ~= socket;

  // Get username
  socket.waitForData(1.seconds);
  // TODO: Write a spec for the game's WS protocol
  // TODO: Receive JSON messages instead
  // TODO: Receive the client's name and other metadata
  string name = socket.receiveText;

  // Server-side validation of results
  if (name !is null) {
    logInfo("%s connected @ %s.", name, socket.request.mut.peer);
    broadcast(socket, "System", name ~ " connected to the chat.");
  } else {
    // Kick client
    // TODO: Use std.json with protocol primitives
    socket.send("{\"name\":\"System\", \"text\":\"Invalid name.\"}");

    socket.close;
    // FIXME: sockets.removeFromArray!WebSocket(socket);
    logInfo("%s disconnected.", name);
    return;
  }

  // message loop
  while (socket.waitForData) {
    if (!socket.connected) break;

    // Receive message
    auto text = socket.receiveText;
    // Close if receive "/close"
    if (text == "/close") break;

    logInfo("Received: \"%s\" from %s.", text, name);
    // Relay message to everyone else
    broadcast(socket, name, text);
  }

  // Remove socket from sockets list and close socket
  assert(sockets.canFind(socket), "Socket was lost!");
  socket.close;
  sockets.remove(sockets.countUntil(socket));
  logInfo("%s disconnected.", name);

  broadcast(null, "System", name ~ " disconnected to the chat.");
}

void broadcast(scope WebSocket source, string name, string text) {
  if (source !is null) assert(source.connected, "Boradcast source is not connected.");

  foreach (socket; sockets) {
    // Don't send it to people who won't get it.
    if (!socket.connected) continue;

    logInfo("Sending: \"%s\" to %s.", text, socket.request.mut.peer);
    // TODO: Use std.json with protocol primitives
    socket.send("{\"name\":\"" ~ name ~ "\", \"text\":\"" ~ text ~ "\"}");
  }
}
