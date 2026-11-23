using System.Diagnostics;
using System.Windows;
using Olden_Era___Template_Editor.Services.Update;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Auto-update wiring for the main window: a quiet startup check against the
    /// public GitHub repository, plus the banner shown when a newer release is
    /// available ("notify + button" mode).
    /// </summary>
    public partial class MainWindow
    {
        private UpdateInfo? _pendingUpdate;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Skip the network check on a quiet/minimized launch (e.g. started
            // from a shortcut while gaming) — same flags as the constructor.
            bool startMinimized = Environment.GetCommandLineArgs().Any(a =>
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-m", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/min", StringComparison.OrdinalIgnoreCase));
            if (startMinimized || Environment.GetCommandLineArgs().Any(a => a.StartsWith("--shoot", StringComparison.OrdinalIgnoreCase) || App.IsPatchVerificationCommand(a))) return;

            // Fire-and-forget: a failed check must never block or crash startup.
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            UpdateInfo? info;
            try { info = await UpdateService.CheckForUpdateAsync(); }
            catch { info = null; }
            if (info is null) return;

            _pendingUpdate = info;
            ShowUpdateBanner(info);
        }

        private void ShowUpdateBanner(UpdateInfo info)
        {
            string newLabel = FormatVersion(info.Version);
            string curLabel = FormatVersion(UpdateService.CurrentVersion);
            TxtUpdateBanner.Text = L.Get("S.Upd.Available", newLabel, curLabel);
            BtnUpdateNotes.Visibility = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate is not { } update || _shutdownInProgress) return;
            _shutdownInProgress = true;
            try
            {
                if (await RequestShutdownApprovalAsync() is not { } approval) return;
                var dlg = new UpdateProgressWindow(update) { Owner = this };
                if (dlg.ShowDialog() != true) return;
                // The helper is waiting for us to exit before swapping the exe.
                CommitShutdownApproval(approval);
                Application.Current.Shutdown();
            }
            finally { _shutdownInProgress = false; }
        }

        private sealed record ShutdownApproval(TemplateEditorWindow[] Editors, bool DiscardSimpleSettings);

        private async Task<ShutdownApproval?> RequestShutdownApprovalAsync(
            Func<TemplateEditorWindow, Task<bool>>? requestEditor = null, Func<bool, MessageBoxResult>? requestSettings = null)
        {
            // WPF closes modeless owned windows without their Closing event when the owner closes.
            var editors = OwnedWindows.OfType<TemplateEditorWindow>().ToArray();
            var enabledEditors = editors.Where(editor => editor.IsEnabled).ToArray();
            foreach (var editor in enabledEditors) editor.IsEnabled = false;
            bool wasEnabled = IsEnabled;
            IsEnabled = false;
            try
            {
                foreach (var editor in editors)
                {
                    bool accepted = await (requestEditor?.Invoke(editor) ?? editor.RequestCloseApprovalAsync());
                    if (!accepted) return null;
                }

                bool simpleFirst = IsSimpleMode;
                var first = requestSettings?.Invoke(simpleFirst) ?? RequestSettingsDecision(simpleFirst);
                if (!ApplySettingsDecision(simpleFirst, first)) return null;
                var second = requestSettings?.Invoke(!simpleFirst) ?? RequestSettingsDecision(!simpleFirst);
                if (!ApplySettingsDecision(!simpleFirst, second)) return null;
                return new(editors, (simpleFirst ? first : second) == MessageBoxResult.No);
            }
            finally
            {
                IsEnabled = wasEnabled;
                foreach (var editor in enabledEditors) editor.IsEnabled = true;
            }
        }

        private void CommitShutdownApproval(ShutdownApproval approval)
        {
            if (approval.DiscardSimpleSettings && _savedSimpleSettings is { } saved)
            {
                Services.GameData.AppSettings.Current.Simple = saved.Options;
                Services.GameData.AppSettings.Current.Save();
            }
            foreach (var editor in approval.Editors) editor.ApproveClose();
            _settingsCloseApproved = true;
        }

        private void BtnUpdateNotes_Click(object sender, RoutedEventArgs e)
        {
            string url = _pendingUpdate?.ReleaseUrl
                         ?? $"https://github.com/{UpdateService.Owner}/{UpdateService.Repo}/releases/latest";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(this, url, L.Get("S.Upd.RelPage"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = $"https://github.com/{UpdateService.Owner}/{UpdateService.Repo}";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(this, url, L.Get("S.GH.Tooltip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
