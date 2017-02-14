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

namespace MarkersLib
{
	/// <summary>
	/// MarkerContextMenu.xaml 的交互逻辑
	/// </summary>
	public partial class MarkerContextMenu : UserControl
	{
		public static readonly RoutedEvent ClearMarkerEvent
			= EventManager.RegisterRoutedEvent("ClearMarker", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MarkerContextMenu));
	
		public event RoutedEventHandler ClearMarker
		{
			add { AddHandler(ClearMarkerEvent, value); }
			remove { RemoveHandler(ClearMarkerEvent, value); }
		}

		public MarkerContextMenu()
		{
			InitializeComponent();
		}

		private void clearMarker(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			RoutedEventArgs args = new RoutedEventArgs();
			args.RoutedEvent = ClearMarkerEvent;
			RaiseEvent(args);
		}
	}
}
