using FMOD.Studio;

namespace FMODSbox;

[Title( "FMOD Sound Point" ), Category( "FMOD" ), Tint( EditorTint.Green )]
public class StudioSoundPoint : Component
{
	/// <summary>
	/// EventInstance of this sound point
	/// </summary>
	public EventInstance Instance { get; set; }
	/// <summary>
	/// The event to be played by this sound point
	/// </summary>
	[Property] public string Event { get; set; }
	/// <summary>
	/// Start playing when the component is enabled
	/// </summary>
	[Property, Title( "Play OnEnable" )] public bool AutoPlay { get; set; } = true;
	/// <summary>
	/// Do we attach to a GO or we are static on the position of this component's GO?
	/// </summary>
	[Property, Title( "Attach to a GameObject" )] public bool AttachToObject { get; set; } = true;
	[Property, ShowIf( nameof( AttachToObject ), true )] GameObject AttachTarget { get; set; }
	/// <summary>
	/// Do we immediately release the event instance, or do we want to hold it for a bit?
	/// </summary>
	[Property, Space, Order( 99 )] public bool ReleaseEvent { get; set; } = true;
	/// <summary>
	/// Float parameters for this event to have when the event starts playing
	/// </summary>
	[Property, Feature( "Floats" )] public List<ParamFloat> FloatParameters { get; set; }
	/// <summary>
	/// Label parameters for this event to have when the event starts playing
	/// </summary>
	[Property, Feature( "Labels" )] public List<ParamLabel> LabelParameters { get; set; }
	[Property, FeatureEnabled( "Floats" )] private bool HasFloats { get; set; } = false;
	[Property, FeatureEnabled( "Labels" )] private bool HasLabels { get; set; } = false;

	/// <summary>
	/// Entry point for playing our event, has all the necessary checks to not shit the pants, use this to start the sound from code
	/// </summary>
	public void StartSound()
	{
		if ( Instance.isValid() )
		{
			Instance.start(); // just restart it if we didn't release
			return;
		}

		if ( string.IsNullOrEmpty( Event ) )
		{
			Log.Warning( $"No event given for {this}" );
			return;
		}

		Instance = AttachToObject && AttachTarget.IsValid() ?
		FMODSound.Play( Event, AttachTarget, ReleaseEvent ) :
		FMODSound.Play( Event, GameObject.WorldPosition, ReleaseEvent );

		if ( HasFloats && FloatParameters.Count > 0 )
		{
			foreach ( var floater in FloatParameters )
			{
				if ( string.IsNullOrEmpty( floater.ParameterName ) ) continue;
				FMODSound.SetParameter( Instance, floater );
			}
		}

		if ( HasLabels && LabelParameters.Count > 0 )
		{
			foreach ( var labeler in LabelParameters )
			{
				if ( string.IsNullOrEmpty( labeler.ParameterName ) || string.IsNullOrEmpty( labeler.Value ) ) continue;
				FMODSound.SetParameter( Instance, labeler );
			}
		}
	}

	protected override void OnEnabled()
	{
		if ( AutoPlay ) StartSound();
	}

	/// <summary>
	/// Set pause state on 
	/// </summary>
	/// <param name="set"></param>
	public void SetPaused( bool set )
	{
		if ( Instance.isValid() ) FMODSound.SetPause( Instance, set );
	}

	public void StopSound( bool allowfade = true )
	{
		if ( Instance.isValid() ) FMODSound.Stop( Instance, allowfade );
		else return;
	}
}
