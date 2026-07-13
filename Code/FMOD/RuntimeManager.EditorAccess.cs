namespace FMODSbox;

public partial class FMODManagerSystem
{
	/// <summary>
	/// Returns the expected folder where FMOD banks live
	/// Useful for editor tooling before the runtime FMOD system is initialized.
	/// </summary>
	public static string GetBankFolderLocation() => $"{GetAssetFolderLocation()}\\{fmodSettings.BankFolder}";

	public static string GetAssetFolderLocation() => Game.IsEditor
			? $"{Project.Current.GetAssetsPath()}"
			: System.IO.Path.GetFullPath( $"Assets" );
}

