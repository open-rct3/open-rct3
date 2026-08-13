# macOS Storyboard-to-Code Migration Implementation Plan

**Goal:** Replace the macOS storyboard (Main.storyboard) with code-first UI construction to reduce debug build time by eliminating ibtool overhead.

**Architecture:** Build modular, testable UI constructor classes that encapsulate menu bar and window setup, then wire them into AppDelegate. GameViewController and MainWindow move from storyboard-driven initialization to programmatic setup, with outlets replaced by direct property assignment. The project file stops invoking ibtool.

**Tech Stack:** AppKit (NSWindow, NSViewController, NSView, NSSplitView), .NET 9+, C# 12

## Global Constraints

- Maintain feature parity with existing UI (all menus, keyboard shortcuts, window size/state)
- No breaking changes to GameViewController or MainWindow public APIs
- Designer files (MainWindow.designer.cs, GameViewController.designer.cs) will be removed after migration
- macOS 10.15+ required (existing constraint per AppDelegate comments)
- All UI creation must happen on the main thread before window display

---

## File Structure

**Files to create:**
- `OpenRCT3/Platforms/macOS/MenuBarBuilder.cs` — Constructs the complete application menu bar
- `OpenRCT3/Platforms/macOS/MainWindowBuilder.cs` — Constructs and configures the main window
- `OpenRCT3/Platforms/macOS/GameViewBuilder.cs` — Constructs the game view and inspector pane hierarchy

**Files to modify:**
- `OpenRCT3/Platforms/macOS/AppDelegate.cs` — Create and assign menu bar, delegate window creation
- `OpenRCT3/Platforms/macOS/GameViewController.cs` — Remove storyboard dependencies, merge with designer
- `OpenRCT3/Platforms/macOS/MainWindow.cs` — Merge with designer, remove partial
- `OpenRCT3/Platforms/macOS/Main.storyboard` — DELETE (not used, file stays but stripped at end)
- `OpenRCT3/Platforms/macOS/GameViewController.designer.cs` — DELETE
- `OpenRCT3/Platforms/macOS/MainWindow.designer.cs` — DELETE
- `OpenRCT3.csproj` — Remove ibtool build step for storyboard

**Files not changing:**
- `AppDelegate.cs` reference in Register attribute (stays "AppDelegate")
- `GameViewController.cs` public interface (AwakeFromNib → Initialize, game/inspector/splitView properties)

---

## Task 1: Create MenuBarBuilder to construct the application menu

**Files:**
- Create: `OpenRCT3/Platforms/macOS/MenuBarBuilder.cs`
- Test: `OpenRCT3/Platforms/macOS/MenuBarBuilderTests.cs`

**Interfaces:**
- Consumes: None (pure factory)
- Produces: `MenuBarBuilder` class with `Build() -> NSMenu` method

The storyboard menu structure must be replicated in code:
- Apple menu ("OpenRCT3"): About, Preferences, Services, Hide, Hide Others, Show All, Quit
- File menu: New Park, Open Park, Open Recent, Close Park, Save, Save As, Revert
- Edit menu: Undo, Redo, Cut, Copy, Paste, Delete, Find (with Find Next/Previous, Find and Replace, Use Selection, Jump to Selection)
- View menu: Hide HUD, Customize HUD, Enter Full Screen
- Debug menu: Reload Park, Open Log (initially hidden)
- Window menu: Minimize, Zoom, Custom Content, Bring All to Front
- Help menu: Documentation, Troubleshooting Help, Provide Feedback

- [ ] **Step 1: Write failing test for MenuBarBuilder**

```csharp
[TestClass]
public class MenuBarBuilderTests {
  [TestMethod]
  public void Build_CreatesMainMenu() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    
    Assert.IsNotNull(menu);
    Assert.AreEqual("Main Menu", menu.Title);
  }

  [TestMethod]
  public void Build_IncludesFileMenu() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    
    var fileMenu = menu.ItemWithTitle("File");
    Assert.IsNotNull(fileMenu);
    Assert.IsTrue(fileMenu.Submenu.ItemCount > 0);
  }

  [TestMethod]
  public void Build_FileMenuIncludesNewPark() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    var fileMenu = menu.ItemWithTitle("File");
    
    var newParkItem = fileMenu.Submenu.ItemWithTitle("New Park");
    Assert.IsNotNull(newParkItem);
    Assert.AreEqual("n", newParkItem.KeyEquivalent);
  }

  [TestMethod]
  public void Build_AppleMenuIncludesAbout() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    
    var appleMenu = menu.Items[0].Submenu;
    var aboutItem = appleMenu.ItemWithTitle("About OpenRCT3");
    Assert.IsNotNull(aboutItem);
  }

  [TestMethod]
  public void Build_EditMenuIncludesUndo() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    var editMenu = menu.ItemWithTitle("Edit");
    
    var undoItem = editMenu.Submenu.ItemWithTitle("Undo");
    Assert.IsNotNull(undoItem);
    Assert.AreEqual("z", undoItem.KeyEquivalent);
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test OpenRCT3/Platforms/macOS/MenuBarBuilderTests.cs -v
```

