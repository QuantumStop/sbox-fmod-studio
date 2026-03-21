namespace Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using FMOD;
using FMOD.Studio;
using FMODSbox;

/// <summary>
/// Lightweight FMOD Studio system for editor auditioning of events before the game is running.
/// Loads banks from "Assets/BankFolder" and allows playing events + previewing parameters.
/// </summary>
public static class FMODEditorPreviewPlayer
{
	private static FMOD.Studio.System _studio;
	private static FMOD.System _core;
	private static readonly List<Bank> _banks = [];
	private static readonly List<IntPtr> _playingHandles = [];
	private static ChannelGroup _masterGroup;
	private static DSP _masterDsp;

	private static DateTime _banksLoadedFromUtc = DateTime.MinValue;
	private static string _banksFolder = null;

	public static bool IsInitialized => _studio.isValid();

	public sealed class PreviewPrefs
	{
		public float Volume { get; set; } = 1f;
		public bool Muted { get; set; } = false;
		public bool Loop { get; set; } = false;
	}

	private static readonly JsonSerializerOptions PrefsJsonOptions = new() { WriteIndented = true };
	private static PreviewPrefs _lastSavedPrefs;

	private static string GetPrefsPath() =>
		Path.Combine( FMODEventResourceGenerator.GetGeneratedRoot(), ".fmod_preview_prefs.json" );

	public static void EnsureInitialized( bool forceReloadBanks = false )
	{
		RuntimeUtils.EnforceLibraryOrder();

		if ( !_studio.isValid() )
		{
			if ( FMOD.Studio.System.create( out _studio ) != RESULT.OK )
				return;

			if ( _studio.getCoreSystem( out _core ) != RESULT.OK )
			{
				_studio.release();
				_studio.clearHandle();
				return;
			}

			_core.setOutput( OUTPUTTYPE.AUTODETECT );
			_core.set3DSettings( 1, 1, 1 );

			var studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS;
			var coreInitFlags = FMOD.INITFLAGS.NORMAL | FMOD.INITFLAGS._3D_RIGHTHANDED;

			// Keep channel count low; this is for auditioning only.
			if ( _studio.initialize( 128, studioInitFlags, coreInitFlags, IntPtr.Zero ) != RESULT.OK )
			{
				_studio.release();
				_studio.clearHandle();
				return;
			}

			_studio.setNumListeners( 1 );
			ApplyDefaultListener();

			TrySetupMasterMetering();
		}

		LoadBanksIfNeeded( forceReloadBanks );
	}

	public static PreviewPrefs LoadPrefs()
	{
		if ( _lastSavedPrefs is not null )
			return _lastSavedPrefs;

		try
		{
			var path = GetPrefsPath();
			if ( File.Exists( path ) )
			{
				var prefs = JsonSerializer.Deserialize<PreviewPrefs>( File.ReadAllText( path ) );
				if ( prefs is not null )
				{
					prefs.Volume = Math.Clamp( prefs.Volume, 0f, 1f );
					_lastSavedPrefs = prefs;
					return _lastSavedPrefs;
				}
			}
		}
		catch { }

		_lastSavedPrefs = new PreviewPrefs();
		return _lastSavedPrefs;
	}

	public static void TrySyncPrefs( float volume, bool muted, bool loop )
	{
		var current = new PreviewPrefs { Volume = volume, Muted = muted, Loop = loop };

		if ( _lastSavedPrefs is not null
			&& _lastSavedPrefs.Volume == current.Volume
			&& _lastSavedPrefs.Muted == current.Muted
			&& _lastSavedPrefs.Loop == current.Loop )
			return;

		try
		{
			var path = GetPrefsPath();
			Directory.CreateDirectory( Path.GetDirectoryName( path )! );
			File.WriteAllText( path, JsonSerializer.Serialize( current, PrefsJsonOptions ) );
			_lastSavedPrefs = current;
		}
		catch { }
	}

	public static void UpdatePrefsCache( float volume, bool muted, bool loop )
	{
		_lastSavedPrefs = new PreviewPrefs { Volume = volume, Muted = muted, Loop = loop };
	}

	private static void TrySetupMasterMetering()
	{
		try
		{
			if ( !_core.hasHandle() )
				return;

			if ( _core.getMasterChannelGroup( out _masterGroup ) != RESULT.OK || !_masterGroup.hasHandle() )
				return;

			// Enable metering on the tail DSP so we see audible output.
			if ( _masterGroup.getNumDSPs( out var num ) == RESULT.OK && num > 0 )
			{
				var tailIndex = Math.Max( 0, num - 1 );
				if ( _masterGroup.getDSP( tailIndex, out _masterDsp ) == RESULT.OK && _masterDsp.hasHandle() )
					_masterDsp.setMeteringEnabled( true, true );
			}
		}
		catch
		{
			_masterGroup.clearHandle();
			_masterDsp.clearHandle();
		}
	}

