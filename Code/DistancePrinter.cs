using FMOD.Studio;
using FMODSbox;

public class DistancePrinter : Component
{
	[Property] public GameObject Obj1 { get; set; }
	[Property] public GameObject Obj2 { get; set; }
	[Property] public ParamFloat TestFloat { get; set; }
	private EventInstance instance { get; set; }

	protected override void OnUpdate()
	{
		if ( Obj2.IsValid() ) Log.Info( "Meters: " + MathX.InchToMeter( Obj1.WorldPosition.Distance( Obj2.WorldPosition ) ) );
	}

	[Button]
	void TestSound()
	{
		FMODSound.Play( "event:/Weapons/1P/Sniper/SniperA_1P" );
	}

	[Button]
	void Test3DSound()
	{
		if ( Obj1.IsValid() )
		{
			instance = FMODSound.Play( "event:/Objects/Combine/Forcefield/ForcefieldMain", Obj1 );
		}
	}

	[Button]
	void SetParam()
	{
		FMODSound.SetParameter( instance, TestFloat );
	}

	bool pause = false;
	[Button]
	void TestPause()
	{
		pause = !pause;
		FMODSound.SetPauseOnAll( pause );
	}

	[Button]
	void TestDirtFootstep()
	{
		var instance = FMODSound.Play( "event:/Physics/StepLeft" );
		FMODSound.SetParameter( instance, "parameter:/Physics/MaterialType", "Dirt" );
	}

	[Button]
	void TestPlasterFootstep()
	{
		var instance = FMODSound.Play( "event:/Physics/StepLeft", Obj1.WorldPosition );
		FMODSound.SetParameter( instance, "parameter:/Physics/MaterialType", "Plaster" );
	}

}