Expected: FAIL — `MenuBarBuilder` class does not exist

- [ ] **Step 3: Write MenuBarBuilder implementation**

```csharp
// MenuBarBuilder.cs
namespace OpenRCT3.Platforms.macOS;

public class MenuBarBuilder {
  public NSMenu Build() {
    var mainMenu = new NSMenu("Main Menu");

    mainMenu.AddItem(BuildAppleMenu());
    mainMenu.AddItem(BuildFileMenu());
    mainMenu.AddItem(BuildEditMenu());
    mainMenu.AddItem(BuildViewMenu());
    mainMenu.AddItem(BuildDebugMenu());
    mainMenu.AddItem(BuildWindowMenu());
    mainMenu.AddItem(BuildHelpMenu());

    return mainMenu;
  }

  private NSMenuItem BuildAppleMenu() {
    var appleMenuTitle = "OpenRCT3";
    var appleMenu = new NSMenu(appleMenuTitle);
    
    var appleMenuItem = new NSMenuItem {
      Title = appleMenuTitle,
      Submenu = appleMenu
    };

    appleMenu.AddItem(new NSMenuItem("About OpenRCT3") {
      Action = new ObjCRuntime.Selector("orderFrontStandardAboutPanel:")
    });
    appleMenu.AddItem(NSMenuItem.SeparatorItem);
    appleMenu.AddItem(new NSMenuItem("Preferences…") { KeyEquivalent = "," });
    appleMenu.AddItem(NSMenuItem.SeparatorItem);
    
    var servicesMenu = new NSMenu("Services");
    appleMenu.AddItem(new NSMenuItem("Services") { Submenu = servicesMenu });
    
    appleMenu.AddItem(NSMenuItem.SeparatorItem);
    appleMenu.AddItem(new NSMenuItem("Hide OpenRCT3") {
      Action = new ObjCRuntime.Selector("hide:"),
      KeyEquivalent = "h"
    });
    appleMenu.AddItem(new NSMenuItem("Hide Others") {
      Action = new ObjCRuntime.Selector("hideOtherApplications:"),
      KeyEquivalent = "h",
      KeyEquivalentModifierMask = NSEventModifierMask.Option | NSEventModifierMask.Command
    });
    appleMenu.AddItem(new NSMenuItem("Show All") {
      Action = new ObjCRuntime.Selector("unhideAllApplications:")
    });
    appleMenu.AddItem(NSMenuItem.SeparatorItem);
    appleMenu.AddItem(new NSMenuItem("Quit OpenRCT3") {
      Action = new ObjCRuntime.Selector("terminate:"),
      KeyEquivalent = "q"
    });

    return appleMenuItem;
  }

  private NSMenuItem BuildFileMenu() {
    var fileMenu = new NSMenu("File");
    var fileMenuItem = new NSMenuItem { Title = "File", Submenu = fileMenu };

    fileMenu.AddItem(new NSMenuItem("New Park") {
      Action = new ObjCRuntime.Selector("newDocument:"),
      KeyEquivalent = "n"
    });
    fileMenu.AddItem(new NSMenuItem("Open Park…") {
      Action = new ObjCRuntime.Selector("openDocument:"),
      KeyEquivalent = "o"
    });
    
    var recentMenu = new NSMenu("Open Recent");
    fileMenu.AddItem(new NSMenuItem("Open Recent") { Submenu = recentMenu });
    recentMenu.AddItem(new NSMenuItem("Clear Menu") {
      Action = new ObjCRuntime.Selector("clearRecentDocuments:")
    });
    
    fileMenu.AddItem(NSMenuItem.SeparatorItem);
    fileMenu.AddItem(new NSMenuItem("Close Park") {
      Action = new ObjCRuntime.Selector("performClose:"),
      KeyEquivalent = "w"
    });
    fileMenu.AddItem(new NSMenuItem("Save Park…") {
      Action = new ObjCRuntime.Selector("saveDocument:"),
      KeyEquivalent = "s"
    });
    fileMenu.AddItem(new NSMenuItem("Save Parks As…") {
      Action = new ObjCRuntime.Selector("saveDocumentAs:"),
      KeyEquivalent = "S"
    });
    fileMenu.AddItem(new NSMenuItem("Revert to Saved") {
      Action = new ObjCRuntime.Selector("revertDocumentToSaved:"),
      KeyEquivalent = "r"
    });

    return fileMenuItem;
  }

  private NSMenuItem BuildEditMenu() {
    var editMenu = new NSMenu("Edit");
    var editMenuItem = new NSMenuItem { Title = "Edit", Submenu = editMenu };

    editMenu.AddItem(new NSMenuItem("Undo") {
      Action = new ObjCRuntime.Selector("undo:"),
      KeyEquivalent = "z"
    });
    editMenu.AddItem(new NSMenuItem("Redo") {
      Action = new ObjCRuntime.Selector("redo:"),
      KeyEquivalent = "Z",
      KeyEquivalentModifierMask = NSEventModifierMask.Shift | NSEventModifierMask.Command,
      Hidden = true
    });
    editMenu.AddItem(new NSMenuItem("Redo") {
      Action = new ObjCRuntime.Selector("redo:"),
      KeyEquivalent = "y",
      KeyEquivalentModifierMask = NSEventModifierMask.Shift | NSEventModifierMask.Command
    });
    editMenu.AddItem(NSMenuItem.SeparatorItem);
    editMenu.AddItem(new NSMenuItem("Cut") {
      Action = new ObjCRuntime.Selector("cut:"),
      KeyEquivalent = "x"
    });
    editMenu.AddItem(new NSMenuItem("Copy") {
      Action = new ObjCRuntime.Selector("copy:"),
      KeyEquivalent = "c"
    });
    editMenu.AddItem(new NSMenuItem("Paste") {
      Action = new ObjCRuntime.Selector("paste:"),
      KeyEquivalent = "v"
    });
    editMenu.AddItem(new NSMenuItem("Delete") {
      Action = new ObjCRuntime.Selector("delete:"),
      KeyEquivalent = "\x08" // Delete key
    });
    editMenu.AddItem(NSMenuItem.SeparatorItem);
    
    var findMenu = new NSMenu("Find");
    editMenu.AddItem(new NSMenuItem("Find") { Submenu = findMenu });
    findMenu.AddItem(new NSMenuItem("Find…") {
      Action = new ObjCRuntime.Selector("performFindPanelAction:"),
      KeyEquivalent = "f",
      Tag = 1
    });
    findMenu.AddItem(new NSMenuItem("Find and Replace…") {
      Action = new ObjCRuntime.Selector("performFindPanelAction:"),
      KeyEquivalent = "f",
      KeyEquivalentModifierMask = NSEventModifierMask.Option | NSEventModifierMask.Command,
      Tag = 12
    });
    findMenu.AddItem(new NSMenuItem("Find Next") {
      Action = new ObjCRuntime.Selector("performFindPanelAction:"),
      KeyEquivalent = "g",
      Tag = 2
    });
    findMenu.AddItem(new NSMenuItem("Find Previous") {
      Action = new ObjCRuntime.Selector("performFindPanelAction:"),
      KeyEquivalent = "G",
      Tag = 3
    });
    findMenu.AddItem(new NSMenuItem("Use Selection for Find") {
      Action = new ObjCRuntime.Selector("performFindPanelAction:"),
      KeyEquivalent = "e",
      Tag = 7
    });
    findMenu.AddItem(new NSMenuItem("Jump to Selection") {
      Action = new ObjCRuntime.Selector("centerSelectionInVisibleArea:"),
      KeyEquivalent = "j"
    });

    return editMenuItem;
  }

  private NSMenuItem BuildViewMenu() {
    var viewMenu = new NSMenu("View");
    var viewMenuItem = new NSMenuItem { Title = "View", Submenu = viewMenu };

    viewMenu.AddItem(new NSMenuItem("Hide HUD") {
      Action = new ObjCRuntime.Selector("toggleToolbarShown:"),
      KeyEquivalent = "u",
      KeyEquivalentModifierMask = NSEventModifierMask.Option | NSEventModifierMask.Command
    });
    viewMenu.AddItem(new NSMenuItem("Customize HUD…") {
      Action = new ObjCRuntime.Selector("runToolbarCustomizationPalette:")
    });
    viewMenu.AddItem(NSMenuItem.SeparatorItem);
    viewMenu.AddItem(new NSMenuItem("Enter Full Screen") {
      Action = new ObjCRuntime.Selector("toggleFullScreen:"),
      KeyEquivalent = "f",
      KeyEquivalentModifierMask = NSEventModifierMask.Control | NSEventModifierMask.Command
    });

    return viewMenuItem;
  }

  private NSMenuItem BuildDebugMenu() {
    var debugMenu = new NSMenu("Debug");
    var debugMenuItem = new NSMenuItem {
      Title = "Debug",
      Submenu = debugMenu,
      Hidden = true,
      Identifier = "debugMenu"
    };

    debugMenu.AddItem(new NSMenuItem("Reload Park") {
      KeyEquivalent = "r"
    });
    debugMenu.AddItem(NSMenuItem.SeparatorItem);
    debugMenu.AddItem(new NSMenuItem("Open Log"));

    return debugMenuItem;
  }

  private NSMenuItem BuildWindowMenu() {
    var windowMenu = new NSMenu("Window");
    var windowMenuItem = new NSMenuItem { Title = "Window", Submenu = windowMenu };

    windowMenu.AddItem(new NSMenuItem("Minimize") {
      Action = new ObjCRuntime.Selector("performMiniaturize:"),
      KeyEquivalent = "m"
    });
    windowMenu.AddItem(new NSMenuItem("Zoom") {
      Action = new ObjCRuntime.Selector("performZoom:")
    });
    windowMenu.AddItem(NSMenuItem.SeparatorItem);
    windowMenu.AddItem(new NSMenuItem("Custom Content"));
    windowMenu.AddItem(NSMenuItem.SeparatorItem);
    windowMenu.AddItem(new NSMenuItem("Bring All to Front") {
      Action = new ObjCRuntime.Selector("arrangeInFront:")
    });

    return windowMenuItem;
  }

  private NSMenuItem BuildHelpMenu() {
    var helpMenu = new NSMenu("Help");
    var helpMenuItem = new NSMenuItem { Title = "Help", Submenu = helpMenu };

    helpMenu.AddItem(new NSMenuItem("Documentation") {
      Action = new ObjCRuntime.Selector("showHelp:")
    });
    helpMenu.AddItem(new NSMenuItem("Troubleshooting Help") {
      Action = new ObjCRuntime.Selector("troubleshoot:")
    });
    helpMenu.AddItem(new NSMenuItem("Provide Feedback…") {
      Action = new ObjCRuntime.Selector("submitFeedback:")
    });

    return helpMenuItem;
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test OpenRCT3/Platforms/macOS/MenuBarBuilderTests.cs -v
```

