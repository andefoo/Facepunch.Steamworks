﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Steamworks.Internal
{
	public abstract class BaseSteamInterface
	{
		public IntPtr Self;
		public IntPtr VTable;

		public virtual string InterfaceName => null;

		public BaseSteamInterface( bool server = false )
		{
			var hUser = server ? SteamApi.SteamGameServer_GetHSteamUser() : SteamApi.GetHSteamUser();

			if ( hUser == 0 )
				throw new System.Exception( "Steamworks is uninitialized" );

			if ( server )
			{
				Self = SteamInternal.FindOrCreateGameServerInterface( hUser, InterfaceName );
			}
			else
			{
				Self = SteamInternal.FindOrCreateUserInterface( hUser, InterfaceName );
			}

			if ( Self == IntPtr.Zero )
				throw new System.Exception( $"Couldn't find interface {InterfaceName} (server:{server})" );


			VTable = Marshal.ReadIntPtr( Self, 0 );
			if ( Self == IntPtr.Zero )
				throw new System.Exception( $"Invalid VTable for {InterfaceName}" );

			InitInternals();
		}

		public abstract void InitInternals();

		static byte[] stringbuffer = new byte[1024 * 128];

		internal string GetString( IntPtr p )
		{
			if ( p == IntPtr.Zero )
				return null;

			// return Marshal.PtrToStringUTF8( p );
			lock ( stringbuffer )
			{
				int len = 0;
				while ( Marshal.ReadByte( p, len ) != 0 && len < stringbuffer.Length ) { ++len; }
				Marshal.Copy( p, stringbuffer, 0, len );
				return Encoding.UTF8.GetString( stringbuffer, 0, len );
			}
		}
	}
}