using System.Diagnostics.CodeAnalysis;

namespace mkmrk.Channels;

/// <summary>
/// Error occurred when writing to Channel
/// </summary>
public class ChannelWriteException : System.Exception {
    
    /// <inheritdoc cref="ChannelWriteException" />
    public ChannelWriteException( ) : this( "Error occurred when writing to Channel" ) { }

    /// <inheritdoc cref="ChannelWriteException" />
    public ChannelWriteException( string message ) : base( message ) { }

    /// <inheritdoc cref="ChannelWriteException" />
    public ChannelWriteException( string message, System.Exception innerException ) : base( message, innerException ) { }

    /// <inheritdoc cref="ChannelWriteException" />
    [ DoesNotReturn ]
    public static void Throw( string message ) => throw new ChannelWriteException( message );
    
    /// <inheritdoc cref="ChannelWriteException" />
    [ DoesNotReturn ]
    public static void Throw( string message, System.Exception exception ) => throw new ChannelWriteException( message, exception );
    
    /// <inheritdoc cref="ChannelWriteException" />
    [ DoesNotReturn ]
    public static void Throw<TChannelWriter>( string operation ) => throw new ChannelWriteException( $"An error occurred when writing to {typeof(TChannelWriter).GenericTypeShortDescriptor(  )}: {operation}" );
}