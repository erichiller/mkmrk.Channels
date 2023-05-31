using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace mkmrk.Channels.Tests;

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelMessage( int Id, string Property1 = "some text" );

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelResponse(
    int               ReadId,
    string            ReaderType,
    System.Exception? Exception = null
) : IBroadcastChannelResponse;

public class BroadcastChannelTests : TestBase<BroadcastChannelTests> {
    public BroadcastChannelTests( ITestOutputHelper testOutputHelper ) : base( testOutputHelper ) { }

    static async Task<int> writerTask( BroadcastChannelWriter<ChannelMessage, ChannelResponse> bqWriter, int messageCount, CancellationToken ct, ( int min, int max )? delayMs = null, ILogger? logger = null ) {
        int i      = 0;
        var random = new Random();
        while ( await bqWriter.WaitToWriteAsync( ct ) ) {
            while ( bqWriter.TryWrite( new ChannelMessage( i ) ) ) {
                if ( i >= messageCount ) {
                    logger?.LogDebug( "[BroadcastChannelWriter] wrote messageCount: {MessageCount}", i );
                    return i;
                }

                if ( delayMs is var (min, max) ) {
                    await Task.Delay( random.Next( min, max ), ct );
                }

                i++;
            }
        }

        return -1;
    }

    static async Task<int> writerTryWriteEnumerableTask( BroadcastChannelWriter<ChannelMessage, ChannelResponse> bqWriter, int messageCount, CancellationToken ct, ( int min, int max )? delayMs = null, ILogger? logger = null ) {
        int i      = 0;
        var random = new Random();
        while ( await bqWriter.WaitToWriteAsync( ct ) ) {
            var messages = new[] { new ChannelMessage( i++ ), new ChannelMessage( i++ ) };
            while ( bqWriter.TryWrite( messages ) ) {
                logger?.LogDebug( "[BroadcastChannelWriter] wrote messages: {Messages}", new List<ChannelMessage>( messages ) );
                if ( i > messageCount ) {
                    logger?.LogDebug( "[BroadcastChannelWriter] wrote messageCount: {MessageCount}", i );
                    return i;
                }

                if ( delayMs is var (min, max) ) {
                    await Task.Delay( random.Next( min, max ), ct );
                }

                // i++;
                messages = new[] { new ChannelMessage( i++ ), new ChannelMessage( i++ ) };
            }
        }

        return -1;
    }

    static async Task<int> readerTask( BroadcastChannelReader<ChannelMessage, ChannelResponse> bqReader, int messageCount, string taskName, CancellationToken ct, ILogger? logger = null ) {
        int lastMessage = -1;
        logger?.LogDebug( $"[BroadcastChannelReader] start" );
        while ( await bqReader.WaitToReadAsync( ct ) ) {
            logger?.LogDebug( $"[BroadcastChannelReader] start receiving" );
            while ( bqReader.TryRead( out ChannelMessage? result ) ) {
                logger?.LogDebug( "[BroadcastChannelReader] received messageCount: {MessageId}", result.Id );
                result.Id.Should().Be( lastMessage + 1, "[BroadcastChannelReader] ERROR at message ID" );
                if ( result.Id >= messageCount ) {
                    await bqReader.WriteResponseAsync( new ChannelResponse( result.Id, taskName ), ct );
                    return result.Id;
                }

                lastMessage++;
            }
        }

        await bqReader.WriteResponseAsync( new ChannelResponse( -1, taskName, new EmptyException( "Incomplete sequence" ) ), ct );
        return -1;
    }

    static async Task<(int readerCount, List<int> uniqueThreadIds, List<long> intervals)> addReaderTask( BroadcastChannel<ChannelMessage, ChannelResponse> broadcastChannel, int expectedMessageCount, CancellationToken ct, ILogger? logger = null ) {
        logger ??= NullLogger.Instance;
        int        readerCount = 0;
        List<int>  threadIds   = new List<int>();
        List<long> intervals   = new ();
        long       lastTime    = DateTime.UtcNow.Ticks;
        while ( !ct.IsCancellationRequested ) {
            long now = DateTime.UtcNow.Ticks;
            intervals.Add( now - lastTime );
            lastTime = now;
            logger!.LogDebug( "[{MethodName}][{ReaderCount}] looping", nameof(addReaderTask), readerCount );
            using var reader = broadcastChannel.GetReader();
            logger?.LogDebug( "[{MethodName}][{ReaderCount}] Waiting to read", nameof(addReaderTask), readerCount );
            await reader.WaitToReadAsync( ct ); // read at least one message
            logger?.LogDebug( "[{MethodName}] Waiting to read...found {ReaderCount}", nameof(addReaderTask), readerCount );
            while ( reader.TryRead( out ChannelMessage? message ) ) {
                if ( !threadIds.Contains( Thread.CurrentThread.ManagedThreadId ) ) {
                    threadIds.Add( Thread.CurrentThread.ManagedThreadId );
                }

                logger?.LogDebug( "[{MethodName}][{ReaderCount}] New reader read: {Message}", nameof(addReaderTask), readerCount, message );
                if ( message.Id == expectedMessageCount ) {
                    return ( readerCount, threadIds, intervals );
                }
            }

            readerCount++;
        }

        return ( readerCount, threadIds, intervals );
    }

