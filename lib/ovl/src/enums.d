/// License: GPL 2.0
module ovl.enums;

enum Addon {
  vanilla =                   0x0000,
  soaked =                    0x0001,
  wild =                      0x0002,
  soakedHi =                 0x0200,
  wildHi =                   0x0300
}

enum Recolorable : ulong {
  first = 1,
  second = 2,
  third = 4
}

enum SvdLodType {
  static_ = 0,
  animated =3,
  billboard = 4
}

enum SvdFlags {
  /// Set on Trees, Shrubs, Ferns, and also on lots of walls
  greenery =             0x00000001,
  noShadow =            0x00000002,
  flower =              0x00000002,
  rotation =            0x00000004,
  unknown01 =           0x00000010,
  unknown02 =           0x00000020,
  unknown03 =           0x00000040,
  animatedPreview =     0x00000800,
  unknownGiantFerris =  0x00001000,
  soaked =              0x01000000,
  wild =                0x02000000,
  soakedOrWild =        0x03000000
}

enum SidPosition {
  Tile_Full =                 0,
  Path_Edge_Inner =           1,
  Path_Edge_Outer =           2,
  Wall =                      3,
  Tile_Quarter =              4,
  Tile_Half =                 5,
  Path_Center =               6,
  Corner =                    7,
  Path_Edge_Join =            8
}

enum SidFlags {
  Unknown_01 =            0x00000001,
  Unknown_02 =            0x00000002, ///< Never set?
  Unknown_03 =            0x00000004,
  Unknown_04 =            0x00000008,
  Delete_On_Ground_Change=0x00000010,
  Ground_Change =         0x00000020, ///< Goes with ground if non-colliding, blocks ground change otherwise
  Unknown_07 =            0x00000040,
  Unknown_08 =            0x00000080,
  Support_Block =         0x00000100, ///< Blocks supports (from above only, may require collision detection)?
  ///< Set on terrain blocks and courtyard column
  Unknown_10 =            0x00000200, ///< Several of Track2-Track6, Track8-Track15, Track18, Track22, Track24, Track26-Track36, Track59, Track60
  ///<   TrackBased01-TrackBased04, TrackBased07, TrackBased14, TrackBased21-TrackBased24, TrackBased33, TrackBased37
  ///< TrackBased10, TrackBased12: SteepSlopeDown
  Unknown_11 =            0x00000400, ///< Few of Track20;
  ///< Most of Track3, Track31, Track33_CT, Track34 (those that do not have Flag27), Track35, Track59, TrackBased09, TrackBased22
  ///< All sections of Track2, Track4 - Track6, Track8 - Track15, Track18, Track22, Track24, Track26-Track29, Track32, Track36, Track60,
  ///<   TrackBased01-TrackBased04, TrackBased07-TrackBased08, TrackBased14, TrackBased21, TrackBased23, TrackBased24, TrackBased33, TrackBased37
  Unknown_12 =            0x00000800, ///< Some sections of Track6, Track9, Track10, Track60; Most sections of Track8, Track12, Track13, TrackBased04
  Skew =                  0x00001000, ///< Object skews with ground (like fences). Might depend on other things
  Smooth_Height =         0x00002000, ///< Smooth height change, not in increments.
  Unknown_15 =            0x00004000, ///< Some Supports: ts2, ts3
  Unknown_16 =            0x00008000, ///< Some Supports: ts5
  On_Water_Only =         0x00010000, ///< Removes base of flats and makes them placable on water only. Asso on some stations
  Unknown_18 =            0x00020000, ///< Never set
  Unknown_19 =            0x00040000, ///< Never set
  Ride_Leave_After_Stop = 0x00080000, ///< Peeps leave from end of stop animation instead of idle.
  Unknown_21 =            0x00100000, ///< Track33_CT, Vertical chain; Trackbased21, Bowl and Funnel
  Ride_Exact_Fence =      0x00200000, ///< On rides, fence only around parts touching the floor (collision detection)
  Unknown_23 =            0x00400000, ///< All ride events
  Fences =                0x00800000, ///< Set for fences and fence-like objects. Purpose unknown.
  Unknown_25 =            0x01000000, ///< Jungle Trees
  Billboard_Menu =        0x02000000, ///< Puts object in billboard menu
  Unknown_27 =            0x04000000, ///< Right part of joint sections for the splitting cosater
  Unknown_28 =            0x08000000, ///< Never set
  Unknown_29 =            0x10000000, ///< Never set?
  Unknown_30 =            0x20000000, ///< Never set?
  Unknown_31 =            0x40000000, ///< Never set?
  Unknown_32 =            0x80000000, ///< Never set?
}

enum SidSquareFlags {
  Collision =             0x00000001, ///< Activates collision detection
  Supports =              0x00000002, ///< Show supports. Needs Collision and the respective setting
  Unknown_35 =            0x00000004, ///< Never set?
  Unknown_36 =            0x00000008,
  Unknown_37 =            0x00000010,
  Unknown_38 =            0x00000020,
  Unknown_39 =            0x00000040,
  Unknown_40 =            0x00000080,
  Unknown_41 =            0x00000100,
  Unknown_42 =            0x00000200,
  Unknown_43 =            0x00000400,
  Unknown_44 =            0x00000800
}

enum SidType {
  Tree =                       0,
  Plant =                      1,
  Shrub =                      2,
  Flowers =                    3,
  Fence =                      4,
  Wall_Misc =                  5,
  Path_Lamp =                  6,
  Scenery_Small =              7,
  Scenery_Medium =             8,
  Scenery_Large =              9,
  Scenery_Anamatronic =       10,
  Scenery_Misc =              11,
  Support_Middle =            12,
  Support_Top =               13,
  Support_Bottom =            14,
  Support_Bottom_Extra =      15,
  Support_Girder =            16,
  Support_Cap =               17,
  Ride_Track =                18,
  Path =                      19,
  Park_Entrance =             20,
  Litter =                    21,
  Guest_Inject =              22,
  Ride =                      23,
  Ride_Entrance =             24,
  Ride_Exit =                 25,
  Keep_Clear_Fence =          26,
  Stall =                     27,
  Ride_Event =                28,
  Firework =                  29,
  Litter_Bin =                30,
  Bench =                     31,
  Sign =                      32,
  Photo_Point =               33,
  Wall_Straight =             34,
  Wall_Roof =                 35,
  Wall_Corner =               36,
  //new for Soaked!
  Water_Cannon =              37,
  Pool_Piece =                38,
  Pool_Extra =                39,
  Changing_Room =             40,
  Laser_Dome =                41, //???
  Water_Jet =                 42, //???
  Terrain_Piece =             43, //???
  Particle_Effect =           44, //???
  //new for Wild!
  Animal_Fence =              45, //???
  Animal_Misc =               46  //???
}

// TODO: Tracked rides. See https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/rct3constants.h#L505
