using Steamworks.Data;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Steamworks
{
	public struct Controller
	{
		internal InputHandle_t Handle;

		internal Controller( InputHandle_t inputHandle_t )
		{
			this.Handle = inputHandle_t;
		}

		public ulong Id => Handle.Value;
		public InputType InputType => SteamInput.Internal.GetInputTypeForHandle( Handle );

		/// <summary>
		/// Reconfigure the controller to use the specified action set (ie 'Menu', 'Walk' or 'Drive')
		/// This is cheap, and can be safely called repeatedly. It's often easier to repeatedly call it in
		/// our state loops, instead of trying to place it in all of your state transitions.
		/// </summary>
		public string ActionSet
		{
			set => SteamInput.Internal.ActivateActionSet( Handle, SteamInput.Internal.GetActionSetHandle( value ) );
		}

		/// <summary>
		/// Returns ActionSetHandle as ulong
		/// </summary>
		public ulong GetActionSetHandle( string actionSet )
		{
			return SteamInput.Internal.GetActionSetHandle( actionSet );
		}

		public void ActivateActionSet( ulong actionSetHandle )
		{
			SteamInput.Internal.ActivateActionSet( Handle, actionSetHandle );
		}

		public void DeactivateLayer( string layer ) => SteamInput.Internal.DeactivateActionSetLayer( Handle, SteamInput.Internal.GetActionSetHandle( layer ) );
		public void ActivateLayer( string layer ) => SteamInput.Internal.ActivateActionSetLayer( Handle, SteamInput.Internal.GetActionSetHandle( layer ) );
		public void ClearLayers() => SteamInput.Internal.DeactivateAllActionSetLayers( Handle );

		/// <summary>
		/// Returns the current state of the supplied digital game action
		/// </summary>
		public DigitalState GetDigitalState( string actionName )
		{
			return SteamInput.Internal.GetDigitalActionData( Handle, SteamInput.GetDigitalActionHandle( actionName ) );
		}

		/// <summary>
		/// Returns InputDigitalActionHandle_t as ulong
		/// </summary>
		public ulong GetDigitalActionHandle( string actionName )
		{
			return SteamInput.GetDigitalActionHandle( actionName );
		}

		/// <summary>
		/// Returns the current state of the supplied digital game action, actionHandle will be used as InputDigitalActionHandle_t
		/// </summary>
		public DigitalState GetDigitalState( ulong actionHandle )
		{
			return SteamInput.Internal.GetDigitalActionData( Handle, actionHandle );
		}

		/// <summary>
		/// Returns the current state of these supplied analog game action
		/// </summary>
		public AnalogState GetAnalogState( string actionName )
		{
			return SteamInput.Internal.GetAnalogActionData( Handle, SteamInput.GetAnalogActionHandle( actionName ) );
		}

		/// <summary>
		/// Returns InputAnalogActionHandle_t as ulong
		/// </summary>
		public ulong GetAnalogActionHandle( string actionName )
		{
			return SteamInput.GetAnalogActionHandle( actionName );
		}

		/// <summary>
		/// Returns the current state of these supplied analog game action, actionHandle ulong to InputAnalogActionHandle_t
		/// </summary>
		public AnalogState GetAnalogState( ulong actionHandle )
		{
			return SteamInput.Internal.GetAnalogActionData( Handle, actionHandle );
		}

		private static InputActionOrigin[] sm_inputActionOriginsBuffer = new InputActionOrigin[ISteamInput.STEAM_INPUT_MAX_ORIGINS];

		public void GetDigitalActionOrigins( ulong actionSetHandle, ulong digitalActionHandle, out int? origin1, out int? origin2 )
		{
			int count = SteamInput.Internal.GetDigitalActionOrigins( Handle, actionSetHandle, digitalActionHandle, sm_inputActionOriginsBuffer );
			origin1 = count >= 1 ? (int)sm_inputActionOriginsBuffer[0] : null;
			origin2 = count >= 2 ? (int)sm_inputActionOriginsBuffer[1] : null;
		}

		public void GetAnalogActionOrigins( ulong actionSetHandle, ulong analogActionHandle, out int? origin1, out int? origin2 )
		{
			int count = SteamInput.Internal.GetDigitalActionOrigins( Handle, actionSetHandle, analogActionHandle, sm_inputActionOriginsBuffer );
			origin1 = count >= 1 ? (int)sm_inputActionOriginsBuffer[0] : null;
			origin2 = count >= 2 ? (int)sm_inputActionOriginsBuffer[1] : null;
		}

		/// <summary>
		/// Returns the current state of these supplied analog game action
		/// </summary>
		public MotionState GetMotionState()
		{
			return SteamInput.Internal.GetMotionData( Handle );
		}

		/// <summary>
		/// Invokes the Steam overlay and brings up the binding screen.
		/// Returns true for success; false if overlay is disabled/unavailable, or the user is not in Big Picture Mode.
		/// </summary>
		public bool ShowBindingPanel()
		{
			return SteamInput.Internal.ShowBindingPanel( Handle );
		}

		public void TriggerVibration( ushort usLeftSpeed, ushort usRightSpeed )
		{
			SteamInput.Internal.TriggerVibration( Handle, usLeftSpeed, usRightSpeed );
		}

		public void TriggerVibration( ushort usLeftSpeed, ushort usRightSpeed, ushort usLeftTriggerSpeed, ushort usRightTriggerSpeed )
		{
			SteamInput.Internal.TriggerVibrationExtended( Handle, usLeftSpeed, usRightSpeed, usLeftTriggerSpeed, usRightTriggerSpeed );
		}

		public void SetLEDColor( byte nColorR, byte nColorG, byte nColorB, uint nFlags )
		{
			SteamInput.Internal.SetLEDColor( Handle, nColorR, nColorG, nColorB, nFlags );
		}

		public override string ToString() => $"{InputType}.{Handle.Value}";


		public static bool operator ==( Controller a, Controller b ) => a.Equals( b );
		public static bool operator !=( Controller a, Controller b ) => !(a == b);
		public override bool Equals( object p ) => this.Equals( (Controller)p );
		public override int GetHashCode() => Handle.GetHashCode();
		public bool Equals( Controller p ) => p.Handle == Handle;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct AnalogState
	{
		public InputSourceMode EMode; // eMode EInputSourceMode
		public float X; // x float
		public float Y; // y float
		internal byte BActive; // bActive byte
		public bool Active => BActive != 0;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct MotionState
	{
		public float RotQuatX; // rotQuatX float
		public float RotQuatY; // rotQuatY float
		public float RotQuatZ; // rotQuatZ float
		public float RotQuatW; // rotQuatW float
		public float PosAccelX; // posAccelX float
		public float PosAccelY; // posAccelY float
		public float PosAccelZ; // posAccelZ float
		public float RotVelX; // rotVelX float
		public float RotVelY; // rotVelY float
		public float RotVelZ; // rotVelZ float
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct DigitalState
	{
		[MarshalAs( UnmanagedType.I1 )]
		internal byte BState; // bState byte
		[MarshalAs( UnmanagedType.I1 )]
		internal byte BActive; // bActive byte

		public bool Pressed => BState != 0;
		public bool Active => BActive != 0;
	}
}
