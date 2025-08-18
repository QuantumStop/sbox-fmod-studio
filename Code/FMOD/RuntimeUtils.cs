using System;

namespace FMOD
{
	[Serializable]
	public partial struct GUID : IEquatable<GUID>
	{
		public GUID( Guid guid )
		{
			byte[] bytes = guid.ToByteArray();

			Data1 = BitConverter.ToInt32( bytes, 0 );
			Data2 = BitConverter.ToInt32( bytes, 4 );
			Data3 = BitConverter.ToInt32( bytes, 8 );
			Data4 = BitConverter.ToInt32( bytes, 12 );
		}

		public static GUID Parse( string s )
		{
			return new GUID( new Guid( s ) );
		}

		public readonly bool IsNull => Data1 == 0
					&& Data2 == 0
					&& Data3 == 0
					&& Data4 == 0;

		public override readonly bool Equals( object other )
		{
			return (other is GUID gUID) && Equals( gUID );
		}

		public readonly bool Equals( GUID other )
		{
			return Data1 == other.Data1
				&& Data2 == other.Data2
				&& Data3 == other.Data3
				&& Data4 == other.Data4;
		}

		public static bool operator ==( GUID a, GUID b )
		{
			return a.Equals( b );
		}

		public static bool operator !=( GUID a, GUID b )
		{
			return !a.Equals( b );
		}

		public override readonly int GetHashCode()
		{
			return Data1 ^ Data2 ^ Data3 ^ Data4;
		}

		public static implicit operator Guid( GUID guid )
		{
			return new Guid( guid.Data1,
					(short)((guid.Data2 >> 0) & 0xFFFF),
					(short)((guid.Data2 >> 16) & 0xFFFF),
					(byte)((guid.Data3 >> 0) & 0xFF),
					(byte)((guid.Data3 >> 8) & 0xFF),
					(byte)((guid.Data3 >> 16) & 0xFF),
					(byte)((guid.Data3 >> 24) & 0xFF),
					(byte)((guid.Data4 >> 0) & 0xFF),
					(byte)((guid.Data4 >> 8) & 0xFF),
					(byte)((guid.Data4 >> 16) & 0xFF),
					(byte)((guid.Data4 >> 24) & 0xFF)
				);
		}

		public override readonly string ToString()
		{
			return ((Guid)this).ToString( "B" );
		}
	}
}

namespace FMODSbox
{
	public class EventNotFoundException : Exception
	{
		public FMOD.GUID Guid;
		public string Path;

		public EventNotFoundException( string path )
			: base( "[FMOD] Event not found: '" + path + "'" )
		{
			Path = path;
		}

		public EventNotFoundException( FMOD.GUID guid )
			: base( "[FMOD] Event not found: " + guid )
		{
			Guid = guid;
		}

		public EventNotFoundException( EventReference eventReference )
			: base( "[FMOD] Event not found: " + eventReference.ToString() )
		{
			Guid = eventReference.Guid;
		}
	}

	public class BusNotFoundException( string path ) : Exception( "[FMOD] Bus not found '" + path + "'" )
	{
		public string Path = path;
	}

	public class VCANotFoundException( string path ) : Exception( "[FMOD] VCA not found '" + path + "'" )
	{
		public string Path = path;
	}

	public class BankLoadException : Exception
	{
		public string Path;
		public FMOD.RESULT Result;

		public BankLoadException( string path, FMOD.RESULT result )
			: base( string.Format( "[FMOD] Could not load bank '{0}' : {1} : {2}", path, result.ToString(), FMOD.Error.String( result ) ) )
		{
			Path = path;
			Result = result;
		}
		public BankLoadException( string path, string error )
			: base( string.Format( "[FMOD] Could not load bank '{0}' : {1}", path, error ) )
		{
			Path = path;
			Result = FMOD.RESULT.ERR_INTERNAL;
		}
	}

	public class SystemNotInitializedException : Exception
	{
		public FMOD.RESULT Result;
		public string Location;

		public SystemNotInitializedException( FMOD.RESULT result, string location )
			: base( string.Format( "[FMOD] Initialization failed : {2} : {0} : {1}", result.ToString(), FMOD.Error.String( result ), location ) )
		{
			Result = result;
			Location = location;
		}

		public SystemNotInitializedException( Exception inner )
			: base( "[FMOD] Initialization failed", inner )
		{
		}
	}

