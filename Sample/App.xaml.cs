using System.Windows;

namespace Sample
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            D2dControl.D2dControl.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            D2dControl.D2dControl.Destroy();

        }
    }
}