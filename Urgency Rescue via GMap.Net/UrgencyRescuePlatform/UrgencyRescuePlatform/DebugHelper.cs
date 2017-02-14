using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace DebugHelperNameSpace
{
	public class DebugHelpers
	{
		public static void ThreadCheck()
		{
			System.Diagnostics.Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
		}

		public static void CustomMessageShow(string msg)
		{
			System.Diagnostics.Debug.WriteLine(msg);
		}
	}
}
