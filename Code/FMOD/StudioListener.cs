using Sandbox;
using System;

namespace FMODSbox;


[Title( "FMOD Listener" )]
public partial class StudioListener : Component
{
	// lowkey this is a stupid system, but I personally don't understand the concept of Listeners (when there is more than one -> why would you need that)
	[Property] public bool NonRigidbodyVelocity { get; set; } = false;
	[Property] public GameObject AttenuationObject { get; set; }

	private Vector3 _lastFramePosition = Vector3.Zero;
	private Rigidbody _rigidBody;

	public bool PlayerListener { get; set; }

	public int ListenerNumber { get => FMODManager.Listeners.IndexOf( this ); }

	protected override void OnEnabled()
	{
		RuntimeUtils.EnforceLibraryOrder();

		_rigidBody = GameObject.Components.Get<Rigidbody>( FindMode.EnabledInSelfAndChildren );

		if ( NonRigidbodyVelocity && _rigidBody.IsValid() )
		{
			Log.Info( string.Format( "[FMOD] Non-Rigidbody Velocity is enabled on Listener attached to GameObject \"{0}\", which also has a Rigidbody component attached - this will be disabled in favor of velocity from Rigidbody component.", this.GameObject.Name ) );
			NonRigidbodyVelocity = false;
		}

		FMODManager.AddListener( this );

		_lastFramePosition = WorldTransform.Position;
	}

	protected override void OnDisabled()
	{
		if (!PlayerListener) FMODManager.RemoveListener( this );
	}

	protected override void OnUpdate()
	{
		if ( ListenerNumber < 0 || ListenerNumber >= FMOD.CONSTANTS.MAX_LISTENERS )
			return;

		if ( NonRigidbodyVelocity )
		{
			var velocity = Vector3.Zero;
			var position = WorldTransform.Position;

			if ( Time.Delta != 0 )
			{
				velocity = (position - _lastFramePosition) / Time.Delta;
				velocity = velocity.Clamp( velocity, 20f );
			}

			_lastFramePosition = position;

			FMODManager.SetListenerLocation( ListenerNumber, GameObject, AttenuationObject, velocity );
		}
		else
		{
			if ( _rigidBody.IsValid() )
				FMODManager.SetListenerLocation( ListenerNumber, GameObject, _rigidBody, AttenuationObject );
			else
				FMODManager.SetListenerLocation( ListenerNumber, GameObject, AttenuationObject );
		}
	}

}
