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
  /// Safely handles all unhandled exceptions.
  /// </summary>
  private static void HandleException(Exception? e) {
    if (e == null) return;
    var message = e.InnerException != null
      ? $"{e.Message}\n\n{e.InnerException.Message}"
      : e.Message;
    logger.Fatal(e, "An unhandled exception occurred.");
    Debug.Assert(false);

    // TODO: Refactor to custom modal with "Restart" label in place of "Retry".
    var result = MessageBox.Show(
      $"An unhandled exception occurred:\n\n{message}",
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

    LogManager.Setup().LoadConfigurationFromFile("nlog.config");
    logger.Info("Starting OpenRCT3 on Windows...");

    // ‼ This order matters!
    // SetCompatibleTextRenderingDefault() must be called before any UI elements are created.
    // LoadConfigAndFindInstall may show a dialog to the user.
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    LoadConfigAndFindInstall();

    // Create the main window
    var mainWindow = new GameWindow();
    Scene.IoC.RegisterInstance<IWindow>(
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
