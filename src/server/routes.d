module rct3.server.routes;

import rct3.env;
import std.conv : to;
import std.traits : Unconst, Unqual;
import vibe.core.log;
import vibe.http.router;
import vibe.http.server;
import vibe.http.websockets;
import vibe.web.auth;
import vibe.web.web;

auto router() {
  import std.array : array;
  import std.path : asAbsolutePath, dirSeparator, expandTilde, isRooted;
  import std.file : exists;
  import vibe.http.fileserver : serveStaticFiles;

  const publicDir = envOrDefault("PUBLIC_DIR", "public/").expandTilde.asAbsolutePath.array.to!string;
  logInfo("Reading static files from: %s", publicDir);

  /// Use `/ws` to identify WebSocket requests, otherwise, serve files out of the public folder.
  auto router = new URLRouter;
  /// Section: Routes
  router.registerWebInterface(new API);
  if (publicDir.isRooted && publicDir.exists)
    router.get("*", serveStaticFiles(publicDir));

  return router;
}

/// See_Also: https://vibed.org/api/vibe.web.auth
static struct User {
@safe:
  string username;
  bool admin;

  bool isAdmin() { return this.admin; }
  bool isRoomMember(int chat_room) {
    if (chat_room == 0) return this.username == "macy" || this.username == "peter";
    else if (chat_room == 1) return this.username == "macy";
    return false;
  }
}

/// See_Also: https://vibed.org/api/vibe.web.web/registerWebInterface
@requiresAuth
class API {
  private SessionVar!(User, "user") user;

  /// `GET /`
  @noAuth
  void index() {
    render!("index.dt");
  }

  /// `POST /login`
  @method(HTTPMethod.POST) @noAuth
  void login() {
    enforceBadRequest(
      "username" in request.form && "password" in request.form,
      "Missing username/password field."
    );

    auto username = request.form["username"];
    auto password = request.form["password"];
    // TODO: Validate credentials

    user = User(username);
    redirect("/");
  }

  @noRoute User authenticate(scope HTTPServerRequest req, scope HTTPServerResponse res) {
    // TODO: Verify bearer auth token
    if ("Authorization" in req.headers) return user.value;

    // TODO: https://datatracker.ietf.org/doc/html/rfc6750
    res.headers["WWW-Authenticate"] = "Bearer";
    throw new HTTPStatusException(HTTPStatus.unauthorized);
  }

  /// `POST /logout`
  @method(HTTPMethod.POST) @noAuth
  void logout() {
    terminateSession();
    redirect("/");
  }

  /// WebSocket entry point.
  @path("/ws") @noAuth
  void connectWebSocket(scope WebSocket socket) {
    import std.algorithm : canFind, countUntil, remove;
    import vibe.d : seconds;

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
}

// See https://wiki.dlang.org/User:Csmith1991/Vibe.d_Documentation/websocket
package WebSocket[] sockets;

/// Returns: Mutable reference to the given `object`.
Unconst!T mut(T)(T object) if (is(Unqual!T : Object)) {
  import std.conv : castFrom;
  return castFrom!T.to!(Unconst!T)(object);
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
