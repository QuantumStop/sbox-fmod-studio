namespace FMODSbox;

public partial class FMODManagerSystem
{
	public static void LoadBank( string bankName, bool loadSamples = false )
	{
		LoadBank( bankName, loadSamples, bankName );
	}

	private static void LoadBank( string bankName, bool loadSamples, string bankId )
	{
		if ( Current._loadedBanks.ContainsKey( bankId ) )
		{
			ReferenceLoadedBank( bankId, loadSamples );
		}
		else
		{
			const string BankExtension = ".bank";

			string bankPath;
			string assetsFolder = Game.IsEditor ? $"{Project.Current.GetAssetsPath()}\\{fmodSettings.BankFolder}" : System.IO.Path.GetFullPath( $"Assets\\{fmodSettings.BankFolder}" );

			if ( System.IO.Path.GetExtension( bankName ) != BankExtension )
			{
				bankPath = string.Format( "{0}\\{1}{2}", assetsFolder, bankName, BankExtension );
			}
			else
			{
				bankPath = string.Format( "{0}\\{1}", assetsFolder, bankName );
			}

			Current.loadingBanksRef++;

			LoadedBank loadedBank = new();
			FMOD.RESULT loadResult = Current.studioSystem.loadBankFile( bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank );
			Current.RegisterLoadedBank( loadedBank, bankPath, bankId, loadSamples, loadResult );

			Current.loadingBanksRef--;
		}
	}
	public static bool IsInitialized => Current != null && Current.studioSystem.isValid();

	public static bool HaveAllBanksLoaded => Current.loadingBanksRef == 0;

	/*
	public static bool HaveMasterBanksLoaded
	{
		get
		{
			var banks = Current.Banks;
			foreach ( var bank in banks )
			{
				if ( !HasBankLoaded( bank. ) ) return false;
			}
			return true;
		}
	}
	*/
	public static bool HasBankLoaded( string loadedBank )
	{
		return Current._loadedBanks.ContainsKey( loadedBank );
	}

	private static void ReferenceLoadedBank( string bankName, bool loadSamples )
	{
		LoadedBank loadedBank = Current._loadedBanks[bankName];
		loadedBank.RefCount++;

		if ( loadSamples )
		{
			loadedBank.Bank.loadSampleData();
		}

		Current._loadedBanks[bankName] = loadedBank; // Save the incremented reference count
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

			Current._loadedBanks.Add( bankName, loadedBank );
			MarkEventCacheDirty();
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
		if ( _sampleLoadRequests.Count > 0 )
		{
			foreach ( string bankName in _sampleLoadRequests )
			{
				if ( !_loadedBanks.ContainsKey( bankName ) )
				{
					// Not ready
					return;
				}
			}

			// All requested banks are loaded, so we can now load sample data
			foreach ( string bankName in _sampleLoadRequests )
			{
				LoadedBank loadedBank = _loadedBanks[bankName];
				CheckInitResult( loadedBank.Bank.loadSampleData(),
					string.Format( "Loading sample data for bank: {0}", bankName ) );
			}

			_sampleLoadRequests.Clear();
		}
	}

	private void LoadBanks( Settings fmodSettings )
	{
		if ( fmodSettings.ImportType == ImportType.StreamingAssets )
		{
			if ( fmodSettings.AutomaticSampleLoading )
			{
				_sampleLoadRequests.AddRange( WhichBanksToLoad( fmodSettings ) );
			}

			try
			{
				var WhichBanksToLoadTemp = WhichBanksToLoad( fmodSettings ).ToList();

				foreach ( string bankName in WhichBanksToLoadTemp )
				{
					LoadBank( bankName );
					//	Banks.Remove( bankName );
					//	im confused why it breaks the loading if the manager is spawned using a GOS, but im not complaining, the list is cleared anyway
				}

				WaitForAllSampleLoading();
			}
			catch ( BankLoadException e )
			{
				Log.Warning( e );
			}
		}
	}
	private IEnumerable<string> WhichBanksToLoad( Settings fmodSettings )
	{
		switch ( fmodSettings.BankLoadType )
		{
			case BankLoadType.All:
				//				foreach ( string masterBankFileName in MasterBanks )
				//				{
				//					if ( !string.IsNullOrEmpty( masterBankFileName ) )
				//					{
				//						yield return masterBankFileName + ".strings";
				//						yield return masterBankFileName;
				//					}
				//				}

				var path = Game.IsEditor ? $"{Project.Current.GetAssetsPath()}\\{fmodSettings.BankFolder}" : System.IO.Path.GetFullPath( $"Assets\\{fmodSettings.BankFolder}" );
				var AllBanks = System.IO.Directory.GetFiles( path, "*.bank" ).Select( file => System.IO.Path.GetFileName( file ) ).ToArray();

				foreach ( var bank in AllBanks )
				{
					if ( !string.IsNullOrEmpty( bank ) )
					{
						yield return bank;
					}
				}
				break;
			case BankLoadType.Specified:
				foreach ( var bank in BanksToLoad )
				{
					if ( !string.IsNullOrEmpty( bank ) )
					{
						yield return bank;
					}
				}
				break;
			case BankLoadType.None:
				break;
			default:
				break;
		}
	}

	public static void WaitForAllSampleLoading()
	{
		Current.studioSystem.flushSampleLoading();
	}
}
