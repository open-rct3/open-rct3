// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Platform;
using OpenCobra.OVL;
using OpenRCT3.Platforms;
using OpenRCT3.Platforms.Windows;
using System.Windows.Forms;

namespace OpenRCT3;

internal static class Program {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>
  /// Bridges failed <see cref="Debug.Assert"/> calls into NLog. Without this, a failed assert falls
  /// through to the CLR's <see cref="DefaultTraceListener"/>, which calls <see cref="Environment.FailFast"/>
  /// and kills the process immediately — bypassing <see cref="AppDomain.UnhandledException"/> and NLog
  /// alike, so the failure is otherwise invisible in the log. This listener logs first (and flushes
  /// synchronously) so the assertion message survives; the default listener still runs afterward and
  /// still fails the process, since that's the correct behavior for a Debug build.
  /// </summary>
  private sealed class NLogAssertListener : TraceListener {
    public override void Fail(string? message) => Fail(message, null);

    public override void Fail(string? message, string? detailMessage) {
      logger.Fatal($"Assertion failed: {message} {detailMessage}");
      LogManager.Flush();
    }

    public override void Write(string? message) { }
    public override void WriteLine(string? message) { }
  }

  /// <summary>
  /// Safely handles all unhandled exceptions.
  /// </summary>
  private static void HandleException(Exception? e) {
    if (e == null) return;
    logger.Fatal(e, "An unhandled exception occurred.");
    Debug.Assert(false);

    // TODO: Refactor to custom modal with "Restart" label in place of "Retry".
    var result = MessageBox.Show(
      $"An unhandled exception occurred:\n\n{(e.InnerException != null ? $"{e.Message}\n\n{e.InnerException.Message}" : e.Message)}",
      "OpenRCT3 Error",
      MessageBoxButtons.AbortRetryIgnore,
      MessageBoxIcon.Error
    );
    if (result == DialogResult.Abort) Environment.Exit(1);
    else if (result == DialogResult.Retry) Application.Restart();
  }

  // TODO: Extract this to a common place to keep `Program`s DRY
  private static AppConfig LoadConfigAndFindInstall() {
    var config = AppConfig.Load();
    if (!string.IsNullOrEmpty(config.InstallPath) && InstallFinder.Validate(config.InstallPath)) {
      return config;
    }

    try {
      var installPath = InstallFinder.Find(config.ExtraPaths);
      config = config with { InstallPath = installPath };
      config.Save();
      return config;
    }
    catch (InstallNotFoundException) {
      Application.EnableVisualStyles();
      var picker = new FolderPicker();
      var selectedPath = picker.PickFolder("Select your RCT3 installation folder");

      if (!string.IsNullOrEmpty(selectedPath) && InstallFinder.Validate(selectedPath)) {
        config = config with { InstallPath = selectedPath };
        config.Save();
        return config;
      }

      logger.Fatal("Could not find RCT3 installation folder.");
      Environment.Exit(1);
      return config;
    }
  }

  [STAThread]
  // FIXME: Refactor to share common code among platforms, i.e. consolidate this, Program.macOS.cs, and Program.linux.cs
  public static void Main(string[] args) {
    AppDomain.CurrentDomain.UnhandledException += (sender, e) => HandleException(e.ExceptionObject as Exception);
    Application.ThreadException += (sender, e) => HandleException(e.Exception);
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

    // TODO: Extract this section to share code among platforms
    var internalNlog = Path.Combine(AppConfig.LogsFolder, "internal-nlog.txt");
    LogManager.Setup()
      .LoadConfigurationFromFile("nlog.config")
      .SetupInternalLogger(log => log
        .LogToFile(internalNlog)
#if DEBUG
        .SetMinimumLogLevel(LogLevel.Trace)
#else
        .SetMinimumLogLevel(LogLevel.Warn)
#endif
      );

#if DEBUG
    // Raise every rule to Trace (nlog.config's file/console rule otherwise floors at Debug) and bridge
    // failed Debug.Assert calls into NLog — see NLogAssertListener. Debug builds only: Debug.Assert
    // itself is already [Conditional("DEBUG")] and compiles out entirely in Release.
    foreach (var rule in LogManager.Configuration!.LoggingRules)
      rule.SetLoggingLevels(LogLevel.Trace, LogLevel.Fatal);
    LogManager.ReconfigExistingLoggers();
    // Insert (not Add) at index 0: TraceListenerCollection runs Fail() on listeners in order, and
    // the CLR's default DefaultTraceListener.Fail calls Environment.FailFast synchronously — if it
    // ran first, the process would die before our listener ever got a turn to log anything.
    Trace.Listeners.Insert(0, new NLogAssertListener());
#endif

    // ‼ This order matters!
    // SetCompatibleTextRenderingDefault() must be called before any UI elements are created.
    // LoadConfigAndFindInstall may show a dialog to the user.
    logger.Info("Starting OpenRCT3 on Windows...");
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    LoadConfigAndFindInstall();

    // Create the main window
    var mainWindow = new GameWindow();
    Game.IoC.RegisterInstance<IWindow>(
      mainWindow,
      IfAlreadyRegistered.Replace,
      // This Program manages the game window's lifetime
      Setup.With(preventDisposal: true));

    // Start the game
    mainWindow.Start();
    Application.Run(mainWindow);

    logger.Info("Application exited");
  }
}