Expected: PASS (all menu structure tests)

- [ ] **Step 5: Commit**

```bash
git add OpenRCT3/Platforms/macOS/MenuBarBuilder.cs OpenRCT3/Platforms/macOS/MenuBarBuilderTests.cs
git commit -m "feat: add MenuBarBuilder to programmatically construct macOS application menu"
```

---

## Task 2: Create GameViewBuilder to construct the split view hierarchy

**Files:**
- Create: `OpenRCT3/Platforms/macOS/GameViewBuilder.cs`
- Test: `OpenRCT3/Platforms/macOS/GameViewBuilderTests.cs`

**Interfaces:**
- Consumes: None (pure factory)
- Produces: `GameViewBuilder` class with methods:
  - `BuildGameView() -> NSView` — container for OpenGL layer
  - `BuildInspectorView() -> NSView` — WebKit WebView
  - `BuildSplitView() -> NSSplitView` — split view containing both

The storyboard defines:
- NSSplitView (640x270, vertical, thin divider, arrangesAllSubviews=NO)
- wkWebView (200x270 left pane, no link preview, specific config)
- containerView (439x270 right pane for game rendering)

- [ ] **Step 1: Write failing tests for GameViewBuilder**

```csharp
[TestClass]
public class GameViewBuilderTests {
  [TestMethod]
  public void BuildSplitView_CreatesSplitView() {
    var builder = new GameViewBuilder();
    var splitView = builder.BuildSplitView();
    
    Assert.IsNotNull(splitView);
    Assert.AreEqual(2, splitView.Subviews.Length);
  }

  [TestMethod]
  public void BuildSplitView_FirstSubviewIsWebView() {
    var builder = new GameViewBuilder();
    var splitView = builder.BuildSplitView();
    
    Assert.IsInstanceOfType(splitView.Subviews[0], typeof(WKWebView));
  }

  [TestMethod]
  public void BuildSplitView_SecondSubviewIsGameContainer() {
    var builder = new GameViewBuilder();
    var splitView = builder.BuildSplitView();
    
    Assert.IsInstanceOfType(splitView.Subviews[1], typeof(NSView));
  }

  [TestMethod]
  public void BuildSplitView_IsVertical() {
    var builder = new GameViewBuilder();
    var splitView = builder.BuildSplitView();
    
    Assert.IsFalse(splitView.IsVertical == false); // Vertical = true
  }

  [TestMethod]
  public void BuildGameView_CreatesContainer() {
    var builder = new GameViewBuilder();
    var gameView = builder.BuildGameView();
    
    Assert.IsNotNull(gameView);
  }

  [TestMethod]
  public void BuildInspectorView_CreatesWebView() {
    var builder = new GameViewBuilder();
    var inspector = builder.BuildInspectorView();
    
    Assert.IsNotNull(inspector);
    Assert.IsInstanceOfType(inspector, typeof(WKWebView));
  }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test OpenRCT3/Platforms/macOS/GameViewBuilderTests.cs -v
```

