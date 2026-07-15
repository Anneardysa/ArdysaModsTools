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
    public enum InstallMode
    {
        Install,

        Update,

        Reinstall,

        Uninstall
    }

    public partial class MainWindow : Window
    {
        private readonly InstallerService _installer;
        private readonly CancellationTokenSource _cts = new();

        private string _installPath;

        private bool _advancedExpanded;

        private bool _licenseVisible;

        private InstallMode _mode = InstallMode.Install;

        private bool _uninstallCompleted;

        private bool _pipelineRunning;

        private static readonly string LicenseContent = LoadLicenseText();

        public MainWindow()
        {
            InitializeComponent();

            _installPath = InstallerService.GetDefaultInstallPath();
            _installer = new InstallerService();

            DetectInstallMode();

            PathTextBox.Text = _installPath;
            LicenseText.Text = LicenseContent;

            var payloadSize = InstallerService.GetEmbeddedPayloadSize();
            if (payloadSize > 0)
            {
                SizeText.Text = $"~{payloadSize / (1024 * 1024)} MB";
            }

            ApplyModeUI();

            UpdateDiskSpaceText();

            Closing += Window_Closing;
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_uninstallCompleted)
            {
                UninstallService.ScheduleSelfDeletion(_installPath);
            }
        }


        private void DetectInstallMode()
        {
            var args = Environment.GetCommandLineArgs();
            var existingPath = RegistryHelper.GetInstalledPath();

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

            if (!HasEmbeddedPayload())
            {
                _mode = InstallMode.Uninstall;
                if (existingPath != null) _installPath = existingPath;
                return;
            }

            if (existingPath == null)
            {
                _mode = InstallMode.Install;
                return;
            }

            _installPath = existingPath;

            var exePath = Path.Combine(existingPath, "ArdysaModsTools.exe");
            if (!File.Exists(exePath))
            {
                _mode = InstallMode.Install;
                return;
            }

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

        private static bool HasEmbeddedPayload()
        {
            return InstallerService.GetEmbeddedPayloadSize() > 0;
        }

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

        private static string CleanVersion(string version)
        {
            var plusIdx = version.IndexOf('+');
            if (plusIdx > 0)
                version = version[..plusIdx];

            return version.Trim();
        }

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

        private static int GetInstalledBuildNumber(string? installPath)
        {
            if (installPath == null) return 0;

            var exePath = Path.Combine(installPath, "ArdysaModsTools.exe");
            if (!File.Exists(exePath)) return 0;

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                return versionInfo.FilePrivatePart;
            }
            catch
            {
                return 0;
            }
        }

        private static (string Version, int Build) GetNewVersionInfo()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null && File.Exists(exePath))
                {
                    var info = FileVersionInfo.GetVersionInfo(exePath);
                    var raw = info.ProductVersion ?? info.FileVersion;
                    if (!string.IsNullOrEmpty(raw))
                        return (CleanVersion(raw), info.FilePrivatePart);
                }
            }
            catch
            {
            }

            var asm = Assembly.GetExecutingAssembly();
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var rawAsm = attr?.InformationalVersion
                         ?? asm.GetName().Version?.ToString(3)
                         ?? "Unknown";
            return (CleanVersion(rawAsm), asm.GetName().Version?.Revision ?? 0);
        }

        private static string LoadLicenseText()
        {
            var exeDir = AppContext.BaseDirectory;
            var licenseFile = Path.Combine(exeDir, "LICENSE");

            if (!File.Exists(licenseFile))
            {
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


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await AnimationHelper.EntranceAsync(MainBorder, 400);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pipelineRunning)
            {
                var result = System.Windows.MessageBox.Show(
                    "Setup is still working. Cancel and exit?\n\n" +
                    "The installation may be left incomplete.",
                    "ArdysaModsTools Setup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }

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

            if (_advancedExpanded && !string.IsNullOrWhiteSpace(PathTextBox.Text))
            {
                try
                {
                    _installPath = InstallerService.NormalizeInstallPath(PathTextBox.Text);
                    PathTextBox.Text = _installPath;
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
                {
                    System.Windows.MessageBox.Show(
                        $"The install path is not valid:\n{ex.Message}",
                        "ArdysaModsTools Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
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
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Installation Folder",
                SelectedPath = _installPath,
                ShowNewFolderButton = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _installPath = InstallerService.NormalizeInstallPath(dialog.SelectedPath);
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


        private async Task RunInstallAsync()
        {
            _pipelineRunning = true;
            try
            {
                await ShowProgressAsync();

                var progress = new Progress<InstallProgress>(p =>
                {
                    AnimationHelper.AnimateProgressBar(InstallProgress, p.Percent, 300);
                    ProgressPercent.Text = $"{p.Percent}%";
                    ProgressText.Text = p.Status;
                });

                UpdateStatus("Checking for running instances...");
                await ProcessHelper.CloseRunningAppAsync("ArdysaModsTools.exe", _cts.Token);

                var existingPath = RegistryHelper.GetInstalledPath();
                UpdateStatus("Installing files...");
                await _installer.ExtractPayloadAsync(
                    _installPath, progress, _cts.Token, previousInstallPath: existingPath);

                UpdateStatus("Checking prerequisites...");
                await PrerequisiteChecker.EnsureWebView2Async(_cts.Token);

                UpdateStatus("Creating shortcuts...");
                ShortcutHelper.CreateShortcuts(_installPath);

                UpdateStatus("Registering application...");
                RegistryHelper.RegisterApp(_installPath);

                UpdateStatus("Installing fonts...");
                FontInstaller.InstallFonts(_installPath);

                await ShowCompleteAsync();
            }
            catch (OperationCanceledException)
            {
                await ShowInstallAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
            finally
            {
                _pipelineRunning = false;
            }
        }


        private async Task RunUninstallAsync()
        {
            _pipelineRunning = true;
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

                _uninstallCompleted = true;
                _pipelineRunning = false;

                await ShowCompleteAsync();

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
            finally
            {
                _pipelineRunning = false;
            }
        }


        private async Task ShowInstallAsync()
        {
            await HideAllPanelsAsync();

            StatusText.Text = string.Empty;
            await AnimationHelper.FadeInAsync(InstallPanel, 300);
        }

        private async Task ShowProgressAsync()
        {
            if (_advancedExpanded)
            {
                await AnimationHelper.SlideUpAsync(AdvancedPanel, 150);
                _advancedExpanded = false;
            }

            await AnimationHelper.CrossFadeAsync(InstallPanel, ProgressPanel, 400);
        }

        private async Task ShowCompleteAsync()
        {
            var finalText = CompleteIcon.Text;
            CompleteIcon.Text = "";

            if (_mode == InstallMode.Uninstall)
            {
                LaunchButton.Visibility = Visibility.Collapsed;
                CompleteText.Text = "Uninstalled";
            }

            await AnimationHelper.CrossFadeAsync(ProgressPanel, CompletePanel, 400);

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

        private static async Task TypewriterAsync(System.Windows.Controls.TextBlock target, string text, int charDelayMs)
        {
            for (int i = 0; i <= text.Length; i++)
            {
                var typed = text[..i];
                target.Text = i < text.Length ? typed + "_" : typed;
                await Task.Delay(charDelayMs);
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            ErrorText.Text = message;
            await AnimationHelper.CrossFadeAsync(ProgressPanel, ErrorPanel, 400);

            await AnimationHelper.ShakeAsync(ErrorPanel, 400);
        }

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
