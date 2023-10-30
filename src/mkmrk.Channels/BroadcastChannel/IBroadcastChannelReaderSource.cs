using System.Threading.Channels;

namespace mkmrk.Channels;

/// <summary>
/// Provides method internally to add a reader, particularly a mux.
/// </summary>
/// <typeparam name="TData"></typeparam>
public interface IBroadcastChannelAddReaderProvider<TData> {
    /// <inheritdoc cref="BroadcastChannelWriter{TData,TResponse}.AddReader" />
    /*
     * TODO: Ideally this would be a more solid type than ChannelWriter, because a ChannelWriter would not actually work (or at least it wouldn't be disposed of properly)
     * But as it is an internal method, there is no urgency.
     */
    internal RemoveWriterByHashCode AddReader( ChannelWriter<TData> reader );
}

/// <summary>
/// A portable allocator of <see cref="IBroadcastChannelReader{TData}"/> instances for <see cref="BroadcastChannel{TData,TResponse}"/>.
/// A new <see cref="IBroadcastChannelReader{TData}"/> is not created until <see cref="CreateReader"/> is called.
/// This allows for delaying or avoiding creating a new <see cref="IBroadcastChannelReader{TData}"/> that would be written to even
/// if not being actively read from, which would consume memory.
/// </summary>
/// <typeparam name="TData">Type of data received by <see cref="IBroadcastChannelReader{TData}"/> and sent by <see cref="IBroadcastChannelWriter{TData}"/>.</typeparam>
public interface IBroadcastChannelReaderSource<TData> : IBroadcastChannelAddReaderProvider<TData> { 
    /// <summary>
    /// Create a new <see cref="IBroadcastChannelReader{TData}"/>.
    /// </summary>
    public IBroadcastChannelReader<TData> CreateReader( );
}

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
    /// Create a new <see cref="IBroadcastChannelReader{TData, TResponse}"/>.
    /// </summary>
    public new IBroadcastChannelReader<TData, TResponse> CreateReader( );
}
#pragma warning restore CS1712