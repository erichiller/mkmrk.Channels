using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace mkmrk.Channels;

/// <summary>
/// Extensions for <see cref="IEnumerable{T}"/> and similar, mostly for <see cref="System.Linq"/> type applications
/// </summary>
internal static class Extensions {
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
    [ return: NotNullIfNotNull( "list" ) ]
    private static string? toXSeparatedString( this System.Collections.IEnumerable? list, int? maxElementsToPrint, string joinString ) {
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
    internal static string? ToCommaSeparatedString( this System.Collections.IEnumerable? list, int? maxElementsToPrint = null, string joinString = ", " )
        => toXSeparatedString( list, maxElementsToPrint, joinString );
}

/// <summary>
/// Augmentations for <see cref="System.Threading.Tasks.ValueTask"/>
/// </summary>
internal static class ValueTaskExtensions {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>This runs faster than using <c>Task.WhenAll( ...Select( ...AsTask ) )</c></remarks>
    [ SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "Required in order to combine exceptions for AggregateException" ) ]
    [ SuppressMessage( "Maintainability", "CA1508:Avoid dead conditional code" ) ]
    internal static async ValueTask WhenAll( this ValueTask[] tasks ) {
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
        if ( exceptions is not null ) {
            throw new AggregateException( exceptions );
        }
    }
}