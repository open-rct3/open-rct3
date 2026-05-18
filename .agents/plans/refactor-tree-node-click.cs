private void LoadOvl(Ovl ovl) {
    currentOvl = ovl;
    _nodeEntries.Clear();
    contentPanel.ShowEmpty(true);

    Text = $@"OVL Dumper — {GetDocumentName(ovl)}";

    treeView.BeginUpdate();
    treeView.Nodes.Clear();

    if (ovl.Keys.Count > 0)
        BuildFileNodes(ovl);
    else
        BuildFallbackNode(ovl);

    ExpandTreeNodes();
    treeView.EndUpdate();

    UpdateStatusBar();
    FitSidebarToContent(ClientSize.Width / 2);
}

private static string GetDocumentName(Ovl ovl) {
    var docName = Path.GetFileName(ovl.Keys.First().Path);
    var lower = docName.ToLower();
    if (lower.EndsWith(".common.ovl")) return docName[..^".common.ovl".Length];
    if (lower.EndsWith(".unique.ovl")) return docName[..^".unique.ovl".Length];
    if (lower.EndsWith(".ovl"))        return docName[..^".ovl".Length];
    return Ovl.UnnamedOvl;
}

private void BuildFileNodes(Ovl ovl) {
    EnsureTreeImageList();

    var entriesByFile = ovl.Keys
        .GroupBy(e => e.Path)
        .ToDictionary(g => g.Key, g => g.ToList());

    var fileNames = entriesByFile.Keys
        .OrderBy(f => f.Contains(".unique.") ? 1 : 0)
        .ToList();

    if (fileNames.Count == 0)
        fileNames.Add(Path.GetFileName(ovl.Keys.First().Path));

    foreach (var fileName in fileNames)
        BuildFileNode(fileName, entriesByFile);
}

private void BuildFileNode(string fileName, Dictionary<string, List<OvlEntry>> entriesByFile) {
    var fileNode = treeView.Nodes.Add(fileName, Path.GetFileName(fileName));
    fileNode.ImageKey = "FolderOpen";
    fileNode.SelectedImageKey = "FolderOpen";

    if (!entriesByFile.TryGetValue(fileName, out var entries)) return;

    var resolved = ResolveEntries(entries);

    var resourceGroups = BuildResourceGroups(resolved);
    var groupedNames = new HashSet<string>(
        resourceGroups.Values.SelectMany(g => g).Select(r => r.displayName));

    var remainingEntries = resolved
        .Where(r => !groupedNames.Contains(r.displayName))
        .ToList();

    // FIXME: No nodes shall be nested under nodes of type "Terrain Type"
    // FileType.TerrainType

    var groupsByName = remainingEntries
        .GroupBy(r => r.displayName)
        .Where(g => g.Count() > 1)
        .ToDictionary(g => g.Key, g => g.ToList());
    var duplicateNames = new HashSet<string>(groupsByName.Keys);

    AddAnimatedTextureNodes(fileNode, resolved, resourceGroups, groupedNames);
    AddDuplicateNameNodes(fileNode, groupsByName);
    AddLeafNodes(fileNode, remainingEntries, duplicateNames);
}

private static List<(OvlEntry entry, string displayName, FileType symbolFileType)> ResolveEntries(
    List<OvlEntry> entries) =>
    entries.Select(entry => {
        var colonIdx = entry.Name.LastIndexOf(':');
        if (colonIdx >= 0)
            return (entry, entry.Name[..colonIdx], entry.Name[(colonIdx + 1)..].ToFileType());
        return (entry, entry.Name, entry.Type);
    }).ToList();

private static Dictionary<string, List<(OvlEntry entry, string displayName, FileType symbolFileType)>>
    BuildResourceGroups(List<(OvlEntry entry, string displayName, FileType symbolFileType)> resolved) =>
    resolved
        .Where(r => (r.symbolFileType == FileType.Texture || r.symbolFileType == FileType.Flic)
                    && EndsWithDigit(r.displayName))
        .GroupBy(r => StripTrailingDigits(r.displayName))
        .Where(g => g.Count() > 1)
        .ToDictionary(g => g.Key, g => g.OrderBy(r => r.displayName).ToList());

