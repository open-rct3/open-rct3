export class SplineNode {
  constructor(
    public posX: f32,
    public posY: f32,
    public posZ: f32,
    public cp1X: f32,
    public cp1Y: f32,
    public cp1Z: f32,
    public cp2X: f32,
    public cp2Y: f32,
    public cp2Z: f32
  ) {}
}

export class TrackSplineInfo {
  constructor(
    public role: string,
    public rawPtr: u32,
    public symbolName: string,
    public color: string,
    public cssClass: string,
    public resolved: bool,
    public nodeCount: u32,
    public cyclic: bool,
    public totalLength: f32,
    public invTotalLength: f32,
    public maxY: f32,
    public nodes: Array<SplineNode>
  ) {}
}

function fmin(a: f32, b: f32): f32 {
  return a < b ? a : b;
}

function formatF32(val: f32): string {
  let rounded = Math.round(f64(val) * 100.0) / 100.0;
  return rounded.toString();
}

export function screenTopDownX(lateralX: f32, minLateralX: f32, lateralSpan: f32, scale: f32): f32 {
  const marginOffsetLeft: f32 = 35.0 + (390.0 - lateralSpan * scale) / 2.0;
  return marginOffsetLeft + (lateralX - minLateralX) * scale;
}

export function screenTopDownZ(longitudinalZ: f32, minLongitudinalZ: f32, longitudinalSpan: f32, scale: f32): f32 {
  const marginOffsetBottom: f32 = 35.0 + (290.0 - longitudinalSpan * scale) / 2.0;
  return 360.0 - (marginOffsetBottom + (longitudinalZ - minLongitudinalZ) * scale);
}

export function screenElevationHorizontal(horizontalTrackCoord: f32, minHorizontalTrack: f32, horizontalTrackSpan: f32, scale: f32): f32 {
  const marginOffsetLeft: f32 = 35.0 + (390.0 - horizontalTrackSpan * scale) / 2.0;
  return marginOffsetLeft + (horizontalTrackCoord - minHorizontalTrack) * scale;
}

export function screenElevationVertical(verticalHeight: f32, minVerticalHeight: f32, verticalHeightSpan: f32, scale: f32): f32 {
  const marginOffsetBottom: f32 = 35.0 + (290.0 - verticalHeightSpan * scale) / 2.0;
  return 360.0 - (marginOffsetBottom + (verticalHeight - minVerticalHeight) * scale);
}

