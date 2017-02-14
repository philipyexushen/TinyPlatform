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
		public ClientMessageEventArgs(string msg)                                                           
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

	/// <summary>
	/// AsyncTcpClientHelper给TcpClientCore做信息包装，外部只暴露信息传输的事件接口
	/// 可定义各种其他的信息处理方式
	/// </summary>
	public class AsyncTcpClientHelper : IDisposable
	{
		public event EventHandler<ClientMessageEventArgs> DataOccuredEvent;
		public event EventHandler<ClientMessageEventArgs> ConnectionToServerFailedEvent;
		public event EventHandler<NewUserDetailsArgs> NewUserConnectedEvent;
		public event EventHandler<TransformedCoordinateArgs> NewCoordinateOccuredEvent;

		/// <summary>
		/// 当与服务器丢失连接时尝试重连的次数
		/// </summary>
		public uint MaxReconnectionRetries { get; set; } = 10;

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		public AsyncTcpClientHelper()
		{

		}

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		/// <param name="remoteIpEndPoint">远程服务器节点</param>
		public AsyncTcpClientHelper(string controlerName, IPEndPoint remoteIpEndPoint)
			: this(controlerName, new IPAddress[] { remoteIpEndPoint.Address }, remoteIpEndPoint.Port)
		{

		}

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		/// <param name="address">远程主机地址</param>
		/// <param name="port">远程服务器端口</param>
		public AsyncTcpClientHelper(string controlerName, IPAddress remoteIpaddress, int port)
			: this(controlerName, new IPAddress[] { remoteIpaddress }, port)
		{

		}

		/// <summary>
		/// 创建一个新的客户端创建一个新的客户端助手
		/// </summary>
		/// <param name="addresses">目标服务器地址</param>
		/// <param name="port">目标服务器端口</param>
		public AsyncTcpClientHelper(string controlerName, IPAddress[] remoteIpAddresses, int port)
		{
			_clientCore = new AsyncTcpClient(controlerName, remoteIpAddresses, port);
			InitEventHandler();
		}

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		/// <param name="remoteIpEndPoint">远程服务器节点</param>
		/// <param name="localIpEndPoint">本地客户端节点</param>
		public AsyncTcpClientHelper(string controlerName, IPEndPoint remoteIpEndPoint, IPEndPoint localIpEndPoint)
			: this(controlerName, new IPAddress[] { remoteIpEndPoint.Address }, remoteIpEndPoint.Port, localIpEndPoint)
		{
		}

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		/// <param name="address">主机地址</param>
		/// <param name="port">远程服务器端口</param>
		/// <param name="localIpEndPoint">本地服务器节点</param>
		public AsyncTcpClientHelper(string controlerName, IPAddress remoteIpaddress, int port, IPEndPoint localIpEndPoint)
			: this(controlerName, new IPAddress[] { remoteIpaddress }, port, localIpEndPoint)
		{

		}

		/// <summary>
		/// 创建一个新的客户端助手
		/// </summary>
		/// <param name="addresses">目标服务器地址</param>
		/// <param name="port">目标服务器端口</param>
		/// <param name="localIpEndPoint">本地服务器节点</param>
		public AsyncTcpClientHelper(string controlerName, IPAddress[] remoteIpAddresses, int port, IPEndPoint localIpEndPoint)
		{
			_clientCore = new AsyncTcpClient(controlerName,remoteIpAddresses, port, localIpEndPoint);
			InitEventHandler();
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

		public void StartTcpClientWithHostName(object objArg)
		{
			HostArg arg = (HostArg)objArg;
			try
			{
				IPHostEntry ipHostEntrEntry = Dns.GetHostEntry(arg.HostName);//直接同步，反正已经在线程里面了

				if (arg.LocalIpEndPoint != null)
					_clientCore = new AsyncTcpClient(arg.ControlerName, ipHostEntrEntry.AddressList, arg.Port, arg.LocalIpEndPoint);
				else
					_clientCore = new AsyncTcpClient(arg.ControlerName, ipHostEntrEntry.AddressList, arg.Port);

				InitEventHandler();
				this.StartConnect();
			}
			catch (Exception e)
			{
				ConnectionToServerFailedEvent?.BeginInvoke(this, createDnsFailedEvent(e, arg), null, this);
			}
		}

		/// <summary>
		/// 异步打开连接
		/// </summary>
		public void StartConnect()
		{
			_clientCore.AsyncConnect();

			_connecting = true;
			_connectionCheckTimer.Start();
			_lastConnectionTime = DateTime.Now;
		}

		/// <summary>
		/// 设定启动超时时间
		/// </summary>
		public int StartupTimeout { get; set; } = 30000;

		/// <summary>
		/// 关闭并且释放client的资源
		/// </summary>
		public void Close() { _clientCore.Close(); }

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
		public bool CanWrite { get { return _clientCore.Connected; } }

		/// <summary>
		/// 获取用户名
		/// </summary>
		public string Name { get { return _clientCore.ControlerName; } }

		private AsyncTcpClient _clientCore;
		private uint _nowRetries = 0;
		private bool _connecting = false;

		//check时钟专门拿来清除死链接用的
		private System.Timers.Timer _connectionCheckTimer = new System.Timers.Timer();
		private DateTime _lastConnectionTime;

		private void InitEventHandler()
		{
			_clientCore.PlainTextReceivedEvent        += clientCore_PlaintextReceived;
			_clientCore.ConnectedEvent                += clientCore_ServerConnected;
			_clientCore.DisConnectedEvent             += clientCore_ServerDisconnected;
			_clientCore.ServerConnectedExceptionEvent += clientCore_ServerExceptionOccurred;
			_clientCore.ReconnectedExceptionEvent     += clientCore_Reconnected;
			_clientCore.StartupSucceededEvent		  += clientCore_ConnectionSucceeded;
			_clientCore.NewUserConnectedEvent         += clientCore_NewUserComming;
			_clientCore.NewCoordinateDataOccuredEvent += clientCore_NewCoordinateOccured;
			_clientCore.EncodeType                     = UnicodeEncoding.GetEncoding(0);

			_connectionCheckTimer.Interval = StartupTimeout;
			_connectionCheckTimer.Elapsed += _connectionCheckTimer_Elapsed;
		}

		private void clientCore_ServerExceptionOccurred(object sender, TcpServerExceptionOccurredEventArgs e)
		{
			string message = $"####异常：{e.ExceptionMessage} ";
			_clientCore.Close();
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(message));
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void clientCore_ServerConnected(object sender, TcpConnectedEventArgs e)
		{
			string message = $"####成功连接 ({e.Addresses[0]}:{e.Port}) ...";
			DataOccuredEvent?.Invoke(this,  new ClientMessageEventArgs(message));
			_connecting = false;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void clientCore_ServerDisconnected(object sender, TcpDisconnectedEventArgs e)
		{
			_connecting = false;
			
			string message = $"~####与远程服务器{e.Addresses[0]}断开连接";

			_clientCore.Dispose();
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs(message));
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void clientCore_Reconnected(object sender, ReconnectedEventArgs e)
		{
			if (!_connecting) 
			{
				_connecting = true;
				_nowRetries++;

				string message 
					= $"####正在与远程服务器 {e.Addresses[0]} 重新连接"
					  + $"（次数: {_nowRetries}/{MaxReconnectionRetries}）\n    重连原因：{e.ExceptionMessage}";

				var remoteAddresses = e.Addresses;
				var port = e.Port;
				var localIPEndPoint = e.IpEndPoint;
				var controlerName = _clientCore.ControlerName;

				DataOccuredEvent?.BeginInvoke(this, new ClientMessageEventArgs(message), null, null);

				if (_nowRetries != MaxReconnectionRetries)
				{
					if (localIPEndPoint != null)
						_clientCore = new AsyncTcpClient(controlerName, remoteAddresses, port, localIPEndPoint);
					else
						_clientCore = new AsyncTcpClient(controlerName, remoteAddresses, port);

					var timeInterval = DateTime.Now - _lastConnectionTime;
					if (timeInterval < TimeSpan.FromSeconds(5)) 
						Thread.Sleep(TimeSpan.FromSeconds(5) - timeInterval);

					InitEventHandler();
					this.StartConnect();
				}
				else
				{
					_connectionCheckTimer.Stop();
					_connectionCheckTimer.Close();

					var errorMessage = $"###与远程服务器 {remoteAddresses} 连接失败";
					DataOccuredEvent?.BeginInvoke(this, new ClientMessageEventArgs(errorMessage), null, null);
				}
			}
		}

		private void _connectionCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			var remoteAddresses = _clientCore.RemoteAddresses;
			var port = _clientCore.RemotePort;
			var localIPEndPoint = _clientCore.LocalIpEndPoint;
			clientCore_ServerExceptionOccurred(this, new TcpServerExceptionOccurredEventArgs(remoteAddresses, port, "与服务器连接超时"));

			_connectionCheckTimer.Stop();
		}

		private void clientCore_ConnectionSucceeded(object sender, StartupSucceededEventArgs e)
		{
			_connecting = false;
			_nowRetries = 0;

			_connectionCheckTimer.Stop();
		}

		private void clientCore_PlaintextReceived(object sender, TcpDatagramReceivedEventArgs<string> e)
		{
			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs($"{e.datagram}"));
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void clientCore_NewUserComming(object sender, NewUserDetailsArgs e)
		{
			NewUserConnectedEvent?.Invoke(this, e);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void clientCore_NewCoordinateOccured(object sender, CoordinateInformArgs e)
		{
			//PointLatLng point = CoordinateTransformer.transformFromGCJToWGS(new PointLatLng(e.Lat, e.Lng));
			PointLatLng point = new PointLatLng(e.Lat, e.Lng);
			string s = string.Empty;
			s += "经纬度： ";
			s += "Lag: " + point.Lat.ToString();
			s += "Lng: " + point.Lng.ToString();

			DataOccuredEvent?.Invoke(this, new ClientMessageEventArgs($"{s}"));
			NewCoordinateOccuredEvent?.Invoke(this, new TransformedCoordinateArgs(point,e.SourceFeatureCode));
			DebugHelpers.CustomMessageShow($"(Lag : Lng)转换前:{e.Lat}:{e.Lng}  转换后{point.Lat}:{point.Lng}");
		}

		private ClientMessageEventArgs createDnsFailedEvent(Exception e, HostArg arg)
		{
			ClientMessageEventArgs eventArg = new ClientMessageEventArgs();
			eventArg.AddDateTimeHeaderToMessage();
			eventArg.AppendDataMessage(string.Format("    解析地址 ( {0}:{1} ) 发生异常 !\n", arg.HostName, arg.Port));

			if (e is SocketException)
			{
				SocketException socketException = e as SocketException;
				eventArg.AppendDataMessage(string.Format("    错误码: {0}\n    错误原因: {1}",
					socketException.ErrorCode, socketException.Message));
			}
			else if (e is ArgumentException)
			{
				ArgumentException argumentException = e as ArgumentException;
				eventArg.AppendDataMessage(string.Format("    参数错误: {0}",
						argumentException.HResult));
			}

			return eventArg;
		}

		#region IDisposable Support
		private bool _disposedValue = false; // 要检测冗余调用

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_connectionCheckTimer.Stop();
					_connectionCheckTimer.Close();
					_connectionCheckTimer.Dispose();
				}

				_disposedValue = true;
			}
		}

		// TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
		// ~AsyncTcpClientHelper() {
		//   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
		//   Dispose(false);
		// }

		// 添加此代码以正确实现可处置模式。
		public void Dispose()
		{
			// 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
			Dispose(true);
			// TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
