using System.Diagnostics.CodeAnalysis;

namespace mkmrk.Channels;

/// <summary>
/// Error occurred when writing to Channel
/// </summary>
public class ChannelReadException : System.Exception {
    
    /// <inheritdoc cref="ChannelReadException" />
    private ChannelReadException( ) : this( "Error occurred when reading from Channel" ) { }

    /// <inheritdoc cref="ChannelReadException" />
    private ChannelReadException( string message ) : base( message ) { }

    /// <inheritdoc cref="ChannelReadException" />
    private ChannelReadException( string message, System.Exception innerException ) : base( message, innerException ) { }

    /// <inheritdoc cref="ChannelReadException" />
    [ DoesNotReturn ]
    public static void Throw( string message ) => throw new ChannelReadException( message );
    
    /// <inheritdoc cref="ChannelReadException" />
    [ DoesNotReturn ]
    public static void Throw( string message, System.Exception exception ) => throw new ChannelReadException( message, exception );
    
    /// <inheritdoc cref="ChannelReadException" />
    [ DoesNotReturn ]
    public static void Throw<TChannelReader>( string operation ) => throw new ChannelReadException( $"An error occurred when reading from {typeof(TChannelReader).GenericTypeShortDescriptor(  )}: {operation}" );
}