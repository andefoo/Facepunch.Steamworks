using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Steamworks.ServerList
{
	public class Internet : Base
	{
		// There is a very rare crash in Internal.RequestInternetServerList, somehow related to filters.
		// More info:
		// https://forum.unity.com/threads/il2cpp-marshalling-issue-with-an-empty-arrays-of-structs.1423185
		// https://github.com/Facepunch/Facepunch.Steamworks/issues/658
		//
		// Using this static reference is a hack that tries to make sure filters are not freed prematurely by garbage collection.
		// No idea if this really helps.
		//
		internal static Steamworks.Data.MatchMakingKeyValuePair[] sm_filters;

		internal override void LaunchQuery()
		{
			// var filters = GetFilters(); // Old code before the hack workaround attempt
			sm_filters = GetFilters(); // See comments above - making sure filters has a long lifetime (until the function called again)

			request = Internal.RequestInternetServerList( AppId.Value, ref sm_filters, (uint)sm_filters.Length, IntPtr.Zero );
		}
	}
}
