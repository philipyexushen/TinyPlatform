using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Converters;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Windows.Resources;
using System.Resources;
using GMap.NET.WindowsPresentation;
using GMap.NET.MapProviders;
using GMap.NET;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Data;
using System.ComponentModel;
using System.Threading;

namespace MarkersLib
{
	public class GMapRouteMarker : GMapMarker
	{
		private static DrawingImage _normalDrawing;
		private static DrawingImage _flipedDrawing;
		private static DrawingImage _startDrawing;
		private static DrawingImage _endDrawing;

		static GMapRouteMarker()
		{
			ResourceDictionary resources = new ResourceDictionary();
			resources.Source = new Uri("MarkersLib;component/MarkerShapes.xaml", UriKind.Relative);

			ComponentResourceKey iconKey;

			//iconKey = new ComponentResourceKey(typeof(MarkerShapesKeys), "NormalFlag");
			//_normalDrawing = resources[iconKey] as DrawingImage;
			iconKey = new ComponentResourceKey(typeof(MarkerShapesKeys), "BlueFlag");
			_normalDrawing = new DrawingImage(resources[iconKey] as DrawingGroup);

			iconKey = new ComponentResourceKey(typeof(MarkerShapesKeys), "HighlightFlag");
			_flipedDrawing = new DrawingImage(resources[iconKey] as DrawingGroup);

			iconKey = new ComponentResourceKey(typeof(MarkerShapesKeys), "YellowFlag");
			_startDrawing = new DrawingImage(resources[iconKey] as DrawingGroup);

			iconKey = new ComponentResourceKey(typeof(MarkerShapesKeys), "PurpleFlag");
			_endDrawing = new DrawingImage(resources[iconKey] as DrawingGroup);
		}

		/// <summary>
		/// 创建一个新的路径marker
		/// </summary>
		/// <param name="pos">地点</param>
		/// <param name="iconWidth">图标宽度</param>
		/// <param name="iconHeight">图标长度</param>
		public GMapRouteMarker(PointLatLng pos, double iconWidth, double iconHeight, string userName, bool isOwnbyPlatform = true)
			: base(pos)
		{
			this.Corrdinate_Lat = pos.Lat;
			this.Corrdinate_Lng = pos.Lng;
			this.Time = DateTime.Now;
			this.UserName = userName;
			this.Address = "正在查询...";

			this.IsOwnbyPlatform = isOwnbyPlatform;

			InivilzieIcon(iconWidth, iconHeight);
		}

		private void InivilzieIcon(double iconWidth, double iconHeight)
		{
			Icon.Source = _normalDrawing;

			IconWidth = iconWidth;
			IconHeight = iconHeight;

			Icon.MouseEnter += Icon_MouseEnter;
			Icon.MouseLeave += Icon_MouseLeave;
			Icon.MouseMove += Icon_MouseMove;
			Icon.MouseLeftButtonDown += Icon_MouseLeftButtonDown;
			Icon.MouseLeftButtonUp += Icon_MouseLeftButtonUp;
			Icon.MouseRightButtonUp += Icon_MouseRightButtonDown;
			Icon.SnapsToDevicePixels = true;

			Offset = new Point(-Icon.Width / 3, -Icon.Height);
			Shape = Icon;
		}

