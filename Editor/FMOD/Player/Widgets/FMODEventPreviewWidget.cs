namespace Editor.Assets;

using FMODSbox;
using Sandbox;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

sealed class FMODEventPreviewWidget : Widget
{
	private const float HeaderHeight = 30f;
	private const float RowHeight = 26f;

	public Asset Asset
	{
		get => _asset;
		set
		{
			_asset = value;
			Reload();
		}
	}
	private Asset _asset;

	private Pixmap _pixmap;

	private FMODEventResource _resource;
	private FMOD.Studio.EventInstance _instance;
	private FMOD.Studio.EventDescription _desc;
	private int _eventLengthMs;

	private Label _path;
	private Label _error;
	private IconButton _refreshBanks;

	private VolumeSliderWidget _volumeSlider;
	private WaveformTimelineSlider _timeline;
	private WaveformBackgroundWidget _timelineBg;
	private readonly List<float> _timelineRms = [];

	private ScrollArea _paramScroll;
	private Widget _paramCanvas;
	private Label _paramHeader;
	private Label _noParams;

	private IconButton _stopButton;
	private IconButton _playButton;
	private IconButton _pauseButton;
	private IconButton _loopButton;
	private TimerWidget _timeDisplay;
	private Label _statusLabel;
	
	private bool _lastPlaying;
	private bool _lastPaused;
	private bool _loop = false;

	private readonly Dictionary<string, ThemedFloatSlider> _floatSliders = [];
	private readonly Dictionary<string, ComboBox> _labelCombos = [];

	private CancellationTokenSource _waveCts;
	private string _lastWaveKey;
	private readonly Dictionary<string, string> _audioPathCache = new( StringComparer.OrdinalIgnoreCase );

	internal FMODEditorPreviewPlayer.PreviewPrefs _pendingPrefs;

	public void TrySyncPrefs()
	{
		_pendingPrefs ??= new FMODEditorPreviewPlayer.PreviewPrefs();
		_pendingPrefs.Volume = _volumeSlider?.Volume ?? _pendingPrefs.Volume;
		_pendingPrefs.Muted = _volumeSlider?.Muted ?? _pendingPrefs.Muted;
		_pendingPrefs.Loop = _loop;

		FMODEditorPreviewPlayer.TrySyncPrefs( _pendingPrefs.Volume, _pendingPrefs.Muted, _pendingPrefs.Loop );
	}

	public FMODEventPreviewWidget( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.AddSpacingCell( 2 );
		BuildStaticUI();
	}

	private static Layout AddCompactRow( Layout layout, Widget parent, float height = RowHeight, float spacing = 6f )
	{
		var host = new Widget( parent )
		{
			Layout = Layout.Row(),
			HorizontalSizeMode = SizeMode.Expand,
			MinimumHeight = height,
			MaximumHeight = height
		};

		host.Layout.Spacing = spacing;
		host.Layout.Alignment = TextFlag.LeftCenter;
		layout.Add( host );
		return host.Layout;
	}

	private static void Compact( Widget w, float height = RowHeight )
	{
		w.MinimumHeight = height;
		w.MaximumHeight = height;
	}

	private static void CompactSquare( Widget w, float size )
	{
		w.MinimumHeight = size;
		w.MaximumHeight = size;
		w.MinimumWidth = size;
		w.MaximumWidth = size;
	}

	private Widget LogoWidget( Widget parent )
	{
		_pixmap = Pixmap.FromFile( "logo_fmod.png" );
		Widget w = new( parent );
		w.OnPaintOverride += () => { Paint.Draw( w.LocalRect.Shrink( 1 ), _pixmap ); return true; };
		return w;
	}

