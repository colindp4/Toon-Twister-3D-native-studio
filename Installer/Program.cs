using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace ToonTwisterSetup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox source = new() { ReadOnly = true };
    private readonly TextBox target = new();
    private readonly Label heading = new();
    private readonly Label description = new();
    private readonly Label sourceLabel = new();
    private readonly Label targetLabel = new();
    private readonly Button chooseSource = new();
    private readonly Button chooseTarget = new();
    private readonly Button install = new();
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 18 };
    private readonly Label status = new();
    private readonly CheckBox launchAfter = new();
    private string selectedLanguage = "nl";
    private bool installing;

    private string Workspace => AppContext.BaseDirectory;

    public SetupForm()
    {
        Text = "Toon Twister 3-D Setup";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        ClientSize = new(690, 430);
        MinimumSize = new(690, 430);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(21, 31, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        heading.SetBounds(32, 27, 620, 40);
        heading.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        heading.ForeColor = Color.FromArgb(218, 244, 170);
        description.SetBounds(34, 78, 620, 50);
        description.ForeColor = Color.FromArgb(210, 222, 239);
        language.SetBounds(34, 137, 190, 32);
        language.Items.AddRange(["Nederlands", "English"]);
        language.SelectedIndex = 0;
        language.SelectedIndexChanged += (_, _) => { selectedLanguage = language.SelectedIndex == 0 ? "nl" : "en"; UpdateLanguage(); };

        sourceLabel.SetBounds(34, 187, 610, 22);
        source.SetBounds(34, 211, 500, 32);
        chooseSource.SetBounds(545, 210, 108, 34);
        chooseSource.Click += (_, _) => BrowseSource();
        targetLabel.SetBounds(34, 260, 610, 22);
        target.SetBounds(34, 284, 500, 32);
        chooseTarget.SetBounds(545, 283, 108, 34);
        chooseTarget.Click += (_, _) => BrowseTarget();

        launchAfter.SetBounds(34, 331, 620, 25);
        launchAfter.Checked = true;
        progress.SetBounds(34, 365, 620, 18);
        status.SetBounds(34, 390, 450, 28);
        status.ForeColor = Color.FromArgb(176, 196, 221);
        install.SetBounds(492, 383, 162, 42);
        install.BackColor = Color.FromArgb(0, 170, 190);
        install.ForeColor = Color.FromArgb(8, 31, 43);
        install.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        install.TextAlign = ContentAlignment.MiddleCenter;
        install.UseCompatibleTextRendering = true;
        install.FlatStyle = FlatStyle.Flat;
        install.FlatAppearance.BorderSize = 2;
        install.FlatAppearance.BorderColor = Color.FromArgb(166, 237, 226);
        install.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 211, 211);
        install.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 135, 157);
        install.Click += async (_, _) => await InstallAsync();

        foreach (var c in new[] { source, target })
        {
            c.BackColor = Color.FromArgb(36, 48, 68);
            c.ForeColor = Color.White;
            c.BorderStyle = BorderStyle.FixedSingle;
        }
        foreach (var b in new[] { chooseSource, chooseTarget })
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(43, 58, 82);
            b.ForeColor = Color.White;
        }
        Controls.AddRange([heading, description, language, sourceLabel, source, chooseSource, targetLabel, target, chooseTarget, launchAfter, progress, status, install]);
        install.BringToFront();
        UpdateLanguage();
        target.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Toon Twister 3-D Native Studio");
    }

    private void UpdateLanguage()
    {
        var nl = selectedLanguage == "nl";
        heading.Text = nl ? "TOON TWISTER 3-D  |  INSTALLATIE" : "TOON TWISTER 3-D  |  INSTALLATION";
        description.Text = nl
            ? "Installeer de Native Studio naast je originele game. De originele map blijft ongewijzigd."
            : "Install Native Studio alongside your original game. Your original folder remains unchanged.";
        sourceLabel.Text = nl ? "Originele installatie (map met ToonTwister.exe en main.dxr)" : "Original installation (folder containing ToonTwister.exe and main.dxr)";
        targetLabel.Text = nl ? "Nieuwe doelmap voor Native Studio" : "New destination folder for Native Studio";
        chooseSource.Text = nl ? "Bladeren…" : "Browse…";
        chooseTarget.Text = nl ? "Bladeren…" : "Browse…";
        install.Text = nl ? "Installeren" : "Install";
        launchAfter.Text = nl ? "Native Studio starten wanneer de installatie klaar is" : "Launch Native Studio when installation is complete";
    }

    private void BrowseSource()
    {
        using var dialog = new FolderBrowserDialog { Description = selectedLanguage == "nl" ? "Selecteer de originele Cartoon Twister 3-D map" : "Select the original Cartoon Twister 3-D folder", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) source.Text = dialog.SelectedPath;
    }

    private void BrowseTarget()
    {
        using var dialog = new FolderBrowserDialog { Description = selectedLanguage == "nl" ? "Selecteer een nieuwe installatiemap" : "Select a new installation folder", UseDescriptionForTitle = true, SelectedPath = target.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private async Task InstallAsync()
    {
        if (installing) return;
        var nl = selectedLanguage == "nl";
        if (string.IsNullOrWhiteSpace(target.Text))
        {
            MessageBox.Show(this, nl ? "Kies een doelmap." : "Choose a destination folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryValidateSource(source.Text, out var reason))
        {
            MessageBox.Show(this, reason, nl ? "Ongeldige originele installatie" : "Invalid original installation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var sourcePath = Path.GetFullPath(source.Text);
        var destination = Path.GetFullPath(target.Text.Trim());
        if (IsSameOrNested(destination, sourcePath) || IsSameOrNested(sourcePath, destination))
        {
            MessageBox.Show(this, nl ? "Kies een aparte doelmap buiten de originele installatiemap." : "Choose a separate destination outside the original installation folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            MessageBox.Show(this, nl ? "De doelmap bestaat al en is niet leeg. Kies een lege map." : "The destination already exists and is not empty. Choose an empty folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.ToonTwisterNative.exe");
        var w3dConverter = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.AssetConverter.w3d_export.exe");
        var audioConverter = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.AssetConverter.audio_export.exe");
        if (embedded is null || w3dConverter is null || audioConverter is null)
        {
            MessageBox.Show(this, nl ? "De installer is onvolledig: een editor- of conversieonderdeel ontbreekt." : "The installer is incomplete: an editor or conversion component is missing.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        installing = true;
        install.Enabled = false;
        string? staging = null;
        try
        {
            staging = Path.Combine(Path.GetTempPath(), "ToonTwisterSetup-" + Guid.NewGuid().ToString("N"));
            var converted = Path.Combine(staging, "Converted-v3");
            var audio = Path.Combine(staging, "Audio");
            var converterFolder = Path.Combine(staging, "Converter");
            Directory.CreateDirectory(converterFolder);
            var w3dPath = Path.Combine(converterFolder, "w3d_export.exe");
            var audioPath = Path.Combine(converterFolder, "audio_export.exe");
            using (var output = File.Create(w3dPath)) await w3dConverter.CopyToAsync(output);
            using (var output = File.Create(audioPath)) await audioConverter.CopyToAsync(output);
            w3dConverter.Dispose();
            audioConverter.Dispose();

            status.Text = nl ? "3D-modellen en animaties omzetten… Dit kan enkele minuten duren." : "Converting 3D models and animations… This can take several minutes.";
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 24;
            var models = await RunConverterAsync(w3dPath, sourcePath, converted);
            if (!models.Contains("converted=", StringComparison.OrdinalIgnoreCase) || !TryReadCount(models, "converted=", out var convertedCount) || convertedCount == 0)
                throw new InvalidOperationException(nl ? "De installatie bevat geen omzetbare 3D-modellen. Controleer of je de volledige originele map hebt gekozen." : "No convertible 3D models were found. Check that you selected the complete original installation folder.");
            if (!TryReadCount(models, "failed=", out var failedCount) || failedCount != 0)
                throw new InvalidOperationException(nl ? $"Het omzetten van de originele installatie is gestopt: {failedCount} 3D-bestanden konden niet worden gelezen. Er is niets in de doelmap geïnstalleerd." : $"Conversion stopped: {failedCount} original 3D files could not be read. Nothing was installed in the destination folder.");
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 50;

            status.Text = nl ? "Audio omzetten…" : "Converting audio…";
            progress.Style = ProgressBarStyle.Marquee;
            var sounds = await RunConverterAsync(audioPath, sourcePath, audio);
            if (!TryReadCount(sounds, "exported ", out var soundCount) || soundCount == 0)
                throw new InvalidOperationException(nl ? "Er is geen audio uit de originele installatie gelezen. Controleer de bestanden Music.cxt en sfx.cxt." : "No audio was read from the original installation. Check Music.cxt and sfx.cxt.");

            progress.Style = ProgressBarStyle.Blocks;
            status.Text = nl ? $"Geconverteerde media installeren ({convertedCount} 3D-bestanden, {soundCount} geluiden)…" : $"Installing converted media ({convertedCount} 3D files, {soundCount} sounds)…";
            Directory.CreateDirectory(destination);
            var appAssets = Path.Combine(destination, "Assets");
            Directory.CreateDirectory(appAssets);
            await Task.Run(() =>
            {
                CopyTree(Path.Combine(sourcePath, "w3d"), Path.Combine(appAssets, "w3d"));
                CopyTree(Path.Combine(sourcePath, "bg"), Path.Combine(appAssets, "bg"));
                CopyTree(Path.Combine(sourcePath, "xml"), Path.Combine(appAssets, "xml"));
                CopyIfExists(Path.Combine(sourcePath, "Music.cxt"), appAssets);
                CopyIfExists(Path.Combine(sourcePath, "sfx.cxt"), appAssets);
                foreach (var file in new[] { "intro.mov", "logo_nick.mov", "logo_schol.mov", "logo_wf.mov" }) CopyIfExists(Path.Combine(sourcePath, file), appAssets);
                CopyTree(converted, Path.Combine(appAssets, "Converted-v3"));
                CopyTree(audio, Path.Combine(appAssets, "Audio"));
                CopyTree(Path.Combine(Workspace, "Encoder"), Path.Combine(destination, "Encoder"));
            });
            status.Text = nl ? "Native Studio installeren…" : "Installing Native Studio…";
            using (var output = File.Create(Path.Combine(destination, "ToonTwisterNative.exe"))) await embedded.CopyToAsync(output);
            embedded.Dispose();
            // The editor reads this beside its executable at startup. Persist the
            // installer's language choice so the installed UI follows the choice.
            await File.WriteAllTextAsync(Path.Combine(destination, "studio-language.txt"), selectedLanguage);
            File.Copy(Path.Combine(Workspace, "Handleiding - Nederlands.txt"), Path.Combine(destination, "Handleiding - Nederlands.txt"), true);
            CreateShortcut(destination);
            progress.Value = 100;
            status.Text = nl ? "Installatie voltooid." : "Installation complete.";
            var answer = MessageBox.Show(this, nl ? "Native Studio is geïnstalleerd. Wil je het nu starten?" : "Native Studio is installed. Would you like to launch it now?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (launchAfter.Checked && answer == DialogResult.Yes) Process.Start(new ProcessStartInfo(Path.Combine(destination, "ToonTwisterNative.exe")) { UseShellExecute = true, WorkingDirectory = destination });
        }
        catch (Exception ex)
        {
            status.Text = nl ? "Installatie mislukt." : "Installation failed.";
            MessageBox.Show(this, ex.Message, nl ? "Installatie mislukt" : "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            embedded.Dispose();
            if (staging is not null && Directory.Exists(staging))
            {
                try { await Task.Run(() => Directory.Delete(staging, recursive: true)); } catch { }
            }
            progress.Style = ProgressBarStyle.Blocks;
            progress.MarqueeAnimationSpeed = 0;
            installing = false;
            install.Enabled = true;
        }
    }

    private static async Task<string> RunConverterAsync(string executable, string input, string output)
    {
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.ArgumentList.Add(input);
        start.ArgumentList.Add(output);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The asset converter could not be started.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await stdout;
        var errors = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Asset conversion failed (exit code {process.ExitCode}).\n{errors.Trim()}\n{result.Trim()}");
        return result;
    }

    private static bool TryReadCount(string output, string prefix, out int count)
    {
        count = 0;
        var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(x => x.Contains(prefix, StringComparison.OrdinalIgnoreCase));
        if (line is null) return false;
        var token = line[(line.LastIndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length)..]
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return token is not null && int.TryParse(token, out count);
    }

    private static bool TryValidateSource(string folder, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { reason = "Select the original game folder."; return false; }
        var required = new[] { "ToonTwister.exe", "main.dxr", "Music.cxt", "sfx.cxt" };
        var missing = required.Where(x => !File.Exists(Path.Combine(folder, x))).ToArray();
        var modelDir = Path.Combine(folder, "w3d");
        var bgDir = Path.Combine(folder, "bg");
        if (missing.Length > 0 || !Directory.Exists(modelDir) || !Directory.Exists(bgDir) || !Directory.EnumerateFiles(modelDir, "*.w3d").Any() || !Directory.EnumerateFiles(bgDir, "*.w3d").Any())
        {
            reason = "This does not look like a complete Toon Twister 3-D installation. Required: ToonTwister.exe, main.dxr, Music.cxt, sfx.cxt, and non-empty w3d and bg folders.";
            return false;
        }
        var w3d = Directory.EnumerateFiles(modelDir, "*.w3d").First();
        using var stream = File.OpenRead(w3d);
        Span<byte> header = stackalloc byte[4];
        if (stream.Read(header) != 4 || !header.SequenceEqual("IFX\0"u8))
        {
            reason = "The W3D files do not have a recognized Shockwave 3D (IFX) header.";
            return false;
        }
        return true;
    }

    private static bool IsSameOrNested(string a, string b)
    {
        var root = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyIfExists(string file, string targetFolder)
    {
        if (File.Exists(file)) File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)), true);
    }

    private void CopyTree(string sourceFolder, string destinationFolder)
    {
        if (!Directory.Exists(sourceFolder)) return;
        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories).ToArray();
        int done = 0;
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceFolder, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("_ToonTwister_OriginalGraphics", StringComparer.OrdinalIgnoreCase)) continue;
            var dest = Path.Combine(destinationFolder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
            done++;
            if (done % 80 == 0) BeginInvoke(() => progress.Value = Math.Min(95, progress.Value + 1));
        }
    }

    private void CreateShortcut(string destination)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "Toon Twister 3-D Native Studio.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = Path.Combine(destination, "ToonTwisterNative.exe");
        shortcut.WorkingDirectory = destination;
        shortcut.Description = "Toon Twister 3-D Native Studio";
        shortcut.Save();
    }
}
