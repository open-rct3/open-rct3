// OVL 
//
// Authors:
//  - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.IO;
using System.Text;

namespace OVL;

public struct OVLHeader { }

public class OVL {
  private File handle;

  public OVL(File handle) {
    this.handle = handle;
  }

  static OVL Open(string fileName) {
    var ovl = new OVL(File.Open(fileName));
  }
}