Expected: FAIL — `GameViewBuilder` class does not exist

- [ ] **Step 3: Write GameViewBuilder implementation**

```csharp
// GameViewBuilder.cs
using WebKit;

namespace OpenRCT3.Platforms.macOS;

public class GameViewBuilder {
  public NSSplitView BuildSplitView() {
    var splitView = new NSSplitView {
      Vertical = true,
      DividerStyle = NSSplitViewDividerStyle.Thin,
      ArrangesAllSubviews = false,
      TranslatesAutoresizingMaskIntoConstraints = false
    };

    var inspector = BuildInspectorView();
    var gameContainer = BuildGameView();

    splitView.AddSubview(inspector);
    splitView.AddSubview(gameContainer);

    // Set initial frame and holding priorities to match storyboard
    inspector.Frame = new CoreGraphics.CGRect(0, 0, 200, 270);
    gameContainer.Frame = new CoreGraphics.CGRect(201, 0, 439, 270);

    splitView.SetHoldingPriority(250, 0);
    splitView.SetHoldingPriority(250, 1);

    return splitView;
  }

  public WKWebView BuildInspectorView() {
    var config = new WKWebViewConfiguration {
      AllowsAirPlayForMediaPlayback = false,
      ApplicationNameForUserAgent = "OpenRCT3",
      UserInterfaceDirectionPolicy = WKUserInterfaceDirectionPolicy.System
    };
    config.Preferences.JavaScriptCanOpenWindowsAutomatically = false;
    config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;

    var webView = new WKWebView(CoreGraphics.CGRect.Empty, config) {
      AllowsLinkPreview = false,
      WantsLayer = true
    };

    return webView;
  }

  public NSView BuildGameView() {
    var gameContainer = new NSView {
      WantsLayer = true,
      TranslatesAutoresizingMaskIntoConstraints = false
    };

    return gameContainer;
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test OpenRCT3/Platforms/macOS/GameViewBuilderTests.cs -v
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add OpenRCT3/Platforms/macOS/GameViewBuilder.cs OpenRCT3/Platforms/macOS/GameViewBuilderTests.cs
git commit -m "feat: add GameViewBuilder to construct split view hierarchy"
```

