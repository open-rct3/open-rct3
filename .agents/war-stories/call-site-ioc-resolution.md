# Case Study: Call-Site IoC Resolution & Service Locator Anti-Patterns

## The Core Failures

1. **Leaking Container Resolution to Call Sites:** Rather than allowing the [`OpenRCT3.UI.Debug`](../../OpenRCT3/UI/Debug.cs) component to resolve its own infrastructure dependencies or relying on container resolution, the calling method in [`OpenRCT3.Simulation.World`](../../OpenRCT3/Simulation/World.cs) performed inline queries against `Game.IoC.Resolve<T>()` and passed the resolved objects into `new Debug(...)`.
2. **Defeating the Purpose of Dependency Injection:** Call-site resolution couples the caller directly to the transitive dependencies of the callee. [`World.cs`](../../OpenRCT3/Simulation/World.cs) had to import [`Silk.NET.Input`](https://github.com/dotnet/Silk.NET) purely to resolve an [`IInputContext`](../../OpenRCT3/UI/Debug.cs) instance that [`World`](../../OpenRCT3/Simulation/World.cs) itself never consumed.
3. **Misleading Documentation & Hallucinated Registrations:** The XML documentation comments on [`Debug.cs`](../../OpenRCT3/UI/Debug.cs) claimed the class was instantiated through a `Made.Of` DryIoc container registration in `Game.cs`. In reality, no such registration existed, and [`World.cs`](../../OpenRCT3/Simulation/World.cs) was manually instantiating it with `new` and resolving dependencies inline.
4. **Proliferation of Ambient Service Locator Queries:** The codebase contains multiple instances of components querying `Game.IoC` or `IGame.IoC` directly in field initializers, constructor call sites, and per-frame methods (such as `Clear()` in [`GLContext.windows.cs`](../../OpenRCT3/OpenGL/GLContext.windows.cs)), rather than receiving dependencies via constructor injection.

## Sequence of Events

### 1. Manual Dependency Resolution at the Call Site
When wiring up the diagnostics telemetry window in [`OpenRCT3.Simulation.World.InitializeScene`](../../OpenRCT3/Simulation/World.cs), an agent generated the following instantiation:
```csharp
if (Debug == null) {
  Debug = new UI.Debug(
    game,
    Game.IoC.Resolve<GDK.Platform.IWindow>(),
    Game.IoC.Resolve<IInputContext>());
  Game.IoC.RegisterInstance(Debug, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
  scene.Windows.Add(Debug);
}
```
This forced [`World.cs`](../../OpenRCT3/Simulation/World.cs) to acquire knowledge of [`IWindow`](../../OpenCobra/GDK/Platform/IWindow.cs) and [`IInputContext`](../../OpenRCT3/UI/Debug.cs), adding unnecessary imports and coupling scene creation logic to input handling abstractions.

### 2. Hallucinated Container Registration in Remarks
The header remarks on [`Debug.cs`](../../OpenRCT3/UI/Debug.cs) stated:
```csharp
/// <remarks>
/// Constructed via <see cref="Game.IoC"/> (see <c>Game.cs</c>'s <c>Made.Of</c> registration) rather than
/// <c>new</c> - <paramref name="window"/> and <paramref name="inputContext"/> are resolved from the
/// container's existing registrations...
/// </remarks>
```
This comment contradicted the actual implementation. No registration existed in [`Game.cs`](../../OpenRCT3/Game.cs), and the class was always instantiated via `new` in [`World.cs`](../../OpenRCT3/Simulation/World.cs). Additionally, the comment used an em-dash in violation of the repository prose style.

### 3. User Intervention & Rule Codification
The user flagged the pattern ("Bad robot IoC") and instructed that dependency resolutions be moved inside the [`Debug`](../../OpenRCT3/UI/Debug.cs) constructor. Furthermore, the user mandated codifying this standard into the repository's C# guidelines in [`AGENTS.md`](../../AGENTS.md):
```markdown
- Do NOT resolve IoC dependencies at the call site to pass into constructors; move container resolutions (such as `Game.IoC.Resolve<T>()`) into the constructor or resolve them internally.
```

### 4. Remediation
1. **Secondary Constructor in [`Debug.cs`](../../OpenRCT3/UI/Debug.cs):**
   Added a constructor overload that encapsulates container resolution:
   ```csharp
   public Debug(Game game) : this(
     game,
     Game.IoC.Resolve<PlatformWindow>(),
     Game.IoC.Resolve<IInputContext>()
   ) {}
   ```
   This retains the primary constructor for unit testing with mocks while hiding container mechanics from production callers.
2. **Simplified Call Site in [`World.cs`](../../OpenRCT3/Simulation/World.cs):**
   Simplified window creation to `new UI.Debug(game)` and removed the unused `using Silk.NET.Input;` import.
3. **Accurate Documentation:**
   Reworded the remarks on [`Debug.cs`](../../OpenRCT3/UI/Debug.cs) to document the default resolution behavior without em-dashes or fictional registration references.

## Key Takeaways

1. **Never Resolve Container Dependencies at Call Sites:**
   Passing `IoC.Resolve<T>()` into `new MyClass(...)` is a code smell. The caller should not be burdened with assembling the callee's dependencies. Encapsulate the resolution inside a constructor overload or let the container instantiate the object.
2. **Eliminate Transitive Dependency Leaks:**
   When a caller has to import third-party namespaces (e.g. `Silk.NET.Input`) solely to resolve arguments for another class's constructor, architectural boundaries have broken down.
3. **Keep Architectural Comments Synchronized with Code:**
   Do not describe idealized or imagined container registrations in XML doc comments. Document only the code that actually exists in the repository.
4. **Prefer Constructor Injection Over Ambient Service Locators:**
   Static locators (`Game.IoC`) bypass compile-time dependency graphs. Where container resolution is required, encapsulate it within component constructors rather than scattering ambient queries across methods and call sites.
