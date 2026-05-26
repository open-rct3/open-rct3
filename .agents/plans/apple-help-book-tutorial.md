# Creating and Integrating an Apple Help Book

This guide describes how to author, index, bundle, and register a native **Apple Help Book** for a macOS application and connect it to `NSAlert` buttons.

---

## 1. Prepare the HTML Documentation Files

Apple Help Books are written in standard HTML5 and CSS.

1. Create a workspace folder for your documentation (e.g., `HelpContent/`).
2. Create your main landing page named `index.html`.
3. Create your content pages. 
4. Include **Help Anchors** in your HTML tags where appropriate so you can deep-link your [NSAlert.HelpAnchor](https://developer.apple.com/documentation/appkit/nsalert/1531777-helpanchor) properties to them:
   ```html
   <!-- Inside connection_error.html -->
   <a name="ConnectionErrorAnchor"></a>
   <h2>Troubleshooting Server Connection Failures</h2>
   <p>Ensure that your local network connection is active and try restarting...</p>
   ```

---

## 2. Configure the `.help` Bundle Structure

An Apple Help Book is packaged inside its own subdirectory bundle with a `.help` extension.

1. Create a directory named `OpenRCT3Help.help`.
2. Inside that directory, create a subdirectory named `Contents`.
3. Inside `Contents`, create two folders: `Resources` and `SharedSupport`.
4. Place all your HTML, CSS, and image files inside `Contents/Resources/`.
5. Under `Contents/`, create an `Info.plist` file to register your help book with macOS:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>org.openrct3.help</string>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <!-- The folder inside Resources containing localized help (optional, defaults to Resources) -->
    <key>CFBundleHelpBookFolder</key>
    <string>English.lproj</string>
    <!-- A unique identifier used by NSHelpManager/HelpAnchor -->
    <key>CFBundleHelpBookName</key>
    <string>OpenRCT3 Help Book</string>
    <key>HPDBookTitle</key>
    <string>OpenRCT3 Help</string>
    <!-- Path to the landing page relative to localized folder -->
    <key>HPDBookAccessPath</key>
    <string>index.html</string>
    <!-- Path to the index file (generated in the next step) -->
    <key>HPDBookIndexPath</key>
    <string>OpenRCT3Help.helpindex</string>
</dict>
</plist>
```

---

## 3. Generate the Search Index (`hiutil`)

To make the help content searchable via the native macOS Help Viewer Search Bar, you must compile a search index using the command-line utility `hiutil` (pre-installed on macOS):

1. Open your Terminal.
2. Run the `hiutil` indexer on your `.help` bundle directory:
   ```bash
   hiutil -cf OpenRCT3Help.help/Contents/Resources/OpenRCT3Help.helpindex OpenRCT3Help.help/Contents/Resources/
   ```
   *   `-c`: Create index.
   *   `-f`: Output destination path.

---

## 4. Register the Help Book in the App Bundle

To tell macOS that your application has a native help book, you must copy the `.help` folder into your compiled app's bundle and register it in the application's main `Info.plist`:

1. Put the `OpenRCT3Help.help` folder inside your compiled app bundle's `Contents/Resources/` directory.
2. Open your main application's `Info.plist` file and add the following keys:
   ```xml
   <key>CFBundleHelpBookFolder</key>
   <string>OpenRCT3Help.help</string>
   <key>CFBundleHelpBookName</key>
   <string>OpenRCT3 Help Book</string>
   ```

---

## 5. Triggering Contextual Help in C# / AppKit

Once registered, you can link the Help Button directly to the anchors defined in your HTML files:

```csharp
using AppKit;

var alert = new NSAlert()
{
    MessageText = "Connection Error",
    InformativeText = "Failed to synchronize with the server.",
    ShowsHelp = true, // Enables the question mark help button
    HelpAnchor = "ConnectionErrorAnchor" // Matches the <a name="..."> tag in your Help Book HTML
};

alert.RunModal();
```

If you prefer to link the help button to an external website instead of a local Help Book, you can assign an [NSAlertDelegate](https://developer.apple.com/documentation/appkit/nsalertdelegate) and intercept the [alertShowHelp(_:)](https://developer.apple.com/documentation/appkit/nsalertdelegate/1531782-alertshowhelp) action manually via [NSWorkspace](https://developer.apple.com/documentation/appkit/nsworkspace):

```csharp
using AppKit;
using Foundation;

public class CrashAlertDelegate : NSAlertDelegate {
  public override bool ShowHelp(NSAlert alert) {
    var helpUrl = new NSUrl("https://github.com/open-rct3/open-rct3/wiki/Troubleshooting");
    NSWorkspace.SharedWorkspace.OpenUrl(helpUrl);
    return true;
  }
}

// Assignment:
alert.ShowsHelp = true;
alert.Delegate = new CrashAlertDelegate();
```
