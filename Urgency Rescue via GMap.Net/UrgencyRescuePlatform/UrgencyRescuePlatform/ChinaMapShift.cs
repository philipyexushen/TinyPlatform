using System;
using GMap.NET;

namespace WGS84_GCJ0_Transform
{
	public static class CoordinateTransformer
	{

		public static PointLatLng PointLatLngMake(double lng, double lat)
		{
			PointLatLng loc = new PointLatLng(lat, lng);
			return loc;
		}

		///
		///  Transform WGS-84 to GCJ-02 (Chinese encrypted coordination system)
		///

		const double pi = 3.14159265358979324;

		//
		// Krasovsky 1940
		//
		// a = 6378245.0, 1/f = 298.3
		// b = a * (1 - f)
		// ee = (a^2 - b^2) / a^2;
		const double a = 6378245.0;
		const double ee = 0.00669342162296594323;

		public static bool outOfChina(double lat, double lon)
		{
			if (lon < 72.004 || lon > 137.8347)
				return true;
			if (lat < 0.8293 || lat > 55.8271)
				return true;
			return false;
		}

		public static double transformLat(double x, double y)
		{
			double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(x > 0 ? x : -x);
			ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;
			ret += (20.0 * Math.Sin(y * pi) + 40.0 * Math.Sin(y / 3.0 * pi)) * 2.0 / 3.0;
			ret += (160.0 * Math.Sin(y / 12.0 * pi) + 320 * Math.Sin(y * pi / 30.0)) * 2.0 / 3.0;
			return ret;
		}

		public static double transformLon(double x, double y)
		{
			double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(x > 0 ? x : -x);
			ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;
			ret += (20.0 * Math.Sin(x * pi) + 40.0 * Math.Sin(x / 3.0 * pi)) * 2.0 / 3.0;
			ret += (150.0 * Math.Sin(x / 12.0 * pi) + 300.0 * Math.Sin(x / 30.0 * pi)) * 2.0 / 3.0;
			return ret;
		}

		public static PointLatLng transformFromWGSToGCJ(PointLatLng wgLoc)
		{
			PointLatLng mgLoc = new PointLatLng();
			if (outOfChina(wgLoc.Lat, wgLoc.Lng))
			{
				mgLoc = wgLoc;
				return mgLoc;
			}
			double dLat = transformLat(wgLoc.Lng - 105.0, wgLoc.Lat - 35.0);
			double dLon = transformLon(wgLoc.Lng - 105.0, wgLoc.Lat - 35.0);
			double radLat = wgLoc.Lat / 180.0 * pi;
			double magic = Math.Sin(radLat);
			magic = 1 - ee * magic * magic;
			double sqrtMagic = Math.Sqrt(magic);
			dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * pi);
			dLon = (dLon * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * pi);
			mgLoc.Lat = wgLoc.Lat + dLat;
			mgLoc.Lng = wgLoc.Lng + dLon;

			return mgLoc;
		}

		///
		///  Transform GCJ-02 to WGS-84
		///  Reverse of transformFromWGSToGC() by iteration.
		///
		///  Created by Fengzee (fengzee@fengzee.me).
		///
		public static PointLatLng transformFromGCJToWGS(PointLatLng gcLoc)
		{
			PointLatLng wgLoc = gcLoc;
			PointLatLng currGcLoc = new PointLatLng();
			PointLatLng dLoc = new PointLatLng();
			while (true)
			{
				currGcLoc = transformFromWGSToGCJ(wgLoc);
				dLoc.Lat = gcLoc.Lat - currGcLoc.Lat;
				dLoc.Lng = gcLoc.Lng - currGcLoc.Lng;
				if (Math.Abs(dLoc.Lat) < 1e-7 && Math.Abs(dLoc.Lng) < 1e-7)
				{  // 1e-7 ~ centimeter level accuracy
				   // Result of experiment:
				   //   Most of the time 2 iterations would be enough for an 1e-8 accuracy (milimeter level).
				   //
					return wgLoc;
				}
				wgLoc.Lat += dLoc.Lat;
				wgLoc.Lng += dLoc.Lng;
			}
		}

		///
		///  Transform GCJ-02 to BD-09
		///
		const double x_pi = 3.14159265358979324 * 3000.0 / 180.0;
		public static PointLatLng bd_encrypt(PointLatLng gcLoc)
		{
			double x = gcLoc.Lng, y = gcLoc.Lat;
			double z = Math.Sqrt(x * x + y * y) + 0.00002 * Math.Sin(y * x_pi);
			double theta = Math.Atan2(y, x) + 0.000003 * Math.Cos(x * x_pi);
			return PointLatLngMake(z * Math.Cos(theta) + 0.0065, z * Math.Sin(theta) + 0.006);
		}

		///
		///  Transform BD-09 to GCJ-02
		///
		public static PointLatLng bd_decrypt(PointLatLng bdLoc)
		{
			double x = bdLoc.Lng - 0.0065, y = bdLoc.Lat - 0.006;
			double z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * x_pi);
			double theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * x_pi);
			return PointLatLngMake(z * Math.Cos(theta), z * Math.Sin(theta));
		}
	}
}