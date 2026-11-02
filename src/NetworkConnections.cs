using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace EasyIpSwitch
{
    internal sealed class NetworkConnectionsShortcutResult
    {
        public string Path = "";
        public bool Created;
    }

    internal sealed class NetworkConnectionsResult
    {
        public NetworkConnectionsShortcutResult Shortcut;
        public bool Opened;
        public string ShortcutError = "";
        public string OpenError = "";

        public bool Success
        {
            get { return Shortcut != null && Opened; }
        }

        public string Message
        {
            get
            {
                if (Success)
                {
                    string name = Path.GetFileNameWithoutExtension(Shortcut.Path);
                    if (string.IsNullOrEmpty(name))
                    {
                        name = L.T("Connexions réseau");
                    }
                    return Shortcut.Created
                        ? L.F("Connexions réseau ouvertes ; raccourci « {0} » créé sur le Bureau.", name)
                        : L.F("Connexions réseau ouvertes ; raccourci « {0} » déjà présent sur le Bureau.", name);
                }
                if (Opened)
                {
                    return L.F("Connexions réseau ouvertes, mais raccourci impossible à créer : {0}", ShortcutError);
                }
                if (Shortcut != null)
                {
                    return L.F("Raccourci disponible sur le Bureau, mais ouverture impossible : {0}", OpenError);
                }
                return L.F("Impossible de créer le raccourci : {0} Ouverture impossible : {1}", ShortcutError, OpenError);
            }
        }
    }

    internal interface INetworkConnectionsBackend
    {
        NetworkConnectionsShortcutResult EnsureShortcut();
        void OpenConnections();
    }

    internal static class NetworkConnections
    {
        public static NetworkConnectionsResult Execute()
        {
            return Execute(new WindowsNetworkConnectionsBackend());
        }

        internal static NetworkConnectionsResult Execute(INetworkConnectionsBackend backend)
        {
            NetworkConnectionsResult result = new NetworkConnectionsResult();
            try
            {
                result.Shortcut = backend.EnsureShortcut();
                if (result.Shortcut == null)
                {
                    result.ShortcutError = L.T("Windows n'a pas confirmé la création du raccourci.");
                }
            }
            catch (Exception ex)
            {
                result.ShortcutError = Describe(ex);
            }

            // L'ouverture reste possible même si le Bureau est inaccessible.
            try
            {
                backend.OpenConnections();
                result.Opened = true;
            }
            catch (Exception ex)
            {
                result.OpenError = Describe(ex);
            }
            return result;
        }

        private static string Describe(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }
            string message = (current.Message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (message.Length == 0)
            {
                return L.T("Erreur Windows inconnue.");
            }
            return message.Length > 180 ? message.Substring(0, 180) + "…" : message;
        }
    }

    internal sealed class WindowsNetworkConnectionsBackend : INetworkConnectionsBackend
    {
        public NetworkConnectionsShortcutResult EnsureShortcut()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return NetworkConnectionsShortcut.Ensure(desktop);
        }

        public void OpenConnections()
        {
            string control = Path.Combine(Environment.SystemDirectory, "control.exe");
            ProcessStartInfo start = new ProcessStartInfo(control, "ncpa.cpl");
            start.UseShellExecute = true;
            Process.Start(start);
        }
    }

    internal static class NetworkConnectionsShortcut
    {
        internal static NetworkConnectionsShortcutResult Ensure(string desktop)
        {
            return EnsureWithNotifier(desktop, NotifyDesktop);
        }

        internal static NetworkConnectionsShortcutResult EnsureWithNotifier(string desktop, Action<string, bool> notify)
        {
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            {
                throw new DirectoryNotFoundException(L.T("Le dossier Bureau est introuvable."));
            }

            string control = Path.Combine(Environment.SystemDirectory, "control.exe");
            string temp = null;
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    string candidate = Path.Combine(desktop, CandidateName(i));
                    if (File.Exists(candidate))
                    {
                        if (Matches(candidate, control))
                        {
                            return Result(candidate, false, notify);
                        }
                        continue;
                    }
                    if (Directory.Exists(candidate))
                    {
                        continue;
                    }

                    if (temp == null)
                    {
                        string reserved = Path.Combine(desktop, ".EasyIpSwitch-" + Guid.NewGuid().ToString("N") + ".lnk");
                        // Ne jamais supprimer un fichier que CreateNew n'a pas créé.
                        using (FileStream reservation = new FileStream(reserved, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                        }
                        temp = reserved;
                        WriteShortcut(temp, control);
                    }

                    try
                    {
                        // File.Move échoue si un fichier apparaît entre-temps :
                        // il ne remplace jamais le raccourci d'un utilisateur.
                        File.Move(temp, candidate);
                        temp = null;
                        return Result(candidate, true, notify);
                    }
                    catch (IOException)
                    {
                        if (File.Exists(candidate))
                        {
                            if (Matches(candidate, control))
                            {
                                return Result(candidate, false, notify);
                            }
                            continue;
                        }
                        if (Directory.Exists(candidate))
                        {
                            continue;
                        }
                        throw;
                    }
                }
                throw new IOException(L.T("Aucun nom libre pour le raccourci sur le Bureau."));
            }
            finally
            {
                try
                {
                    if (temp != null && File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch
                {
                }
            }
        }

        private static string CandidateName(int index)
        {
            string baseName = L.T("Connexions réseau");
            if (index == 0)
            {
                return baseName + ".lnk";
            }
            if (index == 1)
            {
                return baseName + " - Easy IP-Switch.lnk";
            }
            return baseName + " - Easy IP-Switch (" + index + ").lnk";
        }

        private static NetworkConnectionsShortcutResult Result(string path, bool created, Action<string, bool> notify)
        {
            NetworkConnectionsShortcutResult result = new NetworkConnectionsShortcutResult();
            result.Path = path;
            result.Created = created;
            if (notify != null)
            {
                try
                {
                    notify(path, created);
                }
                catch
                {
                    // Le raccourci reste valide même si Explorer ne répond pas.
                }
            }
            return result;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern void SHChangeNotify(uint eventId, uint flags,
            [MarshalAs(UnmanagedType.LPWStr)] string item, IntPtr secondItem);

        private static void NotifyDesktop(string path, bool created)
        {
            const uint pathWide = 0x0005;
            const uint flushNoWait = 0x2000;
            const uint create = 0x00000002;
            const uint updateItem = 0x00002000;
            const uint updateDirectory = 0x00001000;
            SHChangeNotify(created ? create : updateItem, pathWide | flushNoWait, path, IntPtr.Zero);
            string desktop = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(desktop))
            {
                SHChangeNotify(updateDirectory, pathWide | flushNoWait, desktop, IntPtr.Zero);
            }
        }

        private static void WriteShortcut(string path, string control)
        {
            object shell = new ShellLinkCom();
            try
            {
                IShellLinkW link = (IShellLinkW)shell;
                link.SetPath(control);
                link.SetArguments("ncpa.cpl");
                link.SetWorkingDirectory(Environment.SystemDirectory);
                link.SetDescription(L.T("Ouvrir les connexions réseau Windows"));
                link.SetIconLocation(Path.Combine(Environment.SystemDirectory, "netshell.dll"), 0);
                ((IPersistFile)shell).Save(path, true);
            }
            finally
            {
                if (Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }

        internal static bool Matches(string path, string control)
        {
            object shell = null;
            try
            {
                shell = new ShellLinkCom();
                ((IPersistFile)shell).Load(path, 0);
                IShellLinkW link = (IShellLinkW)shell;
                StringBuilder target = new StringBuilder(32768);
                StringBuilder arguments = new StringBuilder(1024);
                link.GetPath(target, target.Capacity, IntPtr.Zero, 4);
                link.GetArguments(arguments, arguments.Capacity);
                return string.Equals(Path.GetFullPath(target.ToString()), Path.GetFullPath(control), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(arguments.ToString().Trim(), "ncpa.cpl", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (shell != null && Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class ShellLinkCom
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr findData, uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
