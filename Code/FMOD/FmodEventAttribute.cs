using System;

namespace FMODSbox;

/// <summary>
/// Marks a string property as an FMOD Studio event path, enabling a searchable event picker in the editor.
/// </summary>
[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public sealed class FMODEventAttribute : Attribute
{
}