	private void BuildStaticUI()
	{
		var prefs = FMODEditorPreviewPlayer.LoadPrefs();
		var panel = new Widget( this )
		{
			Layout = Layout.Column()
		};
		panel.SetStyles( "background-color: rgba(25,25,25,0.65); border-radius: 6px; padding: 8px;" );
		Layout.Add( panel, 1 );

		panel.Layout.Spacing = 3;

		var header = AddCompactRow( panel.Layout, panel, HeaderHeight, spacing: 4f );

		header.AddSpacingCell( 8 );

		var logo = header.Add( LogoWidget( panel ) );
		CompactSquare( logo, HeaderHeight - 12 );

		header.AddSpacingCell( 8 );

		_stopButton = header.Add( new IconButton( "stop" ) );
		_stopButton.Background = Color.Transparent;
		_stopButton.IconSize = 18;
		_stopButton.ToolTip = "Stop";
		_stopButton.OnClick += () => Stop();
		CompactSquare( _stopButton, HeaderHeight );

		_playButton = header.Add( new IconButton( "play_arrow" ) );
		_playButton.Background = Color.Transparent;
		_playButton.IconSize = 18;
		_playButton.ToolTip = "Play";
		_playButton.OnClick += () => Play();
		CompactSquare( _playButton, HeaderHeight );

		_pauseButton = header.Add( new IconButton( "pause" ) );
		_pauseButton.Background = Color.Transparent;
		_pauseButton.IconSize = 18;
		_pauseButton.ToolTip = "Pause";
		_pauseButton.OnClick += TogglePause;
		CompactSquare( _pauseButton, HeaderHeight );

		_loopButton = header.Add( new IconButton( "loop" ), 0 );
		_loop = prefs.Loop;
		_loopButton.Background = Color.Transparent;
		_loopButton.IconSize = 18;
		_loopButton.ToolTip = "Loop";
		_loopButton.OnClick += () =>
		{
			_loop = !_loop;
			_loopButton.SetStyles( _loop
				? "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px; background-color: #5a3a8a;"
				: "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px;" );
			TrySyncPrefs();
		};
		_loopButton.SetStyles( _loop
				? "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px; background-color: #5a3a8a;"
				: "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px;" );
		CompactSquare( _loopButton, HeaderHeight );

		header.AddSpacingCell( 8 );

		_volumeSlider = header.Add( new VolumeSliderWidget( this ), 1 );
		_volumeSlider.MinimumWidth = 70;
		_volumeSlider.SetSizeMode( SizeMode.Expand, SizeMode.CanGrow );
		_volumeSlider.Volume = prefs.Volume;
		_volumeSlider.Muted = prefs.Muted;
		_volumeSlider.OnVolumeChanged = () =>
		{
			FMODEditorPreviewPlayer.SetMasterVolume( _volumeSlider.Volume );
			TrySyncPrefs();
		};

		_volumeSlider.OnMuteToggled = () =>
		{
			FMODEditorPreviewPlayer.SetMasterVolume( _volumeSlider.Muted ? 0f : _volumeSlider.Volume );
			TrySyncPrefs();
		};

		FMODEditorPreviewPlayer.SetMasterVolume( prefs.Muted ? 0f : prefs.Volume );

		_statusLabel = header.Add( new Label( "STOPPED" ), 1 );
		_statusLabel.SetStyles( "opacity: 0.6; font-size: 10px; letter-spacing: 1px;" );
		_statusLabel.MinimumWidth = 70;
		_statusLabel.MaximumWidth = 70;
		_statusLabel.Alignment = TextFlag.Center;

		_refreshBanks = header.Add( new IconButton( "library_music" ), 1 );
		_refreshBanks.Background = Color.Transparent;
		_refreshBanks.IconSize = 20;
		_refreshBanks.ToolTip = "Reload FMOD banks (editor preview)";
		_refreshBanks.OnClick += () =>
		{
			FMODEditorPreviewPlayer.EnsureInitialized( forceReloadBanks: true );
			Reload();
		};
		CompactSquare( _refreshBanks, HeaderHeight );

		_timeDisplay = header.Add( new TimerWidget( panel ) );
		_timeDisplay.MinimumWidth = 80;
		_timeDisplay.MaximumWidth = 80;
		Compact( _timeDisplay, HeaderHeight - 6 );
		header.Add( _timeDisplay, 0 );

		var eventRow = new Widget( panel ) { Layout = Layout.Row() };
		eventRow.Layout.Spacing = 4;
		eventRow.Layout.Alignment = TextFlag.LeftCenter;
		panel.Layout.Add( eventRow );

		var pathLabel = eventRow.Layout.Add( new Label( "Event Path:" ) );
		pathLabel.SetStyles( "opacity: 0.75; font-size: 10px; text-transform: uppercase; letter-spacing: 1px;" );
		_path = eventRow.Layout.Add( new Label( "" ), 1 );
		_path.SetStyles( "opacity: 0.75; font-size: 11px;" );

		_error = panel.Layout.Add( new Label( "" ) );
		_error.SetStyles( "opacity: 0.9; color: rgba(255,120,120,0.95); font-size: 11px;" );
		_error.Visible = false;

		var timelineContainer = new TimelineContainerWidget( panel )
		{
			MinimumHeight = 64,
			MaximumHeight = 64,
			HorizontalSizeMode = SizeMode.Expand,
			VerticalSizeMode = SizeMode.CanGrow
		};
		panel.Layout.Add( timelineContainer, 1 );

		var timelineBg = new WaveformBackgroundWidget( timelineContainer, _timelineRms )
		{
			HorizontalSizeMode = SizeMode.Expand,
			VerticalSizeMode = SizeMode.Expand
		};
		_timelineBg = timelineBg;

		_timeline = new WaveformTimelineSlider( timelineContainer, _timelineRms, timelineBg );
		_timeline.HorizontalSizeMode = SizeMode.Ignore;
		_timeline.VerticalSizeMode = SizeMode.Ignore;

		_timeline.TranslucentBackground = true;
		_timeline.NoSystemBackground = true;
		_timeline.Minimum = 0;
		_timeline.Maximum = 1;
		_timeline.Step = 1f;
		_timeline.OnValueEdited = SeekTimeline;
		_timeline.Enabled = false;

		_paramHeader = panel.Layout.Add( new Label( "Parameters" ), 0 );
		_paramHeader.Height = 1f;
		_paramHeader.SetStyles( "opacity: 0.75; font-size: 10px; text-transform: uppercase; letter-spacing: 1px;" );

		panel.Layout.AddStretchCell();

		_noParams = panel.Layout.Add( new Label( "No parameters" ) );
		_noParams.SetStyles( "opacity: 0.7;" );
		_noParams.Visible = false;

		_paramScroll = panel.Layout.Add( new ScrollArea( this ), 1 );
		_paramScroll.NoSystemBackground = true;
		_paramScroll.TranslucentBackground = true;
		_paramScroll.Canvas = _paramCanvas = new Widget( _paramScroll )
		{
			Layout = Layout.Column(),
			HorizontalSizeMode = SizeMode.Expand | SizeMode.CanGrow,
			VerticalSizeMode = SizeMode.Ignore
		};
		_paramCanvas.Layout.Spacing = 4f;

		CompactSquare( _stopButton, HeaderHeight - 6 );
		CompactSquare( _playButton, HeaderHeight - 6 );
		CompactSquare( _pauseButton, HeaderHeight - 6 );
		CompactSquare( _loopButton, HeaderHeight - 6 );
	}

