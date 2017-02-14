using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.ObjectModel;
using GMap.NET.WindowsPresentation;
using GMap.NET.MapProviders;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Globalization;
using System.IO;

namespace UrgencyRescuePlatform
{
	public class MapSourceHandler
	{
		public ICollection<MapSource> GetMaps()
		{
			if (_availableMaps == null)
			{
				_availableMaps = new ObservableCollection<MapSource>();

				_availableMaps.Add(
					new MapSource(GMapProviders.BingHybridMap, "BingHybridMap", "BingHybrid.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.GoogleChinaMap, "GoogleChinaMap", "GoogleChina.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.GoogleChinaHybridMap, "GoogleChinaHybridMap", "GoogleChinaHybrid.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.GoogleChinaSatelliteMap, "GoogleChinaSatelliteMap", "GoogleChinaSatellite.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.ArcGIS_World_Street_Map, "ArcGIS_World_Street_Map", "ArcGIS_Street.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.BingMap, "BingMap", "Bing.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.BingSatelliteMap, "BingSatelliteMap", "BingSatellite.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.OviMap, "OviMap", "OviMap.jpg"));
				_availableMaps.Add(
					new MapSource(GMapProviders.OviHybridMap, "OviHybridMap", "OviMapHybrid.jpg"));
			}

			return _availableMaps;
		}

		private ObservableCollection<MapSource> _availableMaps;
	}

	public class MapSource : INotifyPropertyChanged
	{
		public MapSource(GMapProvider provider, string name, string imagePath)
		{
			Provider = provider;
			ProviderName = name;
			SnapSource = imagePath;
		}

		private GMapProvider _provider;
		public GMapProvider Provider
		{
			get { return _provider; }
			set
			{
				_provider = value;
				OnPropertyChanged(new PropertyChangedEventArgs("Provider"));
			}
		}

		private string _providerName;
		public string ProviderName
		{
			get { return _providerName; }
			set
			{
				_providerName = value;
				OnPropertyChanged(new PropertyChangedEventArgs("ProviderName"));
			}
		}

		public string _snapsSource;
		public string SnapSource
		{
			get { return _snapsSource; }
			set
			{
				_snapsSource = value;
				OnPropertyChanged(new PropertyChangedEventArgs("SnapSource"));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}
	}

	public class MapLoadingModeHelper
	{
		public ICollection<MapLoadingMode> GetModes()
		{
			if (_modes == null)
			{
				_modes = new ObservableCollection<MapLoadingMode>();
				_modes.Add(new MapLoadingMode(GMap.NET.AccessMode.CacheOnly, "CacheOnly"));
				_modes.Add(new MapLoadingMode(GMap.NET.AccessMode.ServerAndCache, "ServerAndCache"));
				_modes.Add(new MapLoadingMode(GMap.NET.AccessMode.ServerOnly, "ServerOnly"));
			}
			return _modes;
		}

		private ObservableCollection<MapLoadingMode> _modes;
	}

	public class MapLoadingMode
	{
		public MapLoadingMode(GMap.NET.AccessMode mode, string modeName)
		{
			Mode = mode;
			ModeName = modeName;
		}

		public GMap.NET.AccessMode Mode { get; set; }
		public string ModeName { get; set; }
	}

	[ValueConversion(typeof(string), typeof(Uri))]
	public class ImagePathConverter : IValueConverter
	{
		private static readonly string _imageDirectory = @"\UrgencyRescuePlatform;component\MapSnaps\";
		public string ImageDirectory
		{
			get { return _imageDirectory; }
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string path = Path.Combine(ImageDirectory + (string)value);

			return new Uri(path, UriKind.Relative);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