export function renderTopDownSvg(
  splines: Array<TrackSplineInfo>,
  minLateralX: f32,
  maxLateralX: f32,
  minLongitudinalZ: f32,
  maxLongitudinalZ: f32,
  scale: f32
): string {
  const lateralSpan = maxLateralX - minLateralX;
  const longitudinalSpan = maxLongitudinalZ - minLongitudinalZ;

  let svg = "<svg class='projection-svg' viewBox='0 0 460 360' xmlns='http://www.w3.org/2000/svg'>";
  svg += "<rect x='0' y='0' width='460' height='360' fill='#f8fafc' stroke='#e2e8f0'/>";

  // Grid lines
  svg += "<line x1='35' y1='325' x2='425' y2='325' stroke='#cbd5e1' stroke-width='1.5'/>";
  svg += "<line x1='35' y1='35' x2='35' y2='325' stroke='#cbd5e1' stroke-width='1.5'/>";
  svg += "<text x='230' y='350' text-anchor='middle' font-size='11' font-family='sans-serif' fill='#64748b'>Lateral (X Axis) &rarr;</text>";
  svg += "<text x='15' y='180' text-anchor='middle' font-size='11' font-family='sans-serif' fill='#64748b' transform='rotate(-90 15 180)'>Length (Z Axis) &rarr;</text>";

  // Axis dimension labels
  svg += "<text x='38' y='338' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(minLateralX) + "</text>";
  svg += "<text x='425' y='338' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(maxLateralX) + "</text>";
  svg += "<text x='30' y='323' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(minLongitudinalZ) + "</text>";
  svg += "<text x='30' y='45' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(maxLongitudinalZ) + "</text>";

  for (let splineIndex = 0; splineIndex < splines.length; splineIndex++) {
    const spline = splines[splineIndex];
    if (spline.nodes.length < 2) continue;

    svg += "<g class='spline-geom " + spline.cssClass + "'>";

    // Track path using cubic bezier segments
    let pathDefinition = "";
    const nodeCount = spline.nodes.length;
    const segmentCount = spline.cyclic ? nodeCount : nodeCount - 1;

    for (let segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++) {
      const startNode = spline.nodes[segmentIndex];
      const nextNodeIndex = (segmentIndex + 1) % nodeCount;
      const endNode = spline.nodes[nextNodeIndex];

      const startScreenX = screenTopDownX(startNode.posX, minLateralX, lateralSpan, scale);
      const startScreenY = screenTopDownZ(startNode.posZ, minLongitudinalZ, longitudinalSpan, scale);
      const startForwardControlScreenX = screenTopDownX(startNode.posX + startNode.cp2X, minLateralX, lateralSpan, scale);
      const startForwardControlScreenY = screenTopDownZ(startNode.posZ + startNode.cp2Z, minLongitudinalZ, longitudinalSpan, scale);
      const endBackwardControlScreenX = screenTopDownX(endNode.posX + endNode.cp1X, minLateralX, lateralSpan, scale);
      const endBackwardControlScreenY = screenTopDownZ(endNode.posZ + endNode.cp1Z, minLongitudinalZ, longitudinalSpan, scale);
      const endScreenX = screenTopDownX(endNode.posX, minLateralX, lateralSpan, scale);
      const endScreenY = screenTopDownZ(endNode.posZ, minLongitudinalZ, longitudinalSpan, scale);

      if (segmentIndex == 0) {
        pathDefinition += "M " + formatF32(startScreenX) + " " + formatF32(startScreenY);
      }
      pathDefinition += " C " + formatF32(startForwardControlScreenX) + " " + formatF32(startForwardControlScreenY) + " " +
        formatF32(endBackwardControlScreenX) + " " + formatF32(endBackwardControlScreenY) + " " +
        formatF32(endScreenX) + " " + formatF32(endScreenY);
    }

    svg += "<path d='" + pathDefinition + "' fill='none' stroke='" + spline.color + "' stroke-width='3' stroke-linecap='round'/>";

    // Control point handles and node markers
    for (let nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
      const node = spline.nodes[nodeIndex];
      const nodeScreenX = screenTopDownX(node.posX, minLateralX, lateralSpan, scale);
      const nodeScreenY = screenTopDownZ(node.posZ, minLongitudinalZ, longitudinalSpan, scale);

      // Forward handle (CP2)
      if (node.cp2X != 0.0 || node.cp2Z != 0.0) {
        const handleScreenX = screenTopDownX(node.posX + node.cp2X, minLateralX, lateralSpan, scale);
        const handleScreenY = screenTopDownZ(node.posZ + node.cp2Z, minLongitudinalZ, longitudinalSpan, scale);
        svg += "<line x1='" + formatF32(nodeScreenX) + "' y1='" + formatF32(nodeScreenY) +
          "' x2='" + formatF32(handleScreenX) + "' y2='" + formatF32(handleScreenY) +
          "' stroke='" + spline.color + "' stroke-width='1' stroke-dasharray='3,3' class='cp-handle'/>";
        svg += "<circle cx='" + formatF32(handleScreenX) + "' cy='" + formatF32(handleScreenY) +
          "' r='2.5' fill='#ffffff' stroke='" + spline.color + "' stroke-width='1.5' class='cp-handle'/>";
      }

      // Backward handle (CP1)
      if (node.cp1X != 0.0 || node.cp1Z != 0.0) {
        const handleScreenX = screenTopDownX(node.posX + node.cp1X, minLateralX, lateralSpan, scale);
        const handleScreenY = screenTopDownZ(node.posZ + node.cp1Z, minLongitudinalZ, longitudinalSpan, scale);
        svg += "<line x1='" + formatF32(nodeScreenX) + "' y1='" + formatF32(nodeScreenY) +
          "' x2='" + formatF32(handleScreenX) + "' y2='" + formatF32(handleScreenY) +
          "' stroke='" + spline.color + "' stroke-width='1' stroke-dasharray='3,3' class='cp-handle'/>";
        svg += "<circle cx='" + formatF32(handleScreenX) + "' cy='" + formatF32(handleScreenY) +
          "' r='2.5' fill='#ffffff' stroke='" + spline.color + "' stroke-width='1.5' class='cp-handle'/>";
      }

      // Node point marker
      svg += "<circle cx='" + formatF32(nodeScreenX) + "' cy='" + formatF32(nodeScreenY) +
        "' r='4' fill='" + spline.color + "' stroke='#ffffff' stroke-width='1.5'>";
      svg += "<title>" + spline.role + " Node " + nodeIndex.toString() + " (" + formatF32(node.posX) + ", " + formatF32(node.posY) + ", " + formatF32(node.posZ) + ")</title>";
      svg += "</circle>";
      svg += "<text x='" + formatF32(nodeScreenX + 5.0) + "' y='" + formatF32(nodeScreenY - 5.0) +
        "' font-size='9' font-family='sans-serif' font-weight='600' fill='#1e293b'>" + nodeIndex.toString() + "</text>";
    }

    svg += "</g>";
  }

  svg += "</svg>";
  return svg;
}

