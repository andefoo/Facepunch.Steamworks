﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Steamworks.Data;

namespace Steamworks
{
	/// <summary>
	/// Functions for accessing and manipulating Steam user information.
	/// This is also where the APIs for Steam Voice are exposed.
	/// </summary>
	public class SteamUGC : SteamSharedClass<SteamUGC>
	{
		internal static ISteamUGC Internal => Interface as ISteamUGC;

		internal override void InitializeInterface( bool server )
		{
			SetInterface( server, new ISteamUGC( server ) );
			InstallEvents( server );
		}

		internal static void InstallEvents( bool server )
		{
			Dispatch.Install<DownloadItemResult_t>( x => OnDownloadItemResult?.Invoke(new DownloadItemResult(x)), server);
			Dispatch.Install<RemoteStoragePublishedFileSubscribed_t>( x => OnItemSubscribed?.Invoke( x.AppID.Value, x.PublishedFileId ), server );
			Dispatch.Install<RemoteStoragePublishedFileUnsubscribed_t>( x => OnItemUnsubscribed?.Invoke( x.AppID.Value, x.PublishedFileId ), server );
			Dispatch.Install<ItemInstalled_t>( x => OnItemInstalled?.Invoke( x.AppID.Value, x.PublishedFileId ), server );
		}

		/// <summary>
		/// Invoked after an item is downloaded.
		/// </summary>
		public static event Action<DownloadItemResult> OnDownloadItemResult;

		/// <summary>
		/// Invoked when a new item is subscribed.
		/// </summary>
		public static event Action<AppId, PublishedFileId> OnItemSubscribed;
		public static event Action<AppId, PublishedFileId> OnItemUnsubscribed;
		public static event Action<AppId, PublishedFileId> OnItemInstalled;

		public struct DownloadItemResult
		{
			internal DownloadItemResult( DownloadItemResult_t result )
			{
				AppID = result.AppID;
				PublishedFileId = result.PublishedFileId;
				Result = result.Result;
			}

			public AppId AppID { get; }
			public PublishedFileId PublishedFileId { get; }
			public Result Result { get; }
		}

		public static async Task<bool> DeleteFileAsync( PublishedFileId fileId )
		{
			var r = await Internal.DeleteItem( fileId );
			return r?.Result == Result.OK;
		}

		/// <summary>
		/// Start downloading this item. You'll get notified of completion via <see cref="OnDownloadItemResult"/>.
		/// </summary>
		/// <param name="fileId">The ID of the file to download.</param>
		/// <param name="highPriority">If <see langword="true"/> this should go straight to the top of the download list.</param>
		/// <returns><see langword="true"/> if nothing went wrong and the download is started.</returns>
		public static bool Download( PublishedFileId fileId, bool highPriority = false )
		{
			return Internal.DownloadItem( fileId, highPriority );
		}

		/// <summary>
		/// Will attempt to download this item asyncronously - allowing you to instantly react to its installation.
		/// </summary>
		/// <param name="fileId">The ID of the file you download.</param>
		/// <param name="progress">An optional callback</param>
		/// <param name="ct">Allows you to send a message to cancel the download anywhere during the process</param>
		/// <param name="milisecondsUpdateDelay">How often to call the progress function</param>
		/// <returns>true if downloaded and installed correctly</returns>
		public static async Task<bool> DownloadAsync(
			PublishedFileId fileId,
			Action<float> progress = null,
			Action<string> onError = null,
			int milisecondsUpdateDelay = 60,
			CancellationToken ct = default,
			bool highPriority = true )
		{
			progress?.Invoke( 0.00f );

			if ( ct == default )
				ct = new CancellationTokenSource( TimeSpan.FromSeconds( 60 ) ).Token;

			Ugc.Item? itemNullable = await Ugc.Item.GetAsync( fileId );

			progress?.Invoke( 0.05f );

			if ( !itemNullable.HasValue )
			{
				onError?.Invoke( $"Workshop item (id: " + fileId.Value + ") failed to load or doesn't exist." );
				return false;
			}

			Ugc.Item item = itemNullable.Value;

			bool itemDownloaded = false;
			Result? downloadError = null;

			Action<DownloadItemResult> onDownloadResult = r =>
			{
				if ( r.AppID == SteamClient.AppId && r.PublishedFileId == fileId )
				{
					if( r.Result != Result.OK )
						downloadError = r.Result;
					else
						itemDownloaded = true;
				}
			};

			try
			{
				OnDownloadItemResult += onDownloadResult;

				progress?.Invoke( 0.1f );
				if ( Download( fileId, highPriority ) == false )
					return false;

				await Task.Delay( milisecondsUpdateDelay ); //have to wait here otherwise first DownloadAmount is 1

				while ( itemDownloaded == false && downloadError == null )
				{
					var state = item.State;
					if ( ct.IsCancellationRequested )
						break;

					//waiting for download to start
					if ( state.HasFlag( ItemState.DownloadPending )
						&& !state.HasFlag( ItemState.Downloading ) )
						progress?.Invoke( 0.2f );
					else
						progress?.Invoke( 0.2f + item.DownloadAmount * 0.8f );

					//skip whole OnDownloadItemResult call as it doesn't work for everyone
					if ( !state.HasFlag( ItemState.DownloadPending )
						&& !state.HasFlag( ItemState.Downloading )
						&& !state.HasFlag( ItemState.NeedsUpdate ) )
						break;

					await Task.Delay( milisecondsUpdateDelay );
				}

				await Task.Delay( milisecondsUpdateDelay );
				if ( downloadError != null )
				{
					onError?.Invoke( "Download item result error: " + downloadError.ToString() );
					return false;
				}

				progress?.Invoke( 1.0f );
				return true;
			}
			finally
			{
				OnDownloadItemResult -= onDownloadResult;
			}
		}

