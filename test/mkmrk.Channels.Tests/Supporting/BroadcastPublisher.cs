using System;
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