export function renderElevationSvg(
  splines: Array<TrackSplineInfo>,
  projectAlongXAxis: bool,
  minHorizontalTrack: f32,
  maxHorizontalTrack: f32,
  minVerticalHeight: f32,
  maxVerticalHeight: f32,
  scale: f32
): string {
  const horizontalTrackSpan = maxHorizontalTrack - minHorizontalTrack;
  const verticalHeightSpan = maxVerticalHeight - minVerticalHeight;

  let svg = "<svg class='projection-svg' viewBox='0 0 460 360' xmlns='http://www.w3.org/2000/svg'>";
  svg += "<rect x='0' y='0' width='460' height='360' fill='#f8fafc' stroke='#e2e8f0'/>";

  svg += "<line x1='35' y1='325' x2='425' y2='325' stroke='#cbd5e1' stroke-width='1.5'/>";
  svg += "<line x1='35' y1='35' x2='35' y2='325' stroke='#cbd5e1' stroke-width='1.5'/>";
  const axisLabel = projectAlongXAxis ? "Horizontal Track (X Axis) &rarr;" : "Horizontal Track (Z Axis) &rarr;";
  svg += "<text x='230' y='350' text-anchor='middle' font-size='11' font-family='sans-serif' fill='#64748b'>" + axisLabel + "</text>";
  svg += "<text x='15' y='180' text-anchor='middle' font-size='11' font-family='sans-serif' fill='#64748b' transform='rotate(-90 15 180)'>Height (Y Axis) &rarr;</text>";

  svg += "<text x='38' y='338' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(minHorizontalTrack) + "</text>";
  svg += "<text x='425' y='338' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(maxHorizontalTrack) + "</text>";
  svg += "<text x='30' y='323' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(minVerticalHeight) + "</text>";
  svg += "<text x='30' y='45' text-anchor='end' font-size='10' font-family='monospace' fill='#94a3b8'>" + formatF32(maxVerticalHeight) + "</text>";

  for (let splineIndex = 0; splineIndex < splines.length; splineIndex++) {
    const spline = splines[splineIndex];
    if (spline.nodes.length < 2) continue;

    svg += "<g class='spline-geom " + spline.cssClass + "'>";

    let pathDefinition = "";
    const nodeCount = spline.nodes.length;
    const segmentCount = spline.cyclic ? nodeCount : nodeCount - 1;

    for (let segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++) {
      const startNode = spline.nodes[segmentIndex];
      const nextNodeIndex = (segmentIndex + 1) % nodeCount;
      const endNode = spline.nodes[nextNodeIndex];

      const startHorizontalCoord = projectAlongXAxis ? startNode.posX : startNode.posZ;
      const startForwardControlOffsetHorizontal = projectAlongXAxis ? startNode.cp2X : startNode.cp2Z;
      const endBackwardControlOffsetHorizontal = projectAlongXAxis ? endNode.cp1X : endNode.cp1Z;
      const endHorizontalCoord = projectAlongXAxis ? endNode.posX : endNode.posZ;

      const startScreenX = screenElevationHorizontal(startHorizontalCoord, minHorizontalTrack, horizontalTrackSpan, scale);
      const startScreenY = screenElevationVertical(startNode.posY, minVerticalHeight, verticalHeightSpan, scale);
      const startForwardControlScreenX = screenElevationHorizontal(startHorizontalCoord + startForwardControlOffsetHorizontal, minHorizontalTrack, horizontalTrackSpan, scale);
      const startForwardControlScreenY = screenElevationVertical(startNode.posY + startNode.cp2Y, minVerticalHeight, verticalHeightSpan, scale);
      const endBackwardControlScreenX = screenElevationHorizontal(endHorizontalCoord + endBackwardControlOffsetHorizontal, minHorizontalTrack, horizontalTrackSpan, scale);
      const endBackwardControlScreenY = screenElevationVertical(endNode.posY + endNode.cp1Y, minVerticalHeight, verticalHeightSpan, scale);
      const endScreenX = screenElevationHorizontal(endHorizontalCoord, minHorizontalTrack, horizontalTrackSpan, scale);
      const endScreenY = screenElevationVertical(endNode.posY, minVerticalHeight, verticalHeightSpan, scale);

      if (segmentIndex == 0) {
        pathDefinition += "M " + formatF32(startScreenX) + " " + formatF32(startScreenY);
      }
      pathDefinition += " C " + formatF32(startForwardControlScreenX) + " " + formatF32(startForwardControlScreenY) + " " +
        formatF32(endBackwardControlScreenX) + " " + formatF32(endBackwardControlScreenY) + " " +
        formatF32(endScreenX) + " " + formatF32(endScreenY);
    }

    svg += "<path d='" + pathDefinition + "' fill='none' stroke='" + spline.color + "' stroke-width='3' stroke-linecap='round'/>";

    for (let nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
      const node = spline.nodes[nodeIndex];
      const nodeHorizontalCoord = projectAlongXAxis ? node.posX : node.posZ;
      const forwardControlOffsetHorizontal = projectAlongXAxis ? node.cp2X : node.cp2Z;
      const backwardControlOffsetHorizontal = projectAlongXAxis ? node.cp1X : node.cp1Z;

      const nodeScreenX = screenElevationHorizontal(nodeHorizontalCoord, minHorizontalTrack, horizontalTrackSpan, scale);
      const nodeScreenY = screenElevationVertical(node.posY, minVerticalHeight, verticalHeightSpan, scale);

      // Forward handle (CP2)
      if (forwardControlOffsetHorizontal != 0.0 || node.cp2Y != 0.0) {
        const handleScreenX = screenElevationHorizontal(nodeHorizontalCoord + forwardControlOffsetHorizontal, minHorizontalTrack, horizontalTrackSpan, scale);
        const handleScreenY = screenElevationVertical(node.posY + node.cp2Y, minVerticalHeight, verticalHeightSpan, scale);
        svg += "<line x1='" + formatF32(nodeScreenX) + "' y1='" + formatF32(nodeScreenY) +
          "' x2='" + formatF32(handleScreenX) + "' y2='" + formatF32(handleScreenY) +
          "' stroke='" + spline.color + "' stroke-width='1' stroke-dasharray='3,3' class='cp-handle'/>";
        svg += "<circle cx='" + formatF32(handleScreenX) + "' cy='" + formatF32(handleScreenY) +
          "' r='2.5' fill='#ffffff' stroke='" + spline.color + "' stroke-width='1.5' class='cp-handle'/>";
      }

      // Backward handle (CP1)
      if (backwardControlOffsetHorizontal != 0.0 || node.cp1Y != 0.0) {
        const handleScreenX = screenElevationHorizontal(nodeHorizontalCoord + backwardControlOffsetHorizontal, minHorizontalTrack, horizontalTrackSpan, scale);
        const handleScreenY = screenElevationVertical(node.posY + node.cp1Y, minVerticalHeight, verticalHeightSpan, scale);
        svg += "<line x1='" + formatF32(nodeScreenX) + "' y1='" + formatF32(nodeScreenY) +
          "' x2='" + formatF32(handleScreenX) + "' y2='" + formatF32(handleScreenY) +
          "' stroke='" + spline.color + "' stroke-width='1' stroke-dasharray='3,3' class='cp-handle'/>";
        svg += "<circle cx='" + formatF32(handleScreenX) + "' cy='" + formatF32(handleScreenY) +
          "' r='2.5' fill='#ffffff' stroke='" + spline.color + "' stroke-width='1.5' class='cp-handle'/>";
      }

      svg += "<circle cx='" + formatF32(nodeScreenX) + "' cy='" + formatF32(nodeScreenY) +
        "' r='4' fill='" + spline.color + "' stroke='#ffffff' stroke-width='1.5'>";
      svg += "<title>" + spline.role + " Node " + nodeIndex.toString() + " (" + formatF32(node.posX) + ", " + formatF32(node.posY) + ", " + formatF32(node.posZ) + ")</title>";
      svg += "</circle>";
      svg += "<text x='" + formatF32(nodeScreenX + 5.0) + "' y='" + formatF32(nodeScreenY - 5.0) +
        "' font-size='9' font-family='sans-serif' font-weight='600' fill='#1e293b'>" + nodeIndex.toString() + "</text>";
    }

    svg += "</g>";
  }

  svg += "</svg>";
  return svg;
}

