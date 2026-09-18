# macOS Game Window Lifecycle

## `NSWindow` Closure Sequence

1. User clicks Close button or selects Close menu item

2. **`windowShouldClose(_:)` delegate method**
   - Window asks delegate for permission to close
   - Delegate returns `Bool`: `true` to allow, `false` to prevent
   - Use case: prompt "Save changes?" before closing
   - [Source: Apple Developer Documentation](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/WinPanel/Tasks/UsingWindowNotDel.html)

3. **If approved: `NSWindowWillCloseNotification` posted**
   - Notification broadcast to all observers
   - [Source: Apple Developer Documentation](https://developer.apple.com/library/archive/documentation/General/Conceptual/DevPedia-CocoaCore/Delegation.html)

4. **`windowWillClose(_:)` delegate method**
   - Delegate performs cleanup tasks
   - Use case: release resources, save state, autorelease the window controller
   - [Source: Apple Developer Documentation](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/WinPanel/Tasks/UsingWindowNotDel.html)

5. **Window is closed and deallocated**
   - If `isReleasedWhenClosed` is `true`, window is released

### Key Distinction

- **`windowShouldClose`** = *veto point* (control whether closure happens)
- **`windowWillClose`** = *cleanup point* (respond after approval)
