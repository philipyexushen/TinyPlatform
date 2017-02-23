using DebugHelperNameSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Net.Http;

namespace IpHelperSpace
{
	public delegate void AsyncIpHelperCallback(IpDetails details);

	public struct IpDetails
	{
		public enum DetailsStatus
		{
			Successful = 0,
			Failed = 1,
		}

		public string Ip;
		public DetailsStatus Status;
	}

	public static class IpHelper
	{ 
		public static readonly Uri IpFetchUri
			= new Uri(@"http://www.ip138.com/ip2city.asp");

		public static bool IsRequery { get; private set; } = false;

		public static void GetIpAndCoordinateAsync(AsyncIpHelperCallback callback)
		{
			if (!IsRequery)
			{
				IsRequery = true;
				new Thread(() => { GetIpAndCoordinatePrivate(callback); }).Start();
			}
		}

		public static IpDetails GetIpAndCoordinate()
		{
			return GetIpAndCoordinatePrivate();
		}	
		
		private static IpDetails GetIpAndCoordinatePrivate()
		{
			IpDetails details = new IpDetails();

			try
			{
				Task<IpDetails> ipFetchTask = fetchIp(details);

				if (ipFetchTask.Wait(10000))
				{

					details = ipFetchTask.Result;
					details.Status = IpDetails.DetailsStatus.Successful;
				}
				else
					throw new Exception("查询超时");
			}
			catch (Exception ex)
			{
				details.Status = IpDetails.DetailsStatus.Failed;
				DebugHelpers.CustomMessageShow(ex.Message);
			}

			return details;
		}

		private static async Task<IpDetails> fetchIp(IpDetails details)
		{
			using (HttpClient client = new HttpClient())
			{
				string ret = await client.GetStringAsync(IpFetchUri);
				int i = ret.IndexOf("[") + 1;
				string tempip = ret.Substring(i, 15);
				string ip = tempip.Replace("]", "").Replace(" ", "").Replace("<", "");

				details.Ip = ip;
			}
			return details;
		}

		/*
		private async Task<IpDetails> fetchCoordinate(IpDetails details)
		{
			using (HttpClient client = new HttpClient())
			{
				StringBuilder jsonAnalysis = new StringBuilder();
				string ret = await client.GetStringAsync(CoordinateFetchUri);

				using (JsonReader reader = new JsonTextReader(new StringReader(ret)))
				{
					while (reader.Read())
					{
						jsonAnalysis.Append(reader.Value + "\n");
						if (reader.Path == "content.point.x")
						{
							reader.Read();
							details.X = double.Parse(reader.Value.ToString());
						}
						else if (reader.Path == "content.point.y")
						{
							reader.Read();
							details.Y = double.Parse(reader.Value.ToString());
						}
					}
				}
			}
			return details;
		}
		*/

		private static void GetIpAndCoordinatePrivate(AsyncIpHelperCallback callback)
		{
			var details = GetIpAndCoordinatePrivate();
			callback(details);
			IsRequery = false;
		}
	}
}
