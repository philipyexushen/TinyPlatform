using System.Windows;

namespace UrgencyRescuePlatform
{
	/// <summary>
	/// TinyMessageBox.xaml 的交互逻辑
	/// </summary>
	public partial class TinyMessageBox : Window
	{
		public TinyMessageBox()
		{
			InitializeComponent();
			this.ResizeMode = ResizeMode.NoResize;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
