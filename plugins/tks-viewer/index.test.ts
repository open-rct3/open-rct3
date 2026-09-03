import { assert, assertEquals } from "@std/assert";
import createPlugin, { type CallContext } from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/tks-viewer.wasm", import.meta.url);
const NOT_FOUND = -9223372036854775808n;

function symbolJson(name: string, tag: string): string {
  return JSON.stringify({ name, tag });
}

Deno.test("tks-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Track Sections Viewer");
  await plugin.close();
});

Deno.test("tks-viewer: version()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("version");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "0.1.0");
  await plugin.close();
});

Deno.test("tks-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()), ["tks"]);
  await plugin.close();
});

Deno.test("tks-viewer: render() with truncated data displays error and hex dump", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const truncatedData = new Uint8Array(50);
  const out = await plugin.call("render", truncatedData);
  assert(out !== null, "Expected a result!");
  const html = out!.text();
  assert(html.includes("Data too short for TrackSection header"), "Expected error message");
  assert(html.includes("hex-view"), "Expected hex dump");
  await plugin.close();
});

Deno.test("tks-viewer: render() with 140 bytes parses metadata and displays summary table", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const data = new Uint8Array(140);
  const view = new DataView(data.buffer);
  view.setUint32(72, 2, true); // entrySlope = 2 (Medium Up)
  view.setUint32(76, 1, true); // entryBank = 1 (Left)
  view.setUint32(100, 3, true); // exitSlope = 3 (Steep Up)
  view.setUint32(104, 5, true); // exitBank = 5 (Right)
  view.setFloat32(124, 25.5, true); // towerRideBase
  view.setFloat32(128, 4.0, true); // waterSplash1
  view.setFloat32(132, 8.0, true); // waterSplash2
  view.setFloat32(136, 1.5, true); // reverserVal

  const out = await plugin.call("render", data);
  assert(out !== null, "Expected a result!");
  const html = out!.text();

  assert(html.includes("TrackSection Summary"), "Expected summary title");
  assert(html.includes("Medium Up (2)"), "Expected entry slope label");
  assert(html.includes("Left (1)"), "Expected entry bank label");
  assert(html.includes("Steep Up (3)"), "Expected exit slope label");
  assert(html.includes("Right (5)"), "Expected exit bank label");
  assert(html.includes("25.5"), "Expected tower ride base");
  assert(html.includes("Referenced Splines"), "Expected splines table");
  assert(html.includes("hex-view"), "Expected hex view");
  await plugin.close();
});