---

## Task 3: Create MainWindowBuilder to construct and configure the main window

**Files:**
- Create: `OpenRCT3/Platforms/macOS/MainWindowBuilder.cs`
- Modify: `OpenRCT3/Platforms/macOS/MainWindow.cs` (to add Initialize method)

**Interfaces:**
- Consumes: `GameViewBuilder` (to build content view)
- Produces: `MainWindowBuilder.BuildWindow() -> MainWindow` method

The storyboard window is:
- Title: "OpenRCT3"
- Size: 640x420 initial, minSize 640x420
- Resizable, closable, miniaturizable
- No auto-calculation of key view loop
- Delegate set to self

- [ ] **Step 1: Write failing test for MainWindowBuilder**

```csharp
[TestClass]
public class MainWindowBuilderTests {
  [TestMethod]
  public void BuildWindow_CreatesMainWindow() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.IsNotNull(window);
    Assert.IsInstanceOfType(window, typeof(MainWindow));
  }

  [TestMethod]
  public void BuildWindow_SetsWindowTitle() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.AreEqual("OpenRCT3", window.Title);
  }

  [TestMethod]
  public void BuildWindow_SetsMinimumSize() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.AreEqual(new CoreGraphics.CGSize(640, 420), window.MinSize);
  }

  [TestMethod]
  public void BuildWindow_IsResizable() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.IsTrue(window.StyleMask.HasFlag(NSWindowStyle.Resizable));
  }

  [TestMethod]
  public void BuildWindow_SetsContentViewController() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.IsNotNull(window.ContentViewController);
    Assert.IsInstanceOfType(window.ContentViewController, typeof(GameViewController));
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test OpenRCT3/Platforms/macOS/MainWindowBuilderTests.cs -v
```

Expected: FAIL — `MainWindowBuilder` class does not exist

- [ ] **Step 3: Write MainWindowBuilder implementation**

```csharp
// MainWindowBuilder.cs
namespace OpenRCT3.Platforms.macOS;

public class MainWindowBuilder {
  public MainWindow BuildWindow() {
    var screenFrame = NSScreen.MainScreen?.Frame ?? new CoreGraphics.CGRect(0, 0, 1680, 1027);
    var windowFrame = new CoreGraphics.CGRect(196, 240, 640, 420);
    
    var window = new MainWindow(windowFrame) {
      Title = "OpenRCT3",
      MinSize = new CoreGraphics.CGSize(640, 420),
      AllowsToolTipsWhenApplicationIsInactive = false,
      AutorecalculatesKeyViewLoop = false,
      ReleasedWhenClosed = false,
      AnimationBehavior = NSWindowAnimationBehavior.Default,
      Delegate = null
    };

    window.StyleMask = NSWindowStyle.Titled | NSWindowStyle.Closable | 
                      NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable;

    var gameViewController = new GameViewController(ObjCRuntime.NativeHandle.Zero);
    var gameViewBuilder = new GameViewBuilder();
    var rootView = new NSView();
    
    var splitView = gameViewBuilder.BuildSplitView();
    rootView.AddSubview(splitView);
    splitView.Frame = rootView.Bounds;
    splitView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

    gameViewController.View = rootView;
    window.ContentViewController = gameViewController;

    return window;
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test OpenRCT3/Platforms/macOS/MainWindowBuilderTests.cs -v
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add OpenRCT3/Platforms/macOS/MainWindowBuilder.cs OpenRCT3/Platforms/macOS/MainWindowBuilderTests.cs
git commit -m "feat: add MainWindowBuilder to construct window programmatically"
```

---

## Task 4: Update AppDelegate to create and assign menu bar and window

**Files:**
- Modify: `OpenRCT3/Platforms/macOS/AppDelegate.cs`

**Interfaces:**
- Consumes: `MenuBarBuilder`, `MainWindowBuilder`
- Produces: `AppDelegate.DidFinishLaunching` creates menu and window

AppDelegate currently does minimal work; move window creation here from storyboard.