export function renderSplineProjections(
  splines: Array<TrackSplineInfo>,
  minX: f32,
  maxX: f32,
  minY: f32,
  maxY: f32,
  minZ: f32,
  maxZ: f32
): string {
  let rawLateralXSpan = maxX - minX;
  if (rawLateralXSpan < 0.1) rawLateralXSpan = 1.0;
  let rawVerticalHeightSpan = maxY - minY;
  if (rawVerticalHeightSpan < 0.1) rawVerticalHeightSpan = 1.0;
  let rawLongitudinalZSpan = maxZ - minZ;
  if (rawLongitudinalZSpan < 0.1) rawLongitudinalZSpan = 1.0;

  // Pad bounds by 8%
  const paddingLateralX = rawLateralXSpan * 0.08;
  const paddingVerticalHeight = rawVerticalHeightSpan * 0.08;
  const paddingLongitudinalZ = rawLongitudinalZSpan * 0.08;

  const paddedMinLateralX = minX - paddingLateralX;
  const paddedMaxLateralX = maxX + paddingLateralX;
  const paddedMinVerticalHeight = minY - paddingVerticalHeight;
  const paddedMaxVerticalHeight = maxY + paddingVerticalHeight;
  const paddedMinLongitudinalZ = minZ - paddingLongitudinalZ;
  const paddedMaxLongitudinalZ = maxZ + paddingLongitudinalZ;

  const paddedLateralXSpan = paddedMaxLateralX - paddedMinLateralX;
  const paddedVerticalHeightSpan = paddedMaxVerticalHeight - paddedMinVerticalHeight;
  const paddedLongitudinalZSpan = paddedMaxLongitudinalZ - paddedMinLongitudinalZ;

  const scaleTopDown = fmin(390.0 / paddedLateralXSpan, 290.0 / paddedLongitudinalZSpan);

  // Elevation view projects along the longer horizontal track axis (X or Z)
  const projectAlongXAxis = paddedLateralXSpan >= paddedLongitudinalZSpan;
  const paddedMinHorizontalTrack = projectAlongXAxis ? paddedMinLateralX : paddedMinLongitudinalZ;
  const paddedMaxHorizontalTrack = projectAlongXAxis ? paddedMaxLateralX : paddedMaxLongitudinalZ;
  const paddedHorizontalTrackSpan = paddedMaxHorizontalTrack - paddedMinHorizontalTrack;
  const scaleElevation = fmin(390.0 / paddedHorizontalTrackSpan, 290.0 / paddedVerticalHeightSpan);

  let html = "<div class='projections-row' style='display:flex; flex-wrap:wrap; gap:16px; margin-bottom:20px;'>";

  // Card 1: Top-down view
  html += "<div class='projection-card' style='flex:1; min-width:380px; border:1px solid #e2e8f0; border-radius:8px; background:#fff; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.05);'>";
  html += "<div style='background:#f8fafc; padding:8px 12px; border-bottom:1px solid #e2e8f0; font-weight:600; font-size:13px; color:#334155;'>";
  html += "Top-Down View (XZ Plane &mdash; Lateral/Longitudinal Track Geometry)</div>";
  html += renderTopDownSvg(splines, paddedMinLateralX, paddedMaxLateralX, paddedMinLongitudinalZ, paddedMaxLongitudinalZ, scaleTopDown);
  html += "</div>";

  // Card 2: Elevation view
  html += "<div class='projection-card' style='flex:1; min-width:380px; border:1px solid #e2e8f0; border-radius:8px; background:#fff; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.05);'>";
  html += "<div style='background:#f8fafc; padding:8px 12px; border-bottom:1px solid #e2e8f0; font-weight:600; font-size:13px; color:#334155;'>";
  html += "Elevation View (" + (projectAlongXAxis ? "X" : "Z") + "-Y Profile &mdash; Vertical Height)</div>";
  html += renderElevationSvg(splines, projectAlongXAxis, paddedMinHorizontalTrack, paddedMaxHorizontalTrack, paddedMinVerticalHeight, paddedMaxVerticalHeight, scaleElevation);
  html += "</div>";

  html += "</div>";
  return html;
}