	private bool IsPlaying()
	{
		if ( !_instance.isValid() )
			return false;

		if ( _instance.getPlaybackState( out var state ) != FMOD.RESULT.OK )
			return false;

		return state == FMOD.Studio.PLAYBACK_STATE.PLAYING || state == FMOD.Studio.PLAYBACK_STATE.STARTING;
	}

	private void UpdateTransportUI()
	{
		var playing = IsPlaying();
		var paused = _instance.isValid() && FMODEditorPreviewPlayer.IsPaused( _instance );

		_stopButton.SetStyles( "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px;" );

		_playButton.SetStyles( playing && !paused
			? "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px; background-color: #3a6a3a;"
			: "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px;" );

		_pauseButton.SetStyles( paused
			? "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px; background-color: #6a5a1a;"
			: "border: 1px solid rgba(255,255,255,0.12); border-radius: 4px; padding: 3px;" );

		_statusLabel.Text = paused ? "PAUSED" : playing ? "PLAYING" : "STOPPED";
		_statusLabel.SetStyles( paused
			? "color: rgba(255,210,80,0.85); font-size: 10px; letter-spacing: 1px;"
			: playing
				? "color: rgba(100,220,100,0.85); font-size: 10px; letter-spacing: 1px;"
				: "opacity: 0.4; font-size: 10px; letter-spacing: 1px;" );
	}