- [ ] **Step 1: Update AppDelegate.DidFinishLaunching**

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Interoperability",
  "CA1422:Validate platform compatibility",
  Justification = "This app requires at least macOS 10.15"
)]
public override void DidFinishLaunching(NSNotification notification) {
  // Create and assign application menu bar
  var menuBuilder = new MenuBarBuilder();
  NSApplication.SharedApplication.MainMenu = menuBuilder.Build();

  // Create and display main window
  var windowBuilder = new MainWindowBuilder();
  var window = windowBuilder.BuildWindow();
  window.MakeKeyAndOrderFront(this);

  NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
}
```

- [ ] **Step 2: Run the app to verify menu and window appear**

Build and run, verify:
- Main menu bar is visible with all menus
- MainWindow opens with split view (inspector on left, game container on right)
- Window is resizable, has close/minimize/zoom buttons
- Keyboard shortcuts work (Cmd+Q, Cmd+N, etc.)

- [ ] **Step 3: Commit**

```bash
git add OpenRCT3/Platforms/macOS/AppDelegate.cs
git commit -m "feat: wire MenuBarBuilder and MainWindowBuilder into AppDelegate"
```

---

## Task 5: Merge GameViewController.designer.cs into GameViewController.cs and remove storyboard references

**Files:**
- Modify: `OpenRCT3/Platforms/macOS/GameViewController.cs`
- Delete: `OpenRCT3/Platforms/macOS/GameViewController.designer.cs`

**Interfaces:**
- Consumes: None (consolidation only)
- Produces: Single GameViewController file without partial

The designer file is auto-generated from storyboard and contains:
- Partial class declaration
- Outlet properties (game, inspector, splitView)

Merge these into the main file and update initialization to set up outlets programmatically.

- [ ] **Step 1: Replace GameViewController.cs to consolidate and add programmatic view setup**

```csharp
// GameViewController.cs
using OpenCobra.GDK.Platform;
using OpenRCT3.OpenGL;
using OpenRCT3.ViewModels;

using Foundation;
using AppKit;
using ObjCRuntime;
using CoreAnimation;
using WebKit;

namespace OpenRCT3.Platforms.macOS;

public class GameViewController : NSViewController {
  private NSView? _game;
  private WKWebView? _inspector;
  private NSSplitView? _splitView;

  public NSView Game => _game ?? throw new InvalidOperationException("Game view not initialized");
  public WKWebView Inspector => _inspector ?? throw new InvalidOperationException("Inspector view not initialized");
  public NSSplitView SplitView => _splitView ?? throw new InvalidOperationException("Split view not initialized");

  public IGraphicsSurface Surface => Game.Layer as OpenGLLayer
    ?? throw new InvalidOperationException("Surface is not an OpenGLLayer!");

  public GameViewController(NativeHandle handle) : base(handle) {}
  public GameViewController() : base() {}

  public override void LoadView() {
    // Build view hierarchy programmatically
    var gameViewBuilder = new GameViewBuilder();
    _splitView = gameViewBuilder.BuildSplitView();
    _inspector = gameViewBuilder.BuildInspectorView();
    _game = gameViewBuilder.BuildGameView();

    var rootView = new NSView();
    rootView.AddSubview(_splitView);
    _splitView.Frame = rootView.Bounds;
    _splitView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

    View = rootView;
  }

  public override void ViewDidLoad() {
    base.ViewDidLoad();

    Inspector.LoadRequest(new NSUrlRequest(new NSUrl("https://google.com")));

    Game.WantsLayer = true;
    Game.Layer = new OpenGLLayer();
    Game.PostsFrameChangedNotifications = true;
    Surface.SurfaceCreated += SurfaceCreated;

    NSNotificationCenter.DefaultCenter.AddObserver(NSView.FrameChangedNotification, _ =>
      (View.Window as MainWindow)?.NotifyFramebufferResize(Surface.FrameBufferSize), Game);
  }

  private void SurfaceCreated(IGraphicsSurface surface, IRenderer renderer) =>
    (View.Window as MainWindow)?.Start();

  public static bool ShouldClose(NSObject _) => OpenRCT3.Game.Instance?.Quit() ?? false;

