// Finds, closes and restarts the CurseForge app (an Electron app; closing its
// window usually only hides it in the tray, so it is ended if it stays open).
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CurseForgeInstanceCleaner
{
    public static class CurseForgeApp
    {
        const string ProcessName = "CurseForge";

        public static bool IsRunning()
        {
            Process[] procs = Process.GetProcessesByName(ProcessName);
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }

        // The running app's exe, else the usual install location (null if neither).
        public static string ExePath()
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try { return p.MainModule.FileName; }
                catch { }
                finally { p.Dispose(); }
            }
            string usual = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\CurseForge Windows\CurseForge.exe");
            return File.Exists(usual) ? usual : null;
        }

        // Asks it to close, then ends it if it is still running. True once it is gone.
        public static bool Close()
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try { if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); }
                catch { }
                finally { p.Dispose(); }
            }
            if (WaitForExit(3000)) return true;

            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try { p.Kill(); }
                catch { }
                finally { p.Dispose(); }
            }
            return WaitForExit(10000);
        }

        public static void Start(string exe)
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
        }

        static bool WaitForExit(int ms)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < ms)
            {
                if (!IsRunning()) return true;
                Thread.Sleep(200);
            }
            return !IsRunning();
        }
    }
}
