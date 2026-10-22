using System;
using System.Windows;
using Velopack;
using Velopack.Exceptions;

namespace NovaNote
{
    public static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            App.StartupArgs = args;

            try
            {
                VelopackApp.Build()
                    .SetAppUserModelId(FileAssociationHelper.ProgId)
                    .SetArgs(args)
                    .WithFirstRun(version => FileAssociationHelper.RegisterAssociations())
                    .WithAfterUpdate(version => FileAssociationHelper.RegisterAssociations())
                    .WithBeforeUninstall(version => FileAssociationHelper.UnregisterAssociations())
                    .Run();
            }
            catch (NotInstalledException)
            {
            }

            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
