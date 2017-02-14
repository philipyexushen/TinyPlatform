using System;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Globalization;
using System.Timers;
using System.Reflection.Emit;
using System.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DebugHelperNameSpace;
using System.Runtime.InteropServices;
using static TcpClientCore.TcpDatagram;

namespace TcpClientCore
{
	//TODO:解决TCP发送大数据的问题，优化client的结构

	public class TcpDatagram
	{
		/// <summary>
		/// 报文种类
		/// </summary>
		[ComVisible(true)]
		public enum MessageType
		{
			//心跳包检测设施
			PulseFacility = 0,
			//普通报文（非HTML）
			PlainMessage = 2,
			//HTML
			HtmlMessage = 4,
			//声音信息
			VoiceMessage = 8,
			//某设备下线的信息
			DeviceLogOut = 16,
			//某设备上线的信息
			DeviceLogIn = 32,
			//来自服务器的测试
			ServerTest = 64,
			//ACK应答
			ACK = 128,
			//坐标位置
			Coordinate = 256,
			//其他我还没想好的信息
			Other = 512
		}

		/// <summary>
		/// 发送帧头，所以数据前面必须封装帧
		/// </summary>
		//帧头12个字节
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1,Size = 16)]
		[Serializable()]
		public struct DatagramHeaderFrame
		{
			// MessageType类型：
			public MessageType MsgType;

			//一个四个字节的特征码
			public uint FeatureCode;

			//信息来源对应特征码
			public int SourceFeatureCode;

			//用于标识报文的长度，用于校验
			public int MessageLength;
		}

		public static byte[] StructToBytes(object structObj)
		{
			int size = Marshal.SizeOf(structObj);
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.StructureToPtr(structObj, buffer, false);
				byte[] bytes = new byte[size];
				Marshal.Copy(buffer, bytes, 0, size);
				return bytes;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		public static object BytesToStruct(byte[] bytes, Type strcutType)
		{
			int size = Marshal.SizeOf(strcutType);
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.Copy(bytes, 0, buffer, size);
				return Marshal.PtrToStructure(buffer, strcutType);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		/// <summary>
		/// 封装消息和报文
		/// </summary>
		/// <param name="messageType">报文类型</param>
		/// <param name="userNameLength">报文用户名长度</param>
		/// <param name="messageLength">报文长度</param>
		/// <param name="userName">用户名</param>
		/// <param name="message">报文</param>
		/// <param name="encoding">编码器</param>
		/// <returns></returns>
		public static byte[] PackingMessageToBytes
			(MessageType messageType, uint featureCode, int messageLength, byte[] msgBytes)
		{
			int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

			DatagramHeaderFrame frame = new DatagramHeaderFrame();
			frame.MsgType = messageType;
			frame.FeatureCode = featureCode;
			frame.MessageLength = messageLength;

			byte[] header = StructToBytes(frame);

			byte[] datagram = new byte[header.Length + msgBytes.Length];
			header.CopyTo(datagram, 0);
			
			msgBytes.CopyTo(datagram, FrameSize);

			return datagram;
		}

		/// <summary>
		/// 封装消息和报文
		/// </summary>
		/// <param name="headerFrame">报文帧头</param>
		/// <param name="message">报文</param>
		/// <param name="encoding">编码器</param>
		/// <returns></returns>
		public static byte[] PackingMessageToBytes
			(DatagramHeaderFrame headerFrame, byte[] msgBytes)
		{
			int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

			byte[] header = StructToBytes(headerFrame);

			byte[] datagram = new byte[header.Length + msgBytes.Length];
			header.CopyTo(datagram, 0);
			msgBytes.CopyTo(datagram, FrameSize);

			return datagram;
		}

		/// <summary>
		/// 从信息中拆解报文和帧头
		/// </summary>
		/// <param name="datagram">信息</param>
		/// <param name="numberOfRecievedBytes">信息大小</param>
		/// <param name="headerFrame">帧</param>
		/// <param name="recievedByte">报文</param>
		public static bool PrasePacking
			(byte[] datagram, int numberOfRecievedBytes, ref DatagramHeaderFrame headerFrame, ref byte[] recievedByte)
		{
			int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

			byte[] header = new byte[FrameSize];
			recievedByte = new byte[numberOfRecievedBytes - FrameSize];

			Buffer.BlockCopy(datagram, 0, header, 0, FrameSize);

			headerFrame = (DatagramHeaderFrame)BytesToStruct(header, typeof(DatagramHeaderFrame));

			if (numberOfRecievedBytes != FrameSize + headerFrame.MessageLength)
				return false;
			Buffer.BlockCopy(datagram, FrameSize, recievedByte, 0, numberOfRecievedBytes - FrameSize);
			return true;
		}
	}

	/// <summary>
	/// 根据NET.Framework来封装的高性能的客户端
	/// </summary>
	internal class AsyncTcpClient : TcpClient, IDisposable
	{
		private System.Timers.Timer _pulseTimer = new System.Timers.Timer();
		private bool _firstServerACK = false;
		private bool _diposingValue = false;
		private bool _isDataSending = false;

		/// <summary>
		/// 创建一个新的客户端
		/// </summary>
		/// <param name="addresses">目标服务器地址</param>
		/// <param name="port">目标服务器端口</param>
		public AsyncTcpClient(string controlerName, IPAddress[] remoteIpAddresses, int port)
			: base()
		{
			InitAll(controlerName,remoteIpAddresses, port, null);
		}

		/// <summary>
		/// 创建一个新的客户端
		/// </summary>
		/// <param name="addresses">目标服务器地址</param>
		/// <param name="port">目标服务器端口</param>
		/// <param name="localIpEndPoint">本地服务器节点</param>
		/// 
		public AsyncTcpClient(string controlerName, IPAddress[] remoteIpAddresses, int port, IPEndPoint localIpEndPoint)
			:base(localIpEndPoint)
		{
			InitAll(controlerName, remoteIpAddresses, port, localIpEndPoint);

		}

		private void InitAll(string controlerName, IPAddress[] remoteIpAddresses, int port, IPEndPoint localIpEndPoint)
		{
			this.RemoteAddresses = remoteIpAddresses;
			this.RemotePort = port;
			this.LocalIpEndPoint = null;
			this.EncodeType = Encoding.Unicode;
			this.ControlerName = controlerName;

			_pulseTimer.Interval = PulseIntervalTime;
			_pulseTimer.AutoReset = true;
			_pulseTimer.Elapsed += PulseTimer_Elapsed;
		}

		#region Properties
		/// <summary>
		/// 远端服务器的IP地址列表
		/// </summary>
		public IPAddress[] RemoteAddresses { get; private set; }

		/// <summary>
		/// 远程服务器端口
		/// </summary>
		public int RemotePort { get; private set; }

		/// <summary>
		/// 获取socket的心跳包时间
		/// </summary>
		public int PulseIntervalTime { get; private set; } = 10000;

		/// <summary>
		/// 本地客户端节点
		/// </summary>
		public IPEndPoint LocalIpEndPoint { get; private set; }

		/// <summary>
		/// 当前客户端的编码方式
		/// </summary>
		public Encoding EncodeType { get; set; }

		/// <summary>
		/// 当前控制台在服务器的名称
		/// </summary>
		public string ControlerName { get; private set; }

		#endregion Properties

		#region Methods

		/// <summary>
		/// 连接远程服务器
		/// </summary>
		/// <returns></returns>
		public AsyncTcpClient AsyncConnect()
		{
			if (!Connected)
			{
				try
				{
					BeginConnect(RemoteAddresses[0], RemotePort, HandleTcpServerConnected, this);
				}
				catch(Exception e)
				{
					RaiseServerConnectedExceptionEvent(RemoteAddresses, RemotePort,e.Message);
					RaiseReconnectedEvent(RemoteAddresses, RemotePort, e.Message);
				}
			}
			return this;
		}

		/// <summary>
		/// 关闭远程服务器
		/// </summary>
		/// <returns></returns>
		public new AsyncTcpClient Close()
		{
			base.Close();

			_pulseTimer.Stop();
			_pulseTimer.Close();

			RaiseDisConnectedEvent(RemoteAddresses, RemotePort);

			return this;
		}

		private void HandleTcpServerConnected(IAsyncResult result)
		{
			try
			{
				this.EndConnect(result);
				RaiseConnectedEvent(RemoteAddresses, RemotePort);

				//如果连接成功，则重新接受信息
				//如果接受信息失败，立马重连
				byte[] buffer = new byte[ReceiveBufferSize];
				GetStream().BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);

				_pulseTimer.Start();
				WriteData(ControlerName, MessageType.DeviceLogIn);

				//当登陆成功，发送登陆信息	
			}
			catch(Exception e)
			{
				RaiseReconnectedEvent(RemoteAddresses, RemotePort, e.Message);
			}
		}

		private void HandleDatagramReceived(IAsyncResult ar)
		{
			try
			{
				int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

				var stream = GetStream();
				int numberOfRecievedBytes = 0;
				numberOfRecievedBytes = stream.EndRead(ar);

				if (numberOfRecievedBytes == 0)
				{
					RaiseReconnectedEvent(RemoteAddresses, RemotePort, "与服务器意外断开连接");
					_pulseTimer.Stop();
					return;
				}

				DatagramHeaderFrame headerFrame = new DatagramHeaderFrame();
				byte[] datagramBytes = new byte[0];

				byte[] datagramBuffer = (byte[])ar.AsyncState;
				byte[] recievedBytes = new byte[numberOfRecievedBytes];

				Buffer.BlockCopy(datagramBuffer, 0, recievedBytes, 0, numberOfRecievedBytes);

				if (numberOfRecievedBytes >= FrameSize) 
				{
					if (PrasePacking(recievedBytes, numberOfRecievedBytes, ref headerFrame, ref datagramBytes))
					{
						if (headerFrame.MsgType == MessageType.PulseFacility) 
						{
							DebugHelpers.CustomMessageShow(EncodeType.GetString(datagramBytes));
							if (!_firstServerACK)
							{
								_firstServerACK = true;
								RaiseStartupSuccessed(RemoteAddresses, RemotePort);
							}
							
						}
						else if(headerFrame.MsgType == MessageType.ServerTest)
						{
							RaiseDatagramRecievedEvent(this, headerFrame, datagramBytes);
							RaisePlainTextReceivedEvent(this, headerFrame, datagramBytes);
							WriteData("ACK back!", MessageType.ServerTest);
						}
						else if(headerFrame.MsgType == MessageType.PlainMessage)
						{
							RaiseDatagramRecievedEvent(this, headerFrame, datagramBytes);
							RaisePlainTextReceivedEvent(this, headerFrame, datagramBytes);
						}
						else if(headerFrame.MsgType == MessageType.DeviceLogIn)
						{
							RaiseNewUserCommingEvent("", headerFrame.SourceFeatureCode, datagramBytes);
						}
						else if(headerFrame.MsgType == MessageType.Coordinate)
						{
							RaiseNewCoordinateOccuredEvent(headerFrame.SourceFeatureCode, datagramBytes);
						}
					}
				}

				GetStream().BeginRead(datagramBuffer, 0, datagramBuffer.Length, HandleDatagramReceived, datagramBuffer);
			}
			catch (Exception e)
			{
				RaiseServerConnectedExceptionEvent(RemoteAddresses, RemotePort, e.Message);
				RaiseReconnectedEvent(RemoteAddresses, RemotePort, e.Message);
			}
		}

		private void HandleDatagramWritten(IAsyncResult ar)
		{
			try
			{
				this.GetStream().EndWrite(ar);
			}
			catch(Exception e)
			{
				RaiseServerConnectedExceptionEvent(RemoteAddresses, RemotePort, e.Message);
				RaiseReconnectedEvent(RemoteAddresses, RemotePort, e.Message);
			}
		}

		private void PulseTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (!_isDataSending) 
				WriteData("", TcpDatagram.MessageType.PulseFacility);
		}

		private void ConnectingTimeout_Elapsed(object sender, ElapsedEventArgs e)
		{
			RaiseReconnectedEvent(RemoteAddresses, RemotePort, "连接远程服务器超时");
		}

		/// <summary>
		/// 发送报文
		/// </summary>
		/// <param name="bytes">需要发送的报文</param>
		public void WriteData(byte[] bytes, MessageType messageType)
		{
			try
			{
				DatagramHeaderFrame headerFrame = new DatagramHeaderFrame();
				headerFrame.MsgType = messageType;
				headerFrame.MessageLength = bytes.Length;

				byte[] datagram = PackingMessageToBytes(headerFrame, bytes);

				GetStream().BeginWrite(datagram, 0, datagram.Length, HandleDatagramWritten, this);
				_isDataSending = true;
			}
			catch (Exception e)
			{
				RaiseServerConnectedExceptionEvent(RemoteAddresses, RemotePort, e.Message);
				RaiseReconnectedEvent(RemoteAddresses, RemotePort, e.Message);
			}
			finally
			{
				_isDataSending = false;
			}
		}

		/// <summary>
		/// 发送报文
		/// </summary>
		/// <param name="msg">需要发送的报文</param>
		public void WriteData(string msg, MessageType messageType)
		{
			WriteData(EncodeType.GetBytes(msg), messageType);
		}

		public new void Dispose()
		{
			Dispose(true);
		}

		protected override void Dispose(bool disposing)
		{
			if (_diposingValue) 
			{
				if (disposing) 
				{
					base.Dispose(disposing);
					_pulseTimer.Dispose();
				}

				_diposingValue = true;
			}
		}

		#endregion Methods

		#region Events
		/// <summary>
		/// 接收到数据报文事件
		/// </summary>
		public event EventHandler<TcpDatagramReceivedEventArgs<byte[]>> DatagramReceivedEvent;

		/// <summary>
		/// 接受数据明文事件
		/// </summary>
		public event EventHandler<TcpDatagramReceivedEventArgs<string>> PlainTextReceivedEvent;

		/// <summary>
		/// 与服务器已连接事件
		/// </summary>
		public event EventHandler<TcpConnectedEventArgs> ConnectedEvent;

		/// <summary>
		/// 与服务器已断开连接事件
		/// </summary>
		public event EventHandler<TcpDisconnectedEventArgs> DisConnectedEvent;

		/// <summary>
		/// 与服务器的连接发生异常事件
		/// </summary>
		public event EventHandler<TcpServerExceptionOccurredEventArgs> ServerConnectedExceptionEvent;

		/// <summary>
		/// 与服务器的连接重新连接异常事件
		/// </summary>
		public event EventHandler<ReconnectedEventArgs> ReconnectedExceptionEvent;

		/// <summary>
		/// 有一个新的连接端连接到本终端
		/// </summary>
		public event EventHandler<NewUserDetailsArgs> NewUserConnectedEvent;

		/// <summary>
		/// 对应的目标设备发出坐标信息
		/// </summary>
		public event EventHandler<CoordinateInformArgs> NewCoordinateDataOccuredEvent;

		/// <summary>
		/// 开始连接服务器不可达异常事件
		/// </summary>
		public event EventHandler<StartupSucceededEventArgs> StartupSucceededEvent;

		private void RaiseConnectedEvent(IPAddress[] ipAddresses, int port)
		{
			ConnectedEvent?.Invoke(this, new TcpConnectedEventArgs(ipAddresses, port));
		}

		private void RaiseDisConnectedEvent(IPAddress[] ipAddresses, int port)
		{
			//这里同步调用
			_pulseTimer.Elapsed -= PulseTimer_Elapsed;
			_pulseTimer.Stop();
			DisConnectedEvent?.Invoke(this, new TcpDisconnectedEventArgs(ipAddresses, port)); 
		}

		private void RaiseServerConnectedExceptionEvent(IPAddress[] ipAddresses, int port, string exceptionResult)
		{
			_pulseTimer.Elapsed -= PulseTimer_Elapsed;
			_pulseTimer.Enabled = false;
			ServerConnectedExceptionEvent?.Invoke(this,
				new TcpServerExceptionOccurredEventArgs(ipAddresses, port, exceptionResult));
		}

		private void RaiseDatagramRecievedEvent(AsyncTcpClient client, DatagramHeaderFrame header, byte[] bytes)
		{
			TcpDatagramReceivedEventArgs<byte[]> arg = new TcpDatagramReceivedEventArgs<byte[]>(client, header, bytes);
			DatagramReceivedEvent?.BeginInvoke(this, arg, null, null); 
		}

		private void RaisePlainTextReceivedEvent(AsyncTcpClient client, DatagramHeaderFrame header, byte[] datagram)
		{
			TcpDatagramReceivedEventArgs<string> arg 
				= new TcpDatagramReceivedEventArgs<string>(client, header, EncodeType.GetString(datagram));
			PlainTextReceivedEvent?.BeginInvoke(this, arg, null, null);
		}

		private void RaiseReconnectedEvent(IPAddress[] ipAddresses, int port, string reconnectResult)
		{
			_pulseTimer.Elapsed -= PulseTimer_Elapsed;
			_pulseTimer.Stop();

			var arg = new ReconnectedEventArgs(ipAddresses, port, LocalIpEndPoint,reconnectResult);
			ReconnectedExceptionEvent?.Invoke(this, arg);
		}

		private void RaiseStartupSuccessed(IPAddress[] ipAddresses, int port)
		{
			var arg = new StartupSucceededEventArgs(ipAddresses, port);
			StartupSucceededEvent?.Invoke(this, arg);
		}
		
		private void RaiseNewUserCommingEvent(string ip, int sourceFeatureCode, byte[] userName)
		{
			var arg = new NewUserDetailsArgs(ip, sourceFeatureCode, EncodeType.GetString(userName));
			NewUserConnectedEvent?.Invoke(this, arg);
		}

		private void RaiseNewCoordinateOccuredEvent(int sourceFeatureCode, byte[] coordinate)
		{
			var arg = new CoordinateInformArgs(sourceFeatureCode);
			if(arg.TryPraseCoordinate(EncodeType.GetString(coordinate)))
			{
				NewCoordinateDataOccuredEvent?.BeginInvoke(this, arg, null, null);
			}
		}

		#endregion Events
	}

	/// <summary>
	/// 接收到数据报文事件参数
	/// </summary>
	/// <typeparam name="T">报文类型</typeparam>
	public class TcpDatagramReceivedEventArgs<T> :EventArgs
	{
		public TcpDatagramReceivedEventArgs(TcpClient client, DatagramHeaderFrame frame, T datagram)
		{
			this.tcpClient = client;
			this.datagram = datagram;
			this.datagramFrame = frame;
		}

		public DatagramHeaderFrame datagramFrame { get; set; }
		public TcpClient tcpClient { get; private set; }
		public T datagram { get; private set; }
	}

	/// <summary>
	/// 与服务器的连接发生事件参数
	/// </summary>
	public class TcpConnectedEventArgs : EventArgs
	{
		public TcpConnectedEventArgs
			(IPAddress[] addresses, int port)
		{
			this.Addresses = addresses;
			this.Port = port;
		}

		/// <summary>
		/// 目标服务器的地址
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器的端口
		/// </summary>
		public int Port { get; private set; }
	}

	/// <summary>
	/// 与服务器的连接关闭时发生事件参数
	/// </summary>
	public class TcpDisconnectedEventArgs : EventArgs
	{
		public TcpDisconnectedEventArgs
			(IPAddress[] addresses, int port)
		{
			this.Addresses = addresses;
			this.Port = port;
		}

		/// <summary>
		/// 目标服务器的地址
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器的端口
		/// </summary>
		public int Port { get; private set; }
	}

	/// <summary>
	/// 与服务器的连接发生异常时的异常参数
	/// </summary>
	public class TcpServerExceptionOccurredEventArgs : EventArgs
	{
		public TcpServerExceptionOccurredEventArgs
			(IPAddress[] addresses, int port, string exceptionMessgae)
		{
			this.Addresses = addresses;
			this.Port = port;
			this.ExceptionMessage = exceptionMessgae;
		}

		/// <summary>
		/// 目标服务器的地址
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器的端口
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// 与远程服务器发生的异常的原因
		/// </summary>
		public string ExceptionMessage { get; private set; }
	}

	/// <summary>
	/// 与服务器的连接发生重连时的异常参数
	/// </summary>
	/// <param name="addresses">目标地址</param>
	/// <param name="port">远程端口</param>
	/// <param name="exceptionMessgae">重连原因</param>
	/// <param name="currentTrial">当前尝试重连次数</param>
	/// <param name="maxTrial">最大重连次数</param>
	public class ReconnectedEventArgs : EventArgs
	{
		public ReconnectedEventArgs (IPAddress[] addresses, int port, IPEndPoint ipEndPoint, string message)
		{
			this.Addresses = addresses;
			this.Port = port;
			this.ExceptionMessage = message;
			this.IpEndPoint = ipEndPoint;
		}

		/// <summary>
		/// 目标服务器的地址
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器的端口
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// 本地节点
		/// </summary>
		public IPEndPoint IpEndPoint { get; private set; }

		/// <summary>
		/// 与远程服务器发生的异常的原因
		/// </summary>
		public string ExceptionMessage { get; private set; }
	}

	/// <summary>
	/// 与服务器的连接发生重连时的异常参数
	/// </summary>
	/// <param name="addresses">目标地址</param>
	/// <param name="port">远程端口</param>
	/// <param name="exceptionMessgae">重连原因</param>
	/// <param name="currentTrial">当前尝试重连次数</param>
	/// <param name="maxTrial">最大重连次数</param>
	public class StartupSucceededEventArgs : EventArgs
	{
		public StartupSucceededEventArgs(IPAddress[] addresses, int port)
		{
			this.Addresses = addresses;
			this.Port = port;
		}

		/// <summary>
		/// 目标服务器的地址
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器的端口
		/// </summary>
		public int Port { get; private set; }
	}

	/// <summary>
	/// 有新的设备连入信息
	/// </summary>
	public class NewUserDetailsArgs : EventArgs
	{
		public NewUserDetailsArgs(string ip, int sourceFeatureCode, string userName)
		{
			this.Ip = ip;
			this.SourceFeatureCode = sourceFeatureCode;
			this.UserName = userName;
		}

		public string Ip { get; private set; }
		public int SourceFeatureCode { get; private set; }
		public string UserName { get; private set; }
	}

	/// <summary>
	/// 坐标信息
	/// </summary>
	public class CoordinateInformArgs :EventArgs
	{
		public CoordinateInformArgs(int sourceFeatureCode)
		{
			this.SourceFeatureCode = sourceFeatureCode;
		}

		public bool TryPraseCoordinate(string messgae)
		{
			char[] charsToTrim = { ' ', ',' };
			string[] strs = messgae.Split();

			double lat, lng;
			if (!double.TryParse(strs[0].TrimEnd(charsToTrim),out lat))
				return false;

			if (!double.TryParse(strs[1].TrimEnd(charsToTrim), out lng))
				return false;

			this.Lat = lat;
			this.Lng = lng;
			return true;
		}

		public int SourceFeatureCode { get; private set; }
		public double Lat { get; private set; }
		public double Lng { get; private set; }
	}

}