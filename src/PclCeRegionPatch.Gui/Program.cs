using System;
using System.IO;
using System.Windows.Forms;

namespace PclCeRegionPatch.Gui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? initialInput = null;
        if (args.Length > 0 && File.Exists(args[0]))
            initialInput = Path.GetFullPath(args[0]);

        Application.Run(new MainForm(initialInput));
    }
}