private void AddAnimatedTextureNodes(
    TreeNode fileNode,
    List<(OvlEntry entry, string displayName, FileType symbolFileType)> resolved,
    Dictionary<string, List<(OvlEntry entry, string displayName, FileType symbolFileType)>> resourceGroups,
    HashSet<string> groupedNames) {
    foreach (var (_, displayName, symbolFileType) in resolved) {
        if (!groupedNames.Contains(displayName)) continue;

        var baseName = StripTrailingDigits(displayName);
        if (!resourceGroups.Remove(baseName, out var group)) continue;

        var parentNode = fileNode.Nodes.Add(baseName);
        parentNode.ImageKey = FileType.Flic.ToIconName();
        parentNode.SelectedImageKey = FileType.Flic.ToIconName();
        parentNode.Tag = FileType.Flic;
        parentNode.ToolTipText =
            $"Animated texture ({group.Count} frames) \u00b7 Loader: {symbolFileType.ToDisplayName()}";

        foreach (var frame in group) {
            var childNode = parentNode.Nodes.Add(frame.displayName[baseName.Length..]);
            childNode.ImageKey = FileType.Texture.ToIconName();
            childNode.SelectedImageKey = FileType.Texture.ToIconName();
            childNode.ToolTipText = frame.symbolFileType.ToDisplayName();
            childNode.Tag = frame.symbolFileType;
            _nodeEntries[childNode] = frame.entry;
        }
    }
}

private void AddDuplicateNameNodes(
    TreeNode fileNode,
    Dictionary<string, List<(OvlEntry entry, string displayName, FileType symbolFileType)>> groupsByName) {
    foreach (var (name, group) in groupsByName) {
        var commonType = group.Select(r => r.symbolFileType).Distinct().Count() == 1
            ? group.First().symbolFileType
            : FileType.Unknown;

        var parentNode = fileNode.Nodes.Add($"{group.Count} {Pluralize(commonType.ToDisplayName())}");
        parentNode.ImageKey = commonType == FileType.Unknown ? "FileMultipleOutline" : commonType.ToGroupIconName();
        parentNode.SelectedImageKey = parentNode.ImageKey;
        parentNode.Tag = commonType;
        parentNode.ToolTipText = $"{group.Count} entries named \"{name}\"";

        foreach (var (entry, _, symbolFileType) in group) {
            var childIconKey = symbolFileType == FileType.Flic
                ? FileType.Texture.ToIconName()
                : symbolFileType.ToIconName();
            var childNode = parentNode.Nodes.Add(name);
            childNode.ImageKey = childIconKey;
            childNode.SelectedImageKey = childIconKey;
            childNode.Tag = symbolFileType;
            childNode.ToolTipText = symbolFileType.ToDisplayName();
            _nodeEntries[childNode] = entry;
        }
    }
}

private void AddLeafNodes(
    TreeNode fileNode,
    List<(OvlEntry entry, string displayName, FileType symbolFileType)> remainingEntries,
    HashSet<string> duplicateNames) {
    foreach (var (entry, displayName, symbolFileType) in remainingEntries) {
        if (duplicateNames.Contains(displayName)) continue;

        var iconKey = symbolFileType == FileType.Flic
            ? FileType.Texture.ToIconName()
            : symbolFileType.ToIconName();
        var node = fileNode.Nodes.Add(displayName);
        node.ImageKey = iconKey;
        node.SelectedImageKey = iconKey;
        node.Tag = symbolFileType;
        node.ToolTipText = symbolFileType.ToDisplayName();
        _nodeEntries[node] = entry;
    }
}

private void BuildFallbackNode(Ovl ovl) {
    EnsureTreeImageList();
    var fileName = Path.GetFileName(ovl.Keys.First().Path);
    var root = treeView.Nodes.Add(fileName, fileName);
    root.ImageKey = "FolderOpen";
    root.SelectedImageKey = "FolderOpen";
    foreach (var header in ovl.Keys) {
        var node = root.Nodes.Add(header.Name);
        node.ImageKey = header.Type.ToIconName();
        node.SelectedImageKey = node.ImageKey;
        node.Tag = header.Type;
        node.ToolTipText = header.Type.ToDisplayName();
    }
}

private void ExpandTreeNodes() {
    foreach (TreeNode fileNode in treeView.Nodes) {
        fileNode.Expand();
        foreach (TreeNode child in fileNode.Nodes) {
            if (child.Tag is FileType ft && ft != FileType.Flic && !IsDuplicateGroup(child.Text))
                child.Expand();
        }
    }
}
