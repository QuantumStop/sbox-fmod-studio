namespace Editor;

using System;
using System.Collections.Generic;
using System.IO;
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

	private static void TrySetupMasterMetering()
	{
		try
		{
			if ( !_core.hasHandle() )
				return;

			if ( _core.getMasterChannelGroup( out _masterGroup ) != RESULT.OK || !_masterGroup.hasHandle() )
				return;

			// The channel group has a DSP chain - enable metering on the head DSP so we can sample output level.
			if ( _masterGroup.getDSP( 0, out _masterDsp ) == RESULT.OK && _masterDsp.hasHandle() )
			{
				_masterDsp.setMeteringEnabled( false, true );
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

		if ( !_studio.isValid() || !_masterDsp.hasHandle() )
			return false;

		// FMOD reports metering in dB (typically 0..-80) for peak/rms. Convert to linear 0..1.
		if ( _masterDsp.getMeteringInfo( IntPtr.Zero, out var outputInfo ) != RESULT.OK )
			return false;

		var channels = Math.Clamp((int)outputInfo.numchannels, 1, 32);
		if ( outputInfo.rmslevel is null || outputInfo.rmslevel.Length < channels )
			return false;

		var sum = 0f;
		for ( int i = 0; i < channels; i++ )
		{
			var v = outputInfo.rmslevel[i];

			// If it looks like dB (<= 0), convert to amplitude.
			if ( v <= 0f )
			{
				var amp = MathF.Pow( 10f, v / 20f );
				sum += Math.Clamp( amp, 0f, 1f );
			}
			else
			{
				sum += Math.Clamp( v, 0f, 1f );
			}
		}

		rms01 = sum / channels;
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

	public static EventInstance Play( string eventPath, Vector3 position = default )
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
