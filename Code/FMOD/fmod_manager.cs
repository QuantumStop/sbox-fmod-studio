public class FMODManager : Component
{
	FMODStudio.System system => FMODStudio.System.Instance;

	protected override void OnAwake()
	{
		Log.Info(system);
	}
}