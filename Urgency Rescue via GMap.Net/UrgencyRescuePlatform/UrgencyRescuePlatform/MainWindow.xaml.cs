using System;
using System.Windows;
using System.Windows.Input;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using TcpClientHelper;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Windows.Shapes;
using System.Windows.Media;
using MarkersLib;
using IpHelperSpace;
using DebugHelperNameSpace;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Controls;
using System.ComponentModel;
using Microsoft.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Collections;
using System.Windows.Media.Imaging;
using System.Media;

namespace UrgencyRescuePlatform
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public EventHandler<ClientMessageEventArgs> WriteDataEvent;

		private AsyncTcpClientHelper _asyncTcpClientHelper;
		private Thread _backgourndThread;
		private bool _isNormal = true;
		private bool _isWidden = false;
		private bool _isSearchingRoute = false;
		private bool _isGetDirectDistanceButtonClicked = false;
		private Tuple<GMapMarker, GMapMarker> _startPointWithShape;
		private Tuple<GMapMarker, GMapMarker> _endPointWithShape;
		public LocatePosition _locationSetting = new LocatePosition();

		public string EServerHostName { get; private set; }
		public int EServerPort { get; private set; }
		public string TeminalName { get; private set; }
		public string TeminalIp { get; private set; }
		public Size MarkerSize { get; private set; } = new Size(60, 60);

		protected override void OnClosed(EventArgs e)
		{
			if (_asyncTcpClientHelper != null)
				_asyncTcpClientHelper.Close();
			base.OnClosed(e);
		}

		public MainWindow()
		{
			InitializeComponent();
			InivilizeMap();

			double workHeight = SystemParameters.WorkArea.Height;
			double workWidth  = SystemParameters.WorkArea.Width;

			this.Top  = (workHeight - this.Height) / 2;
			this.Left = (workWidth - this.Width) / 2;

			this.ResizeMode = ResizeMode.CanResizeWithGrip;

			this._comboBox_mapSources.SelectedIndex = 0;
			this._comboBox_mapAccessMode.SelectedIndex = 1;

		}

		private void InivilizeMap()
		{
			GMapManagerLoader.Instance.Load(":/MapCache/DataExp.gmdb");
			mapMainWindow.Zoom = 12;

			mapMainWindow.ShowCenter = true;
			mapMainWindow.DragButton = MouseButton.Middle; //中键拖拽地图
			mapMainWindow.MouseMove += _mapMainWindow_Move;

			//地图中心广州
			mapMainWindow.Position = new PointLatLng(23.137714, 113.355401);

			_group_mapfunctions.DataContext = _locationSetting;
			_locationSetting.Lat = $"{23.137714}";
			_locationSetting.Lng = $"{113.355401}";
		}

		private void _mapMainWindow_Move(object sender, MouseEventArgs e)
		{
			mapMainWindow.Cursor = Cursors.Hand;
		}

		private void InivilizeAsyncTcpClient(string hostName, int port)
		{
			_asyncTcpClientHelper = new AsyncTcpClientHelper();
			AsyncTcpClientHelper.HostArg arg = new AsyncTcpClientHelper.HostArg(TeminalName, hostName, port, null);

			ParameterizedThreadStart workerThreadMethod
				= new ParameterizedThreadStart(_asyncTcpClientHelper.StartTcpClientWithHostName);
			_backgourndThread = new Thread(workerThreadMethod);

			_asyncTcpClientHelper.DataOccuredEvent              += ClientLogOutputToMsgBox;
			_asyncTcpClientHelper.ConnectionToServerFailedEvent += HandleClientConnectionFailed;
			_asyncTcpClientHelper.NewUserConnectedEvent         += on_NewUserComming;
			_asyncTcpClientHelper.NewCoordinateOccuredEvent     += on_CoordinateOccured;

			WriteDataEvent += _asyncTcpClientHelper.WriteData;
			_backgourndThread.IsBackground = true;

			_backgourndThread.Start(arg);
		}

		void mapMainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Point clickPoint = e.GetPosition(mapMainWindow);
			PointLatLng point = mapMainWindow.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

			markerManualAdding(point);
		}

		private GMapRouteMarker createRouteMarker(PointLatLng point, double iconWidth, double iconHeight, string name)
		{
			GMapRouteMarker marker = new GMapRouteMarker(point, iconWidth, iconHeight, name);

			marker.SetStartPointEvent += Marker_SetStartPointEvent;
			marker.CancelSetStartPointEvent += Marker_CancelSetStartedEvent;
			marker.SetEndPointEvent += Marker_SetEndPointEvent;
			marker.CancelSetEndPointEvent += Marker_CancelSetEndPointEvent;

			return marker;
		}

		private void Marker_CancelSetEndPointEvent(object sender, EventArgs e)
		{
			if (_endPointWithShape != null)
			{
				mapMainWindow.Markers.Remove(_endPointWithShape.Item2);
				_endPointWithShape = null;
			}
		}

		private void Marker_SetEndPointEvent(object sender, GMapRouteMarker.SetEndPointEventArgs e)
		{
			if (_endPointWithShape != null)
			{
				mapMainWindow.Markers.Remove(_endPointWithShape.Item2);
				(_endPointWithShape.Item1 as GMapRouteMarker).IsEndPoint = false;
			}

			_endPointWithShape = new Tuple<GMapMarker, GMapMarker>(sender as GMapMarker, e.Tip);
			mapMainWindow.Markers.Add(_endPointWithShape.Item2);
		}

		private void Marker_CancelSetStartedEvent(object sender, EventArgs e)
		{
			if (_startPointWithShape != null)
			{
				mapMainWindow.Markers.Remove(_startPointWithShape.Item2);
				_startPointWithShape = null;
			}		
		}

		private void Marker_SetStartPointEvent(object sender, GMapRouteMarker.SetStartPointEventArgs e)
		{
			if (_startPointWithShape != null)
			{
				mapMainWindow.Markers.Remove(_startPointWithShape.Item2);
				(_startPointWithShape.Item1 as GMapRouteMarker).IsStartPoint = false;
			}

			_startPointWithShape = new Tuple<GMapMarker, GMapMarker>(sender as GMapMarker, e.Tip);
			mapMainWindow.Markers.Add(_startPointWithShape.Item2);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void on_CoordinateOccured(object sender, TransformedCoordinateArgs e)
		{
			try
			{
				if (!UserManagerHelper<int>.GetManager.IsContainKey(e.SourceFeatureCode))
				{
					DebugHelpers.CustomMessageShow($"Key {e.SourceFeatureCode} is not contained");
					return;
				}
				Application.Current.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						PointLatLng point = e.Point;

						GMapRouteMarker marker = createRouteMarker(point, MarkerSize.Width, MarkerSize.Height, $"{e.SourceFeatureCode}");
						GMapPostionSearchHelper.SearchPointInformation(point, mapMainWindow.MapProvider as GeocodingProvider, marker);
						mapMainWindow.Markers.Add(marker);

						GMapRouteMarker lastMarker = UserManagerHelper<int>.GetManager.GetLastMarker(e.SourceFeatureCode);

						if (lastMarker != null)
						{
							PointLatLng lastPoint = lastMarker.Position;

							double distance = GetDirectDistanceHelper.GetDistance(point.Lat, point.Lng, lastPoint.Lat, lastPoint.Lng);

							double rate = distance / (marker.Time - lastMarker.Time).TotalSeconds;
							marker.Rate = rate * 3600;

							List<PointLatLng> points = new List<PointLatLng> { point, lastPoint };

							Path lineShape = createLineTilePath(Brushes.Blue);

							GMapRoute line = new GMapRoute(points);
							line.Shape = lineShape;						
							mapMainWindow.Markers.Add(line);
							line.RegenerateShape(mapMainWindow);
						}
						UserManagerHelper<int>.GetManager.AddMarkerToItem(e.SourceFeatureCode, marker);
					}
				));
			}
			catch(Exception ex)
			{
				DebugHelpers.CustomMessageShow(ex.Message);
			}
		}

		private void sendMsgBtn_Click(object sender, RoutedEventArgs e)
		{
			string msg = inputMsgBox.Text;
			ClientMessageEventArgs arg = new ClientMessageEventArgs(msg);

			msgBox.AppendText(_asyncTcpClientHelper.Name + ": " + msg);
			WriteDataEvent?.BeginInvoke(this, arg, null, this);
			inputMsgBox.Clear();
			msgBox.ScrollToEnd();
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void ClientLogOutputToMsgBox(object sender, ClientMessageEventArgs e)
		{
			msgBox.Dispatcher.BeginInvoke(
				new Action(() =>
				{
					msgBox.AppendText(e.DataMsg + "\n");
					msgBox.ScrollToEnd();
				}));
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void on_NewUserComming(object sender, TcpClientCore.NewUserDetailsArgs args)
		{
			//TODO:数据绑定一个东西
			UserManagerHelper<int>.GetManager.AddItem(args.SourceFeatureCode, args.Ip, args.UserName);
		}

		private void HandleClientConnectionFailed(object sender, ClientMessageEventArgs e)
		{
			msgBox.Dispatcher.BeginInvoke(new Action(() =>{ msgBox.AppendText(e.DataMsg); msgBox.ScrollToEnd();}));

			_button_login.IsEnabled = true;
			_button_logout.IsEnabled = false;
		}

		private Path createLineTilePath(Brush color)
		{
			Path lineShape = new Path();
			
			lineShape.Stroke = color;
			lineShape.StrokeThickness = 6;
			lineShape.Opacity = 0.7;

			return lineShape;
		}

		#region Window
		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void minimizeButton_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Minimized;
		}

		private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if(e.ClickCount == 1)
			{
				this.DragMove();
			}

			if (e.ClickCount == 2)
			{
				maximizeOrNormalButton_Click(sender, e);
			}
		}

		private void mapMainWindow_MouseMove(object sender, MouseEventArgs e)
		{
			Point clickPoint = e.GetPosition(mapMainWindow);
			PointLatLng point = mapMainWindow.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

			textBlock_Lat.Text = point.Lat.ToString();
			textBlock_Lng.Text = point.Lng.ToString();
		}

		private void maximizeOrNormalButton_Click(object sender, RoutedEventArgs e)
		{
			if (_isNormal)
			{
				_isNormal = false;
				this.WindowState = WindowState.Maximized;
			}
			else
			{
				_isNormal = true;
				this.WindowState = WindowState.Normal;
			}
		}

		private void window_initateWidden(object sender, MouseButtonEventArgs e)
		{
			_isWidden = true;
		}

		private void window_endWiden(object sender, MouseButtonEventArgs e)
		{
			_isWidden = false;
			Rectangle rect = (Rectangle)sender;
			rect.ReleaseMouseCapture();
		}

		private void window_weBorder_widden(object sender, MouseEventArgs e)
		{
			Rectangle rect = (Rectangle)sender;

			if (_isWidden)
			{
				if (rect == rightBorder)
				{
					rect.CaptureMouse();
					double newWidth = e.GetPosition(this).X + rightBorder.Width;
					if (newWidth > 0)
						this.Width = newWidth;
				}
				else if (rect == leftBorder)
				{
					rect.CaptureMouse();
					double length = e.GetPosition(this).X;
					double newWidth = this.Width - length;
					if (newWidth > 0)
					{
						this.Width = newWidth;
						this.Left += length;
					}
				}
			}
		}

		private void window_nsBorder_widden(object sender, MouseEventArgs e)
		{
			Rectangle rect = (Rectangle)sender;
			if (_isWidden)
			{
				if (rect == buttomBorder)
				{
					rect.CaptureMouse();
					double newHeight = e.GetPosition(this).Y + buttomBorder.Height;
					if (newHeight > 0)
						this.Height = newHeight;
				}
				else if (rect == topBorder)
				{
					rect.CaptureMouse();
					double length = e.GetPosition(this).Y;
					double newHeight = this.Height - length;
					if (newHeight > 0)
					{
						this.Height = newHeight;
						this.Top += length;
					}
				}
			}
		}
		#endregion

		private void loginButton_Click(object sender, RoutedEventArgs e)
		{
			LoginWindow w = new LoginWindow();

			w.Owner = this;

			if (this.WindowState ==WindowState.Normal)
			{
				w.Left = this.Left + this.Width / 2 - w.Width / 2;
				w.Top = this.Top + this.Height / 2 - w.Height / 2;
			}
			else if(this.WindowState == WindowState.Maximized)
			{
				double screenHeight = SystemParameters.FullPrimaryScreenHeight;
				double screenWidth = SystemParameters.FullPrimaryScreenWidth;

				w.Left = (screenWidth - w.Width) / 2;
				w.Top = (screenHeight - w.Height) / 2;
			}

			if (w.ShowDialog().Value)
			{
				EServerHostName = w.HostName;
				EServerPort = w.Port;
				TeminalName = w.UserName;
				TeminalIp = w.Ip;

				textBlock_IPAddress.Text = w.Ip;
				textBlock_UserName.Text = w.UserName;

				_button_login.IsEnabled = false;
				_button_logout.IsEnabled = true;

				InivilizeAsyncTcpClient(EServerHostName, EServerPort);
			}
		}

		private void _comboBox_mapSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox box = sender as ComboBox;
			MapSource s;
			if (box.SelectedIndex == -1)
			{
				box.SelectedIndex = 0;
				s = _comboBox_mapSources.Items[0] as MapSource;
			}
			else
			{
				object[] sources = (object[])e.AddedItems;
				s = sources[0] as MapSource;
			}

			if (s != null)
			{
				
				mapMainWindow.MapProvider = s.Provider;
				GMapProvider.Language = LanguageType.ChineseSimplified;
			}
				
		}

		private void _button_informPanel_Click(object sender, RoutedEventArgs e)
		{
			RibbonToggleButton button = (RibbonToggleButton)sender;

			if (button.IsChecked == true)
			{
				_grid_infromPanel.Visibility = Visibility.Visible;
			}
			else if (button.IsChecked == false)
			{
				_grid_infromPanel.Visibility = Visibility.Collapsed;
			}
			
		}

		private void _comboBox_mapAccessMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox box = sender as ComboBox;
			MapLoadingMode s;
			if (box.SelectedIndex == -1)
			{
				box.SelectedIndex = 0;
				s = _comboBox_mapAccessMode.Items[0] as MapLoadingMode;
			}
			else
			{
				object[] sources = (object[])e.AddedItems;
				s = sources[0] as MapLoadingMode;
			}

			if (s != null)
			{
				mapMainWindow.Manager.Mode = s.Mode;
			}
				
		}

		private void addRouteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = IsStartPointandendPointExist() && !_isSearchingRoute;
		}

		private void addRouteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			_button_addRoute_Click(this, null);
		}

		private void _button_addRoute_Click(object sender, RoutedEventArgs e)
		{
			_isSearchingRoute = true;
			GMapRouteSearchHelper.SearchRouteInformation
				(_startPointWithShape.Item1, 
				_endPointWithShape.Item1,
				GMapProviders.BingHybridMap as GeocodingProvider,
				(int)mapMainWindow.Zoom,
				handleRouteSearchCallback);
		}

		private void handleRouteSearchCallback(MapRoute route)
		{
			string result = string.Empty;
			if (route != null)
			{
				double pathLength = route.Distance;

				List<PointLatLng> points = route.Points;
				GMapRoute line = new GMapRoute(points);
				mapMainWindow.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						Path lineShape = createLineTilePath(Brushes.Red);
						line.Shape = lineShape;
						line.ZIndex = -1;

						mapMainWindow.Markers.Add(line);
						line.RegenerateShape(mapMainWindow);
					}));

				result = $"已找到路径,长度为{pathLength}km";
			}
			else
				result = "查找路径失败";

			mapMainWindow.Dispatcher.BeginInvoke(
			new Action(() => {
				MessageBox.Show(this, result,this.Title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
				_isSearchingRoute = false;
			}));
		}

		private void getDirectDistance_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = IsStartPointandendPointExist();
		}

		private bool IsStartPointandendPointExist()
		{
			return _startPointWithShape != null && _endPointWithShape != null;
		}

		private void _button_getDirectDistance_Click(object sender, RoutedEventArgs e)
		{
			_isGetDirectDistanceButtonClicked = true;
			PointLatLng point1 = _startPointWithShape.Item1.Position;
			PointLatLng point2 = _endPointWithShape.Item1.Position;

			double directDistanceLength
				= GetDirectDistanceHelper.GetDistance(point1.Lat, point1.Lng, point2.Lat, point2.Lng);

			MessageBox.Show(this,$"起点与终点间直线距离为：{directDistanceLength}km", 
				this.Title, MessageBoxButton.OK,MessageBoxImage.Asterisk);
		}

		private void getDirectDistance_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (!_isGetDirectDistanceButtonClicked)
				_button_getDirectDistance_Click(this, null);
		}

		private void _button_zoomOut_Click(object sender, RoutedEventArgs e)
		{
			mapMainWindow.Zoom++;
		}

		private void _button_zoomIn_Click(object sender, RoutedEventArgs e)
		{
			mapMainWindow.Zoom--;
		}

		private void _button_reloadMap_Click(object sender, RoutedEventArgs e)
		{
			mapMainWindow.ReloadMap();
		}

		private void ReloadMap_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			mapMainWindow.ReloadMap();
		}

		private void LocateCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = !_locationSetting.HasErrors;
		}

		private void _button_locate_Click(object sender, RoutedEventArgs e)
		{
			double lat = double.Parse(_locationSetting.Lat);
			double lng = double.Parse(_locationSetting.Lng);
			mapMainWindow.Position = new PointLatLng(lat, lng);
		}

		private void _drawingImage_addMarker_Click(object sender, RoutedEventArgs e)
		{
			double lat = double.Parse(_locationSetting.Lat);
			double lng = double.Parse(_locationSetting.Lng);
			markerManualAdding(new PointLatLng(lat, lng));
		}

		private void markerManualAdding(PointLatLng pointLatLng)
		{
			GMapRouteMarker marker = createRouteMarker(pointLatLng, MarkerSize.Width, MarkerSize.Height, TeminalName);
			GMapPostionSearchHelper.SearchPointInformation(pointLatLng, mapMainWindow.MapProvider as GeocodingProvider, marker);
			mapMainWindow.Markers.Add(marker);
		}
	}

	public class AddRouteCommand
	{
		private static RoutedUICommand addRoute;

		static AddRouteCommand()
		{
			InputGestureCollection inputs = new InputGestureCollection();
			inputs.Add(new KeyGesture(Key.F, ModifierKeys.Control, "Ctrl + F"));
			addRoute = new RoutedUICommand("AddRoute", "AddRoute", typeof(AddRouteCommand), inputs);
		}

		public static RoutedUICommand AddRoute
		{
			get { return addRoute; }
		}
	}

	public class GetDirectDistanceCommand
	{
		private static RoutedUICommand getDirectDistance;

		static GetDirectDistanceCommand()
		{
			InputGestureCollection inputs = new InputGestureCollection();
			inputs.Add(new KeyGesture(Key.T, ModifierKeys.Control, "Ctrl + T"));
			getDirectDistance = new RoutedUICommand("GetDirectDistance", "GetDirectDistance", typeof(GetDirectDistanceCommand), inputs);
		}

		public static RoutedUICommand GetDirectDistance
		{
			get { return getDirectDistance; }
		}
	}

	public class ReLoadMapCommand
	{
		private static RoutedUICommand _reloadMap;

		static ReLoadMapCommand()
		{
			InputGestureCollection inputs = new InputGestureCollection();
			inputs.Add(new KeyGesture(Key.F5, ModifierKeys.None, "F5"));
			_reloadMap = new RoutedUICommand("ReloadMap", "ReloadMap", typeof(ReLoadMapCommand),inputs);
		}

		public static RoutedUICommand ReloadMap
		{
			get { return _reloadMap; }
		}
	}

	public class LocateCommand
	{
		private static RoutedUICommand _locate;

		static LocateCommand()
		{
			_locate = new RoutedUICommand("Locate", "Locate", typeof(ReLoadMapCommand));
		}

		public static RoutedUICommand Locate
		{
			get { return _locate; }
		}
	}

	public class LocatePosition : INotifyPropertyChanged, INotifyDataErrorInfo
	{
		/// <summary>
		/// 指定目标纬度
		/// </summary>
		private string _lat;
		public string Lat
		{
			get { return _lat; }
			set
			{
				_lat = value;
				double tryPraseValue;
				List<string> errors = new List<string>();

				if (double.TryParse(value, out tryPraseValue))
				{
					bool isValid = true;

					isValid = 0.0 <= tryPraseValue && tryPraseValue <= 90.0;

					if (isValid)
						clearErros("Lat");
					else
					{
						errors.Add("纬度值超出范围");
						setErrors("Lat", errors);
					}
					
				}
				else
				{
					errors.Add("输入不合法");
					setErrors("Lat", errors);
				}
				
				OnPropertyChanged(new PropertyChangedEventArgs("Lat"));
			}
		}

		/// <summary>
		/// 指定目标经度
		/// </summary>
		private string _lng;
		public string Lng
		{
			get { return _lng; }
			set
			{
				_lng = value;

				double tryPraseValue;
				List<string> errors = new List<string>();

				if (double.TryParse(value, out tryPraseValue))
				{
					bool isValid = true;

					isValid = 0.0 <= tryPraseValue && tryPraseValue <= 180.0;

					if (isValid)
						clearErros("Lng");
					else
					{
						errors.Add("经度值超出范围");
						setErrors("Lng", errors);
					}
				}
				else
				{
					errors.Add("输入不合法");
					setErrors("Lng", errors);
				}

				OnPropertyChanged(new PropertyChangedEventArgs("Lng"));
			}
		}

		private Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

		public bool HasErrors
		{
			get { return _errors.Count > 0; }
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		public void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		public IEnumerable GetErrors(string propertyName)
		{
			if (string.IsNullOrEmpty(propertyName))
				return _errors.Values;

			if (_errors.ContainsKey(propertyName))
				return _errors[propertyName];
			else
				return null;
		}

		private void setErrors(string propertyName, List<string> propertyErrors)
		{
			_errors.Remove(propertyName);
			_errors.Add(propertyName, propertyErrors);
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}

		private void clearErros(string propertyName)
		{
			_errors.Remove(propertyName);

			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
