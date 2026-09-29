using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class RecapVoxLauncher
{
    private const string Footer = "RVPKG001";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Payload payload = ReadPayload();
            string cacheRoot = CacheRoot();
            string cache = Path.Combine(cacheRoot, payload.Hash);
            if (IsReady(cache, payload.Hash)) { StartApp(cache); return; }
            Application.Run(new SplashForm(payload, cache));
        }
        catch (Exception ex)
        {
#if DEBUG
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "RecapVox-launcher-debug.txt"), ex.ToString());
#endif
            MessageBox.Show(ex.Message, "RecapVox", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string CacheRoot()
    {
        string overrideRoot = Environment.GetEnvironmentVariable("RECAPVOX_CACHE_ROOT");
        if (!String.IsNullOrWhiteSpace(overrideRoot)) return overrideRoot;
        string preferred = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecapVox", "R");
        try { Directory.CreateDirectory(preferred); return preferred; }
        catch (UnauthorizedAccessException)
        {
            string fallback = Path.Combine(Path.GetTempPath(), "RecapVox", "R");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private sealed class Payload
    {
        internal long Offset;
        internal long Length;
        internal string Hash;
    }

    private static Payload ReadPayload()
    {
        using (var file = File.OpenRead(Application.ExecutablePath))
        using (var reader = new BinaryReader(file))
        {
            if (file.Length < 16) throw new InvalidDataException("RecapVox package is incomplete.");
            file.Seek(-16, SeekOrigin.End);
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Footer)
                throw new InvalidDataException("RecapVox package footer is missing.");
            long length = reader.ReadInt64();
            long offset = file.Length - 16 - length;
            if (length <= 0 || offset < 0) throw new InvalidDataException("RecapVox package length is invalid.");
            file.Position = offset;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buffer = new byte[1024 * 1024];
                long remaining = length;
                while (remaining > 0)
                {
                    int read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0) throw new EndOfStreamException();
                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                    remaining -= read;
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return new Payload { Offset = offset, Length = length,
                    Hash = BitConverter.ToString(sha.Hash).Replace("-", "").Substring(0, 20) };
            }
        }
    }

    private static bool IsReady(string cache, string hash)
    {
        return File.Exists(Path.Combine(cache, ".complete")) &&
            File.ReadAllText(Path.Combine(cache, ".complete")) == hash &&
            File.Exists(Path.Combine(cache, "runtime", "dotnet.exe")) &&
            File.Exists(Path.Combine(cache, "app", "RecapVox.dll"));
    }

    private static void Extract(Payload payload, string cache)
    {
        string parent = Path.GetDirectoryName(cache);
        Directory.CreateDirectory(parent);
        using (var mutex = new Mutex(false, "Local\\RecapVox-" + payload.Hash))
        {
            try { mutex.WaitOne(); }
            catch (AbandonedMutexException) { }
            try
            {
                if (IsReady(cache, payload.Hash)) return;
                string temporary = Path.Combine(parent, ".x" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(temporary);
                try
                {
                    string zip = Path.Combine(temporary, "payload.zip");
                    using (var source = File.OpenRead(Application.ExecutablePath))
                    using (var target = File.Create(zip))
                    {
                        source.Position = payload.Offset;
                        byte[] buffer = new byte[1024 * 1024];
                        long remaining = payload.Length;
                        while (remaining > 0)
                        {
                            int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (read == 0) throw new EndOfStreamException();
                            target.Write(buffer, 0, read);
                            remaining -= read;
                        }
                    }
                    using (var archive = ZipFile.OpenRead(zip))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            int separator = entry.FullName.IndexOf('/');
                            if (separator < 0) continue;
                            string relative = entry.FullName.Substring(separator + 1).Replace('/', Path.DirectorySeparatorChar);
                            if (relative.Length == 0 || relative.EndsWith("\\")) continue;
                            string destination = Path.GetFullPath(Path.Combine(temporary, relative));
                            if (!destination.StartsWith(temporary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("RecapVox package path is invalid.");
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            entry.ExtractToFile(destination);
                        }
                    }
                    File.Delete(zip);
                    if (!File.Exists(Path.Combine(temporary, "app", "RecapVox.dll")) ||
                        !File.Exists(Path.Combine(temporary, "runtime", "dotnet.exe")))
                        throw new InvalidDataException("RecapVox package contents are invalid.");
                    if (Directory.Exists(cache)) Directory.Delete(cache, true);
                    Directory.Move(temporary, cache);
                    File.WriteAllText(Path.Combine(cache, ".complete"), payload.Hash);
                }
                finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
            }
            finally { mutex.ReleaseMutex(); }
        }
    }

    private static void StartApp(string cache)
    {
        var start = new ProcessStartInfo(Path.Combine(cache, "runtime", "dotnet.exe"),
            "\"" + Path.Combine(cache, "app", "RecapVox.dll") + "\"");
        start.WorkingDirectory = cache;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        if (Process.Start(start) == null) throw new InvalidOperationException("RecapVox could not start.");
    }

    private sealed class SplashForm : Form
    {
        private readonly Payload _payload;
        private readonly string _cache;
        private readonly Label _status;

        internal SplashForm(Payload payload, string cache)
        {
            _payload = payload;
            _cache = cache;
            Text = "RecapVox";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 132);
            BackColor = Color.FromArgb(19, 43, 72);
            ShowInTaskbar = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var mark = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(23, 28), Size = new Size(75, 75) };
            var title = new Label { Text = "RecapVox", ForeColor = Color.White,
                Font = new Font("Segoe UI", 22, FontStyle.Bold), Location = new Point(112, 18), AutoSize = true };
            var subtitle = new Label { Text = "Live translation · Audio recording · AI summaries",
                ForeColor = Color.FromArgb(215, 233, 252), Font = new Font("Segoe UI", 9),
                Location = new Point(114, 68), AutoSize = true };
            _status = new Label { Text = "Preparing first launch…", ForeColor = Color.FromArgb(127, 219, 239),
                Font = new Font("Segoe UI", 9), Location = new Point(114, 98), AutoSize = true };
            Controls.Add(mark);
            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(_status);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Extract(_payload, _cache);
                    BeginInvoke(new Action(delegate
                    {
                        try { StartApp(_cache); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "RecapVox", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                        Close();
                    }));
                }
                catch (Exception ex)
                {
#if DEBUG
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "RecapVox-launcher-debug.txt"), ex.ToString());
#endif
                    BeginInvoke(new Action(delegate
                    {
                        MessageBox.Show(this, ex.Message, "RecapVox", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Close();
                    }));
                }
            });
        }
    }
}
