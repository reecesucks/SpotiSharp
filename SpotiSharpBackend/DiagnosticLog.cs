namespace SpotiSharpBackend;

public static class DiagnosticLog
{
    private const long MAX_LOG_BYTES = 512 * 1024;

    private static readonly object Lock = new object();

    private static string _directory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

    private static bool _fileFailureReported;

    public static void SetDirectory(string directory)
    {
        if (!string.IsNullOrEmpty(directory)) _directory = directory;
    }

    public static void Write(string line)
    {
        var stamped = $"{DateTime.Now:MM-dd HH:mm:ss} {line}";
        Console.WriteLine(stamped);

        try
        {
            lock (Lock)
            {
                var path = Path.Combine(_directory, "radio-diagnostics.log");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MAX_LOG_BYTES)
                {
                    File.Move(path, Path.Combine(_directory, "radio-diagnostics.old.log"), overwrite: true);
                }
                File.AppendAllText(path, stamped + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            if (_fileFailureReported) return;
            _fileFailureReported = true;
            Console.WriteLine($"[DiagnosticLog] file logging unavailable in '{_directory}': {ex.Message}");
        }
    }
}