	private void Reload()
	{
		Stop( immediate: true );

		_resource = null;
		_desc = default;
		_eventLengthMs = 0;

		if ( _asset == null )
		{
			_path.Text = "";
			_error.Visible = false;
			RebuildParameterUI( [] );
			return;
		}

		if ( _asset.TryLoadResource<FMODEventResource>( out var obj ) )
		{
			_resource = obj;
		}

		if ( _resource == null || string.IsNullOrWhiteSpace( _resource ) )
		{
			_path.Text = "";
			_error.Text = "Could not load resource or missing EventPath.";
			_error.Visible = true;
			RebuildParameterUI( [] );
			return;
		}

		_path.Text = _resource;
		_error.Visible = false;

		_eventLengthMs = _resource.LengthMs;

		if ( FMODEditorPreviewPlayer.TryGetEventDescription( _resource, out var desc ) )
		{
			_desc = desc;

			if ( _eventLengthMs <= 0 && _desc.isValid() && _desc.getLength( out var len ) == FMOD.RESULT.OK )
				_eventLengthMs = len;
		}

		RebuildParameterUI( _resource.Parameters ?? [] );
		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource?.EventPath );
		UpdateTransportUI();
	}

	private void RebuildParameterUI( IEnumerable<FMODEventResource.FMODEventParameter> parameters )
	{
		_paramCanvas.Layout.Clear( true );
		_floatSliders.Clear();
		_labelCombos.Clear();

		var list = parameters?.ToList() ?? [];
		if ( list.Count == 0 )
		{
			_paramHeader.Visible = false;
			_paramScroll.Visible = false;
			_noParams.Visible = true;
			_paramCanvas.AdjustSize();
			return;
		}

		_paramHeader.Visible = true;
		_paramScroll.Visible = true;
		_noParams.Visible = false;

		foreach ( var p in list.OrderBy( x => x.Name ) )
		{
			if ( string.IsNullOrWhiteSpace( p?.Name ) )
				continue;

			var host = new Widget( _paramCanvas )
			{
				Layout = Layout.Row(),
				HorizontalSizeMode = SizeMode.CanGrow,
				VerticalSizeMode = SizeMode.CanGrow,
				MinimumHeight = RowHeight,
				MaximumHeight = RowHeight
			};
			host.Layout.Spacing = 2;
			host.Layout.Alignment = TextFlag.LeftCenter;
			_paramCanvas.Layout.Add( host );

			var row = host.Layout;

			var name = row.Add( new GradientLabel( host ) );
			name.Text = p.Name;
			name.AccentColor = p.IsLabeled ? Color.Parse( "#454852" )!.Value : Color.Parse( "#524648" )!.Value;
			name.MinimumWidth = 120;
			name.MaximumWidth = 120;

			if ( p.IsLabeled )
			{
				var combo = row.Add( new ComboBox(), 1 );
				combo.MinimumWidth = 220;
				combo.Enabled = _desc.isValid();
				Compact( combo );

				if ( _desc.isValid() )
				{
					foreach ( var label in FMODEditorPreviewPlayer.GetParameterLabels( _desc, p.Name ) )
					{
						var captured = label;
						combo.AddItem( label, null, () =>
						{
							if ( _instance.isValid() )
								FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, captured );
						} );
					}
				}

				_labelCombos[p.Name] = combo;
			}
			else
			{
				var min = MathF.Min( p.Min, p.Max );
				var max = MathF.Max( p.Min, p.Max );
				var def = Math.Clamp( p.Default, min, max );

				var rangeLabel = row.Add( new Label( $"{min:0.###}..{max:0.###}" ) );
				rangeLabel.Alignment = TextFlag.LeftCenter;
				rangeLabel.MinimumWidth = 46;
				rangeLabel.MaximumWidth = 46;
				rangeLabel.SetStyles( "opacity: 0.55; font-size: 10px;" );

				row.AddSpacingCell( 2 );

				var slider = row.Add( new ThemedFloatSlider( this ), 1 );
				slider.Minimum = min;
				slider.Maximum = max;
				slider.Value = def;
				slider.Step = p.IsDiscrete ? 1f : MathF.Max( (max - min) / 200f, 0.001f );
				slider.OnValueEdited = () =>
				{
					if ( _instance.isValid() )
						FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, slider.Value );
				};
				Compact( slider, 16f );

				row.AddSpacingCell( 2 );

				var value = row.Add( new LineEdit() );
				value.Text = def.ToString( "0.###", CultureInfo.InvariantCulture );
				value.MinimumWidth = 60;
				value.MaximumWidth = 60;
				Compact( value );
				value.EditingFinished += () =>
				{
					if ( float.TryParse( value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v ) )
					{
						v = Math.Clamp( v, min, max );
						slider.Value = v;

						if ( _instance.isValid() )
							FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, v );
					}

					value.Text = slider.Value.ToString( "0.###", CultureInfo.InvariantCulture );
				};

				slider.OnValueEdited += () =>
				{
					value.Text = slider.Value.ToString( "0.###", CultureInfo.InvariantCulture );
				};

				_floatSliders[p.Name] = slider;
			}
		}

		_paramCanvas.AdjustSize();
	}

	private void Play()
	{
		if ( _resource == null || string.IsNullOrWhiteSpace( _resource ) )
			return;

		// If we're pasued we won't restart the whole sequence
		if ( _instance.isValid() && FMODEditorPreviewPlayer.IsPaused( _instance ) )
		{
			FMODEditorPreviewPlayer.SetPaused( _instance, false );
			return;
		}

		_error.Visible = false;
		Stop( immediate: true );

		_instance = FMODEditorPreviewPlayer.Play( _resource, Vector3.Zero, startPaused: true );
		if ( !_instance.isValid() )
		{
			_error.Text = $"Failed to play: {_resource}";
			_error.Visible = true;
			UpdateTransportUI();
			return;
		}

		if ( _eventLengthMs <= 0 && _desc.isValid() && _desc.getLength( out var len ) == FMOD.RESULT.OK )
			_eventLengthMs = len;

		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource?.EventPath );
		var startPosMs = (int)_timeline.Value;

		// Apply slider and label defaults to the new instance
		foreach ( var kv in _floatSliders )
		{
			FMODEditorPreviewPlayer.SetParameter( _instance, kv.Key, kv.Value.Value );
		}

		foreach ( var kv in _labelCombos )
		{
			if ( !string.IsNullOrWhiteSpace( kv.Value.CurrentText ) )
				FMODEditorPreviewPlayer.SetParameter( _instance, kv.Key, kv.Value.CurrentText );
		}

		// Start playback immediately; waveform generation should not pause/resume audio.
		startPosMs = Math.Clamp( startPosMs, 0, Math.Max( 1, _eventLengthMs ) );
		_instance.setTimelinePosition( startPosMs );
		_instance.setPaused( false );
		FMODEditorPreviewPlayer.UpdateOnce();

		_ = RegenerateWaveformOnPlayAsync( startPosMs );

		UpdateTransportUI();
	}

	public void Stop( bool immediate = false )
	{
		CancelWaveRegen();

		if ( _instance.isValid() )
			FMODEditorPreviewPlayer.Stop( _instance, immediate );

		_instance = default;
		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource?.EventPath );
		UpdateTransportUI();
	}

	private void TogglePause()
	{
		if ( !_instance.isValid() )
			return;

		var paused = FMODEditorPreviewPlayer.IsPaused( _instance );
		FMODEditorPreviewPlayer.SetPaused( _instance, !paused );
	}

	private void UpdateTimelineUI( bool enabled, int lengthMs, string seedKey )
	{
		_timeline.Enabled = enabled;
		_timeline.LengthMs = lengthMs;
		_timeline.Minimum = 0f;
		_timeline.Maximum = Math.Max( 1, lengthMs );
		_timeline.Step = 1f;

		// Keep current scrub position when stopping/starting; just clamp to length.
		_timeline.Value = Math.Clamp( _timeline.Value, 0f, Math.Max( 1, lengthMs ) );
		if ( !_instance.isValid() )
			_timeline.EnsureStaticWave( seedKey ?? string.Empty, lengthMs );
	}

	private void CancelWaveRegen()
	{
		try { _waveCts?.Cancel(); } catch { }
		try { _waveCts?.Dispose(); } catch { }
		_waveCts = null;
	}

	private async Task RegenerateWaveformOnPlayAsync( int startPosMs )
	{
		CancelWaveRegen();

		if ( !_instance.isValid() )
			return;

		_waveCts = new CancellationTokenSource();
		var token = _waveCts.Token;

		try
		{
			// Capture the dominant sound while audio is playing.
			// Some events (streams / transitions) need a few updates before any channels exist.

			FMOD.ChannelGroup rootGroup = default;
			FMOD.Sound sound = default;
			string soundName = null;
			var found = false;

			for ( int i = 0; i < 60; i++ )
			{
				token.ThrowIfCancellationRequested();
				FMODEditorPreviewPlayer.UpdateOnce();

				if ( !_instance.isValid() )
					return;

				if ( _instance.getChannelGroup( out rootGroup ) == FMOD.RESULT.OK && rootGroup.hasHandle() )
				{
					if ( TryFindDominantSound( rootGroup, out sound, out soundName ) )
					{
						found = true;
						break;
					}
				}

				await Task.Delay( 3, token );
			}

			FMODEditorPreviewPlayer.UpdateOnce();

			if ( !found || !sound.hasHandle() )
				return;

			var waveCount = Math.Max( 64, _timeline.MaxSamples );
			if ( sound.getFormat( out _, out var fmt, out var channels, out var bits ) != FMOD.RESULT.OK || channels <= 0 )
				return;

			// Wait briefly for the sound to be readable
			for ( int i = 0; i < 50; i++ )
			{
				token.ThrowIfCancellationRequested();
				if ( sound.getOpenState( out var state, out var percent, out _, out _ ) == FMOD.RESULT.OK )
				{
					if ( state == FMOD.OPENSTATE.READY && percent >= 100 )
						break;
					if ( state == FMOD.OPENSTATE.READY )
						break;
				}

				await Task.Delay( 2, token );
			}

			var key = $"{soundName}|{fmt}|{channels}|{bits}|{waveCount}";
			if ( !string.Equals( _lastWaveKey, key, StringComparison.Ordinal ) || _timelineRms.Count != waveCount )
			{
				var waveform = await TryBuildWaveformFromAudioFileAsync( soundName, waveCount, token )
					?? await BuildWaveformFromSoundAsync( sound, fmt, channels, bits, waveCount, token );
				if ( waveform is { Length: >= 2 } )
				{
					_lastWaveKey = key;
					_timelineRms.Clear();
					_timelineRms.AddRange( waveform );
					_timelineBg?.Update();
				}
			}
		}
		catch ( OperationCanceledException )
		{
			// ignored
		}
	}

	private async Task<float[]> TryBuildWaveformFromAudioFileAsync( string soundName, int waveCount, CancellationToken token )
	{
		var path = TryResolveAudioPath( soundName );
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		try
		{
			var soundFile = SoundFile.Load( path );
			if ( !soundFile.IsValid() )
				return null;

			if ( !await soundFile.LoadAsync() )
				return null;

			var samples = await soundFile.GetSamplesAsync();
			if ( samples is null || samples.Length < 2 )
				return null;

			token.ThrowIfCancellationRequested();

			var result = DownsamplePeaks( samples, waveCount );
			if ( result is null || result.Length < 2 )
				return null;

			// Normalize.
			var max = 0f;
			for ( int i = 0; i < result.Length; i++ )
				if ( result[i] > max ) max = result[i];

			if ( max < 0.0025f )
				return null;

			for ( int i = 0; i < result.Length; i++ )
				result[i] = Math.Clamp( result[i] / max, 0f, 1f );

			return result;
		}
		catch
		{
			return null;
		}
	}

	private string TryResolveAudioPath( string soundName )
	{
		if ( string.IsNullOrWhiteSpace( soundName ) )
			return null;

		if ( _audioPathCache.TryGetValue( soundName, out var cached ) && File.Exists( cached ) )
			return cached;

		// If FMOD gives an absolute path, just use it.
		if ( Path.IsPathRooted( soundName ) && File.Exists( soundName ) )
		{
			_audioPathCache[soundName] = soundName;
			return soundName;
		}

		// Try resolve relative to the FMOD project root (..\\.fmod_project\\<name>\\).
		var root = TryGetFmodProjectRootFromAsset();
		if ( string.IsNullOrWhiteSpace( root ) )
			return null;

		var rel = soundName.Replace( '/', Path.DirectorySeparatorChar ).Replace( '\\', Path.DirectorySeparatorChar );
		rel = rel.TrimStart( Path.DirectorySeparatorChar );

		var candidates = new[]
		{
			Path.Combine( root, "Audio", rel ),
			Path.Combine( root, "audio", rel ),
			Path.Combine( root, rel )
		};

		foreach ( var c in candidates )
		{
			if ( File.Exists( c ) )
			{
				_audioPathCache[soundName] = c;
				return c;
			}
		}

		// As a last resort, search by filename under Audio/audio.
		var fileName = Path.GetFileName( rel );
		if ( string.IsNullOrWhiteSpace( fileName ) )
			return null;

		foreach ( var folder in new[] { Path.Combine( root, "Audio" ), Path.Combine( root, "audio" ) } )
		{
			if ( !Directory.Exists( folder ) )
				continue;

			try
			{
				var match = Directory.EnumerateFiles( folder, fileName, SearchOption.AllDirectories ).FirstOrDefault();
				if ( !string.IsNullOrWhiteSpace( match ) && File.Exists( match ) )
				{
					_audioPathCache[soundName] = match;
					return match;
				}
			}
			catch { }
		}

		return null;
	}

	private string TryGetFmodProjectRootFromAsset()
	{
		try
		{
			var absolute = _asset?.AbsolutePath;
			if ( string.IsNullOrWhiteSpace( absolute ) )
				return null;

			var dir = Path.GetDirectoryName( absolute );
			while ( !string.IsNullOrWhiteSpace( dir ) )
			{
				// We want the folder under ".fmod_project" (the one that contains "events"/"Audio").
				var parent = Path.GetDirectoryName( dir );
				if ( string.IsNullOrWhiteSpace( parent ) )
					break;

				if ( string.Equals( Path.GetFileName( parent ), ".fmod_project", StringComparison.OrdinalIgnoreCase ) )
					return dir;

				dir = parent;
			}
		}
		catch { }

		return null;
	}

	private static float[] DownsamplePeaks( short[] samples, int waveCount )
	{
		if ( samples is null || samples.Length < 2 || waveCount < 2 )
			return null;

		var result = new float[waveCount];
		var n = samples.Length;

		for ( int i = 0; i < waveCount; i++ )
		{
			var t0 = i / (float)waveCount;
			var t1 = (i + 1) / (float)waveCount;
			var a = Math.Clamp( (int)MathF.Floor( t0 * n ), 0, n - 1 );
			var b = Math.Clamp( (int)MathF.Floor( t1 * n ), a + 1, n );

			var maxAbs = 0;
			for ( int s = a; s < b; s++ )
			{
				var v = samples[s];
				var av = v < 0 ? -v : v;
				if ( av > maxAbs ) maxAbs = av;
			}

			result[i] = maxAbs / 32768f;
		}

		return result;
	}

	private static bool TryFindDominantSound( FMOD.ChannelGroup group, out FMOD.Sound sound, out string soundName )
	{
		sound = default;
		soundName = null;

		FMOD.Channel bestChannel = default;
		float bestAud = -1f;
		FMOD.Sound bestSound = default;
		FMOD.Channel bestFallbackChannel = default;
		FMOD.Sound bestFallbackSound = default;

		void Recurse( FMOD.ChannelGroup g, int depth )
		{
			if ( depth > 8 || !g.hasHandle() )
				return;

			if ( g.getNumChannels( out var numChannels ) == FMOD.RESULT.OK && numChannels > 0 )
			{
				for ( int i = 0; i < numChannels; i++ )
				{
					if ( g.getChannel( i, out var ch ) != FMOD.RESULT.OK || !ch.hasHandle() )
						continue;

					if ( ch.getCurrentSound( out var s ) != FMOD.RESULT.OK || !s.hasHandle() )
						continue;

					if ( bestFallbackSound.hasHandle() == false )
					{
						bestFallbackChannel = ch;
						bestFallbackSound = s;
					}

					var aud = 0f;
					ch.getAudibility( out aud );
					if ( aud > bestAud )
					{
						bestAud = aud;
						bestChannel = ch;
						bestSound = s;
					}
				}
			}

			if ( g.getNumGroups( out var numGroups ) == FMOD.RESULT.OK && numGroups > 0 )
			{
				for ( int i = 0; i < numGroups; i++ )
				{
					if ( g.getGroup( i, out var child ) == FMOD.RESULT.OK && child.hasHandle() )
						Recurse( child, depth + 1 );
				}
			}
		}

		Recurse( group, 0 );

		if ( bestSound.hasHandle() )
			sound = bestSound;
		else if ( bestFallbackSound.hasHandle() )
			sound = bestFallbackSound;
		else
			return false;

		if ( sound.getName( out var n, 512 ) == FMOD.RESULT.OK )
			soundName = n ?? string.Empty;
		else
			soundName = string.Empty;

		return true;
	}

	private static async Task<float[]> BuildWaveformFromSoundAsync( FMOD.Sound sound, FMOD.SOUND_FORMAT fmt, int channels, int bits, int waveCount, CancellationToken token )
	{
		try
		{
			if ( !sound.hasHandle() || waveCount < 2 || channels <= 0 )
				return null;

			var bytesPerSample = fmt switch
			{
				FMOD.SOUND_FORMAT.PCM8 => 1,
				FMOD.SOUND_FORMAT.PCM16 => 2,
				FMOD.SOUND_FORMAT.PCM24 => 3,
				FMOD.SOUND_FORMAT.PCM32 => 4,
				FMOD.SOUND_FORMAT.PCMFLOAT => 4,
				_ => Math.Max( 1, Math.Max( 1, bits / 8 ) )
			};

			var frameBytes = bytesPerSample * channels;
			if ( frameBytes <= 0 )
				return null;

			// Reading from the start makes this deterministic per selected sound/variant.
			sound.seekData( 0 );

			var bufSize = Math.Max( frameBytes * 1024, 64 * 1024 );
			bufSize -= bufSize % frameBytes;
			var buffer = new byte[bufSize];

			// Accumulate peaks in fixed blocks, then resample to desired waveform width.
			const int framesPerBlock = 2048;
			var blockPeaks = new List<float>( 4096 );
			var framesInBlock = 0;
			var peakInBlock = 0f;

			var reads = 0;
			while ( true )
			{
				token.ThrowIfCancellationRequested();

				if ( sound.readData( buffer, out var readBytes ) != FMOD.RESULT.OK || readBytes == 0 )
					break;

				var framesRead = (int)(readBytes / (uint)frameBytes);
				if ( framesRead <= 0 )
					break;

				if ( fmt == FMOD.SOUND_FORMAT.PCM16 && bytesPerSample == 2 )
				{
					var sampleSpan = MemoryMarshal.Cast<byte, short>( buffer.AsSpan( 0, framesRead * frameBytes ) );
					var samplesPerFrame = channels;
					for ( int f = 0; f < framesRead; f++ )
					{
						var maxAbs = 0;
						var baseIdx = f * samplesPerFrame;
						for ( int c = 0; c < samplesPerFrame; c++ )
						{
							var v = sampleSpan[baseIdx + c];
							var a = v < 0 ? -v : v;
							if ( a > maxAbs ) maxAbs = a;
						}

						var amp = maxAbs / 32768f;
						if ( amp > peakInBlock ) peakInBlock = amp;
						framesInBlock++;
						if ( framesInBlock >= framesPerBlock )
						{
							blockPeaks.Add( peakInBlock );
							framesInBlock = 0;
							peakInBlock = 0f;
						}
					}
				}
				else if ( fmt == FMOD.SOUND_FORMAT.PCMFLOAT && bytesPerSample == 4 )
				{
					var sampleSpan = MemoryMarshal.Cast<byte, float>( buffer.AsSpan( 0, framesRead * frameBytes ) );
					var samplesPerFrame = channels;
					for ( int f = 0; f < framesRead; f++ )
					{
						var maxAbs = 0f;
						var baseIdx = f * samplesPerFrame;
						for ( int c = 0; c < samplesPerFrame; c++ )
						{
							var v = sampleSpan[baseIdx + c];
							var a = v < 0 ? -v : v;
							if ( a > maxAbs ) maxAbs = a;
						}

						var amp = Math.Clamp( maxAbs, 0f, 1f );
						if ( amp > peakInBlock ) peakInBlock = amp;
						framesInBlock++;
						if ( framesInBlock >= framesPerBlock )
						{
							blockPeaks.Add( peakInBlock );
							framesInBlock = 0;
							peakInBlock = 0f;
						}
					}
				}
				else
				{
					// Fallback: treat as signed 16-bit if possible, otherwise skip.
					var sampleSpan = MemoryMarshal.Cast<byte, short>( buffer.AsSpan( 0, Math.Min( buffer.Length, framesRead * frameBytes ) ) );
					var samplesPerFrame = Math.Max( 1, Math.Min( channels, sampleSpan.Length ) );
					for ( int f = 0; f < framesRead && (f * samplesPerFrame + samplesPerFrame) <= sampleSpan.Length; f++ )
					{
						var maxAbs = 0;
						var baseIdx = f * samplesPerFrame;
						for ( int c = 0; c < samplesPerFrame; c++ )
						{
							var v = sampleSpan[baseIdx + c];
							var a = v < 0 ? -v : v;
							if ( a > maxAbs ) maxAbs = a;
						}

						var amp = maxAbs / 32768f;
						if ( amp > peakInBlock ) peakInBlock = amp;
						framesInBlock++;
						if ( framesInBlock >= framesPerBlock )
						{
							blockPeaks.Add( peakInBlock );
							framesInBlock = 0;
							peakInBlock = 0f;
						}
					}
				}

				reads++;
				if ( (reads % 32) == 0 )
					await Task.Yield();
			}

			if ( framesInBlock > 0 )
				blockPeaks.Add( peakInBlock );

			if ( blockPeaks.Count < 2 )
				return null;

			var result = new float[waveCount];
			for ( int i = 0; i < waveCount; i++ )
			{
				var t = i / (float)(waveCount - 1);
				var x = t * (blockPeaks.Count - 1);
				var x0 = (int)MathF.Floor( x );
				var x1 = Math.Min( blockPeaks.Count - 1, x0 + 1 );
				var frac = x - x0;
				result[i] = blockPeaks[x0] * (1f - frac) + blockPeaks[x1] * frac;
			}

			// Normalize to 0..1 (avoid volume differences affecting visuals).
			var max = 0f;
			for ( int i = 0; i < result.Length; i++ )
				if ( result[i] > max ) max = result[i];

			if ( max < 0.0025f )
				return null;

			if ( max > 0.0001f )
			{
				for ( int i = 0; i < result.Length; i++ )
					result[i] = Math.Clamp( result[i] / max, 0f, 1f );
			}

			return result;
		}
		catch
		{
			return null;
		}
	}

	private void SeekTimeline()
	{
		if ( !_instance.isValid() || !_timeline.Enabled )
			return;

		_instance.setTimelinePosition( (int)_timeline.Value );
	}

	[EditorEvent.Frame]
	private void Tick()
	{
		var playing = IsPlaying();
		var paused = _instance.isValid() && FMODEditorPreviewPlayer.IsPaused( _instance );

		if ( playing != _lastPlaying || paused != _lastPaused )
		{
			_lastPlaying = playing;
			_lastPaused = paused;
			UpdateTransportUI();
		}

		if ( !_instance.isValid() || !_timeline.Enabled )
			return;

		if ( _instance.getTimelinePosition( out var posMs ) == FMOD.RESULT.OK )
		{
			_timeline.SetPlayheadPos( posMs );
			_timeDisplay.SetMs( posMs );
		}

		// Handle loop
		if ( _instance.getPlaybackState( out var state ) == FMOD.RESULT.OK && state == FMOD.Studio.PLAYBACK_STATE.STOPPED )
		{
			if ( _loop )
				Play();
		}
	}
}
