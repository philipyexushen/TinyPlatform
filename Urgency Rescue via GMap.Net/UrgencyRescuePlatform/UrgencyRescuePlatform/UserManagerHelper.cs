using System.Collections.Generic;
using System.Threading;
using System;
using MarkersLib;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections;
using System.Data;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows;

namespace UrgencyRescuePlatform
{
	public sealed class UserManagerHelper : INotifyPropertyChanged
	{
		private UserManagerHelper()
		{
			Users = new ObservableCollection<UserInfo>();
		}

		public static UserManagerHelper GetManager { get; } = new UserManagerHelper();

		/// <summary>
		/// 添加新项目
		/// </summary>
		/// <param name="itemKey"></param>
		/// <param name="ip"></param>
		/// <param name="userName"></param>
		public UserInfo AddItem(int itemKey, string ip, string userName,bool isPlatformItem = false)
		{
			foreach (UserInfo userinfo in Users)
			{
				if (userinfo.Key == itemKey)
					return userinfo;
			}

			UserInfo info = new UserInfo(ip, userName, itemKey);
			Users.Add(info);
			info.IsLogin = true;
			info.IsPlatformItem = isPlatformItem;

			return info;
		}

		/// <summary>
		/// 删除一个项目
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public int RemoveItem(int itemKey)
		{
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					Users.Remove(user);
					break;
				}
			}
			return itemKey;
		}

		/// <summary>
		/// 获得指定user的上一个Marker
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public GMapMarker GetLastMarker(int itemKey)
		{
			GMapMarker marker = null;

			foreach(UserInfo user in Users)
			{
				if (user.Key == itemKey)
					marker = user.GetLastMarker();
			}
			return marker;
		}

		/// <summary>
		/// 往指定User中添加marker
		/// </summary>
		/// <param name="itemKey"></param>
		/// <param name="marker"></param>
		public bool AddMarkerToItem(int itemKey, GMapMarker marker)
		{
			bool result = true;
			UserInfo target = null;

			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					target = user;
					break;
				}
			}
			if (target == null)
			{
				//注意不可以调用AddItem，会死锁
				UserInfo info = new UserInfo("", itemKey.ToString(), itemKey);
				Users.Add(info);
				target = info;
				target.IsLogin = true;
			}

			target.AddMarker(marker);
			return result;
		}

		/// <summary>
		/// 删除指定的marker
		/// </summary>
		/// <param name="itemKey"></param>
		/// <param name="marker"></param>
		public void DeleteWrapper(MainWindow sender, int itemKey, MarkersWrapper wrapper)
		{
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					user.DeleteMarker(sender, wrapper.Content);
					break;
				}
			}
		}

		/// <summary>
		/// 清除当前对应项的所有markers
		/// </summary>
		/// <param name="itemKey"></param>
		public void ClearMarkers(int itemKey)
		{
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					user.ClearMarkers();
					break;
				}
			}
		}

		/// <summary>
		/// 检查是否有对应的key
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public bool IsContainKey(int itemKey)
		{
			bool result = false;
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					result = true;
					break;
				}
			}
			return result;
		}

		/// <summary>
		/// 设置某项目为在线
		/// </summary>
		/// <param name="itemKey"></param>
		public void SetIslogin(int itemKey, bool islogin)
		{
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					user.IsLogin = islogin;
					break;
				}
			}
		}

		/// <summary>
		/// 清除表项
		/// </summary>
		/// <param name="info"></param>
		public void DeleteItem(MainWindow sender, UserInfo info)
		{
			info.DeleteAll(sender);
			Users.Remove(info);
		}

		/// <summary>
		/// 根据itemKey找到对应name
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public string GetUserName(int itemKey)
		{
			UserInfo target = null;

			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					target = user;
					break;
				}
			}
			if (target == null)
			{
				//注意不可以调用AddItem，会死锁
				UserInfo info;
				if (itemKey == 0)
					info = new UserInfo("", "服务器", itemKey);
				else
					info = new UserInfo("", itemKey.ToString(), itemKey);
				Users.Add(info);
				target = info;
				target.IsLogin = true;
			}

			return target.UserName;
		}

		/// <summary>
		/// 设定所有info都为logout
		/// </summary>
		public void SetAllLogout()
		{
			foreach(UserInfo info in Users)
				info.IsLogin = false;
		}

		public void RenewExist(int itemKey, GMapMarker marker, bool exist)
		{
			foreach (UserInfo user in Users)
			{
				if (user.Key == itemKey)
				{
					user.RenewExist(marker, exist);
					break;
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		private ObservableCollection<UserInfo> _users;
		public ObservableCollection<UserInfo> Users
		{
			get { return _users; }
			set
			{
				_users = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Users"));
			}
		}
	}

	public class UserInfo : INotifyPropertyChanged
	{
		public UserInfo(string ip, string userName, int key)
		{
			this.Ip = ip;
			this.UserName = userName;
			this.Key = key;

			MarkerList = new ObservableCollection<MarkersWrapper>();

			ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(MarkerList);
			view.CustomSort = new SortByModelDateTimeDescending();
			view.IsLiveSorting = true;//开启实时成型
			view.LiveSortingProperties.Add("Time");
		}

		/// <summary>
		/// 获得上一个marker
		/// </summary>
		/// <returns></returns>
		public GMapMarker GetLastMarker()
		{
			GMapMarker marker = null;
			if (_markerList.Count != 0)
				for (int i = _markerList.Count - 1;i >=0; i--)
				{
					if (_markerList[i].Type == typeof(GMapRouteMarker))
					{
						marker = _markerList[i].Content;
						break;
					}
				}
			return marker;
		}

		/// <summary>
		/// 往表中添加marker
		/// </summary>
		/// <param name="marker"></param>
		public void AddMarker(GMapMarker marker)
		{
			MarkersWrapper wrapper = new MarkersWrapper(DateTime.Now, marker.GetType(), marker, Key);
			wrapper.IsExistInMap = true;
			_markerList.Add(wrapper);
		}

		public void DeleteMarker(MainWindow sender, GMapMarker marker)
		{
			foreach(MarkersWrapper wrapper in MarkerList)
			{
				if (wrapper.Content == marker)
				{
					sender.ClaerMarkerItemFromMap(wrapper);
					MarkerList.Remove(wrapper);
					break;
				}
			}
		}

		public void DeleteAll(MainWindow sender)
		{
			foreach (MarkersWrapper wrapper in MarkerList)
				sender.ClaerMarkerItemFromMap(wrapper);
			MarkerList.Clear();
		}

		/// <summary>
		/// 清除所有markers
		/// </summary>
		public void ClearMarkers()
		{
			_markerList.Clear();
		}

		public void RenewExist(GMapMarker marker, bool exist)
		{
			foreach (MarkersWrapper wrapper in MarkerList)
			{
				if (wrapper.Content == marker)
				{
					wrapper.IsExistInMap = exist;
					break;
				}
			}
		}

		private ObservableCollection<MarkersWrapper> _markerList;
		public ObservableCollection<MarkersWrapper> MarkerList
		{
			get { return _markerList; }
			set
			{
				_markerList = value;
				OnPropertyChanged(new PropertyChangedEventArgs("MarkerList"));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		public string Ip { get; private set; }

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

		private int _key;
		public int Key
		{
			get { return _key; }
			set
			{
				_key = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Key"));
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

		private bool _isPlatformItem = false;
		public bool IsPlatformItem
		{
			get { return _isPlatformItem; }
			set
			{
				_isPlatformItem = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsPlatformItem"));
			}
		}
	}

	public class MarkersWrapper : INotifyPropertyChanged
	{
		public MarkersWrapper(DateTime time, Type type, GMapMarker marker, int itemKey)
		{
			this.Time = time;
			this.Type = type;
			this.Content = marker;
			this.ParentKey = itemKey;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		private Type _type;
		public Type Type
		{
			get { return _type; }
			private set
			{
				_type = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Type"));
			}
		}

		private DateTime _time;
		public DateTime Time
		{
			get { return _time; }
			private set
			{
				_time = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Time"));
			}
		}

		private bool _isExistInMap;
		public bool IsExistInMap
		{
			get { return _isExistInMap; }
			set
			{
				_isExistInMap = value;
				OnPropertyChanged(new PropertyChangedEventArgs("IsExistInMap"));
			}
		}

		private int _parentKey;
		public int ParentKey
		{
			get { return _parentKey; }
			private set
			{
				_parentKey = value;
				OnPropertyChanged(new PropertyChangedEventArgs("ParentKey"));
			}
		}

		public GMapMarker Content { get; private set; }
	}

	[ValueConversion(typeof(Type), typeof(string))]
	public class TypeConveter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Type type = value as Type;
			string result = string.Empty;

			if (type == typeof(GMapRouteMarker))
				result = $"坐标";
			else if (type == typeof(GMapRoute))
				result = $"路径";

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	[ValueConversion(typeof(bool), typeof(string))]
	public class IsLoginConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string result = string.Empty;
			bool isLogin = (bool)value;

			if (isLogin)
				result = "（在线）";
			else
				result = "（离线）";

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	[ValueConversion(typeof(bool), typeof(bool))]
	public class NegationConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	[ValueConversion(typeof(Type), typeof(Uri))]
	public class TreeViewItemImageConvrter : IValueConverter
	{
		public static readonly string ImagePath = @"\UrgencyRescuePlatform;component\images"; 

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Type type = value as Type;
			Uri result = null;

			if (type == typeof(GMapRouteMarker))
				result = new Uri(Path.Combine(ImagePath, "markerIcon.png"),UriKind.Relative);
			else if (type == typeof(GMapRoute))
				result = new Uri(Path.Combine(ImagePath, "roadIcon.png"), UriKind.Relative);

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	[ValueConversion(typeof(bool), typeof(string))]
	public class IsExistInMapConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool isExistInMap = (bool)value;
			string result = string.Empty;

			if (isExistInMap == false)
				result = "（不在地图上）";

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
	
	[ValueConversion(typeof(string),typeof(Style))]
	public class IsLoginItemStyleSelector : StyleSelector
	{
		public Style DefaultStyle { get; set; }

		public Style LoginStyle { get; set; }

		public string PropertyToEvaluate{ get; set; }

		public override Style SelectStyle(object item, DependencyObject container)
		{
			UserInfo info = item as UserInfo;

			if (info == null)
				return DefaultStyle;

			Type type = info.GetType();

			PropertyInfo property = type.GetProperty(PropertyToEvaluate);

			bool? result = property.GetValue(info, null) as bool?;

			if (!result.HasValue || result.Value == false)
				return DefaultStyle;
			else
				return LoginStyle;

		}
	}

	public class SortByModelDateTimeDescending : IComparer
	{
		public int Compare(object x, object y)
		{
			MarkersWrapper xWrapper = (MarkersWrapper)x;
			MarkersWrapper yWrapper = (MarkersWrapper)y;

			int result = xWrapper.Time.CompareTo(yWrapper.Time);

			if (result == 0)
				return 0;
			else if (result > 0)
				return -1;
			else
				return 1;
		}
	}
}
