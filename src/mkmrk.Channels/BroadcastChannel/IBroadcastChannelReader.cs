using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannelReader{TData,TResponse}" />
public interface IBroadcastChannelReader<TData> : IDisposable {
    /// <inheritdoc cref="ChannelReader{T}.WaitToReadAsync" />
    public ValueTask<bool> WaitToReadAsync( CancellationToken cancellationToken = default );

#pragma warning disable CS8424
    /// <inheritdoc cref="ChannelReader{T}.ReadAllAsync" />
    public IAsyncEnumerable<TData> ReadAllAsync( [ EnumeratorCancellation ] CancellationToken cancellationToken = default );
#pragma warning restore CS8424

    /// <inheritdoc cref="ChannelReader{T}.ReadAsync" />
    public ValueTask<TData> ReadAsync( CancellationToken cancellationToken = default );

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out TData item );

}

// TODO: improve docs
/// <summary>
/// 
/// </summary>
/// <typeparam name="TData">Type of data the <see cref="BroadcastChannelReader{TData,TResponse}"/> will receive.</typeparam>
/// <typeparam name="TResponse">Type of data the <see cref="BroadcastChannelReader{TData,TResponse}"/> will send.</typeparam>
public interface IBroadcastChannelReader<TData, TResponse> : IBroadcastChannelReader<TData>, IDisposable {

    /// <inheritdoc cref="ChannelWriter{T}.WriteAsync" />
    public ValueTask WriteResponseAsync( TResponse response, CancellationToken cancellationToken = default );

}