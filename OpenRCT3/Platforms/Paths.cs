namespace OpenRCT3.Platforms;

public static class Paths {
  // TODO: Ensure this works on macOS and Linux
  public static string UserDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

  public static string CampaignsDirectory => Path.Combine(UserDocuments, "RCT3", "Campaigns");
  public static string CoastersDirectory => Path.Combine(UserDocuments, "RCT3", "Coasters");
  public static string FireworkEffectsDirectory => Path.Combine(UserDocuments, "RCT3", "FireworkEffects");
  public static string FireworksDirectory => Path.Combine(UserDocuments, "RCT3", "Fireworks");
  public static string LaserEffectsDirectory => Path.Combine(UserDocuments, "RCT3", "LaserEffects");
  public static string LaserWritingDirectory => Path.Combine(UserDocuments, "RCT3", "LaserWriting");
  public static string NewScenariosDirectory => Path.Combine(UserDocuments, "RCT3", "Start New Scenarios");
  public static string ParksDirectory => Path.Combine(UserDocuments, "RCT3", "Parks");
  public static string PeepsDirectory => Path.Combine(UserDocuments, "RCT3", "Peeps");
  public static string PoolsDirectory => Path.Combine(UserDocuments, "RCT3", "Pools");
  public static string ScenariosDirectory => Path.Combine(UserDocuments, "RCT3", "Scenarios");
  public static string StructuresDirectory => Path.Combine(UserDocuments, "RCT3", "Structures");
  public static string WaterJetEffectsDirectory => Path.Combine(UserDocuments, "RCT3", "WaterJetEffects");
}
