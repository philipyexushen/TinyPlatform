using System;
using System.Windows;

namespace MarkersLib
{
	public static class MarkerShapesKeys
	{
		public static ComponentResourceKey YellowFlag
		{
			get { return new ComponentResourceKey(typeof(MarkerShapesKeys), "YellowFlag"); }
		}

		public static ComponentResourceKey PurpleFlag
		{
			get { return new ComponentResourceKey(typeof(MarkerShapesKeys), "PurpleFlag"); }
		}

		public static ComponentResourceKey HighlightFlag
		{
			get { return new ComponentResourceKey(typeof(MarkerShapesKeys), "HighlightFlag"); }
		}

		public static ComponentResourceKey NormalFlag
		{
			get { return new ComponentResourceKey(typeof(MarkerShapesKeys), "NormalFlag"); }
		}

		public static ComponentResourceKey BlueFlag
		{
			get { return new ComponentResourceKey(typeof(MarkerShapesKeys), "BlueFlag"); }
		}
	}
}