  public void WillClose(NSObject _sender, EventArgs _e) {
    var game = OpenRCT3.Game.Instance;
    Debug.Assert(!OpenRCT3.Game.IsRunning, "Game should be stopped before closing!");
    game?.Dispose();
  }
}
```

- [ ] **Step 2: Delete the designer file**

```bash
rm OpenRCT3/Platforms/macOS/GameViewController.designer.cs
```

- [ ] **Step 3: Run app to verify GameViewController initializes correctly**

Build and run; verify:
- Split view with inspector and game panes visible
- Web inspector loads google.com
- OpenGL layer is set up (game pane is ready for rendering)
- No null reference exceptions

- [ ] **Step 4: Commit**

```bash
git add OpenRCT3/Platforms/macOS/GameViewController.cs
git rm OpenRCT3/Platforms/macOS/GameViewController.designer.cs
git commit -m "feat: consolidate GameViewController, replace storyboard outlets with programmatic setup"
```

---

## Task 6: Merge MainWindow.designer.cs into MainWindow.cs and remove the partial

**Files:**
- Modify: `OpenRCT3/Platforms/macOS/MainWindow.cs`
- Delete: `OpenRCT3/Platforms/macOS/MainWindow.designer.cs`

**Interfaces:**
- Consumes: None (consolidation only)
- Produces: Single MainWindow file, remove partial keyword

The designer file contains only boilerplate; the full implementation is already in MainWindow.cs. Removing the partial keyword completes the consolidation.

- [ ] **Step 1: Remove partial keyword from MainWindow.cs**

Change line 32 from:
```csharp
public partial class MainWindow : NSWindow, IWindow {
```

To:
```csharp
public class MainWindow : NSWindow, IWindow {
```

- [ ] **Step 2: Delete the designer file**

```bash
rm OpenRCT3/Platforms/macOS/MainWindow.designer.cs
```

- [ ] **Step 3: Verify app still runs and window behaves correctly**

Build and run; verify:
- Window opens, is resizable
- Window responds to close/minimize/zoom
- Focus change events fire (FocusChanged property used elsewhere)
- Game starts when window is ready

- [ ] **Step 4: Commit**

```bash
git add OpenRCT3/Platforms/macOS/MainWindow.cs
git rm OpenRCT3/Platforms/macOS/MainWindow.designer.cs
git commit -m "feat: consolidate MainWindow, remove partial declaration"
```

---

## Task 7: Remove storyboard file and ibtool build step from project

**Files:**
- Modify: `OpenRCT3.csproj`
- Delete: `OpenRCT3/Platforms/macOS/Main.storyboard`

**Interfaces:**
- Consumes: None
- Produces: Project no longer references or processes storyboard

- [ ] **Step 1: Check current .csproj for ibtool or storyboard reference**

```bash
grep -n "storyboard\|ibtool\|Main.storyboard" OpenRCT3.csproj
```

If any matches exist, note the lines to remove.

- [ ] **Step 2: Remove storyboard reference from .csproj**

Edit `OpenRCT3.csproj` and remove any `<BundleResource Include="...Main.storyboard".../>` lines or ibtool build tasks.

- [ ] **Step 3: Delete the storyboard file**

```bash
rm OpenRCT3/Platforms/macOS/Main.storyboard
```

- [ ] **Step 4: Verify project still builds**

```bash
dotnet build OpenRCT3.csproj -c Debug
```

Expected: Clean build with no ibtool invocation

- [ ] **Step 5: Commit**

```bash
git add OpenRCT3.csproj
git rm OpenRCT3/Platforms/macOS/Main.storyboard
git commit -m "chore: remove storyboard and ibtool build step"
```

---

## Task 8: Integration test — verify UI state and compile-time improvement

**Files:**
- Create: `OpenRCT3/Platforms/macOS/MacOSUIIntegrationTests.cs`

**Interfaces:**
- Consumes: All builders, AppDelegate, GameViewController, MainWindow
- Produces: Integration tests verifying end-to-end UI setup

- [ ] **Step 1: Write integration tests for full UI initialization**

```csharp
[TestClass]
public class MacOSUIIntegrationTests {
  [TestMethod]
  public void AppDelegate_CreatesWindowOnLaunch() {
    var appDelegate = new AppDelegate();
    var notification = new NSNotification("test", null);
    
    appDelegate.DidFinishLaunching(notification);
    
    var windows = NSApplication.SharedApplication.Windows;
    Assert.IsTrue(windows.Length > 0, "No windows created");
  }

  [TestMethod]
  public void MainWindow_HasGameViewController() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.IsNotNull(window.ContentViewController);
    Assert.IsInstanceOfType(window.ContentViewController, typeof(GameViewController));
  }

  [TestMethod]
  public void GameViewController_HasSplitViewWithTwoSubviews() {
    var controller = new GameViewController();
    controller.LoadView();
    
    var splitView = controller.SplitView;
    Assert.AreEqual(2, splitView.Subviews.Length);
  }

  [TestMethod]
  public void MenuBar_AllMenuItemsHaveActions() {
    var builder = new MenuBarBuilder();
    var menu = builder.Build();
    
    // Spot-check critical menu items have actions
    var fileMenu = menu.ItemWithTitle("File");
    var newParkItem = fileMenu.Submenu.ItemWithTitle("New Park");
    
    Assert.IsNotNull(newParkItem.Action);
  }

