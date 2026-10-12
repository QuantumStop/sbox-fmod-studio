using FMOD.Studio;
using FMODSbox;

public class DistancePrinter : Component
{
	[Property] private FMODEventResource _event { get; set; }

	[Property] private GameObject _obj1 { get; set; }
	[Property] private GameObject _obj2 { get; set; }
	[Property] private ParamFloat _testFloat { get; set; }
	private EventInstance _instance { get; set; }

	protected override void OnUpdate()
	{
		if ( _obj2.IsValid() ) Log.Info( "Meters: " + MathX.InchToMeter( _obj1.WorldPosition.Distance( _obj2.WorldPosition ) ) );
	}

	[Button]
	private void TestSound() => FMODSound.Play( _event );

	[Button]
	private void Test3DSound()
	{
		if ( _obj1.IsValid() )
		{
			_instance = FMODSound.Play( _event, _obj1 );
		}
	}

	[Button]
	private void SetParam() => FMODSound.SetParameter( _instance, _testFloat );

	bool _pause = false;
	[Button]
	private void TestPause()
	{
		_pause = !_pause;
		FMODSound.SetPauseOnAll( _pause );
	}
	[Property, Header( "Audio Table" )] private string _key { get; set; } = "wax";

	[Button]
	private void TestAudioTable()
	{
		var snd = FMODSound.Create( _event );
		snd.addAudioTableKey( _key );
		FMODSound.Play( snd );
	}
}