Deno.test("tks-viewer: render() resolves splines, renders side-by-side SVG projections, and ownership indicators", async () => {
  // Build a synthetic 140-byte TrackSection
  const tksData = new Uint8Array(140);
  const tksView = new DataView(tksData.buffer);
  tksView.setUint32(0, 100, true); // internalNamePtr = 100
  tksView.setUint32(4, 500, true); // sceneryItemRef = 500
  tksView.setUint32(24, 3, true); // entryFlags = 3 (0x00000003)
  tksView.setUint32(28, 12, true); // exitFlags = 12 (0x0000000c)
  tksView.setUint32(32, 2000, true); // splineLeftRef = 2000
  tksView.setUint32(36, 3000, true); // splineRightRef = 3000
  tksView.setUint32(72, 0, true); // entrySlope = Flat
  tksView.setUint32(100, 1, true); // exitSlope = Gentle Up

  // Build a synthetic 32-byte Spline Header with 2 nodes (72 bytes nodes payload)
  function makeSplineBytes(nodeCount: number, nodesPtr: number, totalLength: number): Uint8Array {
    const bytes = new Uint8Array(32);
    const view = new DataView(bytes.buffer);
    view.setUint32(0, nodeCount, true);
    view.setUint32(4, nodesPtr, true);
    view.setUint32(8, 0, true); // cyclic = 0
    view.setFloat32(12, totalLength, true);
    view.setFloat32(16, 1.0 / totalLength, true);
    view.setFloat32(28, 5.0, true); // maxY
    return bytes;
  }

  // Build nodes data: 2 nodes * 36 bytes = 72 bytes
  // Node 0: Pos(0, 0, 0), CP1(0, 0, 0), CP2(2, 0, 0)
  // Node 1: Pos(10, 0, 2), CP1(-2, 0, 0), CP2(0, 0, 0)
  function makeNodesData(xOffset: number): Uint8Array {
    const bytes = new Uint8Array(72);
    const view = new DataView(bytes.buffer);
    // Node 0:
    view.setFloat32(0, xOffset, true); // Pos.X
    view.setFloat32(4, 0, true); // Pos.Y
    view.setFloat32(8, 0, true); // Pos.Z
    view.setFloat32(24, 2, true); // CP2.X
    // Node 1:
    view.setFloat32(36, xOffset + 10, true); // Pos.X
    view.setFloat32(40, 0, true); // Pos.Y
    view.setFloat32(44, 2, true); // Pos.Z
    view.setFloat32(48, -2, true); // CP1.X
    return bytes;
  }

  const leftSplineHeader = makeSplineBytes(2, 4000, 10.2);
  const leftNodesData = makeNodesData(0);
  const rightSplineHeader = makeSplineBytes(2, 5000, 10.2);
  const rightNodesData = makeNodesData(1.5);

  const tksFunctions = {
    ...functions,
    "ovl": {
      "current_resource_address": () => 1000n,
      "find_symbol": (ctx: CallContext, dataPtr: bigint) => {
        if (dataPtr === 1000n) return ctx.store(new TextEncoder().encode(symbolJson("CTR_Track_Straight", "tks")));
        if (dataPtr === 500n) return ctx.store(new TextEncoder().encode(symbolJson("CTR_Coaster_Train", "sid")));
        if (dataPtr === 2000n) return ctx.store(new TextEncoder().encode(symbolJson("track_spl_left", "spl")));
        if (dataPtr === 3000n) return ctx.store(new TextEncoder().encode(symbolJson("track_spl_right", "spl")));
        return NOT_FOUND;
      },
      "read_resource": (
        ctx: CallContext,
        namePtr: bigint,
        _nameLen: bigint,
        _tagPtr: bigint,
        _tagLen: bigint
      ) => {
        const name = ctx.read(namePtr)?.text() ?? "";
        if (name === "track_spl_left") return ctx.store(leftSplineHeader);
        if (name === "track_spl_right") return ctx.store(rightSplineHeader);
        return NOT_FOUND;
      },
      "resolve_pointer": (ctx: CallContext, dataPtr: bigint) => {
        if (dataPtr === 100n) return ctx.store(new TextEncoder().encode("Straight 1x1 Track Piece\0"));
        if (dataPtr === 4000n) return ctx.store(leftNodesData);
        if (dataPtr === 5000n) return ctx.store(rightNodesData);
        return NOT_FOUND;
      },
      "get_relocation_source": () => NOT_FOUND,
      "resolve_symbol_reference": () => NOT_FOUND,
      "symbol_address": () => NOT_FOUND,
    },
  };

  const plugin = await createPlugin(wasmUrl, { functions: tksFunctions });
  const out = await plugin.call("render", tksData);
  assert(out !== null, "Expected a result!");
  const html = out!.text();

  // 1. Verify TrackSection Metadata & Title
  assert(html.includes("Straight 1x1 Track Piece"), "Expected resolved internal name");
  assert(html.includes("CTR_Track_Straight"), "Expected symbol name");
  assert(html.includes("Scenery Item Ref (sceneryItemRef)"), "Expected Scenery Item Ref row");
  assert(html.includes("CTR_Coaster_Train"), "Expected resolved vehicle/scenery item ID");
  assert(html.includes("SceneryItem: CTR_Coaster_Train"), "Expected SceneryItem badge");

  // 2. Verify Connection & Behavior Flags
  assert(html.includes("Entry Flags (entryFlags)"), "Expected Entry Flags row");
  assert(html.includes("Exit Flags (exitFlags)"), "Expected Exit Flags row");
  assert(html.includes("0x00000003"), "Expected entryFlags hex representation");
  assert(html.includes("0x0000000c"), "Expected exitFlags hex representation");

  // 3. Verify Ownership Badges linking TrackSection to Splines
  assert(html.includes("Left Spline: track_spl_left"), "Expected Left Spline badge indicator");
  assert(html.includes("Right Spline: track_spl_right"), "Expected Right Spline badge indicator");

  // 4. Verify Toggle Controls Toolbar
  assert(html.includes("tks-toolbar"), "Expected toggle toolbar");
  assert(html.includes("chk-left"), "Expected Left Spline checkbox");
  assert(html.includes("chk-right"), "Expected Right Spline checkbox");
  assert(html.includes("chk-handles"), "Expected Bezier Handles checkbox");

  // 5. Verify Side-by-side 2D Projections (Top-down and Elevation)
  assert(html.includes("Top-Down View (XY Plane"), "Expected Top-down view header");
  assert(html.includes("Elevation View ("), "Expected Elevation view header");
  assert(html.includes("projection-svg"), "Expected SVG projections rendered");
  assert(html.includes("spl-left"), "Expected left spline svg group");
  assert(html.includes("spl-right"), "Expected right spline svg group");

  await plugin.close();
});
