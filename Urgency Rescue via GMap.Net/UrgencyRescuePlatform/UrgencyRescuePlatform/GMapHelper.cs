using System.Threading;
using GMap.NET.WindowsPresentation;
using GMap.NET.MapProviders;
using GMap.NET;
using System.Windows.Media;
using System;
using MarkersLib;
using System.Windows.Shapes;

namespace UrgencyRescuePlatform
{
	public class GMapManagerLoader
	{
		public static GMapManagerLoader Instance { get; private set;} = new GMapManagerLoader();

		//不允许直接创建一个对象，这样就能保证整个程序只有一个对象
		private GMapManagerLoader()
		{

		}

		private bool _isLoaded;

		public bool Load(string fileName)
		{
			if (!_isLoaded)
			{
				new Thread(() => GMaps.Instance.ImportFromGMDB(fileName)).Start();
				_isLoaded = true;
			}
			return _isLoaded;
		}
	}

	public class GMapPostionSearchHelper
	{
		private GMapPostionSearchHelper() { }

		private struct Parameter
		{
			public PointLatLng point;
			public GeocodingProvider provider;
			public GMapRouteMarker marker;
		}

		public static void SearchPointInformation(PointLatLng point, GeocodingProvider provider, GMapRouteMarker marker)
		{
			Parameter arg = new Parameter();
			arg.marker = marker;
			arg.point = point;
			arg.provider = provider;

			new Thread(new ParameterizedThreadStart(SearchPointInformation)).Start(arg);
		}

		private static void SearchPointInformation(object objArg)
		{
			Parameter parameter = (Parameter)objArg;

			GeoCoderStatusCode statusCode = GeoCoderStatusCode.Unknow;

			try
			{
				//因为地图之间功能不完整，只能被迫取特定功能了
				//Placemark? placemark = parameter.provider.GetPlacemark(parameter.point, out statusCode);
				Placemark? placemark = GMapProviders.GoogleChinaMap.GetPlacemark(parameter.point, out statusCode);

				if (statusCode == GeoCoderStatusCode.G_GEO_SUCCESS)
					parameter.marker.Address = placemark.Value.Address;
				else
					parameter.marker.Address = "获取失败";

			}
			catch(NotImplementedException)
			{
				parameter.marker.Address = "本地图不支持此操作";
			}

		}
	}

	public class GMapRouteSearchHelper
	{
		private GMapRouteSearchHelper() { }

		private struct Parameter
		{
			public GMapMarker startPoint;
			public GMapMarker endPoint;
			public GeocodingProvider provider;
			public int zoom;
			public HandleRouteSearchResult callback;
		}

		public delegate void HandleRouteSearchResult(MapRoute route);

		public static void SearchRouteInformation
			(GMapMarker startPoint, GMapMarker endPoint, GeocodingProvider provider, int zoom, HandleRouteSearchResult callback)
		{
			Parameter arg = new Parameter();
			arg.startPoint = startPoint;
			arg.endPoint = endPoint;
			arg.provider = provider;
			arg.zoom = zoom;
			arg.callback = callback;

			new Thread(new ParameterizedThreadStart(SearchRouteInformation)).Start(arg);
		}

		private static void SearchRouteInformation
			(object objArg)
		{
			Parameter parameter = (Parameter)objArg;

			var provider   = parameter.provider;
			var startPoint = parameter.startPoint;
			var endPoint   = parameter.endPoint;
			var zoom       = parameter.zoom;
			var callback   = parameter.callback;

			RoutingProvider rp = provider as RoutingProvider;
			if (rp == null)
			{
				rp = GMapProviders.BingHybridMap; // 如果不能查找路径，那么用Bind的
			}

			MapRoute route = rp.GetRoute(startPoint.Position, endPoint.Position, false, false, zoom);

			callback(route);
		}
	}

	public static class GetDirectDistanceHelper
	{
		//地球半径
		private const double EARTH_RADIUS = 6378.137;

		//角度转弧度
		private static double rad(double d)
		{
			return d * Math.PI / 180.0;
		}

		public static double GetDistance(double lat1, double lng1, double lat2, double lng2)
		{
			//其实就是球面距离公式
			double radLat1 = rad(lat1);
			double radLat2 = rad(lat2);
			double a = radLat1 - radLat2;
			double b = rad(lng1) - rad(lng2);
			double s = 2 * Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(a / 2), 2) 
				+ Math.Cos(radLat1) * Math.Cos(radLat2) * Math.Pow(Math.Sin(b / 2), 2)));

			s = s * EARTH_RADIUS;
			s = Math.Round(s * 10000) / 10000;
			return s;
		}
	}
}