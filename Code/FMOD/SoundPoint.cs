using FMOD.Studio;
using System;

namespace FMODSbox;

[Icon( "volume_down" )]
[Title( "FMOD Sound Point" ), Category( "FMOD" ), Tint( EditorTint.Green )]
public class StudioSoundPoint : Component, IFMODEvents
{
	/// <summary>
	/// EventInstance of this sound point
	/// </summary>
	public EventInstance Instance { get; set; }

	/// <summary>
	/// The event to be played by this sound point
	/// </summary>
	[Property, Editor( "fmod_event_ref" )] public string Event { get; set; }

	/// <summary>
	/// When enabled, uses <see cref="EventResource"/> instead of the raw <see cref="Event"/> path.
	/// </summary>
	public bool UseEventResource { get; set; }

	/// <summary>
	/// Optional FMOD event resource reference. Runtime resolves to <see cref="FMODEventResource.EventPath"/>.
	/// </summary>
	public FMODEventResource EventResource { get; set; }

	/// <summary>
	/// Start playing when the component is enabled
	/// </summary>
	[Property, Title( "Play OnEnabled" )] public bool AutoPlay { get; set; } = true;
	/// <summary>
	/// Do we want to override the volume of the chosen event?
	/// </summary>
	[Property, Title( "Override Volume" ), Space] public bool OverrideVolumeBool { get; set; } = false;

	/// <summary>
	/// Scaling factor for the event volume, doesn't override.
	/// </summary>
	[Property, MakeDirty, ShowIf( nameof( OverrideVolumeBool ), true ), Range( 0, 10 )] public float OverrideVolumeScale { get; set; } = 1f;

	/// <summary>
	/// Do we attach to a GO or we are static on the position of this component's GO?
	/// </summary>
	[Property, Title( "Attach to a GameObject" ), Space] public bool AttachToObject { get; set; } = true;
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

		var resolvedEvent = ResolveEventPath();
		if ( string.IsNullOrEmpty( resolvedEvent ) )
		{
			Log.Warning( $"No event given for {this}" );
			return;
		}

		Instance = AttachToObject && AttachTarget.IsValid() ?
		FMODSound.Play( resolvedEvent, AttachTarget, ReleaseEvent ) :
		FMODSound.Play( resolvedEvent, GameObject.WorldPosition, ReleaseEvent );

		if ( OverrideVolumeBool ) Instance.setVolume( OverrideVolumeScale );

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

	/// <summary>
	/// Compoent was NOT enabled when the scene started, but was enabled some time after, when FMOD system was already initialized
	/// </summary>
	protected override void OnEnabled()
	{
		if ( FMODManagerSystem.Current.SceneInitialized && AutoPlay ) StartSound();
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.Color = Color.Parse( "#4dfd7c" ).Value;

		Model model = Model.Load( "models/editor/soundpoint_helper.vmdl" );

		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Model( model );

		Gizmo.Draw.Color = Gizmo.IsSelected
			? Color.Yellow
			: Gizmo.IsHovered
				? Color.White.WithAlpha( PulseAlpha() )
				: Color.White;

		if ( Gizmo.IsSelected || Gizmo.IsHovered )
			Gizmo.Draw.LineBBox( model.Bounds );

		return;

	}

	private static float PulseAlpha()
	{
		return 0.7f + MathF.Sin( Time.Now * 20f ) * 0.3f;
	}

	/// <summary>
	/// Component was enabled when the scene started, but FMOD systems initialize after OnEnabled is fired
	/// </summary>
	void IFMODEvents.OnAfterInit()
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
	}

	protected override void OnDirty()
	{
		if ( Scene.IsEditor ) return; // don't do this if the game is not playing

		if ( ReleaseEvent )
		{
			Log.Warning( "To change volume at runtime, event must be not released!" );
			return;
		}

		if ( Instance.isValid() ) Instance.setVolume( OverrideVolumeBool ? OverrideVolumeScale : 1f );
	}

	private string ResolveEventPath()
	{
		if ( UseEventResource && EventResource is not null && !string.IsNullOrWhiteSpace( EventResource ) )
			return EventResource;

		return Event;
	}
}
