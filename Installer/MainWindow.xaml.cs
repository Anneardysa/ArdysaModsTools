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
    /// Main installer window. Orchestrates the installation/update flow
    /// with animated state transitions:
    ///   Install → Progress → Complete/Error
    ///
    /// Also serves as an updater when invoked with --update or when
    /// an existing installation is detected.
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

        /// <summary>Whether we're in update mode (existing install detected).</summary>
        private bool _isUpdateMode;

        // License text — loaded once from embedded resource
        private static readonly string LicenseContent = LoadLicenseText();

        public MainWindow()
        {
            InitializeComponent();

            _installPath = InstallerService.GetDefaultInstallPath();
            _installer = new InstallerService();

            // Detect update mode
            DetectUpdateMode();

            // Initialize UI state
            PathTextBox.Text = _installPath;
            LicenseText.Text = LicenseContent;

            // Show estimated payload size
            var payloadSize = InstallerService.GetEmbeddedPayloadSize();
            if (payloadSize > 0)
            {
                SizeText.Text = $"~{payloadSize / (1024 * 1024)} MB";
            }

            // Always show the new version being installed
            if (!_isUpdateMode)
            {
                VersionText.Text = $"v{GetNewVersion()}";
                VersionText.Visibility = Visibility.Visible;
            }

            UpdateDiskSpaceText();
        }

        // ================================================================
        // INITIALIZATION
        // ================================================================

        /// <summary>
        /// Detects if we're running an update (existing installation found).
        /// Switches UI to update mode with version comparison.
        /// </summary>
        private void DetectUpdateMode()
        {
            // Check command-line args for --update flag
            var args = Environment.GetCommandLineArgs();
            var forceUpdate = args.Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase));

            // Check registry for existing installation
            var existingPath = RegistryHelper.GetInstalledPath();

            if (existingPath == null && !forceUpdate)
                return;

            _isUpdateMode = true;

            if (existingPath != null)
                _installPath = existingPath;

            // Switch UI to update mode
            InstallButton.Content = "Update Now";

            // Build version comparison string
            var currentVersion = GetInstalledVersion(existingPath);
            var newVersion = GetNewVersion();

            if (currentVersion != null)
            {
                SubtitleText.Text = $"v{currentVersion}  →  v{newVersion}";
                VersionText.Text = "A new update is available";
            }
            else
            {
                SubtitleText.Text = "Update Available";
                VersionText.Text = $"v{newVersion}";
            }

            VersionText.Visibility = Visibility.Visible;

            // Hide advanced options in update mode — path already locked
            AdvancedToggle.Visibility = Visibility.Collapsed;
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

        /// <summary>Read the new version from this assembly's metadata.</summary>
        private static string GetNewVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var raw = attr?.InformationalVersion
                      ?? asm.GetName().Version?.ToString(3)
                      ?? "Unknown";
            return CleanVersion(raw);
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
                UpdateStatus("Registering application...");
                RegistryHelper.RegisterApp(_installPath);

                // Step 7: Install fonts
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
            await AnimationHelper.CrossFadeAsync(ProgressPanel, CompletePanel, 400);

            // Bounce the checkmark icon
            await AnimationHelper.ScaleBounceAsync(CompleteIcon, 500);

            StatusText.Text = $"Installed to {_installPath}";
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
