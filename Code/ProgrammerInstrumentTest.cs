using System;
using System.Runtime.InteropServices;
using FMOD.Studio;
using FMODSbox;

public class Programmer : Component
{
	EVENT_CALLBACK dialogueCallback;

	[Property] public string EventName;

	[Button]
	void PlayVoicelineOne()
	{
		FMODSound.Play( EventName, "test_subdir/behind01", GameObject, dialogueCallback );
	}

	[Button]
	void PlayVoicelineTwo()
	{
		FMODSound.Play( EventName, "wax", GameObject, dialogueCallback );
	}

	protected override void OnStart()
	{
		// Explicitly create the delegate object and assign it to a member so it doesn't get freed
		// by the garbage collected while it's being used
		dialogueCallback = new EVENT_CALLBACK( FMODManagerSystem.ProgrammerEventCallback );
	}
}
