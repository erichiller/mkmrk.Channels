using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace mkmrk.Channels;

/// <summary>
/// Error occurred when writing to Channel
/// </summary>
[ SuppressMessage( "Design", "CA1032:Implement standard exception constructors" ) ]
public class ChannelReadException : System.Exception {
    /// <inheritdoc cref="ChannelReadException" />
    private ChannelReadException( string? message, System.Exception? innerException ) : base( message ?? "Error occurred when reading from Channel", innerException ) { }

    /// <inheritdoc cref="ChannelReadException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw( string message, IDictionary? data = null, System.Exception? innerException = null ) {
        var exception = new ChannelReadException( message, innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }

    /// <inheritdoc cref="ChannelReadException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw<TChannelReader>( string operation, IDictionary? data = null, System.Exception? innerException = null ) {
        var exception = new ChannelReadException( $"An error occurred when reading from {typeof(TChannelReader).GenericTypeShortDescriptor()}: {operation}", innerException );
        if ( data is { } ) {
            foreach ( var key in data.Keys ) {
                exception.Data.Add( key, data[ key ] );
            }
        }
        throw exception;
    }
}