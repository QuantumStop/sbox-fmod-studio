# FMOD Studio For S&Box

This repository contains the source code for the FMOD Studio S&Box integration. Native binaries that are required for this to work have been removed and must be acquired from the FMOD downloads page.

## Installation
This will only work on projects with whitelist disabled, as it heavily relies on `[DllImport]`. It is recommended that you add the `Code/FMOD` folder to your project's code folder directly, to avoid assembly access issues.

Additionally, you have to modify the engine (which means forking is required).
In your fork, find `engine\Sandbox.Engine\Core\Bootstrap.cs`. In it, right before `Networking.Bootstrap()` of the engine init (doing this anywhere else will not work in standalone), add the following:
```cs
if ( Directory.GetFiles( Path.GetFullPath( "bin/thirdparty" ) ).Length > 0 )
{
	LoadLibrary( Path.GetFullPath( "bin/thirdparty/fmod.dll" ) );
	LoadLibrary( Path.GetFullPath( "bin/thirdparty/fmodstudio.dll" ) );
}
```
If you want to load additional dlls to use with fmod, like Steam Audio FMOD integration, you would add the dlls there the same way.

The `LoadLibrary()` function can be added anywhere within that class:
```cs
[DllImport( "kernel32.dll" )]
public static extern IntPtr LoadLibrary( string dllToLoad );
```

Now, acquire the FMOD Engine dlls from the FMOD site, and place them in `game\bin\thirdparty`.

If you want the dlls to be automatically copied when exporting a standalone build, in `StandaloneExporter.Files.cs`, modify `GetDllFiles()` to also consider the `thirdparty` folder. If they are instead put in the `managed` folder, running `Bootstrap.bat` will remove them.

## Loading banks
The bank folder path is hardcoded to be in `Assets/fmod`, and is loading all banks automatically on SceneLoad.
If you want banks to be automatically copied when exporting a standalone build, in the project settings add `fmod/*` to Resource Files list of the `Other` tab.

## Usage
Use the static class `FMODSound`, akin to the regular S&Box's `Sound`, to play sounds. Alternatively, you can access the GameObjectSystem directly, but use that sparingly as it's not recommended.
```cs
FMODSound.Play( "event:/Weapons/1P/Sniper/SniperA_1P" );
```

By default all event instances are immediately released, but in case this is not desired, it can be done manually.
```cs
var instance = FMODSound.Play( "event:/Physics/StepLeft", false );
FMODSound.Release( instance );
```

You can also play sounds at location, or attach a sound to a GameObject.
```cs
FMODSound.Play( "event:/SpatialSound", Obj1 );
```

Set parameters on an event, for which you have to assign the sound to a variable, equivalent to the S&Box's `SoundHandle` (here - `EventInstance`)
```cs
var instance = FMODSound.Play( "event:/Physics/StepLeft" );
FMODSound.SetParameter( instance, "parameter:/Physics/MaterialType", "Dirt" );
```

You can set pause state on all currently playing events (e.g. for game pausing)
```cs
bool pause = false;

void TestPause()
{
	pause = !pause;
	FMODSound.SetPauseOnAll( pause );
}
```

There are more functions that are not mentioned here.
## Links
* Packages which include binaries can be downloaded from the FMOD [download page](https://fmod.com/download#fmodengine).
* For getting started information, up-to-date documentation and compatibility details check the [FMOD Engine Documentation](https://fmod.com/docs/2.03/api).
