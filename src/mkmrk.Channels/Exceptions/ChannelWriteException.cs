using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace mkmrk.Channels;

/// <summary>
/// Error occurred when writing to Channel
/// </summary>
[ SuppressMessage( "Design", "CA1032:Implement standard exception constructors" ) ]
public class ChannelWriteException : System.Exception {
    /// <inheritdoc cref="ChannelWriteException" />
    internal ChannelWriteException( string? message, System.Exception? innerException = null ) : base( message, innerException ) { }

    /// <inheritdoc cref="ChannelWriteException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw( string? message = null, IDictionary? data = null, System.Exception? innerException = null ) {
        var exception = new ChannelWriteException( message, innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }

    /// <inheritdoc cref="ChannelWriteException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw<TChannelWriter>( string operation, IDictionary? data = null, System.Exception? innerException = null ) {
        var exception = new ChannelWriteException( $"An error occurred when writing to {typeof(TChannelWriter).GenericTypeShortDescriptor()}: {operation}", innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }
}