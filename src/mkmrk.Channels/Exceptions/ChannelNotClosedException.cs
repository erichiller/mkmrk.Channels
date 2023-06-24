using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace mkmrk.Channels;

/// <summary>
/// <see cref="Channel"/> is not closed, mutate actions not available.
/// </summary>
public class ChannelNotClosedException : Exception {
    /// <inheritdoc cref="ChannelNotClosedException" />
    private ChannelNotClosedException( ) { }

    /// <inheritdoc cref="ChannelNotClosedException" />
    private ChannelNotClosedException( string message, Exception innerException ) : base( message, innerException ) { }

    /// <inheritdoc cref="ChannelNotClosedException" />
    private ChannelNotClosedException( string? msg = null ) : base( msg ?? "Channel is not closed, mutate actions not available." ) { }

    /// <inheritdoc cref="ChannelNotClosedException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static void Throw( ) {
        throw new ChannelNotClosedException();
    }

    /// <inheritdoc cref="ChannelNotClosedException" />
    [ DoesNotReturn ]
    [ StackTraceHidden ]
    public static TReturn Throw<TReturn>( ) {
        throw new ChannelNotClosedException();
    }
}