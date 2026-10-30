using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("CurseForge Instance Cleaner")]
[assembly: AssemblyProduct("CurseForge Instance Cleaner")]
[assembly: AssemblyDescription("Removes unwanted game instances from the CurseForge app's list.")]
[assembly: AssemblyCopyright("Copyright (c) 2026 goldenpipes, MIT License")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace CurseForgeInstanceCleaner
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // Optional argument: another instance file to open instead of CurseForge's own.
        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : InstanceFile.DefaultPath));
        }
    }
}
