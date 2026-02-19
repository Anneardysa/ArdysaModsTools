/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using ArdysaModsTools.Installer.Helpers;
using ArdysaModsTools.Installer.Services;

namespace ArdysaModsTools.Installer
{
    /// <summary>
    /// The installer operates in one of three modes, determined at startup.
    /// </summary>
    public enum InstallMode
    {
        /// <summary>No existing installation found. Fresh install.</summary>
        Install,

        /// <summary>Existing installation found with an older version.</summary>
        Update,

        /// <summary>Existing installation found with the same (or newer) version.</summary>
        Reinstall,

        /// <summary>User/system requested uninstallation.</summary>
        Uninstall
    }

    /// <summary>
    /// Main installer window. Orchestrates the installation flow
    /// with animated state transitions:
    ///   Install → Progress → Complete/Error
    ///
    /// Supports three modes: Install, Update, and Reinstall.
    /// Mode is auto-detected from registry + version comparison,
    /// or forced via --update / --reinstall CLI flags.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly InstallerService _installer;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Install/update target path.</summary>
        private string _installPath;

        /// <summary>Whether the advanced panel is currently expanded.</summary>
        private bool _advancedExpanded;

        /// <summary>Whether the license modal is currently visible.</summary>
        private bool _licenseVisible;

        /// <summary>Current installer mode (Install, Update, or Reinstall).</summary>
        private InstallMode _mode = InstallMode.Install;

        /// <summary>Set to true after uninstall pipeline completes successfully.</summary>
        private bool _uninstallCompleted;

        // License text — loaded once from embedded resource
        private static readonly string LicenseContent = LoadLicenseText();

        public MainWindow()
        {
            InitializeComponent();

            _installPath = InstallerService.GetDefaultInstallPath();
            _installer = new InstallerService();

            // Detect install mode (Install / Update / Reinstall)
            DetectInstallMode();

            // Initialize UI state
            PathTextBox.Text = _installPath;
            LicenseText.Text = LicenseContent;

            // Show estimated payload size
            var payloadSize = InstallerService.GetEmbeddedPayloadSize();
            if (payloadSize > 0)
            {
                SizeText.Text = $"~{payloadSize / (1024 * 1024)} MB";
            }

            // Apply mode-specific UI
            ApplyModeUI();

            UpdateDiskSpaceText();

            // Wire up window closing for uninstall self-cleanup
            Closing += Window_Closing;
        }

