# Renderer Control Flow

In the OpenRCT3 project, the `Renderer.Render` method is called during the application's paint/render loop, managed by platform-specific UI components.

## Windows Control Flow

1. **Entry Point**: `Program.windows.cs`
   - `Main` method initializes the `Game` instance and starts the application with `Application.Run(new MainForm())`.
2. **UI Setup**: `MainForm.Designer.cs`
   - `MainForm` contains a `GLSurface` control (an OpenGL-enabled Windows Forms control).
3. **Initialization**: `GLSurface.cs`
   - In `OnHandleCreated`, the `Renderer` is instantiated and initialized.
4. **Render Loop**: `GLSurface.cs`
   - The standard Windows Forms `OnPaint` event calls `OnRenderFrame`.
   - `OnRenderFrame` calls `_renderer.Render(Game.Instance.Scene)` before swapping graphics buffers.
   - `OnResize` also triggers a render by calling `Invalidate()`, which prompts a new paint event.

## macOS Control Flow

1. **Rendering**: `OpenGLLayer.cs`
   - The `OpenGLLayer.DrawInContext` method (or similar Core Animation / AppKit event) calls `_renderer.Render(Game.Instance.Scene)`.
   - It also ensures the camera is updated before rendering: `Game.Instance.Scene.UpdateCamera(...)`.

## Call Hierarchy Summary

### Windows
```text
Program.Main()
└── MainForm (WinForms)
    └── GLSurface.OnPaint()
        └── GLSurface.OnRenderFrame()
            └── Renderer.Render(Scene)
```

### macOS
```text
GameViewController
└── OpenGLLayer.DrawInContext() (or similar)
    └── Renderer.Render(Scene)
```

The `Renderer` always renders the `Scene` associated with the global `Game.Instance`.
