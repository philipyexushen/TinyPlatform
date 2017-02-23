using System;
using System.IO;
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
using DebugHelperNameSpace;
using System.Collections.Generic;
using System.Windows.Controls;
using System.ComponentModel;
using Microsoft.Windows.Controls.Ribbon;
using System.Collections;
using System.Windows.Controls.Primitives;
using System.Windows.Resources;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;

namespace UrgencyRescuePlatform
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		private static readonly string _mapMarkerName = "本地";//手动插入时显示

		public EventHandler<ClientMessageEventArgs> WriteDataEvent;

		private AsyncTcpClientHelper _asyncTcpClientHelper;
		private Thread _backgourndThread;
		private bool _isNormal = true;
		private bool _isWidden = false;
		private bool _isSearchingRoute = false;
		private bool _isGetDirectDistanceButtonClicked = false;
		private MediaPlayer _warningSoundsPlayer = new MediaPlayer();
		private GMapRouteMarker _highLightMarker = null;
		private ReaderWriterLock _managerLock = new ReaderWriterLock();
		private UserInfo _currentSelctedInfo = null;

		private Tuple<GMapMarker, GMapMarker> _startPointWithShape;
		private Tuple<GMapMarker, GMapMarker> _endPointWithShape;
		private System.Windows.Forms.NotifyIcon _trayIcon;
		public LocatePosition _locationSetting = new LocatePosition();
		private LoginInformation _loginInformation = new LoginInformation();

		public Size MarkerSize { get; private set; } = new Size(60, 60);

		private static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
		{
			while (source != null && source.GetType() != typeof(T))
				source = VisualTreeHelper.GetParent(source);

			return source;
		}

		protected override void OnClosed(EventArgs e)
		{
			if (_asyncTcpClientHelper != null)
				_asyncTcpClientHelper.Disconnect();
			base.OnClosed(e);
		}

		public MainWindow()
		{
			InitializeComponent();
			InivilizeMap();
			InivilizeTrayIcon();

			double workHeight = SystemParameters.WorkArea.Height;
			double workWidth  = SystemParameters.WorkArea.Width;

			this.Top  = (workHeight - this.Height) / 2;
			this.Left = (workWidth - this.Width) / 2;

			this.ResizeMode = ResizeMode.CanResizeWithGrip;

			this._comboBox_mapSources.SelectedIndex = 0;
			this._comboBox_mapAccessMode.SelectedIndex = 1;

			_treeView_markers.ItemsSource = UserManagerHelper.GetManager.Users;
			UserManagerHelper.GetManager.AddItem(-1, "", $"本地", true);
			UserManagerHelper.GetManager.SetIslogin(-1, false);

			UserManagerHelper.GetManager.AddItem(0, "", $"服务器", true);
			UserManagerHelper.GetManager.SetIslogin(0, false);
			RefreshItemsView();

			_border_informPanel.DataContext = _loginInformation;
		}

		private void InivilizeMap()
		{
			//GMapManagerLoader.Instance.Load(":/MapCache/DataExp.gmdb");
			mapMainWindow.Zoom = 12;

			mapMainWindow.ShowCenter = true;
			mapMainWindow.DragButton = MouseButton.Left;
			mapMainWindow.MouseMove += _mapMainWindow_Move;

			//地图中心广州
			mapMainWindow.Position = new PointLatLng(23.137714, 113.355401);

			_group_mapfunctions.DataContext = _locationSetting;
			_locationSetting.Lat = $"{23.137714}";
			_locationSetting.Lng = $"{113.355401}";
		}

		private void InivilizeTrayIcon()
		{
			_trayIcon = new System.Windows.Forms.NotifyIcon();
			StreamResourceInfo sri = Application.GetResourceStream(
				new Uri("UrgencyRescuePlatform;component/icon.ico", UriKind.Relative));
			
			_trayIcon.Icon = new System.Drawing.Icon(sri.Stream);
			_trayIcon.Text = this.Title;

			System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("打开主窗口");
			System.Windows.Forms.MenuItem hide = new System.Windows.Forms.MenuItem("隐藏主窗口");
			System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("退出");

			exit.Click += _trayIconExit_Click;
			open.Click += _trayIconOpen_Click;
			hide.Click += _trayIcon_Click;
			_trayIcon.MouseDoubleClick += _trayIcon_MouseDoubleClick;

			System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] { open, hide, exit };
			_trayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
			_trayIcon.Visible = true;
		}

		private void _trayIcon_Click(object sender, EventArgs e)
		{
			this.Visibility = Visibility.Hidden;
		}

		private void _trayIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			showWindowNormal();
		}

		private void _trayIconOpen_Click(object sender, EventArgs e)
		{
			showWindowNormal();
		}

		private void showWindowNormal()
		{
			this.Visibility = Visibility.Visible;
			this.WindowState = WindowState.Normal;
		}

		private void _trayIconExit_Click(object sender, EventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void _mapMainWindow_Move(object sender, MouseEventArgs e)
		{
			mapMainWindow.Cursor = Cursors.Hand;
		}

		private void InivilizeAsyncTcpClient(string hostName, int port)
		{
			_asyncTcpClientHelper = new AsyncTcpClientHelper();
			AsyncTcpClientHelper.HostArg arg = new AsyncTcpClientHelper.HostArg(_loginInformation.TeminalName, hostName, port, null);

			ParameterizedThreadStart workerThreadMethod
				= new ParameterizedThreadStart(_asyncTcpClientHelper.StartTcpClientWithHostName);
			_backgourndThread = new Thread(workerThreadMethod);

			_asyncTcpClientHelper.DataOccuredEvent              += ClientLogOutputToMsgBox;
			_asyncTcpClientHelper.DisconnecEvent				+= HandleClientConnectionFailed;
			_asyncTcpClientHelper.NewUserConnectedEvent         += on_NewUserComming;
			_asyncTcpClientHelper.NewCoordinateOccuredEvent     += on_CoordinateOccured;
			_asyncTcpClientHelper.HelpEvent                     += _asyncTcpClientHelper_HelpEvent;
			_asyncTcpClientHelper.UserLogoutEvent				+= _asyncTcpClientHelper_UserLogoutEvent;
			_asyncTcpClientHelper.ConenctedSuccessfulEvent      += _asyncTcpClientHelper_ConenctedSuccessfulEvent;

			if (_checkBox_allowAutoReconnect.IsChecked == true)
				_asyncTcpClientHelper.IsAllowAutoReconnect = true;
			else
				_asyncTcpClientHelper.IsAllowAutoReconnect = false;

			WriteDataEvent += _asyncTcpClientHelper.WriteData;
			_backgourndThread.IsBackground = true;

			_backgourndThread.Start(arg);
		}

		private void _asyncTcpClientHelper_ConenctedSuccessfulEvent(object sender, ClientMessageEventArgs e)
		{
			_managerLock.AcquireWriterLock(int.MaxValue);

			Application.Current.Dispatcher.BeginInvoke(
				new Action(() =>
			{
				UserManagerHelper.GetManager.SetIslogin(-1, true);

				UserManagerHelper.GetManager.AddItem(0, "", "服务器");
				UserManagerHelper.GetManager.SetIslogin(0, true);
				RefreshItemsView();

				insertMessage(e.Source, e.DataMsg);

				_loginInformation.IsLogin = true;

			}));
			_managerLock.ReleaseWriterLock();
		}

		private void _asyncTcpClientHelper_UserLogoutEvent(object sender, TcpClientCore.UserLogoutArgs e)
		{
			Application.Current.Dispatcher.BeginInvoke(
						new Action(() =>
						{
							_managerLock.AcquireReaderLock(int.MaxValue);
							string userName = UserManagerHelper.GetManager.GetUserName(e.SourceFeatureCode);
							_managerLock.ReleaseReaderLock();

							UserManagerHelper.GetManager.SetIslogin(e.SourceFeatureCode, false);
							insertMessage(e.SourceFeatureCode,$"{userName} 已经下线");
							RefreshItemsView();
						}
					));
		}

		private void _asyncTcpClientHelper_HelpEvent(object sender, HelpEventArgs e)
		{
			try
			{
				if (e.IsCoordinate)
				{
					if (!UserManagerHelper.GetManager.IsContainKey(e.SourceFeatureCode))
					{
						DebugHelpers.CustomMessageShow($"Key {e.SourceFeatureCode} is not contained");
					}
					Application.Current.Dispatcher.BeginInvoke(
						new Action(() =>
						{
							GMapRouteMarker marker = addMarkerandRoad(e.Point, e.SourceFeatureCode, false);
							marker.ShowWarning();
							playWarningSounds();
						}
					));
				}
				
			}
			catch (Exception ex)
			{
				DebugHelpers.CustomMessageShow(ex.Message);
			}
		}

		private void playWarningSounds()
		{
			_warningSoundsPlayer.MediaFailed += _warningSoundsPlayer_MediaFailed;
			_warningSoundsPlayer.Open(new Uri(@"sounds\warning.mp3", UriKind.Relative));
			_warningSoundsPlayer.Play();
		}

		private void _warningSoundsPlayer_MediaFailed(object sender, ExceptionEventArgs e)
		{
			DebugHelpers.CustomMessageShow(e.ErrorException.Message);
		}

		private void mapMainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Point clickPoint = e.GetPosition(mapMainWindow);
			PointLatLng point = mapMainWindow.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

			markerManualAdding(point);
		}

		private GMapRouteMarker createRouteMarker(PointLatLng point, double iconWidth, double iconHeight, int itemKey, string name, bool isOwnedByPlatform)
		{
			GMapRouteMarker marker = new GMapRouteMarker(point, iconWidth, iconHeight, itemKey, name, isOwnedByPlatform);

			marker.SetStartPointEvent       += Marker_SetStartPointEvent;
			marker.CancelSetStartPointEvent += Marker_CancelSetStartedEvent;
			marker.SetEndPointEvent         += Marker_SetEndPointEvent;
			marker.CancelSetEndPointEvent   += Marker_CancelSetEndPointEvent;
			marker.ClearMarker              += Marker_ClearMarker;

			return marker;
		}

		private void Marker_ClearMarker(object sender, EventArgs e)
		{
			GMapRouteMarker marker = sender as GMapRouteMarker;
			clearMarkerPrivate(marker);

			renewExistFlag(marker, false);
		}

		private void clearMarkerPrivate(GMapRouteMarker marker)
		{
			if (marker != null)
			{
				if (marker.IsStartPoint)
					marker.IsStartPoint = false;
				else if (marker.IsEndPoint)
					marker.IsEndPoint = false;
				mapMainWindow.Markers.Remove(marker);
			}
		}

		private void clearMarkerPrivate(GMapRoute marker)
		{
			mapMainWindow.Markers.Remove(marker);
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

		private void on_CoordinateOccured(object sender, TransformedCoordinateArgs e)
		{
			_managerLock.AcquireReaderLock(int.MaxValue);
			try
			{
				if (!UserManagerHelper.GetManager.IsContainKey(e.SourceFeatureCode))
				{
					DebugHelpers.CustomMessageShow($"Key {e.SourceFeatureCode} is not contained");
				}
				Application.Current.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						addMarkerandRoad(e.Point, e.SourceFeatureCode, false);
					}
				));
			}
			catch(Exception ex)
			{
				DebugHelpers.CustomMessageShow(ex.Message);
			}
			_managerLock.ReleaseReaderLock();
		}

		private GMapRouteMarker addMarkerandRoad(PointLatLng point, int sourceFeatureCode, bool isOwnedByPlatform)
		{
			GMapRouteMarker marker 
				= createRouteMarker(point, MarkerSize.Width, MarkerSize.Height, 
				sourceFeatureCode, $"{sourceFeatureCode}", isOwnedByPlatform);
			GMapPostionSearchHelper.SearchPointInformation(point, mapMainWindow.MapProvider as GeocodingProvider, marker);
			mapMainWindow.Markers.Add(marker);

			GMapRouteMarker lastMarker = UserManagerHelper.GetManager.GetLastMarker(sourceFeatureCode) as GMapRouteMarker;

			if (lastMarker != null)
			{
				PointLatLng lastPoint = lastMarker.Position;

				double distance = GetDirectDistanceHelper.GetDistance(point.Lat, point.Lng, lastPoint.Lat, lastPoint.Lng);

				double rate = distance / (marker.Time - lastMarker.Time).TotalSeconds;
				marker.Rate = rate * 3600;

				List<PointLatLng> points = new List<PointLatLng> { point, lastPoint };

				System.Windows.Shapes.Path lineShape = createLineTilePath(Brushes.Blue);

				GMapRoute line = new GMapRoute(points);
				line.Shape = lineShape;
				mapMainWindow.Markers.Add(line);
				line.RegenerateShape(mapMainWindow);
				UserManagerHelper.GetManager.AddMarkerToItem(sourceFeatureCode, line);//记得添加道路
			}
			UserManagerHelper.GetManager.AddMarkerToItem(sourceFeatureCode, marker);
			return marker;
		}

		private void sendMsgBtn_Click(object sender, RoutedEventArgs e)
		{
			string msg = inputMsgBox.Text;
			sendMessage(msg);
		}

		private void sendMessage(string message)
		{
			try
			{
				if (_asyncTcpClientHelper != null)
				{
					ClientMessageEventArgs arg = new ClientMessageEventArgs(-1, message);
					WriteDataEvent?.BeginInvoke(this, arg, null, this);
					insertMessage(-1, message);
					inputMsgBox.Clear();
				}
			}
			catch (Exception)
			{

			}
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void ClientLogOutputToMsgBox(object sender, ClientMessageEventArgs e)
		{
			insertMessage(e.Source, e.DataMsg);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void insertMessage(int source,string message)
		{
			msgBox.Dispatcher.BeginInvoke(
						new Action(() =>
						{
							_managerLock.AcquireReaderLock(int.MaxValue);
							string userName = UserManagerHelper.GetManager.GetUserName(source);
							_managerLock.ReleaseReaderLock();

							msgBox.AppendText($">>> {userName} {DateTime.Now:s} \n");
							msgBox.AppendText($"{message}\n");
							msgBox.AppendText("-------------------------------\n");
							msgBox.ScrollToEnd();
						}));
		}

		private void on_NewUserComming(object sender, TcpClientCore.NewUserDetailsArgs args)
		{
			_managerLock.AcquireWriterLock(int.MaxValue);
			mapMainWindow.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						UserManagerHelper.GetManager.AddItem(args.SourceFeatureCode, args.Ip, args.UserName);
					}));
			_managerLock.ReleaseWriterLock();
		}

		private void HandleClientConnectionFailed(object sender, ClientMessageEventArgs e)
		{
			_managerLock.AcquireWriterLock(int.MaxValue);
			msgBox.Dispatcher.BeginInvoke(new Action(() 
				=>{
					insertMessage(-1, e.DataMsg);
					_button_login.IsEnabled = true;

					_textbox_userName.Text = string.Empty;
					_textBox_IP.Text = string.Empty;

					_grid_loginInformPanel.Visibility = Visibility.Collapsed;
					_textBlock_waitingLogin.Visibility = Visibility.Visible;

					UserManagerHelper.GetManager.SetAllLogout();
					RefreshItemsView();

					_loginInformation.IsLogin = false;
				}));
			_managerLock.ReleaseWriterLock();
		}

		private System.Windows.Shapes.Path createLineTilePath(Brush color)
		{
			System.Windows.Shapes.Path lineShape = new System.Windows.Shapes.Path();
			
			lineShape.Stroke = color;
			lineShape.StrokeThickness = 6;
			lineShape.Opacity = 0.7;

			return lineShape;
		}

		#region Window
		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			this.Visibility = Visibility.Hidden;
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

			locateChildWindowInCenterArea(w);

			if (w.ShowDialog().Value)
			{
				_loginInformation.HostName = w.HostName;
				_loginInformation.Port = w.Port;
				_loginInformation.TeminalName = w.UserName;
				_loginInformation.TeminalIp = w.Ip;

				_grid_loginInformPanel.Visibility = Visibility.Visible;
				_textBlock_waitingLogin.Visibility = Visibility.Collapsed;

				_button_login.IsEnabled = false;
				_button_logout.IsEnabled = true;

				InivilizeAsyncTcpClient(_loginInformation.HostName, _loginInformation.Port);
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
			showMessageInStatusBar("正在查找路径...", 0);
		}

		private void handleRouteSearchCallback(MapRoute route)
		{
			_managerLock.AcquireReaderLock(int.MaxValue);
			string result = string.Empty;
			if (route != null)
			{
				double pathLength = route.Distance;

				List<PointLatLng> points = route.Points;
				GMapRoute line = new GMapRoute(points);
				mapMainWindow.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						System.Windows.Shapes.Path lineShape = createLineTilePath(Brushes.Red);
						line.Shape = lineShape;
						line.ZIndex = -1;

						UserManagerHelper.GetManager.AddMarkerToItem(-1, line);

						mapMainWindow.Markers.Add(line);
						line.RegenerateShape(mapMainWindow);
					}));

				result = $"已找到路径,长度为{pathLength}km";
				showMessageInStatusBar("查找路径成功", 3000);
			}
			else
			{
				result = "查找路径失败";
				showMessageInStatusBar("查找路径失败", 3000);
			}

			mapMainWindow.Dispatcher.BeginInvoke(
			new Action(() => {
				MessageBox.Show(this, result,this.Title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
				_isSearchingRoute = false;
			}));
			_managerLock.ReleaseReaderLock();
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
			_managerLock.AcquireReaderLock(int.MaxValue);
			GMapRouteMarker marker 
				= createRouteMarker(pointLatLng, MarkerSize.Width, MarkerSize.Height,-1, _mapMarkerName, true);
			GMapPostionSearchHelper.SearchPointInformation(pointLatLng, mapMainWindow.MapProvider as GeocodingProvider, marker);
			mapMainWindow.Markers.Add(marker);

			UserManagerHelper.GetManager.AddMarkerToItem(-1, marker);

			_managerLock.ReleaseReaderLock();
		}

		private void _toggleButton_allowDrag_Click(object sender, RoutedEventArgs e)
		{
			RibbonToggleButton button = sender as RibbonToggleButton;

			if (button.IsChecked == true)
			{
				GMapRouteMarker.AllowDrag = true;
				mapMainWindow.DragButton = MouseButton.Middle;
				button.ToolTip = "允许拖拽markers";
			}
			else
			{
				GMapRouteMarker.AllowDrag = false;
				mapMainWindow.DragButton = MouseButton.Left;
				button.ToolTip = "不允许拖拽markers";
			}
				
		}

		private void _checkBox_allowAutoReconnect_Checked(object sender, RoutedEventArgs e)
		{
			if (_asyncTcpClientHelper !=null)
			{
				RibbonCheckBox box = (RibbonCheckBox)sender;
				if (box.IsChecked == true)
					_asyncTcpClientHelper.IsAllowAutoReconnect = true;
				else
					_asyncTcpClientHelper.IsAllowAutoReconnect = false;
			}

		}

		private void _button_logout_Click(object sender, RoutedEventArgs e)
		{
			_button_logout.IsEnabled = false;
			_asyncTcpClientHelper.Disconnect();
		}

		private void exitItem_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void aboutMe_Click(object sender, RoutedEventArgs e)
		{
			TinyMessageBox messgaeBox = new TinyMessageBox();
			messgaeBox.Title = this.Title;

			messgaeBox.Owner = this;

			locateChildWindowInCenterArea(messgaeBox);
			messgaeBox.ShowDialog();
		}

		private void locateChildWindowInCenterArea(Window w)
		{
			if (this.WindowState == WindowState.Normal)
			{
				w.Left = this.Left + this.Width / 2 - w.Width / 2;
				w.Top = this.Top + this.Height / 2 - w.Height / 2;
			}
			else if (this.WindowState == WindowState.Maximized)
			{
				double screenHeight = SystemParameters.FullPrimaryScreenHeight;
				double screenWidth = SystemParameters.FullPrimaryScreenWidth;

				w.Left = (screenWidth - w.Width) / 2;
				w.Top = (screenHeight - w.Height) / 2;
			}
		}

		private void _menuItem_clearItems_Click(object sender, RoutedEventArgs e)
		{
			if (_currentSelctedInfo != null)
			{
				MessageBoxResult result
				= MessageBox.Show(this, "你确定要删除此标记吗？该操作不可恢复", this.Title, MessageBoxButton.YesNo, MessageBoxImage.Asterisk);

				if (result == MessageBoxResult.Yes)
				{
					_managerLock.AcquireWriterLock(int.MaxValue);

					UserManagerHelper.GetManager.DeleteItem(this, _currentSelctedInfo);
					_managerLock.ReleaseWriterLock();
				}
				_currentSelctedInfo = null;
			}
		}

		private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;
			if (treeViewItem != null && treeViewItem.Header != null)
			{
				_currentSelctedInfo = treeViewItem.Header as UserInfo;

				treeViewItem.Focus();
			}
		}

		private void TreeViewItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;

			if (item == null)
				return;

			if (item.Header is UserInfo)
			{
				ContextMenu menu = this.FindResource("_contextMenu_mapItems") as ContextMenu;

				menu.DataContext = item.DataContext;
				menu.HasDropShadow = true;
				menu.Placement = PlacementMode.MousePoint;
				menu.IsOpen = true;
			}
			else if (item.Header is MarkersWrapper)
			{
				ContextMenu menu = this.FindResource("_contextMenu_markerItems") as ContextMenu;

				menu.DataContext = item.DataContext;
				menu.HasDropShadow = true;
				menu.Placement = PlacementMode.MousePoint;
				menu.IsOpen = true;
			}
		}

		private void claerMarkerItem_Click(object sender, RoutedEventArgs e)
		{
			MarkersWrapper wrapper = _treeView_markers.SelectedItem as MarkersWrapper;
			ClaerMarkerItemFromMap(wrapper);
		}

		public void ClaerMarkerItemFromMap(MarkersWrapper wrapper)
		{
			if (wrapper != null)
			{
				if (wrapper.Content is GMapRouteMarker)
					clearMarkerPrivate(wrapper.Content as GMapRouteMarker);
				else if (wrapper.Content is GMapRoute)
					clearMarkerPrivate(wrapper.Content as GMapRoute);
				wrapper.IsExistInMap = false;
			}
		}

		private void reAddMarkerItem_Click(object sender, RoutedEventArgs e)
		{
			MarkersWrapper wrapper = _treeView_markers.SelectedItem as MarkersWrapper;

			if (wrapper != null)
			{
				wrapper.IsExistInMap = true;
				mapMainWindow.Markers.Add(wrapper.Content);
			}
				
		}

		private void _treeView_markers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			MarkersWrapper wrapper = _treeView_markers.SelectedItem as MarkersWrapper;

			if (wrapper != null)
			{
				GMapRouteMarker gMapRouteMarker = wrapper.Content as GMapRouteMarker;
				if (gMapRouteMarker != null)
				{
					if (_highLightMarker != null)
						_highLightMarker.RecoverMarker();
					gMapRouteMarker.HighLightMarker();
					_highLightMarker = gMapRouteMarker;
				}
			}
		}

		private void _treeView_markers_LostFocus(object sender, RoutedEventArgs e)
		{
			TreeViewItem item = _treeView_markers.ItemContainerGenerator.ContainerFromItem(_treeView_markers.SelectedItem)
				as TreeViewItem;

			if (item != null)
				item.IsSelected = false;

			if (_highLightMarker != null)
				_highLightMarker.RecoverMarker();
			_highLightMarker = null;
		}

		private void _treeView_markers_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			MarkersWrapper wrapper = _treeView_markers.SelectedItem as MarkersWrapper;

			if (wrapper != null)
			{
				GMapRouteMarker gMapRouteMarker = wrapper.Content as GMapRouteMarker;
				if (gMapRouteMarker != null)
				{
					if (_highLightMarker != null)
					{
						if (_highLightMarker != gMapRouteMarker)
						{
							_highLightMarker.RecoverMarker();
							_highLightMarker = gMapRouteMarker;
						}
						gMapRouteMarker.HighLightMarker();
					}
					else
					{
						_highLightMarker = gMapRouteMarker;
						gMapRouteMarker.HighLightMarker();
					}
				}
			}
		}

		private void renewExistFlag(GMapRouteMarker marker, bool exist)
		{
			_managerLock.AcquireReaderLock(int.MaxValue);
			UserManagerHelper.GetManager.RenewExist(marker.ItemKey, marker, exist);
			_managerLock.ReleaseReaderLock();
		}

		private void destroyMarkerItem_Click(object sender, RoutedEventArgs e)
		{
			MarkersWrapper wrapper = _treeView_markers.SelectedItem as MarkersWrapper;
			if (wrapper !=null)
			{
				MessageBoxResult result
				= MessageBox.Show(this, "你确定要删除此标记吗？该操作不可恢复", this.Title, MessageBoxButton.YesNo, MessageBoxImage.Asterisk);
				if (result == MessageBoxResult.Yes)
				{
					_managerLock.AcquireWriterLock(int.MaxValue);
					UserManagerHelper.GetManager.DeleteWrapper(this, wrapper.ParentKey, wrapper);
					_managerLock.ReleaseWriterLock();
				}
			}
		}

		/// <summary>
		/// 刷新一次控制列表
		/// </summary>
		public void RefreshItemsView()
		{
			StyleSelector selector = _treeView_markers.ItemContainerStyleSelector;
			_treeView_markers.ItemContainerStyleSelector = null;
			_treeView_markers.ItemContainerStyleSelector = selector;
		}

		private void clearMessageBoxClick(object sender, RoutedEventArgs e)
		{
			msgBox.Clear();
		}

		private void inputMsgBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.KeyboardDevice.IsKeyDown(Key.Enter))
			{
				inputMsgBox.AppendText("\n");
				inputMsgBox.ScrollToEnd();
				inputMsgBox.SelectionStart = inputMsgBox.Text.Length;
			}
			else if (e.Key == Key.Enter)
			{
				string message = inputMsgBox.Text;
				sendMessage(message);
			}
		}

		private void saveMessageClick(object sender, RoutedEventArgs e)
		{
			saveTxtFilePrivate(msgBox.Text);
		}

		private void _button_saveLog_Click(object sender, RoutedEventArgs e)
		{
			saveTxtFilePrivate(msgBox.Text);
		}

		private async void saveTxtFilePrivate(string text)
		{
			System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
			dialog.Filter = "文本文件（*.txt）|*.txt";
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();

			text = text.Replace("\n", "\r\n");

			if (result== System.Windows.Forms.DialogResult.OK)
			{
				try
				{
					using (StreamWriter writer = new StreamWriter(dialog.FileName))
					{
						await writer.WriteLineAsync(text);
					}
				}
				catch
				{
					MessageBox.Show(this, "保存记录失败", this.Title,MessageBoxButton.OK,MessageBoxImage.Error);
				}
			}
		}

		private void _toggleButton_openInputBox_Click(object sender, RoutedEventArgs e)
		{
			if (_toggleButton_openInputBox.IsChecked == true)
			{
				inputMsgBox.Visibility = Visibility.Visible;
				inputMsgBox.MinHeight = 100;
				_toggleButton_openInputBox.Content = "关闭输入框";
			}
				
			else if (_toggleButton_openInputBox.IsChecked == false)
			{
				inputMsgBox.Visibility = Visibility.Collapsed;
				
				_toggleButton_openInputBox.Content = "打开输入框";
			}
				
		}

		private void showMessageInStatusBar(string message, int millisecond)
		{
			Application.Current.Dispatcher.Invoke(new Action(async () =>
			{
				_satusBar_message.Text = message;
				if (millisecond != 0)
				{
					await Task.Delay(millisecond);
					_satusBar_message.Text = string.Empty;
				}
			}));
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

	public class LoginInformation : INotifyPropertyChanged
	{
		private string _hostName;
		public string HostName
		{
			get { return _hostName; }
			set
			{
				_hostName = value;
				OnPropertyChanged(new PropertyChangedEventArgs("HostName"));
			}
		}

		private int _port;
		public int Port
		{
			get { return _port; }
			set
			{
				_port = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Port"));
			}
		}

		private string _teminalName;
		public string TeminalName
		{
			get { return _teminalName; }
			set
			{
				_teminalName = value;
				OnPropertyChanged(new PropertyChangedEventArgs("TeminalName"));
			}
		}

		private string _teminalIp;
		public string TeminalIp
		{
			get { return _teminalIp; }
			set
			{
				_teminalIp = value;
				OnPropertyChanged(new PropertyChangedEventArgs("TeminalIp"));
			}
		}

		private bool _isLogin;
		public bool IsLogin
		{
			get { return _isLogin; }
			set
			{
				_isLogin = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsLogin"));
			}
		}

		private void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
