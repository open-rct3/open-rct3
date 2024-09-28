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
  private FileStream file;

  public OVL(FileStream stream) {
    this.file = stream;
  }

  static OVL Open(string fileName) {
    return new OVL(File.Open(fileName, FileMode.Open));
  }
}
