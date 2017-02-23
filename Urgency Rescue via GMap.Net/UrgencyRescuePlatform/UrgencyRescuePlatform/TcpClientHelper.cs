using System;
using System.Net;
using System.Text;
using TcpClientCore;
using DebugHelperNameSpace;
using System.Net.Sockets;
using System.Timers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using WGS84_GCJ0_Transform;
using GMap.NET;
using System.Windows;

namespace TcpClientHelper
{
	public class ClientMessageEventArgs : EventArgs
	{
		/// <summary>
		/// 创建一个被Helper修饰的消息
		/// </summary>
		public ClientMessageEventArgs()
		{

		}

		/// <summary>
		/// 创建一个被Helper修饰的消息
		/// </summary>
		/// <param name="msg">指定消息</param>
		public ClientMessageEventArgs(int source, string msg)                                                           
		{
			DataMsg = msg;
		}

		/// <summary>
		/// 给消息添加本地时间
		/// </summary>
		public string AddDateTimeToMessage()
		{
			DataMsg += DateTime.Now;
			return DataMsg;
		}

		/// <summary>
		/// 给消息添加本地消息头
		/// </summary>
		public string AddDateTimeHeaderToMessage()
		{
			DataMsg += "------" + DateTime.Now + "------\n";
			return DataMsg;
		}

		public string AppendDataMessage(string message)
		{
			DataMsg += message;
			return DataMsg;
		}
		public string DataMsg { get; private set; }

		public int Source { get; private set; }
	}

	public class TransformedCoordinateArgs : EventArgs
	{
		/// <summary>
		/// 创建一个新的TransformedCoordinateArgs
		/// </summary>
		/// <param name="lat">转换后的lat</param>
		/// <param name="lng">转换后的lng</param>
		public TransformedCoordinateArgs(double lat, double lng, int sourceFeatureCode)
		{
			Point = new PointLatLng(lat, lng);
			SourceFeatureCode = sourceFeatureCode;
		}

		/// <summary>
		/// 创建一个新的TransformedCoordinateArgs
		/// </summary>
		/// <param name="point">转换后的point</param>
		public TransformedCoordinateArgs(PointLatLng transformedPoint, int sourceFeatureCode)
		{
			Point = transformedPoint;
			SourceFeatureCode = sourceFeatureCode;
		}

		/// <summary>
		/// 转换后的坐标点
		/// </summary>
		public PointLatLng Point { get; private set; }
		public int SourceFeatureCode { get; set; }
	}

	public class HelpEventArgs :EventArgs
	{
		/// <summary>
		/// 创建一个新的HelpEventArgs
		/// </summary>
		/// <param name="lat">转换后的lat</param>
		/// <param name="lng">转换后的lng</param>
		public HelpEventArgs(double lat, double lng, int sourceFeatureCode)
		{
			Point = new PointLatLng(lat, lng);
			SourceFeatureCode = sourceFeatureCode;
		}

		/// <summary>
		/// 创建一个新的HelpEventArgs
		/// </summary>
		/// <param name="point">转换后的point</param>
		public HelpEventArgs(PointLatLng transformedPoint, int sourceFeatureCode)
		{
			Point = transformedPoint;
			SourceFeatureCode = sourceFeatureCode;
			IsCoordinate = true;
		}

		/// <summary>
		/// 创建一个新的HelpEventArgs
		/// </summary>
		/// <param name="point">转换后的point</param>
		public HelpEventArgs(string message, int sourceFeatureCode)
		{
			this.Message = message;
			SourceFeatureCode = sourceFeatureCode;
			IsCoordinate = false;
		}

		/// <summary>
		/// 转换后的坐标点
		/// </summary>
		public PointLatLng Point { get; private set; }

		public bool IsCoordinate { get; private set; }
		public string Message { get; private set; }

		public int SourceFeatureCode { get; set; }
	} 

	/// <summary>
	/// AsyncTcpClientHelper给TcpClientCore做信息包装，外部只暴露信息传输的事件接口
	/// 可定义各种其他的信息处理方式
	/// </summary>
	public class AsyncTcpClientHelper
	{
		public event EventHandler<ClientMessageEventArgs> DataOccuredEvent;
		public event EventHandler<ClientMessageEventArgs> DisconnecEvent;
		public event EventHandler<ClientMessageEventArgs> ConenctedSuccessfulEvent;
		public event EventHandler<NewUserDetailsArgs> NewUserConnectedEvent;
		public event EventHandler<UserLogoutArgs> UserLogoutEvent;
		public event EventHandler<TransformedCoordinateArgs> NewCoordinateOccuredEvent;
		public event EventHandler<HelpEventArgs> HelpEvent;

		public bool IsAllowAutoReconnect { get; set; } = true;

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		public AsyncTcpClientHelper()
		{

		}

		public struct HostArg
		{
			public string HostName { get; set; }
			public int Port { get; set; }
			public IPEndPoint LocalIpEndPoint { get; set; }
			public string ControlerName { get; set; }

