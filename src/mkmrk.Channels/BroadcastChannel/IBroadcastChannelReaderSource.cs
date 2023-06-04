using System.Threading.Channels;

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

/// <summary>
/// Meant for use as a one time resource allocator of Reader resources for <see cref="BroadcastChannel{TData,TResponse}"/>
/// </summary>
/// <typeparam name="TData"></typeparam>
public interface IBroadcastChannelReaderSource<TData> : IBroadcastChannelAddReaderProvider<TData> { }

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData}"/>
/// <typeparam name="TResponse">
///     <see cref="IBroadcastChannelResponse"/> based response for <see cref="BroadcastChannelReader{TData,TResponse}.WriteResponseAsync"/>
///     and <see cref="BroadcastChannelWriter{TData,TResponse}.ReadResponseAsync"/> and associated <i>Response</i> methods.
/// </typeparam>
#pragma warning disable CS1712
public interface IBroadcastChannelReaderSource<TData, TResponse> :
    IBroadcastChannelReaderSource<TData>
    where TResponse : IBroadcastChannelResponse {
    /// <summary>
    /// Perform conversion to <see cref="IBroadcastChannelReaderSource{TData, TResponse}"/>
    /// </summary>
    public IBroadcastChannelReader<TData, TResponse> ToReader( );
}
#pragma warning restore CS1712