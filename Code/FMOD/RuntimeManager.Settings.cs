namespace FMODSbox;

public enum ImportType
{
	StreamingAssets,
	AssetBundle,
}
public enum BankLoadType
{
	All,
	Specified,
	None
}
public enum MeterChannelOrderingType
{
	Standard,
	SeparateLFE,
	Positional
}
public enum EventLinkage
{
	Path,
	GUID,
}


public partial class FMODManager
{
	private static Settings fmodSettings => new();

	/// <summary>
	/// This is where we store settings for all the shit, and since there is no platform choice we can just hardcode it. 
	/// The exact definition of overengineering, but I don't care.
	/// </summary>
	private readonly struct Settings
	{
		[Property, ReadOnly] public bool AutomaticEventLoading { get; init; }

		[Property, ReadOnly] public BankLoadType BankLoadType { get; init; }

		[Property, ReadOnly] public bool AutomaticSampleLoading { get; init; }
		[Property, ReadOnly] public int SampleRate { get; init; }
		[Property, ReadOnly] public int RealChannels { get; init; }
		[Property, ReadOnly] public int VirtualChannels { get; init; }
		[Property, ReadOnly] public uint DSPBufferLength { get; init; }
		[Property, ReadOnly] public int DSPBufferCount { get; init; }
		[Property, ReadOnly] public FMOD.SPEAKERMODE SpeakerMode { get; init; }
		[Property, ReadOnly] public FMOD.OUTPUTTYPE OutputType { get; init; }
		[Property, ReadOnly] public ushort ProfilerPort { get; init; }
		[Property, ReadOnly] public string BankFolder { get; init; }
		[Property, ReadOnly] public bool StopEventsOutsideMaxDistance { get; init; }
		[Property, ReadOnly] public ImportType ImportType { get; init; }
		public string BankFolderLocation { get => Game.IsEditor ? $"{Project.Current.GetAssetsPath()}\\{fmodSettings.BankFolder}" : System.IO.Path.GetFullPath( $"Assets\\{fmodSettings.BankFolder}" ); }

		public Settings()
		{
			AutomaticEventLoading = true;
			BankLoadType = BankLoadType.All;
			AutomaticSampleLoading = true;
			SampleRate = 48000;
			RealChannels = 256;
			VirtualChannels = 512;
			DSPBufferLength = 1024;
			DSPBufferCount = 4;
			SpeakerMode = FMOD.SPEAKERMODE.STEREO;
			OutputType = FMOD.OUTPUTTYPE.AUTODETECT;
			ProfilerPort = 9264;
			BankFolder = "fmod";
			StopEventsOutsideMaxDistance = false;
		}
	}
}