	public static bool TryGetOutputRms( out float rms01 )
	{
		rms01 = 0f;

		if ( !_studio.isValid() )
			return false;

		if ( !_masterDsp.hasHandle() )
			TrySetupMasterMetering();

		if ( !_masterDsp.hasHandle() )
			return false;

		// FMOD reports metering in dB (typically 0..-80) for peak/rms. Convert to linear 0..1.
		return TryGetDspOutputRms01( _masterDsp, out rms01 );
	}

	public static bool TrySetupInstanceMetering( EventInstance instance, out ChannelGroup group, out DSP headDsp )
	{
		group = default;
		headDsp = default;

		if ( !instance.isValid() )
			return false;

		if ( instance.getChannelGroup( out group ) != RESULT.OK || !group.hasHandle() )
			return false;

		// Enable metering on the tail DSP in this event's chain.
		// (The head DSP can be "upstream" of audible output depending on routing.)
		if ( group.getNumDSPs( out var num ) != RESULT.OK || num <= 0 )
			return false;

		var tailIndex = Math.Max( 0, num - 1 );
		if ( group.getDSP( tailIndex, out headDsp ) != RESULT.OK || !headDsp.hasHandle() )
			return false;

		headDsp.setMeteringEnabled( true, true );
		return true;
	}

	public static bool TryGetDspOutputRms01( DSP dsp, out float rms01 )
	{
		rms01 = 0f;
		if ( !_studio.isValid() || !dsp.hasHandle() )
			return false;

		// FMOD reports metering in dB (typically 0..-80) for peak/rms. Convert to linear 0..1.
		// Depending on where we tap the DSP chain, "output" can be silent; prefer the louder of input/output.
		if ( dsp.getMeteringInfo( out var inputInfo, out var outputInfo ) != RESULT.OK )
			return false;

		static float MeterTo01( float v )
		{
			// Linear meters are typically 0..1. dB meters are typically <= 0.
			if ( v <= 0f )
				return Math.Clamp( MathF.Pow( 10f, v / 20f ), 0f, 1f );
			return Math.Clamp( v, 0f, 1f );
		}

		static bool TryCompute( FMOD.DSP_METERING_INFO info, out float value01 )
		{
			value01 = 0f;
			var channels = Math.Clamp( (int)info.numchannels, 1, 32 );
			var levels = info.peaklevel;
			if ( levels is null || levels.Length < channels )
				levels = info.rmslevel;
			if ( levels is null || levels.Length < channels )
				return false;

			var sum = 0f;
			for ( int i = 0; i < channels; i++ )
				sum += MeterTo01( levels[i] );
			value01 = sum / channels;
			return true;
		}

		var hasIn = TryCompute( inputInfo, out var in01 );
		var hasOut = TryCompute( outputInfo, out var out01 );
		if ( !hasIn && !hasOut )
			return false;

		rms01 = Math.Max( in01, out01 );
		return true;
	}

	private static void LoadBanksIfNeeded( bool forceReloadBanks )
	{
		var bankFolder = FMODManagerSystem.GetBankFolderLocation();
		if ( string.IsNullOrWhiteSpace( bankFolder ) || !Directory.Exists( bankFolder ) )
			return;

		var bankFiles = Directory.GetFiles( bankFolder, "*.bank", SearchOption.TopDirectoryOnly )
			.Where( p => !p.EndsWith( ".assets.bank", StringComparison.OrdinalIgnoreCase ) )
			.ToArray();

		var newestWriteUtc = bankFiles.Select( File.GetLastWriteTimeUtc ).DefaultIfEmpty( DateTime.MinValue ).Max();

		if ( !forceReloadBanks
			&& string.Equals( _banksFolder, bankFolder, StringComparison.OrdinalIgnoreCase )
			&& _banksLoadedFromUtc == newestWriteUtc
			&& _banks.Count > 0 )
		{
			return;
		}

		UnloadBanks();

		foreach ( var bankPath in bankFiles )
		{
			if ( _studio.loadBankFile( bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out var bank ) == RESULT.OK && bank.isValid() )
			{
				_banks.Add( bank );
			}
		}

		_banksFolder = bankFolder;
		_banksLoadedFromUtc = newestWriteUtc;
	}

	private static void UnloadBanks()
	{
		StopAll( immediate: true );

		foreach ( var bank in _banks )
		{
			try { if ( bank.isValid() ) bank.unload(); }
			catch { }
		}

		_banks.Clear();
	}