	public enum EmitterGameEvent : int
	{
		None,
		ObjectStart,
		ObjectDestroy,
		TriggerEnter,
		TriggerExit,
		CollisionEnter,
		CollisionExit,
		ObjectEnable,
		ObjectDisable,
		ObjectMouseEnter,
		ObjectMouseExit,
		ObjectMouseDown,
		ObjectMouseUp,
		UIMouseEnter,
		UIMouseExit,
		UIMouseDown,
		UIMouseUp,
	}

	public enum LoaderGameEvent : int
	{
		None,
		ObjectStart,
		ObjectDestroy,
		TriggerEnter,
		TriggerExit,
		ObjectEnable,
		ObjectDisable,
	}

	// We use our own enum to avoid serialization issues if FMOD.THREAD_TYPE changes
	public enum ThreadType
	{
		Mixer,
		Feeder,
		Stream,
		File,
		Nonblocking,
		Record,
		Geometry,
		Profiler,
		Studio_Update,
		Studio_Load_Bank,
		Studio_Load_Sample,
		Convolution_1,
		Convolution_2,
	}

	// We use our own enum to avoid serialization issues if FMOD.THREAD_AFFINITY changes
	[Flags]
	public enum ThreadAffinity : uint
	{
		Any = 0,
		Core0 = 1 << 0,
		Core1 = 1 << 1,
		Core2 = 1 << 2,
		Core3 = 1 << 3,
		Core4 = 1 << 4,
		Core5 = 1 << 5,
		Core6 = 1 << 6,
		Core7 = 1 << 7,
		Core8 = 1 << 8,
		Core9 = 1 << 9,
		Core10 = 1 << 10,
		Core11 = 1 << 11,
		Core12 = 1 << 12,
		Core13 = 1 << 13,
		Core14 = 1 << 14,
		Core15 = 1 << 15,
	}

	// Using a separate enum to avoid serialization issues if FMOD.SOUND_TYPE changes.
	public enum CodecType : int
	{
		FADPCM,
		Vorbis,
		AT9,
		XMA,
		Opus
	}

	[Serializable]
	public class ThreadAffinityGroup
	{
		public List<ThreadType> threads = [];
		public ThreadAffinity affinity = ThreadAffinity.Any;

		public ThreadAffinityGroup()
		{
		}

		public ThreadAffinityGroup( ThreadAffinityGroup other )
		{
			threads = new List<ThreadType>( other.threads );
			affinity = other.affinity;
		}

		public ThreadAffinityGroup( ThreadAffinity affinity, params ThreadType[] threads )
		{
			this.threads = new List<ThreadType>( threads );
			this.affinity = affinity;
		}
	}

	[Serializable]
	public class CodecChannelCount
	{
		public CodecType format;
		public int channels;

		public CodecChannelCount() { }

		public CodecChannelCount( CodecChannelCount other )
		{
			format = other.format;
			channels = other.channels;
		}
	}

	public static class RuntimeUtils
	{

		public static string GetCommonPlatformPath( string path )
		{
			if ( string.IsNullOrEmpty( path ) )
			{
				return path;
			}

			return path.Replace( '\\', '/' );
		}

		public static FMOD.VECTOR ToFMODVector( this Vector3 vec )
		{
			FMOD.VECTOR temp;
			temp.x = vec.x;
			temp.y = vec.y;
			temp.z = vec.z;

			return temp;
		}

		public static FMOD.VECTOR SourceToFMODVector( this Vector3 vec )
		{
			FMOD.VECTOR temp;
			temp.x = -vec.x;
			temp.y = vec.z;
			temp.z = vec.y;

			return temp;
		}

		public static FMOD.ATTRIBUTES_3D To3DAttributes( this Vector3 pos )
		{
			FMOD.ATTRIBUTES_3D attributes = new()
			{
				forward = SourceToFMODVector( Vector3.Forward ),
				up = SourceToFMODVector( Vector3.Up ),
				position = SourceToFMODVector( pos )
			};
			return attributes;
		}

		public static FMOD.ATTRIBUTES_3D To3DAttributes( this Transform transform )
		{
			FMOD.ATTRIBUTES_3D attributes = new()
			{
				forward = transform.Forward.SourceToFMODVector(),
				up = transform.Up.SourceToFMODVector(),
				position = transform.Position.SourceToFMODVector()
			};

			return attributes;
		}

