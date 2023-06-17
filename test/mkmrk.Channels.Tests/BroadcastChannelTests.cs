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

using Serilog.Events;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace mkmrk.Channels.Tests;

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelMessage {
    public int    Id        { get; init; }
    public string Property1 { get; init; } = "foo value";
    protected ChannelMessage( ) { }
    public ChannelMessage( int id ) => Id = id;
}

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelResponse(
    int               ReadId,
    string            ReaderType,
    System.Exception? Exception = null
) : IBroadcastChannelResponse;

public class BroadcastChannelTests : TestBase<BroadcastChannelTests> {
    public BroadcastChannelTests( ITestOutputHelper testOutputHelper ) : base( testOutputHelper, logLevel: LogEventLevel.Information ) { }

    static async Task<int> writerTask(
        IBroadcastChannelWriter<ChannelMessage, ChannelResponse> bqWriter,
        int                                                     messageCount,
        CancellationToken                                       ct,
        ( int min, int max )?                                   delayMs                        = null,
        bool                                                    completeChannelWhenDoneWriting = false,
        ILogger?                                                logger                         = null
    ) {
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

        if ( completeChannelWhenDoneWriting ) {
            bqWriter.Complete();
        }
        return -1;
    }

    static async Task<int> writerTryWriteEnumerableTask( IBroadcastChannelWriter<ChannelMessage, ChannelResponse> bqWriter, int messageCount, CancellationToken ct, ( int min, int max )? delayMs = null, ILogger? logger = null ) {
        int i      = 0;
        var random = new Random();
        while ( await bqWriter.WaitToWriteAsync( ct ) ) {
            var messages = new[] {
                new ChannelMessage( i++ ),
                new ChannelMessage( i++ )
            };
            while ( bqWriter.TryWrite( messages ) ) {
                logger?.LogTrace( "[BroadcastChannelWriter] wrote messages: {Messages}", new List<ChannelMessage>( messages ) );
                if ( i > messageCount ) {
                    logger?.LogTrace( "[BroadcastChannelWriter] wrote messageCount: {MessageCount}", i );
                    return i;
                }

                if ( delayMs is var (min, max) ) {
                    await Task.Delay( random.Next( min, max ), ct );
                }

                // i++;
                messages = new[] {
                    new ChannelMessage( i++ ),
                    new ChannelMessage( i++ )
                };
            }
        }

        return -1;
    }

