using System;
using System.IO;

namespace EveOPreview.Services.Logs;

public static class EveLogDirectory
{
    public static string Resolve(string configured) => !string.IsNullOrWhiteSpace(configured) ? configured
        : Detect(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string Detect(string documents, string userProfile)
    {
        string conventional = Path.Combine(userProfile, "Documents", "EVE", "logs");
        // MyDocuments uses the Windows known-folder location, including OneDrive
        // and user redirection. Older installations may still use local Documents.
        string preferred = string.IsNullOrWhiteSpace(documents) ? conventional : Path.Combine(documents, "EVE", "logs");
        static bool HasLogs(string root) => Directory.Exists(Path.Combine(root, "Gamelogs")) || Directory.Exists(Path.Combine(root, "Chatlogs"));
        return HasLogs(preferred) || !HasLogs(conventional) ? preferred : conventional;
    }
}
