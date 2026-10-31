using System;
using System.Windows.Forms;

namespace Recora.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form { Text = "Recora" });
            return 0;
        }
    }
}
