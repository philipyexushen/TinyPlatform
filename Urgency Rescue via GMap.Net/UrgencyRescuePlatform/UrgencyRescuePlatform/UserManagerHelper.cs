using System.Collections.Generic;
using System.Threading;
using System;
using MarkersLib;

namespace UrgencyRescuePlatform
{
	public sealed class UserManagerHelper<TKey>
	{
		private UserManagerHelper()
		{
			_dictionary = new SortedDictionary<TKey, UserInfo>();
			_dictionaryLock = new ReaderWriterLock();
		}

		public static UserManagerHelper<TKey> GetManager { get; } = new UserManagerHelper<TKey>();

		/// <summary>
		/// 添加新项目
		/// </summary>
		/// <param name="itemKey"></param>
		/// <param name="ip"></param>
		/// <param name="userName"></param>
		public void AddItem(TKey itemKey, string ip, string userName)
		{
			_dictionaryLock.AcquireWriterLock(int.MaxValue);
			UserInfo info = new UserInfo(ip, userName);
			_dictionary.Add(itemKey, info);

			_dictionaryLock.ReleaseWriterLock();
		}

		/// <summary>
		/// 删除一个项目
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public TKey RemoveItem(TKey itemKey)
		{
			_dictionaryLock.AcquireWriterLock(int.MaxValue);

			if(_dictionary.ContainsKey(itemKey))
				_dictionary.Remove(itemKey);

			_dictionaryLock.ReleaseWriterLock();
			return itemKey;
		}

		/// <summary>
		/// 获得指定user的上一个Marker
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public GMapRouteMarker GetLastMarker(TKey itemKey)
		{
			_dictionaryLock.AcquireReaderLock(int.MaxValue);

			GMapRouteMarker marker = null;
			if (_dictionary.ContainsKey(itemKey))
				marker = _dictionary[itemKey].GetLastMarker();

			_dictionaryLock.ReleaseReaderLock();
			return marker;
		}

		/// <summary>
		/// 往指定User中添加marker
		/// </summary>
		/// <param name="itemKey"></param>
		/// <param name="marker"></param>
		public bool AddMarkerToItem(TKey itemKey, GMapRouteMarker marker)
		{
			bool result;
			_dictionaryLock.AcquireReaderLock(int.MaxValue);
			if (_dictionary.ContainsKey(itemKey))
			{
				_dictionary[itemKey].AddMarker(marker);
				result = true;
			}
			else
				result = false;
			_dictionaryLock.ReleaseReaderLock();

			return result;
		}

		/// <summary>
		/// 清除当前对应项的所有markers
		/// </summary>
		/// <param name="itemKey"></param>
		public void ClearMarkers(TKey itemKey)
		{
			_dictionaryLock.AcquireReaderLock(int.MaxValue);
			_dictionary[itemKey].ClearMarkers();
			_dictionaryLock.ReleaseReaderLock();
		}

		/// <summary>
		/// 检查是否有对应的key
		/// </summary>
		/// <param name="itemKey"></param>
		/// <returns></returns>
		public bool IsContainKey(TKey itemKey)
		{
			return _dictionary.ContainsKey(itemKey);
		}

		private ReaderWriterLock _dictionaryLock;
		SortedDictionary<TKey, UserInfo> _dictionary;
	}

	public class UserInfo
	{
		public UserInfo(string ip, string userName)
		{
			this.Ip = ip;
			this.UserName = userName;
		}

		/// <summary>
		/// 获得上一个marker
		/// </summary>
		/// <returns></returns>
		public GMapRouteMarker GetLastMarker()
		{
			_lock.AcquireReaderLock(int.MaxValue);
			GMapRouteMarker marker = null;
			if (_markerList.Count != 0)
				marker = _markerList[_markerList.Count - 1];
 
			_lock.ReleaseReaderLock();
			return marker;
		}

		/// <summary>
		/// 往表中添加marker
		/// </summary>
		/// <param name="marker"></param>
		public void AddMarker(GMapRouteMarker marker)
		{
			_lock.AcquireWriterLock(int.MaxValue);
			_markerList.Add(marker);
			_lock.ReleaseWriterLock();
		}

		/// <summary>
		/// 清除所有markers
		/// </summary>
		public void ClearMarkers()
		{
			_lock.AcquireWriterLock(int.MaxValue);
			_markerList.Clear();
			_lock.ReleaseWriterLock();
		}

		List<GMapRouteMarker> _markerList = new List<GMapRouteMarker>();
		private ReaderWriterLock _lock = new ReaderWriterLock();

		public string Ip { get; private set; }
		public string UserName { get; private set; }
	}
}
