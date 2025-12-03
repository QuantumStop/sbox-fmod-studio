using FMODSbox;

public class DistancePrinter : Component
{
	[Property] public GameObject Obj1 { get; set; }
	[Property] public GameObject Obj2 { get; set; }

	protected override void OnUpdate()
	{
		if ( Obj2.IsValid() ) Log.Info( "Meters: " + MathX.InchToMeter( Obj1.WorldPosition.Distance( Obj2.WorldPosition ) ) );
	}

	[Button]
	void TestSound()
	{
		FMODSound.Play( "event:/Weapons/1P/Sniper/SniperA_1P" );
	}
}
