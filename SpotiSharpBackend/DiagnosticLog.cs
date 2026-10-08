using System.Collections.Concurrent;
using System.Text;

namespace SpotiSharpBackend;

public static class DiagnosticLog
{
    // Two generations of this are kept (current + .old), so a bug report covers roughly the
    // last twice this much: many hours of radio at the per-sample logging rate.
    private const long MAX_LOG_BYTES = 2 * 1024 * 1024;

    private const string LOG_FILE = "radio-diagnostics.log";
    private const string OLD_LOG_FILE = "radio-diagnostics.old.log";

    private static string _directory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

    private static bool _fileFailureReported;

    private static readonly BlockingCollection<string> PendingLines = new BlockingCollection<string>();

    // Held by the writer while it rotates/appends, and by ReadAll, so a read never sees a
    // half-rotated pair of files.
    private static readonly object FileLock = new object();

    private static long _linesQueued;
    private static long _linesWritten;

    static DiagnosticLog()
    {
        var writer = new Thread(WriteLoop) { IsBackground = true, Name = "DiagnosticLogWriter" };
        writer.Start();
    }

    public static void SetDirectory(string directory)
    {
        if (!string.IsNullOrEmpty(directory)) _directory = directory;
    }

    public static void Write(string line)
    {
        var stamped = $"{DateTime.Now:MM-dd HH:mm:ss.fff} {line}";
        Console.WriteLine(stamped);

        Interlocked.Increment(ref _linesQueued);
        PendingLines.Add(stamped);
    }

    /// <summary>Waits (up to <paramref name="timeout"/>) for every line written so far to reach the file.</summary>
    public static bool Flush(TimeSpan timeout)
    {
        long target = Interlocked.Read(ref _linesQueued);
        var deadline = DateTime.UtcNow + timeout;
        while (Interlocked.Read(ref _linesWritten) < target)
        {
            if (DateTime.UtcNow >= deadline) return false;
            Thread.Sleep(10);
        }
        return true;
    }

    /// <summary>The whole rolling log, oldest generation first.</summary>
    public static string ReadAll()
    {
        Flush(TimeSpan.FromSeconds(2));

        var text = new StringBuilder();
        lock (FileLock)
        {
            foreach (var name in new[] { OLD_LOG_FILE, LOG_FILE })
            {
                var path = Path.Combine(_directory, name);
                if (!File.Exists(path)) continue;

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text.Append(reader.ReadToEnd());
            }
        }
        return text.ToString();
    }

    private static void WriteLoop()
    {
        foreach (var stamped in PendingLines.GetConsumingEnumerable())
        {
            try
            {
                lock (FileLock)
                {
                    var path = Path.Combine(_directory, LOG_FILE);
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MAX_LOG_BYTES)
                    {
                        File.Move(path, Path.Combine(_directory, OLD_LOG_FILE), overwrite: true);
                    }
                    File.AppendAllText(path, stamped + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                if (_fileFailureReported) continue;
                _fileFailureReported = true;
                Console.WriteLine($"[DiagnosticLog] file logging unavailable in '{_directory}': {ex.Message}");
            }
            finally
            {
                Interlocked.Increment(ref _linesWritten);
            }
        }
    }
}
