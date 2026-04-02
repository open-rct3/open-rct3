using System.Collections.Generic;
using OVL;

namespace OvlTestBench.Tests;

public class OvlFile {
  public string Path = "";
  public OvlType Type;
}

public class OvlPair {
  public string Name = "";
  public string CommonPath = "";
  public string UniquePath = "";
  public List<OvlFile> Files = new();
}
