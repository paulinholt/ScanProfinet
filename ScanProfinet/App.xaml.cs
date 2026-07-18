using System.Windows;
using System.Windows.Threading;
using ScanProfinet.Services;

namespace ScanProfinet;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Log global de exceções não tratadas — evita "crash silencioso" em campo.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Exceção não tratada", args.ExceptionObject as Exception);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Erro inesperado na interface", e.Exception);
        MessageBox.Show(
            "Ocorreu um erro inesperado. O detalhe foi registrado no log.\n\n" + e.Exception.Message,
            "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
