/// License: GPL 2.0
module ovl.gui;

struct BitmapTable {
  /// Always `0`.
  ulong unk;
  /// Number of stored textures.
  ulong count;
}

struct FlicData {
  /// Always `0` on disk.
  ulong* data = null;
  /// Always `1`.
  ulong unk1 = 1;
  /// Always `1.0`.
  float unk2 = 1;
}

struct FlicHeader {
  ulong format;
  ulong width;
  ulong height;
  ulong mipCount;
}

struct FlicMipHeader {
  ulong mipWidth;
  ulong mipHeight;
  ulong pitch;
  ulong blocks;
}

struct Flic {
  FlicHeader header;
  FlicMipHeader mipHeader;
  ubyte[] data;
}

struct Icon {
  string name;
  string texture;
  Flic flic;
}

struct TextureStruct {
  /// Always `0x70007`.
  ulong unk1;
  /// Always `0x70007`.
  ulong unk2;
  /// Always `0x70007`.
  ulong unk3;
  /// Always `0x70007`.
  ulong unk4;
  /// Always `0x70007`.
  ulong unk5;
  /// Always `0x70007`.
  ulong unk6;
  /// Always `0x70007`.
  ulong unk7;
  /// Always `0x70007`.
  ulong unk8;
  /// Usually 1.
  /// Seems to be a count, either for the `TextureStruct.Flic` array or for the array inside `TextureStruct2`.
  ulong unk9;
  /// Usually 8.
  /// Other values:
  /// - 4 (if unk9 and unk12 are 0)
  /// - 12 (if unk9 and unk12 are 2)
  ulong unk10;
  /// Usually 0x10.
  /**< Other values:
	                      *     - 0x00 (GUIRendererBitmap:txs, UIRendererColour:txs, GUIRendererZMask:txs, SystemFont:txs, VisibleFootprint:txs)
	                      *     - 0x0D
	                      */
  ulong unk11;
  /// Symbol reference for a TXS (texture style).
  ubyte[] TextureData;
  /**< Always 0 on disk files, use GUIIcon:txs as a symbol resolve for icons.\n
                                 * Common symrefs:
                                 *    - GUIIcon:txs. For icon textures.
                                 *    - PathGround:txs. For paths.
                                 *    - TerrainBlending:txs. For terrain.
                                 *    - TerrainCliff:txs. For cliffs.
                                 *
                                 * Other symrefs (probably not useful for custom stuff):
                                 *    - EnvMap:txs
                                 *    - GUIRendererBitmap:txs. unk9 and unk12 (low) are 0.
                                 *    - GUIRendererColour:txs. unk9 and unk12 (low) are 0.
                                 *    - GUIRendererZMask:txs. unk9 and unk12 (low) are 0.
                                 *    - GUISkin:txs
                                 *    - GUISkinTile:txs
                                 *    - GUISolidColour:txs. unk9 and unk12 (low) are 0.
                                 *    - GUISolidColourOpaque:txs. unk9 and unk12 (low) are 0.
                                 *    - HorizonShadowUV:txs
                                 *    - LaserTS:txs
                                 *    - LaserCapTS:txs
                                 *    - PoolPreviewGrid:txs
                                 *    - PoolPreviewGrid2:txs. unk9 and unk12 (low) are 2.
                                 *    - Rope:txs
                                 *    - ShapeStandard:txs
                                 *    - SolidColourFont:txs
                                 *    - SystemFont:txs
                                 *    - TerrainCircleBack:txs
                                 *    - TerrainCircleFront:txs
                                 *    - TerrainCliffTransparent:txs
                                 *    - TerrainContour:txs. unk9 and unk12 (low) are 2.
                                 *    - TerrainDebug:txs. unk9 and unk12 (low) are 0.
                                 *    - TerrainDetail:txs
                                 *    - TerrainDetailAndLightmap:txs. unk9 and unk12 (low) are 0.
                                 *    - TerrainFakeUp:txs
                                 *    - TerrainFog:txs
                                 *    - TerrainGrid:txs
                                 *    - TerrainGrid2StageDummy:txs. unk9 and unk12 (low) are 0.
                                 *    - TerrainHighlight:txs
                                 *    - TerrainHole:txs
                                 *    - TerrainPosition:txs
                                 *    - TerrainPositionNoZ:txs
                                 *    - TerrainTransparent:txs
                                 *    - TerrainZRender:txs
                                 *    - VisibleFootprint:txs
                                 *    - Underground:txs
                                 *    - WaypointHighlight:txs
                                 *    - ZFillAlpha:txs
                                 */
  /// Usually 1.
  /**< loword is a count like unk9 (see there).
                          * hiword is addonpack and in this case determines the number of unknown longs at the end of
                          * the structure.
                          */
  ulong unk12;
  /// always points to pointer before flic data
  /**< Is an array with either unk9 or unk12 members.
                        * Not relocated if those are 0.
                        */
  FlicData[] Flic;
  TextureStruct2 *ts2;
}

struct TextureStruct2 {
  /// points back to the TextureStruct
  TextureStruct *Texture;
  /// points to the FlicStruct for this item.
  /// Remarks: This is actually seems to be an array with either TextureStruct.unk9 or TextureStruct.unk12 (loword) items.
  FlicData Flic;
}

struct TextureStruct2Ext {
  /// points back to the TextureStruct
  TextureStruct *Texture;
  /// points to the FlicStruct for this item.
  FlicData Flic;
  /// points to the second FlicStruct for this item.
  FlicData Flic2;
}

struct Tex {
  TextureStruct t1;
  TextureStruct2 t2;
}

class GuiSkin {
  string name;
  ulong top, left, bottom, right;
  string texture;
  GuiSkin[] skins;

  this(string name, string texture, ulong top, ulong left, ulong bottom, ulong right) {
    this.name = name;
    this.texture = texture;
    this.top = top;
    this.left = left;
    this.bottom = bottom;
    this.right = right;
  }

  ulong size() const @property {
    import std.algorithm : map, sum;
    return skins.map!(
      skin => skin.name.length + 5 + GuiSkinItemPosition.sizeof + GuiSkin.sizeof
      // FIXME: skin => skin.name.length + 5 + GuiSkinItemPosition.sizeof + GuiSkin.sizeof + loader.length + symbols.length + symbolRefs.length
    ).sum;
  }
}

struct GuiSkinItemPosition {
  ulong left;
  ulong top;
  ulong right;
  ulong bottom;
}

struct GuiSkinItem {
  /// `0` on disk files.
  ulong unk1;
  /// `0` on disk files.
  TextureStruct* tex;
  GuiSkinItemPosition* pos;
  /// `0` on disk files.
  ulong unk2;
}

struct GSI {
  GuiSkinItem item;
  GuiSkinItemPosition position;
}