    [ Fact ]
    public void AddingAndRemovingReadersShouldNeverError( ) {
        int subscriberCount = 3;
        // int messageCount    = 10_000;
        int       messageCount     = 1_000;
        using var broadcastChannel = new BroadcastChannel<ChannelMessage, ChannelResponse>();
        using var cts              = new CancellationTokenSource();
        // ( int, int ) writeIntervalRange = ( 1, 200 ); 
        ( int, int ) writeIntervalRange = ( 1, 100 );
        cts.CancelAfter( 300_000 );

        List<Task<int>> readerTasks = new List<Task<int>>();
        for ( int i = 0 ; i < subscriberCount ; i++ ) {
            readerTasks.Add( readerTask( broadcastChannel.GetReader(), messageCount, $"readerTask{i}", cts.Token ) );
        }

        var addReaderTaskRunner = addReaderTask( broadcastChannel, messageCount, cts.Token, _logger );

        List<Task> tasks = new List<Task>(
            readerTasks
        ) {
            addReaderTaskRunner, writerTask( broadcastChannel.Writer, messageCount, cts.Token, writeIntervalRange, _logger ),
        };

        try {
            Task.WaitAll(
                tasks.ToArray()
            );
        } catch ( System.AggregateException ex ) {
            bool taskCanceledException = false;
            foreach ( var inner in ex.InnerExceptions ) {
                if ( inner.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ) {
                    _logger.LogDebug( "Task was cancelled" );
                    taskCanceledException = true;
                }
            }

            if ( !taskCanceledException ) {
                throw;
            }
        }

        foreach ( var task in readerTasks ) {
            task.Result.Should().Be( messageCount );
            _logger.LogDebug( "Task had result {ResultMessageId}", task.Result );
        }

        _logger.LogDebug( "BroadcastChannel ended with {ReaderCount} readers", broadcastChannel.Writer.ReaderCount );
        _logger.LogDebug(
            "AddReaderTask created {ReaderCount} on {UniqueTaskIdCount} threads with an average interval between messages of {AverageInterval} ms",
            addReaderTaskRunner.Result.readerCount,
            addReaderTaskRunner.Result.uniqueThreadIds.Count,
            Math.Round( addReaderTaskRunner.Result.intervals.Average() / System.TimeSpan.TicksPerMillisecond, 2 ) );
        addReaderTaskRunner.Result.readerCount.Should().BeGreaterThan( 2 );
        addReaderTaskRunner.Result.uniqueThreadIds.Should().HaveCountGreaterThan( 2 );
        broadcastChannel.Writer.ReaderCount.Should().Be( 3 );
    }


    [ Fact ]
    public void PublisherShouldWriteWithoutReaders( ) {
        this._logger.LogDebug( $"In {nameof(PublisherShouldWriteWithoutReaders)}" );
        int       messageCount     = 10_000;
        using var broadcastChannel = new BroadcastChannel<ChannelMessage, ChannelResponse>();
        using var cts              = new CancellationTokenSource();

        var writerTaskNoReaders = writerTask( broadcastChannel.Writer, messageCount, cts.Token ); // must create / start last so that it doesn't write into nothing.

        Task.WaitAll(
            writerTaskNoReaders
        );
        writerTaskNoReaders.Result.Should().Be( messageCount );
    }

    [ Theory ]
    [ InlineData( 1 ) ]
    [ InlineData( 2 ) ]
    [ InlineData( 3 ) ]
    public void SubscribersShouldReceiveAllMessagesInOrder( int subscriberCount ) {
        int       messageCount     = 10_000;
        using var broadcastChannel = new BroadcastChannel<ChannelMessage, ChannelResponse>();
        using var cts              = new CancellationTokenSource();

        List<Task<int>> readerTasks = new List<Task<int>>();
        for ( int i = 0 ; i < subscriberCount ; i++ ) {
            readerTasks.Add( readerTask( broadcastChannel.GetReader(), messageCount, $"readerTask{i}", cts.Token ) );
        }

        List<Task> tasks = new List<Task>( readerTasks ) { writerTask( broadcastChannel.Writer, messageCount, cts.Token ) };


        Task.WaitAll(
            tasks.ToArray()
        );
        foreach ( var task in readerTasks ) {
            task.Result.Should().Be( messageCount );
            _logger.LogDebug( "Task had result {ResultMessageId}", task.Result );
        }
    }

