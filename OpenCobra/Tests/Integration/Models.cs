using System.Collections.Generic;
using OpenCobra.OVL;

namespace OvlTestBench.Tests;

public record OvlPair(string Name, string CommonPath, string UniquePath) {
  public List<OvlFile> Files = [];
}
