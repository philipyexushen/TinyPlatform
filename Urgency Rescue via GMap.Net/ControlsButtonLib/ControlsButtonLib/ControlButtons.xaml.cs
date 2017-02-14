using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;

namespace ControlsButtonLib
{
	/// <summary>
	/// ControlButtons.xaml 的交互逻辑
	/// </summary>
	public partial class ControlButtons : UserControl
	{
		public static readonly RoutedEvent OnCloseEvent
			= EventManager.RegisterRoutedEvent("OnClose", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ControlButtons));
		public static readonly RoutedEvent OnMinimizeEvent
			= EventManager.RegisterRoutedEvent("OnMinimize", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ControlButtons));
		public static readonly RoutedEvent OnMaximizeOrNormalEvent
			= EventManager.RegisterRoutedEvent("OnMaximizeOrNormal", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ControlButtons));

		public event RoutedEventHandler OnClose
		{
			add { AddHandler(OnCloseEvent, value); }
			remove { RemoveHandler(OnCloseEvent, value); }
		}

		public event RoutedEventHandler OnMinimize
		{
			add { AddHandler(OnMinimizeEvent, value); }
			remove { RemoveHandler(OnMinimizeEvent, value); }
		}

		public event RoutedEventHandler OnMaximizeOrNormal
		{
			add { AddHandler(OnMaximizeOrNormalEvent, value); }
			remove { RemoveHandler(OnMaximizeOrNormalEvent, value); }
		}

		public ControlButtons()
		{
			InitializeComponent();
		}

		private void minimizeButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;

			RoutedEventArgs args = new RoutedEventArgs();
			args.RoutedEvent = OnMinimizeEvent;

			RaiseEvent(args);
		}

		private void maximizeOrNormalButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;

			RoutedEventArgs args = new RoutedEventArgs();
			args.RoutedEvent = OnMaximizeOrNormalEvent;

			RaiseEvent(args);
		}

		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;

			RoutedEventArgs args = new RoutedEventArgs();
			args.RoutedEvent = OnCloseEvent;

			RaiseEvent(args);
		}
	}
}
