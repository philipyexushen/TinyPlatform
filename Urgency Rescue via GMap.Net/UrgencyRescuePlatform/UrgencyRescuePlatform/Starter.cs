using System;
using System.Windows;
using Microsoft.VisualBasic.ApplicationServices;

namespace UrgencyRescuePlatform
{
	public class Startup
	{
		[STAThread]
		public static void Main(string[] args)
		{
			SingleInstanceApplicationWrapper wrapper = new SingleInstanceApplicationWrapper();
			wrapper.Run(args);
		}
	}

	public class SingleInstanceApplicationWrapper :WindowsFormsApplicationBase
	{
		public SingleInstanceApplicationWrapper()
		{
			// 允许单实例
			this.IsSingleInstance = true;
		}

		//创建WPF程序
		private WpfApp app;
		protected override bool OnStartup(
			Microsoft.VisualBasic.ApplicationServices.StartupEventArgs e)
		{
			app = new WpfApp();
			app.Run();

			return false;
		}

		protected override void OnStartupNextInstance(StartupNextInstanceEventArgs e)
		{
			app.Windows[0].Visibility = Visibility.Visible;
			app.Windows[0].Activate();
		}
	}

	public class WpfApp : Application
	{
		protected override void OnStartup(System.Windows.StartupEventArgs e)
		{
			base.OnStartup(e);

			MainWindow window = new MainWindow();
			this.MainWindow = window;
			window.Show();
		}
	}
}