			public HostArg(string controlerName, string hostName, int port, IPEndPoint IPEndPoint)
			{
				this.HostName = hostName;
				this.Port = port;
				this.LocalIpEndPoint = IPEndPoint;
				this.ControlerName = controlerName;
			}
		}

		public HostArg Arg { get; private set; }
		public IPHostEntry IpHostEntrEntry { get; private set; }

		public void StartTcpClientWithHostName(object objArg)
		{
			Arg = (HostArg)objArg;
			try
			{
				IpHostEntrEntry = Dns.GetHostEntry(Arg.HostName);//直接同步，反正已经在线程里面了
				this.StartConnect();
			}
			catch (Exception e)
			{
				DisconnecEvent?.BeginInvoke(this, createDnsFailedEvent(e, Arg), null, this);
			}
		}

		/// <summary>
		/// 异步打开连接
		/// </summary>
		public void StartConnect()
		{
			if (Arg.LocalIpEndPoint != null)
				_clientCore = new AsyncTcpClient(Arg.ControlerName, IpHostEntrEntry.AddressList, Arg.Port, Arg.LocalIpEndPoint);
			else
				_clientCore = new AsyncTcpClient(Arg.ControlerName, IpHostEntrEntry.AddressList, Arg.Port);
			InitEventHandler();

			_clientCore.AsyncConnect();
			_connectionCheckTimer.Start();
		}

		/// <summary>
		/// 设定启动超时时间
		/// </summary>
		public int StartupTimeout { get; set; } = 5000;

		/// <summary>
		/// 设定重新连接时间
		/// </summary>
		public int ReconnectTime { get; set; } = 3000;

		/// <summary>
		/// 关闭并且释放client的资源
		/// </summary>
		public void Disconnect()
		{
			IsAllowAutoReconnect = false;
			_connectionCheckTimer.Elapsed -= _connectionCheckTimer_Elapsed;

			if (_clientCore != null)
			{
				_clientCore.PlainTextReceivedEvent -= clientCore_PlaintextReceived;
				_clientCore.ConnectedEvent -= clientCore_ServerConnected;
				//_clientCore.DisConnectedEvent -= clientCore_ServerDisconnected;
				_clientCore.ServerConnectedExceptionEvent -= clientCore_ServerExceptionOccurred;
				_clientCore.StartupSucceededEvent -= clientCore_ConnectionSucceeded;
				_clientCore.NewUserConnectedEvent -= clientCore_NewUserComming;
				_clientCore.NewCoordinateDataOccuredEvent -= clientCore_NewCoordinateOccured;
				_clientCore.HelpEvent -= _clientCore_HelpEvent;
				_clientCore.UserLogoutEvent -= _clientCore_UserLogoutEvent;

				_clientCore.DisConnectClient();
			}
		}

		/// <summary>
		/// 异步写入数据
		/// </summary>
		public void WriteData(object sender, ClientMessageEventArgs e)
		{
			if (CanWrite)
				_clientCore.WriteData(e.DataMsg, TcpDatagram.MessageType.PlainMessage);
		}

		/// <summary>
		/// 查看能否写入数据
		/// </summary>
		public bool CanWrite { get { return _clientCore.IsConnected; } }

		/// <summary>
		/// 获取用户名
		/// </summary>
		public string Name { get { return _clientCore.ControlerName; } }

		private AsyncTcpClient _clientCore;

		private System.Timers.Timer _connectionCheckTimer;
		private System.Timers.Timer _reconnectTimer;

		private void InitEventHandler()
		{
			_clientCore.PlainTextReceivedEvent        += clientCore_PlaintextReceived;
			_clientCore.ConnectedEvent                += clientCore_ServerConnected;
			_clientCore.DisConnectedEvent             += clientCore_ServerDisconnected;
			_clientCore.ServerConnectedExceptionEvent += clientCore_ServerExceptionOccurred;
			_clientCore.StartupSucceededEvent		  += clientCore_ConnectionSucceeded;
			_clientCore.NewUserConnectedEvent         += clientCore_NewUserComming;
			_clientCore.NewCoordinateDataOccuredEvent += clientCore_NewCoordinateOccured;
			_clientCore.HelpEvent					  += _clientCore_HelpEvent;
			_clientCore.UserLogoutEvent				  += _clientCore_UserLogoutEvent;
			_clientCore.EncodeType                     = UnicodeEncoding.GetEncoding(0);
			
			_connectionCheckTimer = new System.Timers.Timer();
			_connectionCheckTimer.Interval = StartupTimeout;
			_connectionCheckTimer.Elapsed += _connectionCheckTimer_Elapsed;
		}

		private void _clientCore_UserLogoutEvent(object sender, UserLogoutArgs e)
		{
			UserLogoutEvent?.Invoke(this, e);
		}

		private void clientCore_ServerExceptionOccurred(object sender, TcpServerExceptionOccurredEventArgs e)
		{
			_connectionCheckTimer.Stop();
			_connectionCheckTimer.Close();

			string message = $">>>异常：{e.ExceptionMessage} ";
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(-1,message));
		}

