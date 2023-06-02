using System.Threading.Channels;

using JetBrains.Annotations;

namespace mkmrk.Channels;

/// <summary>
/// Provides method internally to add a reader, particularly a mux.
/// </summary>
/// <typeparam name="TData"></typeparam>
public interface IBroadcastChannelAddReaderProvider<TData> {
    /// <inheritdoc cref="BroadcastChannelWriter{TData,TResponse}.AddReader" />
    /*
     * TODO: Ideally this would be a more solid type that ChannelWriter, because a ChannelWriter would not actually work (or at least it wouldn't be disposed of properly)
     * But as it is an internal method, there is no urgency.
     */
    internal RemoveWriterByHashCode AddReader( ChannelWriter<TData> reader ); 
}

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData}"/>
/// <typeparam name="TResponse">
///     <see cref="IBroadcastChannelResponse"/> based response for <see cref="BroadcastChannelReader{TData,TResponse}.WriteResponseAsync"/>
///     and <see cref="BroadcastChannelWriter{TData,TResponse}.ReadResponseAsync"/> and associated <i>Response</i> methods.
/// </typeparam>
#pragma warning disable CS1712
public interface IBroadcastChannelReaderSource<TData, TResponse> : IBroadcastChannelAddReaderProvider<TData>, IConvertibleTo<BroadcastChannelReaderSource<TData, TResponse>, BroadcastChannelReader<TData, TResponse>> where TResponse : IBroadcastChannelResponse { }
#pragma warning restore CS1712

// TODO: make these docs better
/// <summary>
/// Meant for use as a one time resource allocator of Reader resources for <see cref="BroadcastChannel{TData,TResponse}"/>
/// </summary>
/// <typeparam name="TData"></typeparam>
public interface IBroadcastChannelReaderSource<TData> : IBroadcastChannelReaderSource<TData, IBroadcastChannelResponse> { }

/// <summary>
/// Declares that <typeparamref name="TInput"/> must have an implicit conversion to <typeparamref name="TOutput"/>. 
/// </summary>
public interface IConvertibleTo<in TInput, out TOutput>
    where TInput : IConvertibleTo<TInput, TOutput> {
    /// <inheritdoc cref="IConvertibleTo{TInput,TOutput}"/>
    public static abstract implicit operator TOutput( TInput source );
}

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData,TResponse}"/>
// [EditorBrowsable(EditorBrowsableState.Never)]
public class BroadcastChannelReaderSource<TData, TResponse>
    : IBroadcastChannelReaderSource<TData, TResponse> //, IConvertibleTo<BroadcastChannelReaderSource<TData, TResponse>, BroadcastChannelReader<TData, TResponse>>
    where TResponse : IBroadcastChannelResponse {
    private readonly BroadcastChannel<TData, TResponse>        _broadcastChannel;
    private          BroadcastChannelReader<TData, TResponse>? _reader          = null;
    // private          ChannelWriter<TResponse>?                 _responseChannel = null; // TODO


    // only allow internal code to construct
    internal BroadcastChannelReaderSource( BroadcastChannel<TData, TResponse> broadcastChannel )
        => _broadcastChannel = broadcastChannel;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}.GetReader" />
    /// <remarks>
    /// The reader returned will always be the same.
    /// Only one reader can be allocated from a single <see cref="BroadcastChannelReaderSource{TData,TResponse}"/>.
    /// </remarks>
    [ PublicAPI ]
    internal BroadcastChannelReader<TData, TResponse> GetReader( ) => _reader ??= _broadcastChannel.GetReader();

    // // TODO: FUTURE? GetResponseChannel doesn't exist yet.
    // public ChannelWriter<TResponse> GetResponseChannel( ) => _responseChannel ??= _broadcastChannel.GetResponseChannel();

    /// <inheritdoc />
    RemoveWriterByHashCode IBroadcastChannelAddReaderProvider<TData>.AddReader( ChannelWriter<TData> reader ) => _broadcastChannel.Writer.AddReader( reader );

    /// <summary>
    /// Enables easy use as <see cref="BroadcastChannelReader{TData,TResponse}"/>
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public static implicit operator BroadcastChannelReader<TData, TResponse>( BroadcastChannelReaderSource<TData, TResponse> src ) =>
        src.GetReader();
}

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData}"/>
public class BroadcastChannelReaderSource<TData>
    : BroadcastChannelReaderSource<TData, IBroadcastChannelResponse>, IBroadcastChannelReaderSource<TData> {
    /// <inheritdoc />
    internal BroadcastChannelReaderSource( BroadcastChannel<TData> broadcastChannel ) : base( broadcastChannel ) { }
}