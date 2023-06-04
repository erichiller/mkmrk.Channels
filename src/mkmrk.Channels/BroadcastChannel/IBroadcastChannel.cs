using System;

namespace mkmrk.Channels;

/// <summary>
/// A FIFO Queue that can have 1 publisher / writer, and 0 to many subscribers / readers.
/// With a return message channel with message type of <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// If there are no readers currently, all write activity will simply return as if it was successful.
/// </remarks>
/// <typeparam name="TData">Type of the messages which <see cref="BroadcastChannelWriter{TData,TResponse}"/> writes and <see cref="BroadcastChannelReader{TData,TResponse}"/> reads.</typeparam>
/// <typeparam name="TResponse">Type of the responses which <see cref="BroadcastChannelReader{TData,TResponse}"/> writes and <see cref="BroadcastChannelWriter{TData,TResponse}"/> reads.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelwriter-1">ChannelWriter&lt;T&gt;</seealso>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1">ChannelReader&lt;T&gt;</seealso>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1">Channel&lt;T&gt;</seealso>
public interface IBroadcastChannel<TData, TResponse> : IBroadcastChannelAddReaderProvider<TData>, IDisposable where TResponse : IBroadcastChannelResponse {
    /// <summary>
    /// Get the single <see cref="BroadcastChannelWriter{TData,TResponse}"/>
    /// </summary>
    IBroadcastChannelWriter<TData, TResponse> Writer { get; }

    /// <summary>
    /// Create a new <see cref="BroadcastChannelReader{TData,TResponse}"/> and return it.
    /// </summary>
    IBroadcastChannelReader<TData, TResponse> GetReader( );
}

/// <inheritdoc cref="IBroadcastChannel{TData,TResponse}" />
public interface IBroadcastChannel<TData> : IBroadcastChannelAddReaderProvider<TData>, IDisposable { 
    /// <summary>
    /// Get the single <see cref="IBroadcastChannelReader{TData}"/>
    /// </summary>
    IBroadcastChannelWriter<TData> Writer { get; }

    /// <summary>
    /// Create a new <see cref="IBroadcastChannelReader{TData}"/> and return it.
    /// </summary>
    IBroadcastChannelReader<TData> GetReader( );
}
