﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Steamworks
{
	//
	// Created on registration of a callback
	//
	internal class Event<T> : IDisposable where T : struct, Steamworks.ISteamCallback
	{
		public Action<T> Action;

		List<GCHandle> Allocations = new List<GCHandle>();
		internal IntPtr vTablePtr;
		internal GCHandle PinnedCallback;

		public void Dispose()
		{
			UnregisterCallback();

			foreach ( var a in Allocations )
			{
				if ( a.IsAllocated )
					a.Free();
			}

			Allocations.Clear();

			if ( PinnedCallback.IsAllocated )
				PinnedCallback.Free();

			if ( vTablePtr != IntPtr.Zero )
			{
				Marshal.FreeHGlobal( vTablePtr );
				vTablePtr = IntPtr.Zero;
			}
		}

		private void UnregisterCallback()
		{
			if ( !PinnedCallback.IsAllocated )
				return;

			SteamClient.UnregisterCallback( PinnedCallback.AddrOfPinnedObject() );
		}

		public virtual bool IsValid { get { return true; } }

		T template;

		internal Event( Action<T> onresult, bool gameserver = false ) 
		{
			Action = onresult;

			template = new T();

			//
			// Create the functions we need for the vtable
			//
			if ( Config.UseThisCall )
			{
				//
				// Create the VTable by manually allocating the memory and copying across
				//
				if ( Config.Os == OsType.Windows )
				{
					vTablePtr = Callback.VTableWinThis.GetVTable( OnResultThis, OnResultWithInfoThis, OnGetSizeThis, Allocations );
				}
				else
				{
					vTablePtr = Callback.VTableThis.GetVTable( OnResultThis, OnResultWithInfoThis, OnGetSizeThis, Allocations );
				}
			}
			else
			{
				//
				// Create the VTable by manually allocating the memory and copying across
				//
				if ( Config.Os == OsType.Windows )
				{
					vTablePtr = Callback.VTableWin.GetVTable( OnResult, OnResultWithInfo, OnGetSize, Allocations );
				}
				else
				{
					vTablePtr = Callback.VTable.GetVTable( OnResult, OnResultWithInfo, OnGetSize, Allocations );
				}
			}

			//
			// Create the callback object
			//
			var cb = new Callback();
			cb.vTablePtr = vTablePtr;
			cb.CallbackFlags = gameserver ? (byte)0x02 : (byte)0;
			cb.CallbackId = template.GetCallbackId();

			//
			// Pin the callback, so it doesn't get garbage collected and we can pass the pointer to native
			//
			PinnedCallback = GCHandle.Alloc( cb, GCHandleType.Pinned );

			//
			// Register the callback with Steam
			//
			SteamClient.RegisterCallback( PinnedCallback.AddrOfPinnedObject(), cb.CallbackId );
		}

		[MonoPInvokeCallback] internal void OnResultThis( IntPtr self, IntPtr param ) => OnResult( param );
		[MonoPInvokeCallback] internal void OnResultWithInfoThis( IntPtr self, IntPtr param, bool failure, SteamAPICall_t call ) => OnResultWithInfo( param, failure, call );
		[MonoPInvokeCallback] internal int OnGetSizeThis( IntPtr self ) => OnGetSize();
		[MonoPInvokeCallback] internal int OnGetSize() => template.GetStructSize();
		[MonoPInvokeCallback] internal void OnResult( IntPtr param ) => OnResultWithInfo( param, false, 0 );

		[MonoPInvokeCallback]
		internal void OnResultWithInfo( IntPtr param, bool failure, SteamAPICall_t call )
		{
			if ( failure ) return;

			var value = (T)template.Fill( param );

			Action( value );
		}
	}
}