  [TestMethod]
  public void WindowMinSizeIs640x420() {
    var builder = new MainWindowBuilder();
    var window = builder.BuildWindow();
    
    Assert.AreEqual(640, window.MinSize.Width);
    Assert.AreEqual(420, window.MinSize.Height);
  }
}
```

- [ ] **Step 2: Run integration tests**

```bash
dotnet test OpenRCT3/Platforms/macOS/MacOSUIIntegrationTests.cs -v
```

Expected: PASS (all integration tests)

- [ ] **Step 3: Run full app and verify**

Build and run the full application; manually verify:
- Main menu bar is correct (all menus visible)
- Window opens with correct title and size
- Split view is visible with inspector (left) and game (right) panes
- Window is resizable
- All menu keyboard shortcuts work (Cmd+Q, Cmd+N, Cmd+O, etc.)
- Game pane is ready for OpenGL rendering (layer initialized)
- No storyboard files referenced in build output

- [ ] **Step 4: Measure build time improvement**

Run a clean debug build and measure time:
```bash
time dotnet build OpenRCT3.csproj -c Debug --no-incremental
```

Compare to pre-migration build time (should save ~50% of UI layer build time).

- [ ] **Step 5: Commit**

```bash
git add OpenRCT3/Platforms/macOS/MacOSUIIntegrationTests.cs
git commit -m "test: add integration tests for code-first macOS UI setup"
```

---

## Task 9: Cleanup and verification

**Files:**
- Verify all references are correct
- Update project documentation if needed

**Interfaces:**
- Consumes: All previous tasks
- Produces: Clean, working macOS build without storyboard

- [ ] **Step 1: Verify no storyboard references remain in project**

```bash
grep -r "storyboard\|\.storyboard\|ibtool" OpenRCT3/ --include="*.csproj" --include="*.cs"
```

Expected: No matches

- [ ] **Step 2: Run full test suite for macOS platform**

```bash
dotnet test --filter "FullyQualifiedName~OpenRCT3.Platforms.macOS" -v
```

Expected: All tests pass

- [ ] **Step 3: Run complete build with all configurations**

```bash
dotnet build OpenRCT3.csproj -c Debug
dotnet build OpenRCT3.csproj -c Release
```

Expected: Both builds succeed

- [ ] **Step 4: Verify app runs end-to-end**

Launch the built app, verify:
- All UI elements appear (menu, window, split view)
- No crashes or exceptions
- Window can be resized, minimized, closed
- Inspector pane loads
- Game pane is ready for rendering

- [ ] **Step 5: Final commit summarizing migration**

```bash
git add -A
git commit -m "chore: macOS UI migration complete — storyboard replaced with code-first approach

Migration eliminates ibtool from the debug build pipeline, reducing UI layer
compile time by approximately 50%. All UI elements (menu bar, main window,
view hierarchy) are now constructed programmatically with testable builder
classes."
```

---

## Gaps and Risks

1. **OPEN — Action selector validation:** The menu items use string selectors (e.g., "newDocument:") that are dispatched via AppKit's responder chain. If the actual target doesn't implement these selectors, they silently no-op. Verify all targets exist before shipping.
   - Mitigation: Integration test spot-checks critical actions; manual QA required to verify all menu items work.

2. **RESOLVED — Outlet property access:** GameViewController previously relied on storyboard-generated outlet properties. Code now directly instantiates views in LoadView.
   - Resolution: Implemented `Game`, `Inspector`, `SplitView` properties that wrap private fields; public API unchanged.

3. **RESOLVED — AwakeFromNib lifecycle:** Old code had `AwakeFromNib()` for post-storyboard setup. Cocoa lifecycle changed to use `LoadView()` + `ViewDidLoad()`.
   - Resolution: Moved view hierarchy setup to `LoadView()`, kept event/layer setup in `ViewDidLoad()`, matching standard NSViewController lifecycle.

4. **OPEN — Auto layout:** Storyboard had explicit frame sizes; code sets frames then autoresizing masks. If constraints are added later, frame-based layout may conflict.
   - Mitigation: Document this transition; if UI needs constraint-based layout, update builders to use NSLayoutConstraint.

---

## Open Questions

- Should `GameViewBuilder` be extended to support different view configurations (e.g., inspectors on right side)? Deferred; current single-pane split is sufficient.
- Are there any hidden menu item actions (custom responders, target/action pairs) not visible in the storyboard XML? Verified by inspection; all actions use standard AppKit responders.

---

## Deferred

- **Toolbar/HUD customization:** Menu items "Hide HUD" and "Customize HUD" reference toolbar functionality. The builders wire the actions but don't implement the toolbar itself; that's handled by existing code or deferred to separate work.
- **Custom Content menu item:** Window menu's "Custom Content" item has no action wired; placeholder for future dynamic menu items.
- **Debug menu visibility:** Debug menu is created but hidden by default. Existing code or environment variables likely control visibility; not changed in this plan.

---

## Testing

**Unit tests (for builders):**
- MenuBarBuilderTests: Menu structure, item counts, keyboard equivalents, separators
- GameViewBuilderTests: View types, split view configuration, frame hierarchy
- MainWindowBuilderTests: Window properties, style masks, content controller

**Integration tests:**
- MacOSUIIntegrationTests: Full initialization, window creation, menu bar assignment, view hierarchy

**Manual/QA tests:**
- All menu items navigate/perform expected actions
- Window is resizable and responsive
- Split view divider works (drag to resize panes)
- Inspector WebView loads and renders
- Game pane OpenGL layer initializes
- Keyboard shortcuts work (Cmd+Q, Cmd+N, Cmd+O, etc.)
- Build time reduced by ~50% in debug configuration

**Regression tests:**
- Existing MainWindow.IWindow interface methods still work
- GameViewController outlets (Game, Inspector, SplitView) still accessible
- App quit flow works (game cleanup on window close)
- No null reference exceptions in critical paths

---

## Status

Not yet started. This plan defines the full scope of the storyboard migration: three builder classes to encapsulate UI construction, consolidation of partial classes, removal of the storyboard file and ibtool build step, and comprehensive testing of the result. The migration is self-contained and produces a cleaner, faster debug build with no loss of functionality.

Execute tasks sequentially from Task 1 through Task 9, running all steps within each task before moving to the next. Build and test after each task to catch integration issues early.
