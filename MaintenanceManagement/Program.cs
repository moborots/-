using System;
using System.Windows.Forms;

namespace MaintenanceManagement;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Database.Initialize();
        Application.Run(new LoginForm());
    }
}
