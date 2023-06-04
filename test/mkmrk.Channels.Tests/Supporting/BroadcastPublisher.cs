using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mkmrk.Channels.Tests;


public abstract class BroadcastPublisher : BackgroundService  {
    internal const int  MAX_MESSAGE_COUNT           = 500;
    internal const bool CLOSE_ON_WRITE_LAST_MESSAGE = true;
}

public class BroadcastPublisher<T> : BroadcastPublisher where T : ChannelMessage, new() {
    private readonly ILogger                   _logger;
    private readonly IBroadcastChannelWriter<T> _writer;
    private          int                       _id                   = 0;
    private          bool                      _writerMarkedComplete = false;

    public BroadcastPublisher( IBroadcastChannelWriter<T> broadcastWriter, ILogger<BroadcastPublisher<T>> logger ) {
        _logger = logger;
        _writer = broadcastWriter;
        _logger.LogDebug( $"Constructed {nameof(BroadcastPublisher<T>)} with data type {{DataType}} and writer {{Writer}}", typeof(T).Name, _writer );
    }

    public override async Task StartAsync( CancellationToken cancellationToken ) {
        _logger.LogDebug( nameof(StartAsync) );
        await base.StartAsync( cancellationToken );
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken ) {
        using IDisposable? logScope = _logger.BeginScope( nameof(ExecuteAsync) + "/" + typeof(T).Name );
        // https://github.com/dotnet/runtime/issues/36063#issuecomment-671110933 ; Fixed with await Task.Yield()
        await Task.Yield();
        _logger.LogDebug( "Beginning to write messages to {ReaderCount} reader", _writer.ReaderCount );
        T? message = null; 
        while ( await _writer.WaitToWriteAsync( stoppingToken ) && _id < MAX_MESSAGE_COUNT ) {
            message = new T {
                Id        = _id,
                Property1 = "some string"
            };
            if ( !_writer.TryWrite( message ) ) {
                _logger.LogError( "Failed to write message: {Message}", message );
                ChannelWriteException.Throw( $"Failed to write: {message}" );
            }

            _id++;
        }
        _logger.LogDebug( "Wrote message #{Id}, returning", message?.Id );
        if ( CLOSE_ON_WRITE_LAST_MESSAGE ) {
            _logger.LogDebug( "Completing channel" );
            _writerMarkedComplete = true;
            _writer.Complete();
        }
        return;
    }

    public override async Task StopAsync( CancellationToken cancellationToken ) {
        using IDisposable? logScope = _logger.BeginScope( nameof(StopAsync) + "/" + typeof(T).Name );
        _logger.LogDebug( "Stopping" );
        if ( !_writerMarkedComplete ) {
            _logger.LogDebug( "Completing channel" );
            _writer.Complete();
        }
        await base.StopAsync( cancellationToken );
    }
}

public abstract class foo<T> {
    /// <summary>Returns a <see cref="ValueTask{Boolean}"/> that will complete when data is available to read.</summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the wait operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{Boolean}"/> that will complete with a <c>true</c> result when data is available to read
    /// or with a <c>false</c> result when no further data will ever be available to be read.
    /// </returns>
    public abstract ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Attempts to read an item from the channel.</summary>
    /// <param name="item">The read item, or a default value if no item could be read.</param>
    /// <returns>true if an item was read; otherwise, false if no item was read.</returns>
    public abstract bool TryRead([MaybeNullWhen(false)] out T item);

    
    /// <summary>Creates an <see cref="IAsyncEnumerable{T}"/> that enables reading all of the data from the channel.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to cancel the enumeration.</param>
    /// <remarks>
    /// Each <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> call that returns <c>true</c> will read the next item out of the channel.
    /// <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> will return false once no more data is or will ever be available to read.
    /// </remarks>
    /// <returns>The created async enumerable.</returns>
    public virtual async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (TryRead(out T? item))
            {
                yield return item;
            }
        }
    }
}