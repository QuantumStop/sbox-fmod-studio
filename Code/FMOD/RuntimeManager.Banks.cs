namespace FMODSbox;
public partial class FMODManager
{
	public static void LoadBank( string bankName, bool loadSamples = false )
	{
		LoadBank( bankName, loadSamples, bankName );
	}

	private static void LoadBank( string bankName, bool loadSamples, string bankId )
	{
		if ( Instance.loadedBanks.ContainsKey( bankId ) )
		{
			ReferenceLoadedBank( bankId, loadSamples );
		}
		else
		{
			const string BankExtension = ".bank";

			string bankPath;

			if ( System.IO.Path.GetExtension( bankName ) != BankExtension )
			{
				bankPath = string.Format( "{0}/{1}{2}", fmodSettings.BankFolder, bankName, BankExtension );
			}
			else
			{
				bankPath = string.Format( "{0}/{1}", fmodSettings.BankFolder, bankName );
			}

			Instance.loadingBanksRef++;

			LoadedBank loadedBank = new();
			FMOD.RESULT loadResult = Instance.studioSystem.loadBankFile( bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank );
			Instance.RegisterLoadedBank( loadedBank, bankPath, bankId, loadSamples, loadResult );

			Instance.loadingBanksRef--;
		}
	}
	public static bool IsInitialized => Instance != null && Instance.studioSystem.isValid();

	public static bool HaveAllBanksLoaded => Instance.loadingBanksRef == 0;


	public static bool HaveMasterBanksLoaded
	{
		get
		{
			var banks = Instance.MasterBanks;
			foreach ( var bank in banks )
			{
				if ( !HasBankLoaded( bank ) ) return false;
			}
			return true;
		}
	}

	public static bool HasBankLoaded( string loadedBank )
	{
		return Instance.loadedBanks.ContainsKey( loadedBank );
	}

	private static void ReferenceLoadedBank( string bankName, bool loadSamples )
	{
		LoadedBank loadedBank = Instance.loadedBanks[bankName];
		loadedBank.RefCount++;

		if ( loadSamples )
		{
			loadedBank.Bank.loadSampleData();
		}

		Instance.loadedBanks[bankName] = loadedBank; // Save the incremented reference count
	}

	private void RegisterLoadedBank( LoadedBank loadedBank, string bankPath, string bankName, bool loadSamples, FMOD.RESULT loadResult )
	{
		if ( loadResult == FMOD.RESULT.OK )
		{
			loadedBank.RefCount = 1;

			if ( loadSamples )
			{
				loadedBank.Bank.loadSampleData();
			}

			Instance.loadedBanks.Add( bankName, loadedBank );
		}
		else if ( loadResult == FMOD.RESULT.ERR_EVENT_ALREADY_LOADED )
		{
			Log.Warning( "[FMOD] Unable to load {0} - bank already loaded. This may occur when attempting to load another localized bank before the first is unloaded, or if a bank has been loaded via the API." );
		}
		else
		{
			throw new BankLoadException( bankPath, loadResult );
		}

		ExecuteSampleLoadRequestsIfReady();
	}

	private void ExecuteSampleLoadRequestsIfReady()
	{
		if ( sampleLoadRequests.Count > 0 )
		{
			foreach ( string bankName in sampleLoadRequests )
			{
				if ( !loadedBanks.ContainsKey( bankName ) )
				{
					// Not ready
					return;
				}
			}

			// All requested banks are loaded, so we can now load sample data
			foreach ( string bankName in sampleLoadRequests )
			{
				LoadedBank loadedBank = loadedBanks[bankName];
				CheckInitResult( loadedBank.Bank.loadSampleData(),
					string.Format( "Loading sample data for bank: {0}", bankName ) );
			}

			sampleLoadRequests.Clear();
		}
	}
}