        /// <summary>
        /// Handles window closing. If uninstall completed, schedules
        /// self-deletion of the uninstaller exe and install directory.
        /// The deletion waits for this process to fully exit (PID-based loop).
        /// </summary>
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_uninstallCompleted)
            {
                UninstallService.ScheduleSelfDeletion(_installPath);
            }
        }

        // ================================================================
        // INITIALIZATION
        // ================================================================

        /// <summary>
        /// Detects the install mode using a strict priority chain:
        ///
        ///   Priority 1: CLI flags (--uninstall, --reinstall, --update)
        ///   Priority 2: No embedded payload → Uninstall (slim uninstaller binary)
        ///   Priority 3: No registry entry → Install (fresh)
        ///   Priority 4: App exe missing at registered path → Install (fresh)
        ///   Priority 5: Version + build comparison → Update or Reinstall
        ///
        /// The payload check (Priority 2) is the key mechanism that distinguishes
        /// the slim uninstaller from the full installer. The uninstaller is built
        /// without payload.zip embedded, so it can never install — only uninstall.
        /// </summary>
        private void DetectInstallMode()
        {
            var args = Environment.GetCommandLineArgs();
            var existingPath = RegistryHelper.GetInstalledPath();

            // ── Priority 1: Explicit CLI flags ───────────────
            if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                _mode = InstallMode.Uninstall;
                if (existingPath != null) _installPath = existingPath;
                return;
            }

            if (args.Any(a => a.Equals("--reinstall", StringComparison.OrdinalIgnoreCase)))
            {
                _mode = InstallMode.Reinstall;
                if (existingPath != null) _installPath = existingPath;
                return;
            }

            if (args.Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase)))
            {
                _mode = InstallMode.Update;
                if (existingPath != null) _installPath = existingPath;
                return;
            }

            // ── Priority 2: No embedded payload → Uninstaller ──
            // The slim uninstaller has no payload.zip embedded.
            // If we have no payload, we cannot install — only uninstall.
            if (!HasEmbeddedPayload())
            {
                _mode = InstallMode.Uninstall;
                if (existingPath != null) _installPath = existingPath;
                return;
            }

            // ── Priority 3: No existing installation ─────────
            if (existingPath == null)
            {
                _mode = InstallMode.Install;
                return;
            }

            _installPath = existingPath;

            // ── Priority 4: App exe missing at registered path ─
            var exePath = Path.Combine(existingPath, "ArdysaModsTools.exe");
            if (!File.Exists(exePath))
            {
                _mode = InstallMode.Install;
                return;
            }

            // ── Priority 5: Version + build comparison ──────
            var installedVersion = GetInstalledVersion(existingPath);
            var installedBuild = GetInstalledBuildNumber(existingPath);
            var (newVersion, newBuild) = GetNewVersionInfo();

            if (installedVersion != null
                && installedVersion == newVersion
                && installedBuild == newBuild)
            {
                _mode = InstallMode.Reinstall;
            }
            else
            {
                _mode = InstallMode.Update;
            }
        }

        /// <summary>
        /// Checks whether this executable has an embedded payload.zip resource.
        /// The full installer has it; the slim uninstaller does not.
        /// This is the definitive way to distinguish installer vs uninstaller.
        /// </summary>
        private static bool HasEmbeddedPayload()
        {
            return InstallerService.GetEmbeddedPayloadSize() > 0;
        }

        /// <summary>
        /// Applies mode-specific UI: button text, subtitle, version badge,
        /// and advanced panel visibility.
        /// </summary>
        private void ApplyModeUI()
        {
            var (newVersion, buildNum) = GetNewVersionInfo();
            var versionDisplay = buildNum > 0
                ? $"{newVersion} (Build {buildNum})"
                : newVersion;

            switch (_mode)
            {
                case InstallMode.Install:
                    InstallButton.Content = "Install Now";
                    SubtitleText.Text = "Setup";
                    VersionText.Text = $"v{versionDisplay}";
                    VersionText.Visibility = Visibility.Visible;
                    // Advanced panel stays visible — path is editable
                    break;

                case InstallMode.Update:
                    InstallButton.Content = "Update Now";
                    var installedVersion = GetInstalledVersion(_installPath);
                    var installedBuild = GetInstalledBuildNumber(_installPath);
                    if (installedVersion != null)
                    {
                        var oldDisplay = installedBuild > 0
                            ? $"{installedVersion} (Build {installedBuild})"
                            : installedVersion;
                        SubtitleText.Text = $"Update to v{versionDisplay}";
                        VersionText.Text = $"from v{oldDisplay}";
                    }
                    else
                    {
                        SubtitleText.Text = "Update Available";
                        VersionText.Text = $"v{versionDisplay}";
                    }
                    VersionText.Visibility = Visibility.Visible;
                    AdvancedToggle.Visibility = Visibility.Collapsed;
                    break;

                case InstallMode.Reinstall:
                    InstallButton.Content = "Reinstall";
                    SubtitleText.Text = $"Reinstall v{versionDisplay}";
                    VersionText.Text = "The same version will be reinstalled";
                    VersionText.Visibility = Visibility.Visible;
                    AdvancedToggle.Visibility = Visibility.Collapsed;
                    break;

                case InstallMode.Uninstall:
                    InstallButton.Content = "Uninstall";
                    // Show the INSTALLED version (not the exe's own version)
                    var uninstVersion = GetInstalledVersion(_installPath);
                    var uninstBuild = GetInstalledBuildNumber(_installPath);
                    if (uninstVersion != null)
                    {
                        var uninstDisplay = uninstBuild > 0
                            ? $"{uninstVersion} (Build {uninstBuild})"
                            : uninstVersion;
                        SubtitleText.Text = "Uninstall";
                        VersionText.Text = $"v{uninstDisplay} will be removed";
                    }
                    else
                    {
                        SubtitleText.Text = "Uninstall";
                        VersionText.Text = "Application will be removed";
                    }
                    VersionText.Visibility = Visibility.Visible;
                    AdvancedToggle.Visibility = Visibility.Collapsed;
                    SizeText.Visibility = Visibility.Collapsed;
                    WaveCanvas.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// Strips the build metadata suffix (+commitHash) from a version string.
        /// "2.1.12-beta+abc123" → "2.1.12-beta"
        /// </summary>
        private static string CleanVersion(string version)
        {
            // Strip +metadata (SemVer build info)
            var plusIdx = version.IndexOf('+');
            if (plusIdx > 0)
                version = version[..plusIdx];

            return version.Trim();
        }

        /// <summary>Read the installed version from the existing exe's FileVersion.</summary>
        private static string? GetInstalledVersion(string? installPath)
        {
            if (installPath == null) return null;

            var exePath = Path.Combine(installPath, "ArdysaModsTools.exe");
            if (!File.Exists(exePath)) return null;

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                var raw = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? null;
                return raw != null ? CleanVersion(raw) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read the build number from the installed exe's AssemblyVersion.
        /// The build number is the 4th part (Revision) of the assembly version:
        /// Major.Minor.Patch.Build → e.g. 2.1.12.2094 → build = 2094
        /// </summary>
        private static int GetInstalledBuildNumber(string? installPath)
        {
            if (installPath == null) return 0;

            var exePath = Path.Combine(installPath, "ArdysaModsTools.exe");
            if (!File.Exists(exePath)) return 0;

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                // FilePrivatePart is the 4th component of the file version
                return versionInfo.FilePrivatePart;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Read the new version and build number from this assembly's metadata.
        /// Returns (version, buildNumber) where buildNumber is the 4th part of AssemblyVersion.
        /// </summary>
        private static (string Version, int Build) GetNewVersionInfo()
        {
            var asm = Assembly.GetExecutingAssembly();
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var raw = attr?.InformationalVersion
                      ?? asm.GetName().Version?.ToString(3)
                      ?? "Unknown";
            var version = CleanVersion(raw);

            // Extract build number from AssemblyVersion (4th part: Major.Minor.Patch.Build)
            var asmVersion = asm.GetName().Version;
            var build = asmVersion?.Revision ?? 0;

            return (version, build);
        }

        /// <summary>Load the license text from the LICENSE file or embedded resource.</summary>
        private static string LoadLicenseText()
        {
            // Try reading from adjacent LICENSE file first
            var exeDir = AppContext.BaseDirectory;
            var licenseFile = Path.Combine(exeDir, "LICENSE");

            // For single-file publish, try going up the directory tree
            if (!File.Exists(licenseFile))
            {
                // Fallback: embedded license summary
                return
                    "GNU GENERAL PUBLIC LICENSE\n" +
                    "Version 3, 29 June 2007\n\n" +
                    "Copyright (C) 2007 Free Software Foundation, Inc.\n" +
                    "Everyone is permitted to copy and distribute verbatim copies\n" +
                    "of this license document, but changing it is not allowed.\n\n" +
                    "PREAMBLE\n\n" +
                    "The GNU General Public License is a free, copyleft license for\n" +
                    "software and other kinds of works.\n\n" +
                    "The licenses for most software and other practical works are\n" +
                    "designed to take away your freedom to share and change the works.\n" +
                    "By contrast, the GNU General Public License is intended to\n" +
                    "guarantee your freedom to share and change all versions of a\n" +
                    "program — to make sure it remains free software for all its users.\n\n" +
                    "TERMS AND CONDITIONS\n\n" +
                    "0. Definitions.\n" +
                    "1. Source Code.\n" +
                    "2. Basic Permissions.\n" +
                    "3. Protecting Users' Legal Rights From Anti-Circumvention Law.\n" +
                    "4. Conveying Verbatim Copies.\n" +
                    "5. Conveying Modified Source Versions.\n" +
                    "6. Conveying Non-Source Forms.\n" +
                    "7. Additional Terms.\n" +
                    "8. Termination.\n" +
                    "9. Acceptance Not Required for Having Copies.\n" +
                    "10. Automatic Licensing of Downstream Recipients.\n" +
                    "11. Patents.\n" +
                    "12. No Surrender of Others' Freedom.\n" +
                    "13. Use with the GNU Affero General Public License.\n" +
                    "14. Revised Versions of this License.\n" +
                    "15. Disclaimer of Warranty.\n" +
                    "16. Limitation of Liability.\n" +
                    "17. Interpretation of Sections 15 and 16.\n\n" +
                    "DISCLAIMER\n\n" +
                    "THIS SOFTWARE IS PROVIDED \"AS IS\" WITHOUT WARRANTY OF ANY KIND.\n" +
                    "The entire risk as to the quality and performance of the software\n" +
                    "is with you.\n\n" +
                    "For the complete license text, visit:\n" +
                    "https://www.gnu.org/licenses/gpl-3.0.html\n\n" +
                    "ADDITIONAL TERMS\n\n" +
                    "This software is NOT affiliated with, endorsed by, or sponsored\n" +
                    "by Valve Corporation. Dota 2 is a trademark of Valve Corporation.\n" +
                    "Use of this software is entirely at your own risk.\n\n" +
                    "© 2025-2026 Ardysa. All rights reserved.";
            }

            try
            {
                return File.ReadAllText(licenseFile);
            }
            catch
            {
                return "License file could not be loaded.";
            }
        }

        // ================================================================
        // EVENT HANDLERS
        // ================================================================

        /// <summary>Window entrance animation on load.</summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await AnimationHelper.EntranceAsync(MainBorder, 400);
        }

        /// <summary>Enables dragging the borderless window.</summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            Close();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == InstallMode.Uninstall)
            {
                await RunUninstallAsync();
                return;
            }

            // Read install path from advanced panel
            if (_advancedExpanded && !string.IsNullOrWhiteSpace(PathTextBox.Text))
            {
                _installPath = PathTextBox.Text.Trim();
            }

            await RunInstallAsync();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var exePath = Path.Combine(_installPath, "ArdysaModsTools.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }
            Close();
        }

        // ----------------------------------------------------------------
        // LICENSE MODAL
        // ----------------------------------------------------------------

        private async void LicenseLink_Click(object sender, RoutedEventArgs e)
        {
            if (_licenseVisible) return;
            _licenseVisible = true;
            await AnimationHelper.FadeInAsync(LicenseModal, 250);
        }

        private async void LicenseClose_Click(object sender, RoutedEventArgs e)
        {
            if (!_licenseVisible) return;
            await AnimationHelper.FadeOutAsync(LicenseModal, 200);
            _licenseVisible = false;
        }

        private async void LicenseBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_licenseVisible) return;
            await AnimationHelper.FadeOutAsync(LicenseModal, 200);
            _licenseVisible = false;
        }

        // ----------------------------------------------------------------
        // ADVANCED INSTALL PANEL
        // ----------------------------------------------------------------

        private async void AdvancedToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_advancedExpanded)
            {
                await AnimationHelper.SlideUpAsync(AdvancedPanel, 200);
                _advancedExpanded = false;
            }
            else
            {
                PathTextBox.Text = _installPath;
                await AnimationHelper.SlideDownAsync(AdvancedPanel, 250, 90);
                _advancedExpanded = true;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Use WinForms FolderBrowserDialog (works without extra NuGet packages)
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Installation Folder",
                SelectedPath = _installPath,
                ShowNewFolderButton = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _installPath = dialog.SelectedPath;
                PathTextBox.Text = _installPath;
                UpdateDiskSpaceText();
            }
        }

        private void UpdateDiskSpaceText()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(_installPath) ?? "C");
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                DiskSpaceText.Text = $"{freeGb:F1} GB free on {drive.Name}";
            }
            catch
            {
                DiskSpaceText.Text = string.Empty;
            }
        }

        // ================================================================
        // INSTALLATION PIPELINE
        // ================================================================

        private async Task RunInstallAsync()
        {
            try
            {
                await ShowProgressAsync();

                var progress = new Progress<InstallProgress>(p =>
                {
                    AnimationHelper.AnimateProgressBar(InstallProgress, p.Percent, 300);
                    ProgressPercent.Text = $"{p.Percent}%";
                    ProgressText.Text = p.Status;
                });

                // Step 1: Close running app if detected
                UpdateStatus("Checking for running instances...");
                await ProcessHelper.CloseRunningAppAsync("ArdysaModsTools.exe", _cts.Token);

                // Step 2: Remove old version (silent cleanup)
                var existingPath = RegistryHelper.GetInstalledPath();
                if (existingPath != null && Directory.Exists(existingPath))
                {
                    UpdateStatus("Removing previous version...");
                    InstallerService.CleanupOldInstallation(existingPath);
                }

                // Step 3: Extract payload
                UpdateStatus("Installing files...");
                await _installer.ExtractPayloadAsync(_installPath, progress, _cts.Token);

                // Step 4: Install prerequisites (WebView2)
                UpdateStatus("Checking prerequisites...");
                await PrerequisiteChecker.EnsureWebView2Async(_cts.Token);

                // Step 5: Create shortcuts
                UpdateStatus("Creating shortcuts...");
                ShortcutHelper.CreateShortcuts(_installPath);

                // Step 6: Register in Add/Remove Programs
                // (unins000.exe is already in the payload — extracted in step 3)
                UpdateStatus("Registering application...");
                RegistryHelper.RegisterApp(_installPath);

                // Step 8: Install fonts
                UpdateStatus("Installing fonts...");
                FontInstaller.InstallFonts(_installPath);

                // Done!
                await ShowCompleteAsync();
            }
            catch (OperationCanceledException)
            {
                await ShowInstallAsync(); // User cancelled, go back
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
        }

        // ================================================================
        // UNINSTALL PIPELINE
        // ================================================================

        private async Task RunUninstallAsync()
        {
            try
            {
                await ShowProgressAsync();

                var uninstaller = new UninstallService();
                var progress = new Progress<UninstallProgress>(p =>
                {
                    AnimationHelper.AnimateProgressBar(InstallProgress, p.Percent, 300);
                    ProgressPercent.Text = $"{p.Percent}%";
                    ProgressText.Text = p.Status;
                });

                await uninstaller.RunUninstallAsync(_installPath, progress, _cts.Token);

                // Mark uninstall as completed — self-deletion happens on window close
                _uninstallCompleted = true;

                await ShowCompleteAsync();

                // Auto-close after 3 seconds — nothing to launch after uninstall
                await Task.Delay(3000);
                Close();
            }
            catch (OperationCanceledException)
            {
                await ShowInstallAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
        }

        // ================================================================
        // ANIMATED UI STATE TRANSITIONS
        // ================================================================

        /// <summary>Transition to install state with fade.</summary>
        private async Task ShowInstallAsync()
        {
            // Hide all other panels
            await HideAllPanelsAsync();

            StatusText.Text = string.Empty;
            await AnimationHelper.FadeInAsync(InstallPanel, 300);
        }

        /// <summary>Transition to progress state: cross-fade from install panel.</summary>
        private async Task ShowProgressAsync()
        {
            // Collapse advanced panel if open
            if (_advancedExpanded)
            {
                await AnimationHelper.SlideUpAsync(AdvancedPanel, 150);
                _advancedExpanded = false;
            }

            await AnimationHelper.CrossFadeAsync(InstallPanel, ProgressPanel, 400);
        }

        /// <summary>
        /// Transition to completion: cross-fade + checkmark scale bounce.
        /// </summary>
        private async Task ShowCompleteAsync()
        {
            // Start with empty text for typewriter effect
            var finalText = CompleteIcon.Text;
            CompleteIcon.Text = "";

            // Hide Launch button for uninstall (no app to launch)
            if (_mode == InstallMode.Uninstall)
            {
                LaunchButton.Visibility = Visibility.Collapsed;
                CompleteText.Text = "Uninstalled";
            }

            await AnimationHelper.CrossFadeAsync(ProgressPanel, CompletePanel, 400);

            // Terminal-retro typewriter: type out [ OK ] character by character
            await Task.Delay(300);
            await TypewriterAsync(CompleteIcon, finalText, 80);

            var action = _mode switch
            {
                InstallMode.Update => "Updated",
                InstallMode.Reinstall => "Reinstalled",
                InstallMode.Uninstall => "Uninstalled",
                _ => "Installed"
            };

            StatusText.Text = _mode == InstallMode.Uninstall
                ? $"Uninstalled from {_installPath}"
                : $"{action} to {_installPath}";
        }

        /// <summary>
        /// Terminal-retro typewriter effect: types text character by character
        /// with a blinking cursor underscore.
        /// </summary>
        private static async Task TypewriterAsync(System.Windows.Controls.TextBlock target, string text, int charDelayMs)
        {
            for (int i = 0; i <= text.Length; i++)
            {
                // Show typed portion + blinking cursor
                var typed = text[..i];
                target.Text = i < text.Length ? typed + "_" : typed;
                await Task.Delay(charDelayMs);
            }
        }

        /// <summary>
        /// Transition to error state: cross-fade + shake animation.
        /// </summary>
        private async Task ShowErrorAsync(string message)
        {
            ErrorText.Text = message;
            await AnimationHelper.CrossFadeAsync(ProgressPanel, ErrorPanel, 400);

            // Shake the error panel for feedback
            await AnimationHelper.ShakeAsync(ErrorPanel, 400);
        }

        /// <summary>Hides all content panels (used before showing a new one).</summary>
        private async Task HideAllPanelsAsync()
        {
            var panels = new UIElement[] { InstallPanel, ProgressPanel, CompletePanel, ErrorPanel };
            foreach (var panel in panels)
            {
                if (panel.Visibility == Visibility.Visible)
                {
                    await AnimationHelper.FadeOutAsync(panel, 150);
                }
            }
        }

        private void UpdateStatus(string text)
        {
            StatusText.Text = text;
        }
    }
}