	private static void ApplyDefaultListener()
	{
		var attrs = FMODSbox.RuntimeUtils.To3DAttributes( Vector3.Zero );
		_studio.setListenerAttributes( 0, attrs );
	}

	public static bool TryGetEventDescription( string eventPath, out EventDescription desc )
	{
		desc = default;

		EnsureInitialized();
		if ( !_studio.isValid() )
			return false;

		return _studio.getEvent( eventPath, out desc ) == RESULT.OK && desc.isValid();
	}

	/// <summary>
	/// Force a synchronous Studio update. Useful for short-lived editor baking/scrubbing workflows.
	/// </summary>
	public static void UpdateOnce()
	{
		if ( !_studio.isValid() )
			return;

		ApplyDefaultListener();
		_studio.update();
	}

	public static EventInstance Play( string eventPath, Vector3 position = default, bool startPaused = false )
	{
		EnsureInitialized();
		if ( !_studio.isValid() )
			return default;

		if ( _studio.getEvent( eventPath, out var desc ) != RESULT.OK || !desc.isValid() )
			return default;

		if ( desc.createInstance( out var instance ) != RESULT.OK || !instance.isValid() )
			return default;

		if ( desc.is3D( out var is3d ) == RESULT.OK && is3d )
		{
			instance.set3DAttributes( RuntimeUtils.To3DAttributes( position ) );
		}

		if ( startPaused )
			instance.setPaused( true );

		instance.start();

		_playingHandles.Add( instance.handle );

		return instance;
	}

	public static void Stop( EventInstance instance, bool immediate = false )
	{
		if ( !instance.isValid() )
			return;

		try
		{
			instance.stop( immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT );
		}
		catch { }

		try { instance.release(); }
		catch { }

		_playingHandles.RemoveAll( h => h == instance.handle );
	}

	public static void StopAll( bool immediate = false )
	{
		if ( !_studio.isValid() )
			return;

		foreach ( var handle in _playingHandles.ToArray() )
		{
			var inst = new EventInstance { handle = handle };
			if ( inst.isValid() )
			{
				Stop( inst, immediate );
			}
		}

		_playingHandles.Clear();
	}

	public static void SetParameter( EventInstance instance, string name, float value, bool ignoreSeekSpeed = false )
	{
		if ( !instance.isValid() || string.IsNullOrWhiteSpace( name ) )
			return;

		instance.setParameterByName( name.Trim(), value, ignoreSeekSpeed );
	}

	public static void SetParameter( EventInstance instance, string name, string label, bool ignoreSeekSpeed = false )
	{
		if ( !instance.isValid() || string.IsNullOrWhiteSpace( name ) || string.IsNullOrWhiteSpace( label ) )
			return;

		instance.setParameterByNameWithLabel( name.Trim(), label.Trim(), ignoreSeekSpeed );
	}

	public static void SetMasterVolume( float volume01 )
	{
		if ( !_studio.isValid() || !_masterGroup.hasHandle() )
			return;

		_masterGroup.setVolume( Math.Clamp( volume01, 0f, 1f ) );
	}

	public static void SetPaused( EventInstance instance, bool paused )
	{
		if ( !instance.isValid() )
			return;
		instance.setPaused( paused );
	}

	public static bool IsPaused( EventInstance instance )
	{
		if ( !instance.isValid() )
			return false;
		instance.getPaused( out var paused );
		return paused;
	}

	public static IReadOnlyList<string> GetParameterLabels( EventDescription desc, string parameterName, int max = 128 )
	{
		if ( !desc.isValid() || string.IsNullOrWhiteSpace( parameterName ) )
			return [];

		var labels = new List<string>();
		for ( int i = 0; i < max; i++ )
		{
			var result = desc.getParameterLabelByName( parameterName, i, out var label );
			if ( result != RESULT.OK || string.IsNullOrWhiteSpace( label ) )
				break;

			labels.Add( label );
		}

		return labels;
	}

	[EditorEvent.Frame]
	private static void Tick()
	{
		if ( !_studio.isValid() )
			return;

		ApplyDefaultListener();

		_studio.update();

		// prune stopped instances
		for ( int i = _playingHandles.Count - 1; i >= 0; i-- )
		{
			var inst = new EventInstance { handle = _playingHandles[i] };
			if ( !inst.isValid() )
			{
				_playingHandles.RemoveAt( i );
				continue;
			}

			if ( inst.getPlaybackState( out var state ) == RESULT.OK && state == PLAYBACK_STATE.STOPPED )
			{
				try { inst.release(); } catch { }
				_playingHandles.RemoveAt( i );
			}
		}
	}
}
