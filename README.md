# FMOD Studio For S&Box

This repository contains the source code for the FMOD Studio S&Box integration. Native binaries that are required for this to work have been removed and must be acquired from the FMOD downloads page.

This implementation comes with it's own addition to the editor's asset browser: new additional section specifically to browse your events, and even preview them!
![Browser](https://media.discordapp.net/attachments/1450076545153765512/1484941338611941456/sbox-dev_cZMtKOfHML.png?ex=69c00ee4&is=69bebd64&hm=0bcd4fc1ac1967be64322b5368faa6ac52790ebb6364a0f6774273746fe99482&=&format=webp&quality=lossless)

Events are `FMODEventResource` GameResource, which can be used instead of direct strings, if you want to have the benefits of an asset browser for the properties. You can use it directly, don't access the `.EventPath` variable, there is an implicit conversion that skips this requirement.
![Resource](https://media.discordapp.net/attachments/1450076545153765512/1484941374032969881/sbox-dev_N6R5Yqfgrl.png?ex=69c00eec&is=69bebd6c&hm=ef04c834a8f74d0c6a6f5d078e15809de4a7933870c4bb9a29af027dd96c735c&=&format=webp&quality=lossless)

The resource can be previewed, with all its parameters tweakable as well.
![Preview](https://media.discordapp.net/attachments/1450076545153765512/1484942386907054081/sbox-dev_zY1HxuobnW.png?ex=69c00fdd&is=69bebe5d&hm=cbd47f70f136a98af87d548f9fb4b1c5d04bd50c15aaaebe48c606fe656a9fd6&=&format=webp&quality=lossless)

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