    static async Task<int> readerTask( IBroadcastChannelReader<ChannelMessage, ChannelResponse> bqReader, int messageCount, string taskName, CancellationToken ct, ILogger? logger = null ) {
        int lastMessage = -1;
        logger?.LogTrace( $"[BroadcastChannelReader] start" );
        while ( await bqReader.WaitToReadAsync( ct ) ) {
            logger?.LogTrace( $"[BroadcastChannelReader] start receiving" );
            while ( bqReader.TryRead( out ChannelMessage? result ) ) {
                logger?.LogTrace( "[BroadcastChannelReader] received messageCount: {MessageId}", result.Id );
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
            logger!.LogTrace( "[{MethodName}][{ReaderCount}] looping", nameof(addReaderTask), readerCount );
            using ( var reader = broadcastChannel.CreateReader() ) {
                logger?.LogTrace( "[{MethodName}][{ReaderCount}] Waiting to read", nameof(addReaderTask), readerCount );
                await reader.WaitToReadAsync( ct ); // read at least one message
                logger?.LogTrace( "[{MethodName}] Waiting to read...found {ReaderCount}", nameof(addReaderTask), readerCount );
                // while ( reader.TryRead( out ChannelMessage? message ) ) {
                if ( !reader.TryRead( out ChannelMessage? message ) ) {
                    // Read a single message
                    break;
                }
                if ( !threadIds.Contains( Thread.CurrentThread.ManagedThreadId ) ) {
                    threadIds.Add( Thread.CurrentThread.ManagedThreadId );
                }

                logger?.LogTrace( "[{MethodName}][{ReaderCount}] New reader read: {Message}", nameof(addReaderTask), readerCount, message );
                if ( message.Id == expectedMessageCount ) {
                    return ( readerCount, threadIds, intervals );
                }
            }
            readerCount++;
        }

        return ( readerCount, threadIds, intervals );
    }


    /*
     * IMPORTANT:
     * THIS TEST CAN TAKE SOME TIME !!! MODIFY THE BELOW IF NECESSARY
     */
    [ InlineData( /* maxTestMs: */ 1_000, /* messageCount: */ 100, /* subscriberCount: */ 3 ) ]
    [ InlineData( /* maxTestMs: */ 10_000, /* messageCount: */ 1_000, /* subscriberCount: */ 3 ) ]
    [ InlineData( /* maxTestMs: */ 300_000, /* messageCount: */ 10_000, /* subscriberCount: */ 3 ) ]
    [ Theory ]
    public void AddingAndRemovingReadersShouldNeverError( int maxTestMs, int messageCount, int subscriberCount ) {
        _logger.LogInformation( $"Running {nameof(AddingAndRemovingReadersShouldNeverError)} for {{MaxTestMs}} ms", maxTestMs );
        int          maxWriteInterval   = maxTestMs / messageCount; // was: 100
        ( int, int ) writeIntervalRange = ( 1, maxWriteInterval );
        using var    broadcastChannel   = new BroadcastChannel<ChannelMessage, ChannelResponse>();
        using var    cts                = new CancellationTokenSource();
        cts.CancelAfter( maxTestMs ); // just in case the test runs askew.

        List<Task<int>> readerTasks = new List<Task<int>>();
        // start {subscriberCount} readers that live the life of the test
        for ( int i = 0 ; i < subscriberCount ; i++ ) {
            readerTasks.Add( readerTask( broadcastChannel.CreateReader(), messageCount, $"readerTask{i}", cts.Token ) );
        }

        // keep starting and stop readers ( within using { } blocks )
        var addReaderTaskRunner = addReaderTask( broadcastChannel, messageCount, cts.Token, _logger );

        List<Task> tasks = new List<Task>(
            readerTasks
        ) {
            addReaderTaskRunner,
            writerTask( broadcastChannel.Writer, messageCount, cts.Token, delayMs: writeIntervalRange, completeChannelWhenDoneWriting: true, logger: _logger ),
        };

        Task.WaitAll(
            tasks.ToArray()
        );

        foreach ( var task in readerTasks ) {
            task.Result.Should().Be( messageCount );
            _logger.LogDebug( "Task had result {ResultMessageId}", task.Result );
        }

        _logger.LogDebug( "BroadcastChannel ended with {ReaderCount} readers", broadcastChannel.Writer.ReaderCount );
        _logger.LogDebug(
            "AddReaderTask created {ReaderCount} readers on {UniqueTaskIdCount} threads with an average interval between messages of {AverageInterval} ms",
            addReaderTaskRunner.Result.readerCount,
            addReaderTaskRunner.Result.uniqueThreadIds.Count,
            Math.Round( addReaderTaskRunner.Result.intervals.Average() / System.TimeSpan.TicksPerMillisecond, 2 ) );
        addReaderTaskRunner.Result.readerCount.Should().BeGreaterThan( 2 );
        // addReaderTaskRunner.Result.uniqueThreadIds.Should().HaveCountGreaterThan( 2 ); // TODO: I don't see a reason that this has to be true.
        broadcastChannel.Writer.ReaderCount.Should().Be( subscriberCount ); // only the life-of-the-test readers from `readerTask` should still be around.
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
            readerTasks.Add( readerTask( broadcastChannel.CreateReader(), messageCount, $"readerTask{i}", cts.Token ) );
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
            readerTasks.Add( readerTask( broadcastChannel.CreateReader(), messageCount, $"readerTask{i}", cts.Token ) );
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

    [ SuppressMessage( "ReSharper", "IteratorNeverReturns" ) ]
    private static async IAsyncEnumerable<int> getIndefinitelyRunningRangeAsync( [ EnumeratorCancellation ] CancellationToken ct = default ) {
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
            // ReSharper disable once MethodSupportsCancellation
            var indefinitelyRunningRange = getIndefinitelyRunningRangeAsync();
            await foreach ( int _ in indefinitelyRunningRange.WithCancellation( cts.Token ) ) {
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
    public async Task CancellationWhileEnumeratingShouldNotThrow( ) {
        static async Task iterate( ) {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter( 250 );
            // ReSharper disable once MethodSupportsCancellation
            var indefinitelyRunningRange = getIndefinitelyRunningRangeAsync();
            await foreach ( int _ in indefinitelyRunningRange.WithCancellation( cts.Token ) ) {
                // Do something with the index 
            }
        }

        Func<Task> action = iterate;
        await action.Should().ThrowAsync<TaskCanceledException>();
    }
}