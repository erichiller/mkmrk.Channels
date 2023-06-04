using System.ComponentModel;
using System.Threading.Channels;

using JetBrains.Annotations;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData,TResponse}"/>
// [EditorBrowsable(EditorBrowsableState.Never)]
public class BroadcastChannelReaderSource<TData, TResponse>
    : IBroadcastChannelReaderSource<TData, TResponse>
    where TResponse : IBroadcastChannelResponse {
    private readonly IBroadcastChannelWriter<TData, TResponse>  _broadcastChannel;
    private          IBroadcastChannelReader<TData, TResponse>? _reader = null;

    /// <inheritdoc cref="BroadcastChannelReaderSource{TData,TResponse}" />
    // Ideally, only internal code would be able to construct, but a public constructor is needed for dependency injection.
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannelReaderSource( IBroadcastChannelWriter<TData, TResponse> broadcastChannel )
        => _broadcastChannel = broadcastChannel;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}.GetReader" />
    /// <remarks>
    /// The reader returned will always be the same.
    /// Only one reader can be allocated from a single <see cref="BroadcastChannelReaderSource{TData,TResponse}"/>.
    /// </remarks>
    [ PublicAPI ]
    internal IBroadcastChannelReader<TData, TResponse> GetReader( ) => _reader ??= _broadcastChannel.GetReader();

    // // TODO: FUTURE? GetResponseChannel doesn't exist yet.
    // public ChannelWriter<TResponse> GetResponseChannel( ) => _responseChannel ??= _broadcastChannel.GetResponseChannel();

    /// <inheritdoc />
    RemoveWriterByHashCode IBroadcastChannelAddReaderProvider<TData>.AddReader( ChannelWriter<TData> reader ) => _broadcastChannel.AddReader( reader );

    /// <inheritdoc />
    public IBroadcastChannelReader<TData, TResponse> ToReader( ) => this.GetReader() as BroadcastChannelReader<TData, TResponse> ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelReader<TData, TResponse>, BroadcastChannelReader<TData, TResponse>>( this.GetReader() );

    /// <summary>
    /// Enables easy use as <see cref="BroadcastChannelReader{TData,TResponse}"/>
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public static implicit operator BroadcastChannelReader<TData, TResponse>( BroadcastChannelReaderSource<TData, TResponse> src ) =>
        src.GetReader() as BroadcastChannelReader<TData, TResponse> ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelReader<TData, TResponse>, BroadcastChannelReader<TData, TResponse>>( src.GetReader() );
}

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData}"/>
[ EditorBrowsable( EditorBrowsableState.Never ) ] // not meant for end user consumption, but the constructor must be accessible for dependency injection.
public class BroadcastChannelReaderSource<TData>
    : BroadcastChannelReaderSource<TData, IBroadcastChannelResponse>, IBroadcastChannelReaderSource<TData> {
    /// <inheritdoc />
    public BroadcastChannelReaderSource( IBroadcastChannelWriter<TData> broadcastChannelWriter ) :
        base( broadcastChannelWriter as IBroadcastChannelWriter<TData, IBroadcastChannelResponse>
              ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelWriter<TData>, IBroadcastChannelWriter<TData, IBroadcastChannelResponse>>( broadcastChannelWriter ) ) { }
}