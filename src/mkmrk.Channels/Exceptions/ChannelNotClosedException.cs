using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace mkmrk.Channels;

/// <summary>
/// <see cref="Channel"/> is not closed, mutate actions not available.
/// </summary>
[ SuppressMessage( "Design", "CA1032:Implement standard exception constructors" ) ]
public sealed class ChannelNotClosedException : Exception {
    /// <inheritdoc cref="ChannelNotClosedException" />
    private ChannelNotClosedException( Exception? innerException ) : base( "Channel is not closed, mutate actions not available.", innerException ) { }

    /// <inheritdoc cref="ChannelNotClosedException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw( Exception? innerException = null, IDictionary? data = null ) {
        var exception = new ChannelNotClosedException( innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }

    /// <inheritdoc cref="ChannelNotClosedException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static TReturn Throw<TReturn>( Exception? innerException = null, IDictionary? data = null ) {
        var exception = new ChannelNotClosedException( innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }
}