		public static FMOD.ATTRIBUTES_3D To3DAttributes( this Transform transform, Vector3 velocity )
		{
			FMOD.ATTRIBUTES_3D attributes = new()
			{
				forward = transform.Forward.SourceToFMODVector(),
				up = transform.Up.SourceToFMODVector(),
				position = transform.Position.SourceToFMODVector(),
				velocity = velocity.SourceToFMODVector()
			};

			return attributes;
		}

		public static FMOD.ATTRIBUTES_3D To3DAttributes( this GameObject go )
		{
			FMOD.ATTRIBUTES_3D attributes = new()
			{
				forward = go.Transform.World.Forward.SourceToFMODVector(),
				up = go.Transform.World.Up.SourceToFMODVector(),
				position = go.WorldPosition.SourceToFMODVector()
			};

			return attributes;
		}

		public static FMOD.THREAD_TYPE ToFMODThreadType( ThreadType threadType )
		{
			return threadType switch
			{
				ThreadType.Mixer => FMOD.THREAD_TYPE.MIXER,
				ThreadType.Feeder => FMOD.THREAD_TYPE.FEEDER,
				ThreadType.Stream => FMOD.THREAD_TYPE.STREAM,
				ThreadType.File => FMOD.THREAD_TYPE.FILE,
				ThreadType.Nonblocking => FMOD.THREAD_TYPE.NONBLOCKING,
				ThreadType.Record => FMOD.THREAD_TYPE.RECORD,
				ThreadType.Geometry => FMOD.THREAD_TYPE.GEOMETRY,
				ThreadType.Profiler => FMOD.THREAD_TYPE.PROFILER,
				ThreadType.Studio_Update => FMOD.THREAD_TYPE.STUDIO_UPDATE,
				ThreadType.Studio_Load_Bank => FMOD.THREAD_TYPE.STUDIO_LOAD_BANK,
				ThreadType.Studio_Load_Sample => FMOD.THREAD_TYPE.STUDIO_LOAD_SAMPLE,
				ThreadType.Convolution_1 => FMOD.THREAD_TYPE.CONVOLUTION1,
				ThreadType.Convolution_2 => FMOD.THREAD_TYPE.CONVOLUTION2,
				_ => throw new ArgumentException( "Unrecognised thread type '" + threadType.ToString() + "'" ),
			};
		}

		public static string DisplayName( this ThreadType thread )
		{
			return thread.ToString().Replace( '_', ' ' );
		}

		public static FMOD.THREAD_AFFINITY ToFMODThreadAffinity( ThreadAffinity affinity )
		{
			FMOD.THREAD_AFFINITY fmodAffinity = FMOD.THREAD_AFFINITY.CORE_ALL;

			SetFMODAffinityBit( affinity, ThreadAffinity.Core0, FMOD.THREAD_AFFINITY.CORE_0, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core1, FMOD.THREAD_AFFINITY.CORE_1, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core2, FMOD.THREAD_AFFINITY.CORE_2, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core3, FMOD.THREAD_AFFINITY.CORE_3, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core4, FMOD.THREAD_AFFINITY.CORE_4, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core5, FMOD.THREAD_AFFINITY.CORE_5, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core6, FMOD.THREAD_AFFINITY.CORE_6, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core7, FMOD.THREAD_AFFINITY.CORE_7, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core8, FMOD.THREAD_AFFINITY.CORE_8, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core9, FMOD.THREAD_AFFINITY.CORE_9, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core10, FMOD.THREAD_AFFINITY.CORE_10, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core11, FMOD.THREAD_AFFINITY.CORE_11, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core12, FMOD.THREAD_AFFINITY.CORE_12, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core13, FMOD.THREAD_AFFINITY.CORE_13, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core14, FMOD.THREAD_AFFINITY.CORE_14, ref fmodAffinity );
			SetFMODAffinityBit( affinity, ThreadAffinity.Core15, FMOD.THREAD_AFFINITY.CORE_15, ref fmodAffinity );

			return fmodAffinity;
		}

		private static void SetFMODAffinityBit( ThreadAffinity affinity, ThreadAffinity mask,
			FMOD.THREAD_AFFINITY fmodMask, ref FMOD.THREAD_AFFINITY fmodAffinity )
		{
			if ( (affinity & mask) != 0 )
			{
				fmodAffinity |= fmodMask;
			}
		}

		public static void EnforceLibraryOrder()
		{
			// Call a function in fmod.dll to make sure it's loaded before fmodstudio.dll
			_ = FMOD.Memory.GetStats( out _, out _ );
			_ = FMOD.Studio.Util.parseID( "", out _ );
		}

	}
}
