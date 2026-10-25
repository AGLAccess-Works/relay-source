using System;
using System.IO;

namespace ControllerMouse.App;

internal static class Log
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerMouse");
    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string file = Path.Combine(Folder, "app.log");
            if (File.Exists(file) && new FileInfo(file).Length > 1_000_000) File.Move(file, file + ".previous", true);
            File.AppendAllText(file, $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