		/// <summary>
		/// Utility function to fetch a single item. Internally this uses <c>Ugc.FileQuery</c> -
		/// which you can use to query multiple items if you need to.
		/// </summary>
		public static async Task<Ugc.Item?> QueryFileAsync( PublishedFileId fileId )
		{
			var result = await Ugc.Query.All
									.WithFileId( fileId )
									.GetPageAsync( 1 );

			if ( !result.HasValue || result.Value.ResultCount != 1 )
				return null;

			var item = result.Value.Entries.First();

			result.Value.Dispose();

			return item;
		}

		/// <summary>
		/// Gets a list of all of the items the current user is subscribed to for the current game.
		/// </summary>
		/// <param name="maxItemCount">The maximum number of items to return</param>
		/// <returns>Array of PublishedFileIds the user has subscribed to</returns>
		public static PublishedFileId[] GetSubscribedItems(uint maxItemCount)
		{
			uint itemCount = Internal.GetNumSubscribedItems();
			if (itemCount > maxItemCount)
			{
				itemCount = maxItemCount;
			}
			var result = new PublishedFileId[itemCount];
			if (itemCount <= 0)
			{
				return result;
			}
			uint realCount = Internal.GetSubscribedItems(result, itemCount);
			if (realCount < result.Length)
			{
				Array.Resize(ref result, (int)realCount);
			}
			return result;
		}

		public static async Task<bool> StartPlaytimeTracking(PublishedFileId[] fileIds)
		{
			var result = await Internal.StartPlaytimeTracking(fileIds, 1);
			return result.Value.Result == Result.OK;
		}

		public static async Task<bool> StartPlaytimeTracking(PublishedFileId fileId)
		{
			var result = await Internal.StartPlaytimeTracking(new[] {fileId}, 1);
			return result.Value.Result == Result.OK;
		}

		public static async Task<bool> StopPlaytimeTracking(PublishedFileId fileId)
		{
			var result = await Internal.StopPlaytimeTracking(new[] {fileId}, 1);
			return result.Value.Result == Result.OK;
		}
		
		public static async Task<bool> StopPlaytimeTrackingForAllItems()
		{
			var result = await Internal.StopPlaytimeTrackingForAllItems();
			return result.Value.Result == Result.OK;
		}

		/// <summary>
		/// Suspends all workshop downloads.
		/// Downloads will be suspended until you resume them by calling <see cref="ResumeDownloads"/> or when the game ends.
		/// </summary>
		public static void SuspendDownloads() => Internal.SuspendDownloads(true);

		/// <summary>
		/// Resumes all workshop downloads.
		/// </summary>
		public static void ResumeDownloads() => Internal.SuspendDownloads(false);

		/// <summary>
		/// Show the app's latest Workshop EULA to the user in an overlay window, where they can accept it or not.
		/// </summary>
		public static bool ShowWorkshopEula()
		{
			return Internal.ShowWorkshopEULA();
		}

		/// <summary>
		/// Retrieve information related to the user's acceptance or not of the app's specific Workshop EULA.
		/// </summary>
		public static async Task<bool?> GetWorkshopEulaStatus()
		{
			var status = await Internal.GetWorkshopEULAStatus();
			return status?.Accepted;
		}

	}
}