		private void Icon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isDragging)
			{
				Image icon = (Image)sender;
				icon.ReleaseMouseCapture();
				_isDragging = false;
			}
		}

		private void Icon_MouseMove(object sender, MouseEventArgs e)
		{
			if (_isDragging)
			{
				Point clickPoint = e.GetPosition(this.Map);
				PointLatLng point = this.Map.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

				point += _dragOffest;

				this.Position = point;
				Corrdinate_Lat = point.Lat;
				Corrdinate_Lng = point.Lng;
			}
		}

		private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (IsOwnbyPlatform)
			{
				Image icon = (Image)sender;
				_isDragging = true;

				Point clickPoint = e.GetPosition(this.Map);
				PointLatLng point = this.Map.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

				_dragOffest = new SizeLatLng();
				_dragOffest.HeightLat = point.Lat - this.Position.Lat; 
				_dragOffest.WidthLng = point.Lng - this.Position.Lng;

				icon.CaptureMouse();
			}
		}

		/// <summary>
		/// 创建一个新的路径marker
		/// </summary>
		/// <param name="pos">地点</param>
		/// <param name="iconSize">图标大小</param>
		public GMapRouteMarker(PointLatLng pos, Size iconSize, string userName) 
			:this(pos,iconSize.Width,iconSize.Height, userName)
		{
		}

		/// <summary>
		/// 显示图标
		/// </summary>
		public Image Icon { get; set; } = new Image();

		/// <summary>
		/// 标识的用户
		/// </summary>
		private string _userName;
		public string UserName
		{
			get { return _userName; }
			set
			{
				_userName = value;
				OnPropertyChanged(new PropertyChangedEventArgs("UserName"));
			}
		}

		/// <summary>
		/// 标识所标记的地点
		/// </summary>
		private string _address;
		public string Address
		{
			get{ return _address; }
			set
			{
				_address = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Address"));
			}
		}

		/// <summary>
		/// 获得图标高度
		/// </summary>
		public double IconHeight { get { return _iconHeight; } set { _iconHeight = value; Icon.Height = value; } }

		/// <summary>
		/// 获得图标长度
		/// </summary>
		public double IconWidth { get { return _iconWidth; } set { _iconWidth = value; Icon.Width = value; } }

		/// <summary>
		/// 标志的维度
		/// </summary>
		private double _corrdinate_Lat;
		public double Corrdinate_Lat
		{
			get { return _corrdinate_Lat; }
			set
			{
				_corrdinate_Lat = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Corrdinate_Lat"));
			}
		}

		/// <summary>
		/// 标志的经度
		/// </summary>
		private double _corrdinate_Lng;

		public double Corrdinate_Lng
		{
			get { return _corrdinate_Lng; }
			set
			{
				_corrdinate_Lng = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Corrdinate_Lng"));
			}
		}

		/// <summary>
		/// 标志添加的时间
		/// </summary>
		private DateTime _time;
		public DateTime Time
		{
			get { return _time; }
			set
			{
				_time = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Time"));
			}
		}

		/// <summary>
		/// 指示是否为起始标志
		/// </summary>
		private bool _isStartPoint;
		public bool IsStartPoint
		{
			get { return _isStartPoint; }
			set
			{
				if (IsEndPoint)
					IsEndPoint = false;

				if (value)
					MenuPanel_SetStartPoint();
				else
					MenuPanel_CancelSetStartPoint();
				_isStartPoint = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsStartPoint"));
			}
		}

		/// <summary>
		/// 指示是否为结束标志
		/// </summary>
		private bool _isEndPoint;
		public bool IsEndPoint
		{
			get { return _isEndPoint; }
			set
			{
				if (IsStartPoint)
					IsStartPoint = false;

				if (value)
					MenuPanel_SetEndPoint();
				else
					MenuPanel_CancelSetEndPoint();

				_isEndPoint = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsEndPoint"));
			}
		}

		/// <summary>
		/// 指示是否为本平台设定的坐标
		/// </summary>
		private bool _isOwnbyPlatform;
		public bool IsOwnbyPlatform
		{
			get { return _isOwnbyPlatform; }
			private set
			{
				_isOwnbyPlatform = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsOwnbyPlatform"));
			}
		}

		/// <summary>
		/// 指定行进速度
		/// </summary>
		private double _rate;
		public double Rate
		{
			get { return _rate; }
			set
			{
				_rate = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Rate"));
			}
		}

		public event EventHandler<SetStartPointEventArgs> SetStartPointEvent;
		public event EventHandler<SetEndPointEventArgs>  SetEndPointEvent;
		public event EventHandler CancelSetStartPointEvent;
		public event EventHandler CancelSetEndPointEvent;

		private MarkerToolTip _markerToolTip;
		private double _iconHeight = 0;
		private double _iconWidth = 0;
		private bool _isDragging = false;
		private SizeLatLng _dragOffest;

		private void Icon_MouseEnter(object sender, MouseEventArgs e)
		{
			if (!IsStartPoint && !IsEndPoint)
			{
				Icon.Source = _flipedDrawing;
				HighlightMarker(Icon, +Icon.Width / 8, +Icon.Height / 8, 200);

				_markerToolTip =  new MarkerToolTip();
				_markerToolTip.DataContext = this;
				Icon.ToolTip = _markerToolTip;
			}
		}

		private void Icon_MouseLeave(object sender, MouseEventArgs e)
		{
			if (!IsStartPoint && !IsEndPoint)
			{
				Icon.Source = _normalDrawing;
				HighlightMarker(Icon, -Icon.Width / 6, -Icon.Height / 6, 100);
				_markerToolTip = null;
			}
		}

		private void Icon_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			Popup contextMenu = new Popup();
			MarkerContextMenu menuPanel = new MarkerContextMenu();
			contextMenu.Child = menuPanel;
			contextMenu.StaysOpen = false;
			contextMenu.AllowsTransparency = true;
			contextMenu.PopupAnimation = PopupAnimation.Fade;
			contextMenu.Placement = PlacementMode.Mouse;
			contextMenu.IsOpen = true;

			contextMenu.DataContext = this;
		}

		private void MenuPanel_CancelSetStartPoint()
		{
			Icon.Source = _normalDrawing;
			HighlightMarker(Icon, -Icon.Width / 7, -Icon.Width / 7, 0);

			CancelSetStartPointEvent?.Invoke(this, null);
		}

		private void MenuPanel_CancelSetEndPoint()
		{
			Icon.Source = _normalDrawing;
			HighlightMarker(Icon, -Icon.Width / 7, -Icon.Width / 7, 0);

			CancelSetEndPointEvent?.Invoke(this, null);
		}

		private void MenuPanel_SetStartPoint()
		{
			Icon.Source = _startDrawing;
			HighlightMarker(Icon, Icon.Width / 7, Icon.Width / 7, 0);

			Border border = CreateTipShape($"起点： Lat: {Corrdinate_Lat} Lng:{Corrdinate_Lng}",
								Brushes.White,
								new SolidColorBrush(Color.FromArgb(0xFF, 64, 54, 55)),
								Brushes.White);

			SetStartPointEvent?.Invoke(this, new SetStartPointEventArgs(this.Position, border));
		}

		private void MenuPanel_SetEndPoint()
		{
			Icon.Source = _endDrawing;
			HighlightMarker(Icon, Icon.Width / 7, Icon.Width / 7, 0);
			Border border = CreateTipShape($"终点： Lat: {Corrdinate_Lat} Lng:{Corrdinate_Lng}",
								Brushes.White,
								new SolidColorBrush(Color.FromArgb(0xFF, 64, 54, 55)),
								Brushes.White);

			SetEndPointEvent?.Invoke(this, new SetEndPointEventArgs(this.Position, border));
		}

		private Border CreateTipShape(string Text, Brush borderBrush, Brush backgroundBrush, Brush foreground)
		{
			Border border = new Border();

			TextBlock block = new TextBlock();
			block.Text = Text;
			block.HorizontalAlignment = HorizontalAlignment.Left;
			block.VerticalAlignment = VerticalAlignment.Center;
			block.Margin = new Thickness(8);
			block.Foreground = foreground;
			block.FontSize = 13;
			block.FontWeight = FontWeights.Bold;

			border.Child = block;
			border.Background = backgroundBrush;
			border.BorderBrush = borderBrush;
			border.BorderThickness = new Thickness(2);

			return border;
		}

		private void HighlightMarker(Image iamge, double widthInc, double heightInc, int milliseconds)
		{
			Storyboard storyBoard = new Storyboard();

			DoubleAnimation heightAnimation = new DoubleAnimation();

			heightAnimation.From = IconHeight;
			heightAnimation.To = IconHeight + heightInc;
			heightAnimation.Duration = TimeSpan.FromMilliseconds(milliseconds);

			DoubleAnimation widthAnimation = new DoubleAnimation();

			widthAnimation.From = IconWidth;
			widthAnimation.To = IconWidth + widthInc;
			widthAnimation.Duration = TimeSpan.FromMilliseconds(milliseconds);

			ThicknessAnimation marginAnimation = new ThicknessAnimation();

			marginAnimation.From = new Thickness(0);
			marginAnimation.To = new Thickness(-widthInc/4, -heightInc, 0, 0);
			marginAnimation.Duration = TimeSpan.FromMilliseconds(milliseconds);

			Storyboard.SetTargetProperty(heightAnimation, new PropertyPath(Image.HeightProperty));
			storyBoard.Children.Add(heightAnimation);

			Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(Image.WidthProperty));
			storyBoard.Children.Add(widthAnimation);

			Storyboard.SetTargetProperty(marginAnimation, new PropertyPath(Image.MarginProperty));
			storyBoard.Children.Add(marginAnimation);

			storyBoard.Begin(iamge);
		}

		public class SetStartPointEventArgs : EventArgs
		{
			public SetStartPointEventArgs(PointLatLng pos, UIElement shape)
			{
				Tip = new StartTip(pos);
				Tip.Shape = shape;
			}
			public StartTip Tip { get; private set; }
		}

		public class SetEndPointEventArgs : EventArgs
		{
			public SetEndPointEventArgs(PointLatLng pos, UIElement shape)
			{
				Tip = new EndTip(pos);
				Tip.Shape = shape;
			}
			public EndTip Tip { get; private set; }
		}
	}

	public class StartTip : GMapMarker
	{
		public StartTip(PointLatLng pos) : base(pos) { }
	}

	public class EndTip : GMapMarker
	{
		public EndTip(PointLatLng pos) : base(pos) { }
	}
}
