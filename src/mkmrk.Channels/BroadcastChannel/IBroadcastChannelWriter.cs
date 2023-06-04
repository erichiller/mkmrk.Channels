using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace mkmrk.Channels;

internal delegate void RemoveWriterByHashCode( in int hashCode );

/// <inheritdoc cref="IBroadcastChannelWriter{TData,TResponse}" />
/// <remarks>
/// Base for <see cref="IBroadcastChannelWriter{TData,TResponse}"/> without the return and <c>TResponse</c> operations.
/// </remarks>
public interface IBroadcastChannelWriter<TData> : IBroadcastChannelAddReaderProvider<TData>, IDisposable {
    /// <summary>
    /// Return the number of <see cref="BroadcastChannelReader{TData,TResponse}"/>
    /// </summary>
    public int ReaderCount { get; }

    /// <inheritdoc cref="ChannelWriter{T}.Complete" />
    public void Complete( Exception? error = default );


    /// <inheritdoc cref="ChannelWriter{T}.TryComplete" />
    public bool TryComplete( Exception? error = default );

    /// <inheritdoc cref="ChannelWriter{T}.TryWrite" />
    /// <remarks>This returns <c>true</c> as if it had written regardless of if there was an actual reader to read it.</remarks>
    public bool TryWrite( TData item );

    /// <summary>Write multiple <paramref name="items"/> to reader(s).</summary>
    /// <remarks>This returns <c>true</c> as if it had written regardless of if there was an actual reader to read it.</remarks>
    /// <seealso cref="TryWrite(TData)" />
    public bool TryWrite( IEnumerable<TData> items );

    /// <inheritdoc cref="ChannelWriter{T}.WaitToWriteAsync" />
    public System.Threading.Tasks.ValueTask<bool> WaitToWriteAsync( System.Threading.CancellationToken cancellationToken = default );

    /// <inheritdoc cref="ChannelWriter{T}.WriteAsync" />
    public System.Threading.Tasks.ValueTask WriteAsync( TData item, CancellationToken cancellationToken );
}

// TODO: add docs
/// <summary>
///
/// </summary>
/// <typeparam name="TData">Type of data the <see cref="BroadcastChannelWriter{TData,TResponse}"/> will send.</typeparam>
/// <typeparam name="TResponse">Type of data the <see cref="BroadcastChannelWriter{TData,TResponse}"/> will receive.</typeparam>
public interface IBroadcastChannelWriter<TData, TResponse> : IBroadcastChannelWriter<TData>, IDisposable where TResponse : IBroadcastChannelResponse {
    /// <summary>
    /// Returns <see cref="BroadcastChannelWriter{TData,TResponse}.ReaderConfiguration"/> containing necessary resources to be written to by this <see cref="BroadcastChannelWriter{TData,TResponse}"/>
    /// </summary>
    internal BroadcastChannelWriter<TData, TResponse>.ReaderConfiguration GetNewReaderConfiguration( );

    internal IBroadcastChannelReader<TData, TResponse> GetReader( );
}