using System;
using System.Windows.Controls;
using System.Windows;

namespace MarkersLib
{
	public static class MarkersLibKeys
	{
		public static ComponentResourceKey GmapRouteMarkerKey
		{
			get { return new ComponentResourceKey(typeof(MarkersLibKeys), "GmapRouteMarker"); }
		}

		public static ComponentResourceKey GmapRouteHMarkerKey
		{
			get { return new ComponentResourceKey(typeof(MarkersLibKeys), "GmapRoute_HMarker"); }
		}
	}

	public static class ToolTipStack
	{
		public static ComponentResourceKey MarkerTooltipKey
		{
			get { return new ComponentResourceKey(typeof(ToolTipStack), "MarkerTooltip"); }
		}
	}
}