    [ Theory ]
    [ InlineData( 1 ) ]
    [ InlineData( 2 ) ]
    [ InlineData( 3 ) ]
    public void WriteEnumerableDataTest( int subscriberCount ) {
        int       messageCount     = 10_000;
        using var broadcastChannel = new BroadcastChannel<ChannelMessage, ChannelResponse>();
        using var cts              = new CancellationTokenSource();
        cts.CancelAfter( 5_000 );

        List<Task<int>> readerTasks = new List<Task<int>>();
        for ( int i = 0 ; i < subscriberCount ; i++ ) {
            readerTasks.Add( readerTask( broadcastChannel.GetReader(), messageCount, $"readerTask{i}", cts.Token ) );
        }

        List<Task> tasks = new List<Task>( readerTasks ) { writerTryWriteEnumerableTask( broadcastChannel.Writer, messageCount, cts.Token, logger: _logger ) };


        Task.WaitAll(
            tasks.ToArray()
        );
        foreach ( var task in readerTasks ) {
            task.Result.Should().Be( messageCount );
            _logger.LogDebug( "Task had result {ResultMessageId}", task.Result );
        }
    }


    /* Cancellation Tests */

    [SuppressMessage("ReSharper", "IteratorNeverReturns")]
    private static async IAsyncEnumerable<int> GetIndefinitelyRunningRangeAsync( [EnumeratorCancellation] CancellationToken ct = default) {
        int index = 0;
        while ( true ) {
            await Task.Delay( 1000, ct );
            yield return index++;
        }
    }

    [ Fact ]
    public async Task CancellationWhileEnumeratingSimpleShouldThrow( ) {

        static async Task iterate( ) {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter( 250 );
            var indefinitelyRunningRange = GetIndefinitelyRunningRangeAsync();
            await foreach ( int index in indefinitelyRunningRange.WithCancellation( cts.Token ) ) {
                // Do something with the index 
            }
        }

        Func<Task> action = iterate;
        await action.Should().ThrowAsync<TaskCanceledException>();
    }


    [ Fact ]
    public async Task CancellationWhileWaitingForChannelReadShouldThrowOperationCancelled( ) {
        
        static async Task iterate( ) {
            System.Threading.Channels.Channel<int> channel = System.Threading.Channels.Channel.CreateBounded<int>( 10 );
            using var                              cts     = new CancellationTokenSource();
            cts.CancelAfter( 250 );
            await channel.Reader.WaitToReadAsync( cts.Token );
            // while ( await enumerator.MoveNextAsync().ConfigureAwait( false ) && !cancellationToken.IsCancellationRequested ) {
            //     if ( filterCallback( enumerator.Current ) ) {
            //         logger?.LogDebug( "Enumerator.Current (initial): {Current}", enumerator.Current );
            //         yield return enumerator.Current;
            //     }
            // }
            // var indefinitelyRunningRange = GetIndefinitelyRunningRangeAsync();
            // await foreach ( int index in indefinitelyRunningRange.WithCancellation( cts.Token ) ) {
            // Do something with the index 
            // }
        }
        Func<Task> action = iterate;
        await action.Should().ThrowAsync<OperationCanceledException>();

        this._logger.LogTrace( "Reading Complete" );
    }

    [ Fact ]
    public async Task CancellationWhileWaitingForChannelReadShouldThrowOperationCancelledxxxx( ) {
        
        static async Task iterate( ) {
            System.Threading.Channels.Channel<int> channel = System.Threading.Channels.Channel.CreateBounded<int>( 10 );
            using var                              cts     = new CancellationTokenSource();
            cts.CancelAfter( 250 );

            var x = await channel.Reader.ReadAsync( cts.Token );
            // await foreach ( var x in channel.Reader.ReadAllAsync( cts.Token ) ) { }
            //     if ( filterCallback( enumerator.Current ) ) {
            //         logger?.LogDebug( "Enumerator.Current (initial): {Current}", enumerator.Current );
            //         yield return enumerator.Current;
            //     }
            // }
            // var indefinitelyRunningRange = GetIndefinitelyRunningRangeAsync();
            // await foreach ( int index in indefinitelyRunningRange.WithCancellation( cts.Token ) ) {
            // Do something with the index 
            // }
        }
        Func<Task> action = iterate;
        await action.Should().ThrowAsync<OperationCanceledException>();

        this._logger.LogTrace( "Reading Complete" );
    }

    [ Fact ]
    public async Task CancellationWhileEnumeratingShouldNotThrow( ) {

        static async Task iterate( ) {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter( 250 );
            var indefinitelyRunningRange = GetIndefinitelyRunningRangeAsync();
            await foreach (int index in indefinitelyRunningRange.WithCancellation( cts.Token ))
            {
                // Do something with the index 
            }
        }

        Func<Task> action = iterate;
        await action.Should().ThrowAsync<TaskCanceledException>();
    }
}