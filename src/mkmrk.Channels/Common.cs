
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace mkmrk.Channels; 

/// <summary>
/// Extensions for <see cref="IEnumerable{T}"/> and similar, mostly for <see cref="System.Linq"/> type applications
/// </summary>
public static class Extensions {
    
    /// <summary>
    /// Display a more concise Generic Type representation
    /// </summary>
    public static string GenericTypeShortDescriptor( this Type type, bool useShortGenericName = true ) =>
        type switch {
            null                      => throw new ArgumentNullException( nameof(type) ),
            _ when type.IsGenericType => $"{type.Name.Split( '`' ).First()}<{type.GenericTypeArguments.Select( t => GenericTypeShortDescriptor( t, useShortGenericName ) ).ToCommaSeparatedString()}>",
            _                         => type.Name
        };

    
    /// <summary>
    /// Create <paramref name="joinString"/> separated string of elements
    /// </summary>
    [PublicAPI]
    [ return: NotNullIfNotNull("list")]
    public static string? ToXSeparatedString( this System.Collections.IEnumerable? list, int? maxElementsToPrint, string joinString ) {
        if ( list is null ) {
            return null;
        }
        List<string> strList = new ();
        int          i       = 0;
        foreach ( var x in list ) {
            strList.Add( x?.ToString() ?? "null" );
            if ( maxElementsToPrint is { } && ++i == maxElementsToPrint ) {
                strList.Add( "..." );
            }
        }
        return String.Join( joinString, strList );
    }

    /// <summary>
    /// Create comma separated string of elements
    /// </summary>
    /// <returns>
    /// eg. <c>element1, element2, element3</c>
    /// </returns>
    [ return: NotNullIfNotNull( "list" ) ]
    public static string? ToCommaSeparatedString( this System.Collections.IEnumerable? list, int? maxElementsToPrint = null, string joinString = ", " )
        => ToXSeparatedString( list, maxElementsToPrint, joinString );
}

/// <summary>
/// Augmentations for <see cref="System.Threading.Tasks.ValueTask"/>
/// </summary>
public static class ValueTaskExtensions {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>This runs faster than using <c>Task.WhenAll( ...Select( ...AsTask ) )</c></remarks>
    public static async ValueTask WhenAll( this ValueTask[] tasks ) {
        // We don't allocate the list if no task throws
        List<Exception>? exceptions = null;

        for ( var i = 0 ; i < tasks.Length ; i++ )
            try {
                await tasks[ i ].ConfigureAwait( false );
                // } catch ( TaskCanceledException ) {
                //     // TODO: is this correct?
                //     return;
            } catch ( Exception ex ) {
                exceptions ??= new List<Exception>( tasks.Length );
                exceptions.Add( ex );
            }

        // return exceptions is null
        // ? results
        if ( exceptions is not null ) {
            throw new AggregateException( exceptions );
        }
    }
}