		private void clientCore_ServerConnected(object sender, TcpConnectedEventArgs e)
		{
			string message = $">>>成功连接 ({e.Addresses[0]}:{e.Port}) ...";
			ConenctedSuccessfulEvent?.Invoke(this,  new ClientMessageEventArgs(-1, message));

			_connectionCheckTimer.Stop();
			_connectionCheckTimer.Close();
		}

		private void clientCore_ServerDisconnected(object sender, TcpDisconnectedEventArgs e)
		{
			string message = $">>>与远程服务器{e.Addresses[0]}断开连接";
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(-1,message));

			if (IsAllowAutoReconnect)
				StartReconnect();
			else
			{
				_clientCore.DisConnectedEvent -= clientCore_ServerDisconnected;
				DisconnecEvent?.Invoke(this, new ClientMessageEventArgs());
				_clientCore = null;
			}
		}

		private void StartReconnect()
		{
			string message = $">>>{ReconnectTime / 1000}秒后重新连接服务器";
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(-1, message));

			_reconnectTimer = new System.Timers.Timer();

			_reconnectTimer.Interval = ReconnectTime;
			_reconnectTimer.AutoReset = false;
			_reconnectTimer.Elapsed += _reconnectTimer_Elapsed;

			_reconnectTimer.Start();
		}

		private void _reconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			_reconnectTimer.Close();
			StartConnect();
		}

		private void _connectionCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (!_clientCore.IsConnected)
			{
				var addresses = _clientCore.Addresses;
				var port = _clientCore.Port;
				TcpServerExceptionOccurredEventArgs arg = new TcpServerExceptionOccurredEventArgs(addresses, port, "与服务器连接超时");
				clientCore_ServerExceptionOccurred(this, arg);

				_connectionCheckTimer.Stop();
			}
		}

		private void clientCore_ConnectionSucceeded(object sender, StartupSucceededEventArgs e)
		{
			_connectionCheckTimer.Stop();
		}

		private void clientCore_PlaintextReceived(object sender, TcpDatagramReceivedEventArgs<string> e)
		{
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(e.Source, $"{e.datagram}"));
		}

		private void clientCore_NewUserComming(object sender, NewUserDetailsArgs e)
		{
			NewUserConnectedEvent?.Invoke(this, e);
		}

		private void clientCore_NewCoordinateOccured(object sender, CoordinateInformArgs e)
		{
			//PointLatLng point = CoordinateTransformer.transformFromGCJToWGS(new PointLatLng(e.Lat, e.Lng));
			PointLatLng point = new PointLatLng(e.Lat, e.Lng);
			string s = string.Empty;
			s += "经纬度： ";
			s += "Lag: " + point.Lat.ToString();
			s += "Lng: " + point.Lng.ToString();

			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(e.SourceFeatureCode, $"{s}"));
			NewCoordinateOccuredEvent?.Invoke(this, new TransformedCoordinateArgs(point,e.SourceFeatureCode));
			DebugHelpers.CustomMessageShow($"(Lag : Lng)转换前:{e.Lat}:{e.Lng}  转换后{point.Lat}:{point.Lng}");
		}

		private void _clientCore_HelpEvent(object sender, HelpInformationArgs e)
		{
			PointLatLng point = new PointLatLng(e.Lat, e.Lng);
			string s = string.Empty;
			s += $">>>突发一处警报！{DateTime.Now:s}";

			if (e.IsCoordinate)
			{
				s += "\n>>>经纬度： ";
				s += "Lag: " + point.Lat.ToString() + " ";
				s += "Lng: " + point.Lng.ToString();

				if (e.Message != "")
					s += $"\n>>>附加信息： {e.Message}";

				HelpEvent?.Invoke(this, new HelpEventArgs(point, e.SourceFeatureCode));
			}
			else
			{
				s += $"\n>>>附加信息： {e.Message}";
				HelpEvent?.Invoke(this, new HelpEventArgs(e.Message, e.SourceFeatureCode));
			}
				
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(e.SourceFeatureCode,$"{s}"));

		}

		private ClientMessageEventArgs createDnsFailedEvent(Exception e, HostArg arg)
		{
			ClientMessageEventArgs eventArg = new ClientMessageEventArgs();
			eventArg.AddDateTimeHeaderToMessage();
			eventArg.AppendDataMessage($"    解析地址 ( {arg.HostName}:{arg.Port} ) 发生异常 !\n");

			if (e is SocketException)
			{
				SocketException socketException = e as SocketException;
				eventArg.AppendDataMessage($"    错误码: {socketException.ErrorCode}\n    错误原因: {socketException.Message}");
			}
			else if (e is ArgumentException)
			{
				ArgumentException argumentException = e as ArgumentException;
				eventArg.AppendDataMessage($"    参数错误: {argumentException.HResult}");
			}

			return eventArg;
		}
	}
}
