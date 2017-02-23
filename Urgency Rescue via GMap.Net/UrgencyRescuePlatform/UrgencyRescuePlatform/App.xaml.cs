using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic.ApplicationServices;

namespace UrgencyRescuePlatform
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
		}
	}

	public class SignleInstanceApplicationWrapper :
		Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase
	{
		public SignleInstanceApplicationWrapper()
		{
			//允许单实例
			this.IsSingleInstance = true;
		}

		private App app;
		protected override bool OnStartup(Microsoft.VisualBasic.ApplicationServices.StartupEventArgs eventArgs)
		{
			app = new App();
			app.Run();
			return false;
		}

		protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
		{
			MessageBox.Show("fuck");
			app.Windows[0].Visibility = Visibility.Visible;
		}
	}
}
