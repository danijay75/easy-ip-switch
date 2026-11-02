using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace EasyIpSwitch
{
    internal static class WindowsNetworkIcon
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
        private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        public static Icon Load()
        {
            IntPtr large = IntPtr.Zero;
            IntPtr small = IntPtr.Zero;
            try
            {
                string path = Path.Combine(Environment.SystemDirectory, "netshell.dll");
                if (ExtractIconEx(path, 0, out large, out small, 1) == 0)
                {
                    return null;
                }
                IntPtr source = large != IntPtr.Zero ? large : small;
                if (source == IntPtr.Zero)
                {
                    return null;
                }
                using (Icon borrowed = Icon.FromHandle(source))
                {
                    return (Icon)borrowed.Clone();
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (large != IntPtr.Zero)
                {
                    DestroyIcon(large);
                }
                if (small != IntPtr.Zero && small != large)
                {
                    DestroyIcon(small);
                }
            }
        }
    }
}
