using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Timers;
using DebugHelperNameSpace;
using System.Collections;
using System.Runtime.InteropServices;
using static TcpClientCore.TcpDatagram;
using System.Collections.Generic;

namespace TcpClientCore
{
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
			//求救信息
			Help = 512,
			//ACK应答心跳包
			ACKPulse = 1024,
			//其他我还没想好的信息
			Other = 2048
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
		public static void PrasePacking
			(byte[] datagram, int numberOfRecievedBytes, ref DatagramHeaderFrame headerFrame, ref byte[] recievedByte)
		{
			int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

			byte[] header = new byte[FrameSize];
			recievedByte = new byte[numberOfRecievedBytes - FrameSize];

			PraseHeader(datagram, ref headerFrame);
			Buffer.BlockCopy(datagram, FrameSize, recievedByte, 0, numberOfRecievedBytes - FrameSize);
		}

		/// <summary>
		/// 拆解帧头
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="headerFrame"></param>
		public static void PraseHeader(byte[] bytes, ref DatagramHeaderFrame headerFrame)
		{
			int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

			byte[] header = new byte[FrameSize];
			Buffer.BlockCopy(bytes, 0, header, 0, FrameSize);

			headerFrame = (DatagramHeaderFrame)BytesToStruct(header, typeof(DatagramHeaderFrame));
		}
	}

	/// <summary>
	/// 根据NET.Framework来封装的高性能的客户端
	/// </summary>
	internal class AsyncTcpClient
	{
		private TcpClient _client;
		private Timer _pulseTimer;

		private bool _isACKChecked = false, _isWriting = false;

		private bool _firstServerACK = false;
		bool _waitingForWholeData = false;
		int _targetLength = 0;
		DatagramHeaderFrame _headerFrame = new DatagramHeaderFrame();

		//recieved byte为接受数据的整体缓冲，每次建立大小为messageLength大小
		private List<byte> _currentBytes = new List<byte>();
		private bool _isReading = false;
		private int _readCount = 0;

		//这个值拿来指示是否需要心跳包
		private int _lastReadCountBeforePulse = 0;
		private bool _isForcePulse = false;

		private void incReadCount()
		{
			if (_readCount == int.MaxValue)
				_readCount = 0;
			_readCount++;
		} 

		/// <summary>
		/// 创建一个新的客户端
		/// </summary>
		/// <param name="addresses">目标服务器地址</param>
		/// <param name="port">目标服务器端口</param>
		public AsyncTcpClient(string controlerName, IPAddress[] remoteIpAddresses, int port)
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
		{
			InitAll(controlerName, remoteIpAddresses, port, localIpEndPoint);
		}

		private void InitAll(string controlerName, IPAddress[] remoteIpAddresses, int port, IPEndPoint localIpEndPoint)
		{
			this.Addresses = remoteIpAddresses;
			this.Port = port;
			this.LocalIpEndPoint = null;
			this.EncodeType = Encoding.Unicode;
			this.ControlerName = controlerName;
		}

		#region Properties
		/// <summary>
		/// 远端服务器的IP地址列表
		/// </summary>
		public IPAddress[] Addresses { get; private set; }

		/// <summary>
		/// 远程服务器端口
		/// </summary>
		public int Port { get; private set; }

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

		public bool IsConnected { get; private set; }

		#endregion Properties

		#region Methods

		private void StartPulseTimer()
		{
			_pulseTimer = new Timer();

			_pulseTimer.Interval = PulseIntervalTime;
			_pulseTimer.AutoReset = true;
			_pulseTimer.Elapsed += PulseTimer_Elapsed;
			_pulseTimer.Start();
		}

		private void StopPulseTimer()
		{
			if (_pulseTimer != null)
			{
				_pulseTimer.AutoReset = false;
				_pulseTimer.Elapsed -= PulseTimer_Elapsed;
				_pulseTimer.Stop();
			}
		}

		/// <summary>
		/// 连接远程服务器
		/// </summary>
		/// <returns></returns>
		public bool AsyncConnect()
		{
			_client = new TcpClient();

			bool isValid = false;
			if (!IsConnected)
			{
				try
				{
					_client.BeginConnect(Addresses[0], Port, HandleTcpServerConnected, this);
					isValid = true;
				}
				catch(Exception e)
				{
					RaiseServerConnectedExceptionEvent(Addresses, Port, e.Message);
					RaiseDisConnectedEvent(Addresses, Port);
				}
			}
			return isValid;
		}

		private void HandleTcpServerConnected(IAsyncResult result)
		{
			try
			{
				_client.EndConnect(result);
				
				byte[] buffer = new byte[_client.ReceiveBufferSize];
				_client.GetStream().BeginRead(buffer, 0, _client.ReceiveBufferSize, HandleDatagramReceived, buffer);
				RaiseConnectedEvent(Addresses, Port);
				_isACKChecked = true;

				StartPulseTimer();
				//当登陆成功，发送登陆信息	
				WriteData(ControlerName, MessageType.DeviceLogIn);	
			}
			catch(Exception)
			{
				//这个时候会发生上一个被销毁的socket的异步对象IAsyncResult没执行的异常，忽略就可以了
				//因为上一个对象已经被回收了
			}
			IsConnected = true;
		}

		private void HandleDatagramReceived(IAsyncResult ar)
		{
			DebugHelpers.CustomMessageShow("begin Read");
			byte[] buffer = (byte[])ar.AsyncState;
			
			try
			{
				_isReading = true;
				int numberOfRecievedBytes;

				int FrameSize = Marshal.SizeOf(typeof(DatagramHeaderFrame));

				var stream = _client.GetStream();
				numberOfRecievedBytes = stream.EndRead(ar);

				DebugHelpers.CustomMessageShow($"numberOfRecievedBytes{numberOfRecievedBytes}");

				if (numberOfRecievedBytes == 0 && IsConnected)
				{
					RaiseServerConnectedExceptionEvent(Addresses, Port, "与服务器断开连接");
					RaiseDisConnectedEvent(Addresses, Port);
					return;
				}

				byte[] bytes = new byte[numberOfRecievedBytes];
				Buffer.BlockCopy(buffer, 0, bytes, 0, numberOfRecievedBytes);
				//把当前读到的数据全部传到大的缓冲区中
				_currentBytes.AddRange(bytes);

				while (_currentBytes.Count >= FrameSize)
				{
					DebugHelpers.CustomMessageShow($"_currentBytesCount{_currentBytes.Count}");
					if (!_waitingForWholeData)
					{
						//如果不是当前处于接收全部数据的状态，则读取帧头

						byte[] headerBytes = new byte[FrameSize];
						_currentBytes.CopyTo(0, headerBytes, 0, FrameSize);
						PraseHeader(headerBytes, ref _headerFrame);

						_targetLength = _headerFrame.MessageLength + FrameSize;
						_waitingForWholeData = true;

						DebugHelpers.CustomMessageShow($"targetLength{_targetLength}");
					}

					if (_currentBytes.Count >= _targetLength)
					{
						List<byte> restBytes = new List<byte>();
						//注意这里会存在一个很大的问题，如果_currentBytes.Count == _targetLength，不能GetRange，
						//因为_targetLength元素不存在

						DebugHelpers.CustomMessageShow($"begin handle");
						if (_currentBytes.Count > _targetLength)
						{
							restBytes = _currentBytes.GetRange(_targetLength, _currentBytes.Count - _targetLength);
							_currentBytes.RemoveRange(_targetLength, _currentBytes.Count - _targetLength);
						}

						//去除帧头
						_currentBytes.RemoveRange(0, FrameSize);
						DebugHelpers.CustomMessageShow($"end handle");

						byte[] a = new byte[0];
						handleRecivedMessage(_currentBytes.ToArray());

						_currentBytes = restBytes;
						_waitingForWholeData = false;

						DebugHelpers.CustomMessageShow($"targetLength{_targetLength}");
						DebugHelpers.CustomMessageShow($"after read{_currentBytes.Count}");

						if (_currentBytes.Count == 0)
							_isReading = false;
					}
					else
					{
						_waitingForWholeData = true;
						break;//必须break，当_currentBytes.Count不够_targetLength但又大于FrameSize时，会死循环
					}
				}
				_client.GetStream().BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
				DebugHelpers.CustomMessageShow("end Read");
			}
			catch (IndexOutOfRangeException e)
			{
				DebugHelpers.CustomMessageShow(e.Message);
			}
			catch(Exception e)
			{
				DebugHelpers.CustomMessageShow(e.Message);
				_isReading = false;
			}
			finally
			{
				incReadCount();
			}
		}

		private void handleRecivedMessage(byte[] recievedBytes)
		{
			if (_headerFrame.MsgType == MessageType.PulseFacility)
			{
				if (!_firstServerACK)
				{
					_firstServerACK = true;
					RaiseStartupSuccessed(Addresses, Port);
				}
			}
			else if (_headerFrame.MsgType == MessageType.ServerTest)
			{
				RaiseDatagramRecievedEvent(_headerFrame.SourceFeatureCode,_headerFrame, recievedBytes);
				RaisePlainTextReceivedEvent(_headerFrame.SourceFeatureCode, _headerFrame, recievedBytes);

				RaiseHelpEvent(_headerFrame.SourceFeatureCode, recievedBytes);//测试
				WriteData("ACK back!", MessageType.ServerTest);
			}
			else if (_headerFrame.MsgType == MessageType.PlainMessage)
			{
				RaiseDatagramRecievedEvent(_headerFrame.SourceFeatureCode, _headerFrame, recievedBytes);
				RaisePlainTextReceivedEvent(_headerFrame.SourceFeatureCode, _headerFrame, recievedBytes);
			}
			else if (_headerFrame.MsgType == MessageType.DeviceLogIn)
			{
				RaiseNewUserCommingEvent("", _headerFrame.SourceFeatureCode, recievedBytes);
			}
			else if (_headerFrame.MsgType == MessageType.Coordinate)
			{
				RaiseNewCoordinateOccuredEvent(_headerFrame.SourceFeatureCode, recievedBytes);
			}
			else if (_headerFrame.MsgType == MessageType.ACKPulse)
			{
				_isACKChecked = true;
			}
			else if (_headerFrame.MsgType == MessageType.Help)
			{
				RaiseHelpEvent(_headerFrame.SourceFeatureCode, recievedBytes);
			}
			else if (_headerFrame.MsgType == MessageType.DeviceLogOut)
			{
				RaiseUserLogoutEvent(_headerFrame.SourceFeatureCode);
				DebugHelpers.CustomMessageShow("Logout inform occured");
			}
		}

		private void HandleDatagramWritten(IAsyncResult ar)
		{
			try
			{
				_client.GetStream().EndWrite(ar);
			}
			catch(Exception)
			{
				//RaiseServerConnectedExceptionEvent(Addresses, Port, e.Message);
				//RaiseDisConnectedEvent(Addresses, Port);
			}
		}

		private void PulseTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (_isForcePulse && _lastReadCountBeforePulse == _readCount)
			{
				RaiseServerConnectedExceptionEvent(Addresses, Port, "服务器响应过慢");
				RaiseDisConnectedEvent(Addresses, Port);
				return;
			}
			else
				_isForcePulse = false;

			if (!_isWriting && !_isReading && IsConnected)
			{
				if (!_isACKChecked)
				{
					RaiseServerConnectedExceptionEvent(Addresses, Port, "服务器响应超时");
					RaiseDisConnectedEvent(Addresses, Port);
					return;
				}
				else
				{
					WriteData("", TcpDatagram.MessageType.PulseFacility);
					_isACKChecked = false;
				}
			}
			else
			{
				_lastReadCountBeforePulse = _readCount;
				_isForcePulse = true;
				_isACKChecked = true;
			}
		}

		/// <summary>
		/// 发送报文
		/// </summary>
		/// <param name="bytes">需要发送的报文</param>
		public void WriteData(byte[] bytes, MessageType messageType)
		{
			try
			{
				_isWriting = true;

				DatagramHeaderFrame headerFrame = new DatagramHeaderFrame();
				headerFrame.MsgType = messageType;
				headerFrame.MessageLength = bytes.Length;

				byte[] datagram = PackingMessageToBytes(headerFrame, bytes);

				_client.GetStream().BeginWrite(datagram, 0, datagram.Length, HandleDatagramWritten, this);
				_isWriting = false;
			}
			catch (Exception)
			{
				//RaiseServerConnectedExceptionEvent(Addresses, Port, e.Message);
				//RaiseDisConnectedEvent(Addresses, Port);
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

		public void DisConnectClient()
		{
			if (IsConnected)
			{
				IsConnected = false;
				RaiseDisConnectedEvent(Addresses, Port);
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

		/// <summary>
		/// 设备下线事件
		/// </summary>
		public event EventHandler<UserLogoutArgs> UserLogoutEvent;

		/// <summary>
		/// 求救信息发生
		/// </summary>
		public event EventHandler<HelpInformationArgs> HelpEvent;

		private void RaiseConnectedEvent(IPAddress[] ipAddresses, int port)
		{
			ConnectedEvent?.BeginInvoke(this, new TcpConnectedEventArgs(ipAddresses, port), null, null);
		}

		private void RaiseDisConnectedEvent(IPAddress[] ipAddresses, int port)
		{
			_client.Close();
			IsConnected = false;
			StopPulseTimer();
			DisConnectedEvent?.Invoke(this, new TcpDisconnectedEventArgs(ipAddresses, port)); 
		}

		private void RaiseServerConnectedExceptionEvent(IPAddress[] ipAddresses, int port, string exceptionResult)
		{
			ServerConnectedExceptionEvent?.Invoke(this,
				new TcpServerExceptionOccurredEventArgs(ipAddresses, port, exceptionResult));
		}

		private void RaiseDatagramRecievedEvent(int source,DatagramHeaderFrame header, byte[] bytes)
		{
			TcpDatagramReceivedEventArgs<byte[]> arg = new TcpDatagramReceivedEventArgs<byte[]>(source,header, bytes);
			DatagramReceivedEvent?.BeginInvoke(this, arg, null, null); 
		}

		private void RaisePlainTextReceivedEvent(int source, DatagramHeaderFrame header, byte[] datagram)
		{
			TcpDatagramReceivedEventArgs<string> arg 
				= new TcpDatagramReceivedEventArgs<string>(source, header, EncodeType.GetString(datagram));
			PlainTextReceivedEvent?.BeginInvoke(this, arg, null, null);
		}

		private void RaiseStartupSuccessed(IPAddress[] ipAddresses, int port)
		{
			var arg = new StartupSucceededEventArgs(ipAddresses, port);
			StartupSucceededEvent?.BeginInvoke(this, arg, null, null);
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

		private void RaiseHelpEvent(int sourceFeatureCode, byte[] coordinate)
		{
			var arg = new HelpInformationArgs(sourceFeatureCode);
			arg.IsCoordinate = arg.TryPraseCoordinate(EncodeType.GetString(coordinate));

			HelpEvent?.BeginInvoke(this, arg, null, null);
		}

		private void RaiseUserLogoutEvent(int sourceFeatureCode)
		{
			var arg = new UserLogoutArgs(sourceFeatureCode);

			UserLogoutEvent?.BeginInvoke(this, arg, null, null);
		}

		#endregion Events
	}

	/// <summary>
	/// 接收到数据报文事件参数
	/// </summary>
	/// <typeparam name="T">报文类型</typeparam>
	public class TcpDatagramReceivedEventArgs<T> :EventArgs
	{
		public TcpDatagramReceivedEventArgs(int source, DatagramHeaderFrame frame, T datagram)
		{
			this.datagram = datagram;
			this.datagramFrame = frame;
			this.Source = source;
		}

		public DatagramHeaderFrame datagramFrame { get; set; }
		public T datagram { get; private set; }
		public int Source { get; private set; }
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
	/// 与服务器的成功连接参数
	/// </summary>
	/// <param name="addresses">目标地址</param>
	/// <param name="port">远程端口</param>
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
	/// 设备退出的信息
	/// </summary>
	public class UserLogoutArgs : EventArgs
	{
		public UserLogoutArgs(int sourceFeatureCode)
		{
			this.SourceFeatureCode = sourceFeatureCode;
		}

		public int SourceFeatureCode { get; private set; }
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
			if (messgae == null)
				return false;

			char[] charsToTrim = { ' ', ',' };
			string[] strs = messgae.Split();

			if (strs.GetLength(0) >= 2)
			{
				double lat, lng;
				if (!double.TryParse(strs[0].TrimEnd(charsToTrim), out lat))
					return false;

				if (!double.TryParse(strs[1].TrimEnd(charsToTrim), out lng))
					return false;

				this.Lat = lat;
				this.Lng = lng;
				return true;
			}
			else
				return false;
		}

		public int SourceFeatureCode { get; private set; }
		public double Lat { get; private set; }
		public double Lng { get; private set; }
	}

	/// <summary>
	/// 求助信息
	/// </summary>
	public class HelpInformationArgs : EventArgs
	{
		public HelpInformationArgs(int sourceFeatureCode)
		{
			this.SourceFeatureCode = sourceFeatureCode;
		}

		public bool TryPraseCoordinate(string messgae)
		{
			if (messgae == null)
				return false;

			char[] charsToTrim = { ' ', ',' };
			string[] strs = messgae.Split();

			double lat, lng;

			if (strs.GetLength(0) >= 2)
			{
				if (!double.TryParse(strs[0].TrimEnd(charsToTrim), out lat))
				{
					this.Message = messgae;
					return false;
				}

				if (!double.TryParse(strs[1].TrimEnd(charsToTrim), out lng))
				{
					this.Message = messgae;
					return false;
				}

				this.Lat = lat;
				this.Lng = lng;

				if (strs.GetLength(0) >= 3)
				{
					this.Message = strs[2];
				}
				return true;
			}
			else
			{
				this.Message = messgae;
				return false;
			}
		}

		public bool IsCoordinate { get; set; }
		public string Message { get; private set; }
		public int SourceFeatureCode { get; private set; }
		public double Lat { get; private set; }
		public double Lng { get; private set; }
	}
}