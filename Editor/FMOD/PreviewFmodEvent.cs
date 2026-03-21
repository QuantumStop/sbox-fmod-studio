namespace Editor.Assets;

using System.Threading.Tasks;

[AssetPreview( "fmevent" )]
public sealed class PreviewFMODEvent( Asset asset ) : AssetPreview( asset )
{
	private FMODEventPreviewWidget _widget;

	public override bool IsAnimatedPreview => false;
	public override float VideoLength => 0.0f;

	public override Task InitializeScene()
	{
		return base.InitializeScene();
	}

	public override Widget CreateWidget( Widget parent )
	{
		_widget = new FMODEventPreviewWidget( parent )
		{
			Asset = Asset
		};

		return _widget;
	}

	public override void Dispose()
	{
		if ( _widget?._pendingPrefs is not null )
			FMODEditorPreviewPlayer.TrySyncPrefs( _widget._pendingPrefs.Volume, _widget._pendingPrefs.Muted, _widget._pendingPrefs.Loop );

		_widget?.Stop();
		_widget = null;
		base.Dispose();
	}
}
