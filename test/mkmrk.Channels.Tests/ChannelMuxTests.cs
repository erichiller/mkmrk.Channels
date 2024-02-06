using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Serilog.Events;

using Xunit;
using Xunit.Abstractions;

namespace mkmrk.Channels.Tests;

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Local" ) ]
file readonly record struct DataTypeA(
    int  Sequence,
    long WrittenTicks
);

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Local" ) ]
file readonly record struct DataTypeB(
    int  Sequence,
    long WrittenTicks
);

file record BaseClass( int PropertyBase );

file record SubClassA( int PropertyBase, int PropertySubA ) : BaseClass( PropertyBase );

file record SubClassB( int PropertyBase, int PropertySubB ) : BaseClass( PropertyBase );

file readonly record struct MessageWithSendTicksA( long Ticks, int Id, int Group );

// ReSharper disable once NotAccessedPositionalProperty.Local
file readonly record struct MessageWithSendTicksB( long Ticks );

file record ProducerParams( ILogger Logger, BroadcastChannel<MessageWithSendTicksA> Channel, Stopwatch Stopwatch );

file record ProducerTaskInput<T>( IBroadcastChannelWriter<T, IBroadcastChannelResponse> ChannelWriter, Stopwatch Stopwatch );

public class DummyException : Exception {
    public DummyException( string message, Exception innerException ) : base( message, innerException ) { }
    public DummyException( ) { }
    public DummyException( string message ) : base( message ) { }
}

public class ChannelMuxTests : TestBase<ChannelMuxTests> {
    public ChannelMuxTests( ITestOutputHelper testOutputHelper ) : base( testOutputHelper, logLevel: LogEventLevel.Information ) { }


    private static void producerTaskSimple<T>( in IBroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
        int i = 0;
        while ( i++ < totalMessages ) {
            writer.TryWrite( objectFactory( i ) );
        }
        writer.Complete();
    }

    [ InlineData( true ) ]
    [ InlineData( false ) ]
    [ Theory ]
    public async Task ChannelMuxBasicTest( bool withCancellableCancellationToken ) {
        int                                    msgCountChannel1 = 100;
        int                                    msgCountChannel2 = 50;
        using BroadcastChannel<DataTypeA>      channel1         = new ();
        using BroadcastChannel<DataTypeB>      channel2         = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux              = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource          cts              = new CancellationTokenSource();
        CancellationToken                      ct               = withCancellableCancellationToken ? cts.Token : CancellationToken.None;
        Stopwatch                              stopwatch        = Stopwatch.StartNew();
        Task                                   producer1        = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
        Task                                   producer2        = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
        int                                    receivedCountA   = 0;
        int                                    receivedCountB   = 0;

        while ( await mux.WaitToReadAsync( ct ) ) {
            if ( mux.TryRead( out DataTypeA _ ) ) {
                receivedCountA++;
            }
            if ( mux.TryRead( out DataTypeB _ ) ) {
                receivedCountB++;
            }
        }
        await producer1;
        await producer2;
        receivedCountA.Should().Be( msgCountChannel1 );
        receivedCountB.Should().Be( msgCountChannel2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
            Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
        }
    }


    [ InlineData( true ) ]
    [ InlineData( false ) ]
    [ Theory ]
    public async Task ChannelMuxLatencyTest( bool withCancellableCancellationToken ) {
        // const int                                                msgCountChannel1  = 50;
        // const int                                                groups            = 10;
        const int                                                      msgCountChannel1  = 1_000;
        const int                                                      groups            = 200;
        const int                                                      groupSize         = msgCountChannel1 / groups;
        const int                                                      sleepMs           = 4;
        using BroadcastChannel<MessageWithSendTicksA>                  channel1          = new ();
        using BroadcastChannel<MessageWithSendTicksB>                  channel2          = new ();
        using ChannelMux<MessageWithSendTicksA, MessageWithSendTicksB> mux               = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource                                  cts               = new CancellationTokenSource();
        CancellationToken                                              ct                = withCancellableCancellationToken ? cts.Token : CancellationToken.None;
        Stopwatch                                                      stopwatch         = new Stopwatch();
        long                                                           _1ms              = Stopwatch.Frequency / 1_000;
        long                                                           maxAllowedLatency = _1ms                * 2;

        channel2.Writer.Complete();

        Task producer1 = Task.Factory.StartNew( static async ( object? x ) => {
            var p = ( x as ProducerParams )!;
            // ReSharper disable once UnusedVariable
            ( ILogger logger, BroadcastChannel<MessageWithSendTicksA> channel, Stopwatch stopwatch ) = p;
            int i      = 0;
            int g      = 0;
            var writer = channel.Writer;
            for ( ; i < msgCountChannel1 ; g++, i++ ) {
                if ( i == 0 ) {
                    stopwatch.Start(); // don't start counting until sending begins
                }
                // logger.LogDebug( ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> [{Id}:{G}] Sending message at {Ticks:N0}", i, g, stopwatch.ElapsedTicks );
                writer.TryWrite( new MessageWithSendTicksA( stopwatch.ElapsedTicks, i, g ) );

                if ( g % groupSize == 0 ) {
                    // logger.LogDebug( ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> [{Id}:_] New group Sleeping at {Ticks:N0}", i, stopwatch.ElapsedTicks );
                    // ReSharper disable once MethodSupportsCancellation
                    await Task.Delay( sleepMs );
                } else {
                    // ReSharper disable once MethodSupportsCancellation
                    await Task.Delay( TimeSpan.FromTicks( 10_000 ) );
                }
            }
            // logger.LogDebug( ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> [{Id}:_] Sleeping {Ticks:N0}", i, stopwatch.ElapsedTicks );
            // logger.LogDebug( ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Writer completing at {Ticks:N0}", stopwatch.ElapsedTicks );
            writer.Complete();
            // logger.LogDebug( ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Writer is Complete at {Ticks:N0}", stopwatch.ElapsedTicks );
            return Task.CompletedTask;
        }, new ProducerParams( _logger, channel1, stopwatch ), cancellationToken: ct, scheduler: TaskScheduler.Current, creationOptions: TaskCreationOptions.None );

        int                         receivedCountA           = 0;
        long[]                      messageLatencies         = new long[ msgCountChannel1 ];
        int                         loops                    = 0;
        List<MessageWithSendTicksA> overlyLatentDataReceived = new ();

        while ( await mux.WaitToReadAsync( ct ) ) {
            var loopStartTicks = stopwatch.ElapsedTicks;
            _logger.LogDebug( "[Loop: {Loop}] Exiting wait to begin loop at {Ticks:N0}", loops, loopStartTicks );
            if ( mux.TryRead( out MessageWithSendTicksA a ) ) {
                long ticksNow   = stopwatch.ElapsedTicks;
                long deltaTicks = ticksNow - a.Ticks;

                _logger.LogDebug( "[Loop: {Loop}] Received {Id}:{Group} at {Ticks:N0}, message Ticks is {MsgTicks:N0}, delta is {Delta:N0} ( {DeltaMs:N3}ms ). SinceLoopStartTicks: {SinceLoopStartTicks:N0} ( {SinceLoopStartMs:N3}ms )",
                                  loops, a.Id, a.Group, ticksNow, a.Ticks, deltaTicks, ( ( double )ticksNow - ( double )a.Ticks ) / _1ms, ticksNow - loopStartTicks, ( ticksNow - loopStartTicks ) / _1ms );
                if ( deltaTicks > maxAllowedLatency && receivedCountA > 5 ) {
                    _logger.LogError( "[Loop: {Loop}, {Id}:{Group}] LATENCY GREATER THAN {MaxLatency:N3}ms: {DeltaTicks:N6}ms !!!!!!!!!!!!!!!!!!!!!!!!!!!!!", loops, a.Id, a.Group, maxAllowedLatency / ( double )_1ms, deltaTicks / ( double )_1ms );
                    overlyLatentDataReceived.Add( a );
                }
                messageLatencies[ receivedCountA ] = deltaTicks;
                receivedCountA++;
            }
            loops++;
        }
        await producer1;
        receivedCountA.Should().Be( msgCountChannel1 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        if ( receivedCountA != msgCountChannel1 ) {
            Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA}" );
        }
        _logger.LogInformation( "Latency Avg: {Avg:N3}ms Min: {Min:N3}ms Max: {Max:N3}ms Top 5: {Top5}", messageLatencies.Average() / ( double )_1ms, messageLatencies.Min() / ( double )_1ms, messageLatencies.Max() / ( double )_1ms, String.Join( ", ", messageLatencies.OrderDescending().Take( 5 ).Select( n => ( ( double )n / ( double )_1ms ).ToString( "N3", CultureInfo.InvariantCulture ) + "ms" ) ) );
        // _logger.LogInformation( "Latencies: {Latencies}", String.Join( ", ", messageLatencies ) );
        overlyLatentDataReceived.Should().BeEmpty();
    }

    [ InlineData( true ) ]
    [ InlineData( false ) ]
    [ Theory ]
    public async Task MuxShouldWaitForBothChannelsToComplete( bool withCancellableCancellationToken ) {
        static void producerTaskWithDelay<T>( in IBroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, TimeSpan sleepTime, System.Func<int, T> objectFactory ) {
            Thread.Sleep( sleepTime );
            for ( int i = 0 ; i < totalMessages ; i++ ) {
                writer.TryWrite( objectFactory( i ) );
            }
            writer.Complete();
        }

        const int                              msgCountChannel1    = 20_000;
        const int                              msgCountChannel2    = 50;
        int                                    receivedCountA      = 0, receivedCountB = 0;
        int                                    waitToReadLoopCount = 0;
        Stopwatch                              stopwatch           = Stopwatch.StartNew();
        using BroadcastChannel<DataTypeA>      channel1            = new ();
        using BroadcastChannel<DataTypeB>      channel2            = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux                 = new (channel1.Writer, channel2.Writer);
        Task                                   producer1           = Task.Run( ( ) => producerTaskWithDelay( channel1.Writer, msgCountChannel1, TimeSpan.FromTicks( 10_000 ), i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), CancellationToken.None );
        Task                                   producer2           = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), CancellationToken.None );

        using CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken             ct  = withCancellableCancellationToken ? cts.Token : CancellationToken.None;
        while ( await mux.WaitToReadAsync( ct ) ) {
            waitToReadLoopCount++;
            if ( mux.TryRead( out DataTypeA _ ) ) {
                receivedCountA++;
            }
            if ( mux.TryRead( out DataTypeB _ ) ) {
                receivedCountB++;
            }
        }
        await producer1;
        await producer2;
        _logger.LogDebug( $"{nameof(waitToReadLoopCount)}: {{WaitToReadLoopCount}}\n\t" +
                          $"receivedCountA: {{ReceivedCountA}}\n\t"                     +
                          $"receivedCountB: {{ReceivedCountB}}", waitToReadLoopCount, receivedCountA, receivedCountB );
        receivedCountA.Should().Be( msgCountChannel1 );
        receivedCountB.Should().Be( msgCountChannel2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [ Fact ]
    public async Task AlternateChannelWriterMethods( ) {
        static async Task producerTaskUsingAsync<T>( IBroadcastChannelWriter<T, IBroadcastChannelResponse> writer, int totalMessages, System.Func<int, T> objectFactory ) {
            for ( int i = 0 ; i < totalMessages ; i++ ) {
                await writer.WriteAsync( objectFactory( i ), CancellationToken.None );
            }
            writer.Complete();
        }

        int                                    msgCountChannel1 = 100;
        int                                    msgCountChannel2 = 50;
        using BroadcastChannel<DataTypeA>      channel1         = new ();
        using BroadcastChannel<DataTypeB>      channel2         = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux              = new (channel1.Writer, channel2.Writer);
        CancellationToken                      ct               = CancellationToken.None;
        Stopwatch                              stopwatch        = Stopwatch.StartNew();
        Task                                   producer1        = producerTaskUsingAsync( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) );
        Task                                   producer2        = producerTaskUsingAsync( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) );
        int                                    receivedCountA   = 0;
        int                                    receivedCountB   = 0;

        while ( await mux.WaitToReadAsync( ct ) ) {
            if ( mux.TryRead( out DataTypeA _ ) ) {
                receivedCountA++;
            }
            if ( mux.TryRead( out DataTypeB _ ) ) {
                receivedCountB++;
            }
        }
        await producer1;
        await producer2;
        receivedCountA.Should().Be( msgCountChannel1 );
        receivedCountB.Should().Be( msgCountChannel2 );
        if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
            Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
        }
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }


    /* **************************************************
     * ReplaceChannel Tests
     * **************************************************/

    #region ReplaceChannel Tests

    [ Fact ]
    public async Task ReplaceChannel_WhenMuxIsCompleted( ) {
        int                               msgCountChannel1    = 100;
        int                               msgCountChannel2    = 50;
        int                               msgCountChannel3    = 75;
        using BroadcastChannel<DataTypeA> channel1            = new ();
        using BroadcastChannel<DataTypeB> channel2            = new ();
        using BroadcastChannel<DataTypeA> channelReplacement1 = new ();
        using ( ChannelMux<DataTypeA, DataTypeB> mux = new (channel1.Writer, channel2.Writer) ) {
            CancellationToken ct             = CancellationToken.None;
            Stopwatch         stopwatch      = Stopwatch.StartNew();
            Task              producer1      = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            Task              producer2      = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            int               receivedCountA = 0;
            int               receivedCountB = 0;
            int               receivedCountC = 0;

            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 0 );

            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountA++;
                }
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    receivedCountB++;
                }
            }
            await producer1;
            await producer2;
            receivedCountA.Should().Be( msgCountChannel1 );
            receivedCountB.Should().Be( msgCountChannel2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );

            // Replace
            mux.ReplaceChannel( channelReplacement1.Writer ).Should().BeEmpty();
            mux.Completion.IsCompleted.Should().BeFalse();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 1 );

            Task producer3 = Task.Run( ( ) => producerTaskSimple( channelReplacement1.Writer, msgCountChannel3, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountC++;
                }
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    receivedCountB++;
                }
            }
            await producer3;
            receivedCountC.Should().Be( msgCountChannel3 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        }
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
    }


    [ Fact ]
    public async Task ReplaceChannel_WhenChannelIsOpen( ) {
        int                               msgCountChannel1    = 100;
        int                               msgCountChannel3    = 75;
        using BroadcastChannel<DataTypeA> channel1            = new ();
        using BroadcastChannel<DataTypeB> channel2            = new (); // this will be closed
        using BroadcastChannel<DataTypeB> channelReplacement1 = new ();
        using ( ChannelMux<DataTypeA, DataTypeB> mux = new (channel1.Writer, channel2.Writer) ) {
            CancellationToken ct             = CancellationToken.None;
            Stopwatch         stopwatch      = Stopwatch.StartNew();
            Task              producer1      = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            int               receivedCountA = 0;
            int               receivedCountB = 0;
            int               receivedCountC = 0;

            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 0 );

            // Write 2 to the channel being closed
            channel2.Writer.TryWrite( new DataTypeB( -1, stopwatch.ElapsedTicks ) );
            channel2.Writer.TryWrite( new DataTypeB( -2, stopwatch.ElapsedTicks ) );

            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountA++;
                }
                if ( receivedCountA == msgCountChannel1 ) {
                    break;
                }
            }
            await producer1;
            receivedCountA.Should().Be( msgCountChannel1 );
            mux.Completion.IsCompleted.Should().BeFalse();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeFalse();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );

            // Replace without force
            mux.Invoking( m => m.ReplaceChannel( channelReplacement1.Writer ) )
               .Should().Throw<ChannelNotClosedException>();
            mux.Completion.IsCompleted.Should().BeFalse();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );


            // Replace with force
            mux.ReplaceChannel( channelReplacement1.Writer, force: true ).Should().HaveCount( 2 );
            mux.Completion.IsCompleted.Should().BeFalse();

            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 1 );

            Task producer3 = Task.Run( ( ) => producerTaskSimple( channelReplacement1.Writer, msgCountChannel3, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    receivedCountC++;
                }
                if ( receivedCountC == msgCountChannel3 ) {
                    break;
                }
            }
            await producer3;
            receivedCountB.Should().Be( 0 );
            receivedCountC.Should().Be( msgCountChannel3 );
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        }
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
    }

    [ Fact ]
    public async Task ReplaceChannel_ShouldNotClearExceptionIfChannelBeingReplacedDidntReportException( ) {
        int                               msgCountChannel2    = 150;
        int                               msgCountChannel3    = 75;
        using BroadcastChannel<DataTypeA> channel1            = new ();
        using BroadcastChannel<DataTypeB> channel2            = new (); // this will be closed
        using BroadcastChannel<DataTypeB> channelReplacement1 = new ();
        using ( ChannelMux<DataTypeA, DataTypeB> mux = new (channel1.Writer, channel2.Writer) { OnChannelComplete = ( _, e ) => e } ) {
            CancellationToken ct        = CancellationToken.None;
            Stopwatch         stopwatch = Stopwatch.StartNew();
            channel1.Writer.TryComplete( new DummyException() ).Should().BeTrue();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
            Task producer2 = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            await producer2;
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeOfType<AggregateException>().Subject
               .InnerException.Should().BeOfType<DummyException>();
            mux.Completion.IsCompletedSuccessfully.Should().BeFalse();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );
            TestUtils.GetPrivateField<ChannelMux, Exception>( mux, "_completeException" ).Should().BeOfType<DummyException>();
            TestUtils.GetPrivateField<ChannelMux, Type>( mux, "_completeExceptionChannelDataType" ).Should().Be( typeof(DataTypeA) );

            // Replace
            mux.ReplaceChannel( channelReplacement1.Writer ).Should().BeEmpty();

            // NOTE: debatable whether the completion task should be reset or not here
            // mux.Completion.IsCompleted.Should().BeTrue();
            // mux.Completion.Exception.Should().BeOfType<AggregateException>().Subject
            //    .InnerException.Should().BeOfType<SomeException>();
            // mux.Completion.IsCompletedSuccessfully.Should().BeFalse();
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
            TestUtils.GetPrivateField<ChannelMux, Exception>( mux, "_completeException" ).Should().BeOfType<DummyException>();
            TestUtils.GetPrivateField<ChannelMux, Type>( mux, "_completeExceptionChannelDataType" ).Should().Be( typeof(DataTypeA) );

            Task producer3 = Task.Run( ( ) => producerTaskSimple( channelReplacement1.Writer, msgCountChannel3, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );

            await mux.Awaiting( x => x.WaitToReadAsync( ct ) ) // WaitToReadAsync will still throw 
                     .Should().ThrowAsync<DummyException>();
            await producer3;
            TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        }
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channelReplacement1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
    }

    #endregion


    /* **************************************************
     * Exception Tests
     * **************************************************/

    #region Exception Tests

    [ Fact ]
    public async Task ExceptionBeingIgnoredShouldStillCloseChannel( ) {
        int                                    msgCountChannel2 = 150;
        using BroadcastChannel<DataTypeA>      channel1         = new ();
        using BroadcastChannel<DataTypeB>      channel2         = new (); // this will be closed
        using ChannelMux<DataTypeA, DataTypeB> mux              = new (channel1.Writer, channel2.Writer) { OnChannelComplete = ( _, _ ) => null };
        CancellationToken                      ct               = CancellationToken.None;
        Stopwatch                              stopwatch        = Stopwatch.StartNew();
        channel1.Writer.TryComplete( new DummyException() ).Should().BeTrue();
        int receivedCountB = 0;
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
        TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
        Task producer2 = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );

        while ( await mux.WaitToReadAsync( ct ) ) {
            if ( mux.TryRead( out DataTypeB _ ) ) {
                receivedCountB++;
            }
            if ( receivedCountB == msgCountChannel2 ) {
                break;
            }
        }
        await producer2;
        receivedCountB.Should().Be( msgCountChannel2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 2 );
        TestUtils.GetPrivateField<ChannelMux, Exception>( mux, "_completeException" ).Should().BeNull();
    }

    [ Fact ]
    public void ExceptionBeingReturnedShouldEndCompletionTask( ) {
        using BroadcastChannel<DataTypeA>      channel1  = new ();
        using BroadcastChannel<DataTypeB>      channel2  = new (); // this will be closed
        using ChannelMux<DataTypeA, DataTypeB> mux       = new (channel1.Writer, channel2.Writer) { OnChannelComplete = ( _, e ) => e };
        Stopwatch                              stopwatch = Stopwatch.StartNew();
        channel1.Writer.TryComplete( new DummyException() ).Should().BeTrue();
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeB>>>( channel2.Writer, "_outputWriters" ).Should().HaveCount( 1 );
        TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
        channel2.Writer.TryWrite( new DataTypeB( -1, stopwatch.ElapsedTicks ) ).Should().BeFalse();
        mux.Completion.IsCompleted.Should().BeTrue();
        var aggregateException = mux.Completion.Exception.Should().BeOfType<AggregateException>().Subject;
        aggregateException.InnerException.Should().BeOfType<DummyException>();
        mux.Completion.IsCompletedSuccessfully.Should().BeFalse();
        TestUtils.GetPrivateField<ChannelMux, int>( mux, "_closedChannels" ).Should().Be( 1 );
        TestUtils.GetPrivateField<ChannelMux, Exception>( mux, "_completeException" ).Should().BeOfType<DummyException>();
    }

    [ InlineData( true ) ]
    [ InlineData( false ) ]
    [ Theory ]
    public async Task ChannelComplete_WithException_ShouldThrow_UponAwait( bool withCancellableCancellationToken ) {
        static void producerTaskCompleteWithErrorAfter<T>( in IBroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int completeWithExceptionAfterCount, System.Func<int, T> objectFactory ) {
            TimeSpan sleepTime = TimeSpan.FromTicks( 10_000 );
            for ( int i = 0 ; i < completeWithExceptionAfterCount ; i++ ) {
                writer.TryWrite( objectFactory( i ) );
            }
            Thread.Sleep( sleepTime );
            writer.Complete( new DummyException() );
        }

        const int                         completeWithExceptionAfterCount = 500;
        const int                         msgCountChannel1                = 50_000;
        int                               receivedCountA                  = 0, receivedCountB = 0;
        int                               waitToReadLoopCount             = 0;
        Stopwatch                         stopwatch                       = Stopwatch.StartNew();
        using BroadcastChannel<DataTypeA> channel1                        = new ();
        using BroadcastChannel<DataTypeB> channel2                        = new ();
        using CancellationTokenSource     cts                             = new CancellationTokenSource();
        CancellationToken                 ct                              = withCancellableCancellationToken ? cts.Token : CancellationToken.None;
        int                               onChannelCompleteCounter        = 0;
        using ChannelMux<DataTypeA, DataTypeB> mux = new (channel1.Writer, channel2.Writer) {
            OnChannelComplete = ( ( dType, exception ) => {
                onChannelCompleteCounter++;
                _logger.LogDebug( $"OnException: {{Exception}} for channel of type {{DType}}\n\t" +
                                  $"{nameof(waitToReadLoopCount)}: {{WaitToReadLoopCount}}\n\t"   +
                                  $"receivedCountA: {{ReceivedCountA}}\n\t"                       +
                                  $"receivedCountB: {{ReceivedCountB}}", exception, dType.Name, waitToReadLoopCount, receivedCountA, receivedCountB );
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if ( exception is { } ) {
                    dType.Should().Be( typeof(DataTypeB) );
                } else {
                    dType.Should().Be( typeof(DataTypeA) );
                }
                return exception;
            } )
        };
        Task producer1 = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), CancellationToken.None );
        Task producer2 = Task.Run( ( ) => producerTaskCompleteWithErrorAfter( channel2.Writer, completeWithExceptionAfterCount, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), CancellationToken.None );
        await producer1;
        await producer2;

        await mux.Awaiting( x => x.WaitToReadAsync( ct ) )
                 .Should().ThrowAsync<DummyException>();

        Func<Task> asyncWriterShouldThrow = async ( ) => {
            await channel1.Writer.WriteAsync( new DataTypeA {
                                                  Sequence     = 10,
                                                  WrittenTicks = -1
                                              }, ct );
        };
        await asyncWriterShouldThrow.Should().ThrowAsync<ChannelClosedException>()
                                    .WithInnerException( typeof(DummyException) );
        _logger.LogDebug( $"{nameof(waitToReadLoopCount)}: {{WaitToReadLoopCount}}\n\t" +
                          $"receivedCountA: {{ReceivedCountA}}\n\t"                     +
                          $"receivedCountB: {{ReceivedCountB}}", waitToReadLoopCount, receivedCountA, receivedCountB );
        // read remaining messages ( if any )
        while ( ( mux.TryRead( out DataTypeA _ ), mux.TryRead( out DataTypeB _ ) ) != ( false, false ) ) { }

        onChannelCompleteCounter.Should().Be( 2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        var aggregateException = mux.Completion.Exception.Should().BeOfType<AggregateException>().Subject;
        aggregateException.InnerException.Should().BeOfType<DummyException>();
        mux.Completion.IsCompletedSuccessfully.Should().BeFalse();
    }

    #endregion


    /* **************************************************
     * CancellationToken Tests
     * **************************************************/

    #region CancellationToken Tests

    /// <summary>
    /// If WaitToReadAsync cancellation token is cancelled when started, it should immediately exit.
    /// </summary>
    [ Fact ]
    public async Task CancellationToken_PreWaitToReadAsync_Test( ) {
        const int                              msgCountChannel1 = 50, msgCountChannel2 = 100;
        int                                    receivedCountA   = 0,  receivedCountB   = 0;
        using BroadcastChannel<DataTypeA>      channel1         = new ();
        using BroadcastChannel<DataTypeB>      channel2         = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux              = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource          cts              = new CancellationTokenSource();
        CancellationToken                      ct               = cts.Token;
        int                                    cancelAfterCount = 25;
        int                                    loopsSinceReadB  = 0;
        Stopwatch                              stopwatch        = Stopwatch.StartNew();
        Task                                   producer1        = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
        Task                                   producer2        = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
        Func<Task> readerTask = async ( ) => {
            while ( await mux.WaitToReadAsync( ct ) ) {
                loopsSinceReadB++;
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountA++;
                }
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    if ( receivedCountB == cancelAfterCount ) {
                        cts.Cancel();
                    } else {
                        receivedCountB++;
                    }
                    loopsSinceReadB = 0;
                }
            }
        };
        await readerTask.Should().ThrowAsync<OperationCanceledException>();
        await producer1;
        await producer2;
        receivedCountA.Should().NotBe( 0 );
        loopsSinceReadB.Should().Be( 0 );
        receivedCountB.Should().Be( cancelAfterCount );
        mux.Completion.IsCompleted.Should().BeFalse(); // not completed because not all messages are read
    }

    /// <summary>
    /// If WaitToReadAsync cancellation token is cancelled when started, it should immediately exit.
    /// </summary>
    [ Fact ]
    public async Task CancellationToken_DuringWaitToReadAsync_Test( ) {
        int loops = 0;
        {
            using BroadcastChannel<DataTypeA>      channel1 = new ();
            using BroadcastChannel<DataTypeB>      channel2 = new ();
            using ChannelMux<DataTypeA, DataTypeB> mux      = new (channel1.Writer, channel2.Writer);
            using CancellationTokenSource          cts      = new CancellationTokenSource();
            // CancellationToken                      ct       = cts.Token; // KILL -- modified to new form 2023 Nov 06
            // Func<Task> readerTask = async ( ) => {
            //     cts.CancelAfter( 5 );
            //     while ( await mux.WaitToReadAsync( ct ) ) {
            //         loops++;
            //     }
            // };
            // await readerTask.Should().ThrowAsync<OperationCanceledException>();
            var readerTask = readLoop( cts, mux );
            await Assert.ThrowsAsync<OperationCanceledException>( ( ) => readerTask );
            loops.Should().Be( 0 );
            mux.Completion.IsCompleted.Should().BeFalse(); // not completed because not all messages are read
        }

        async Task<int> readLoop( CancellationTokenSource cts, ChannelMux<DataTypeA, DataTypeB> mux ) {
            cts.CancelAfter( 5 );
            while ( await mux.WaitToReadAsync( cts.Token ) ) {
                loops++;
            }
            return loops;
        }
    }


    [ Fact ]
    public async Task CancellationToken_PreWaitToReadAsync_NewAfterException_Test( ) {
        // URGENT: sporadic test failures ?? ;; June 15, 2023 ;; Reworked October 30, 2023 ; continuing to test
        const int                              msgCountChannel1                 = 50_000, msgCountChannel2 = 1_000;
        const int                              cancelEveryCount                 = 25;
        const int                              throwExceptionCount              = 4;
        int                                    operationCancelledExceptionsSeen = 0;
        int                                    receivedCountA                   = 0, receivedCountB = 0;
        using BroadcastChannel<DataTypeA>      channel1                         = new ();
        using BroadcastChannel<DataTypeB>      channel2                         = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux                              = new (channel1.Writer, channel2.Writer);
        Stopwatch                              stopwatch                        = Stopwatch.StartNew();
        Task producer1 = Task.Factory.StartNew( static args => {
            if ( args is not ProducerTaskInput<DataTypeA> { ChannelWriter: var channelWriter, Stopwatch: var stopwatch } ) {
                throw new ArgumentException();
            }
            producerTaskSimple( channelWriter, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) );
        }, new ProducerTaskInput<DataTypeA>( channel1.Writer, stopwatch ), scheduler: TaskScheduler.Default, cancellationToken: CancellationToken.None, creationOptions: default );

        Task producer2 = Task.Factory.StartNew( static args => {
            if ( args is not ProducerTaskInput<DataTypeB> { ChannelWriter: var channelWriter, Stopwatch: var stopwatch } ) {
                throw new ArgumentException();
            }
            producerTaskSimple( channelWriter, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) );
        }, new ProducerTaskInput<DataTypeB>( channel2.Writer, stopwatch ), scheduler: TaskScheduler.Default, cancellationToken: CancellationToken.None, creationOptions: default );

        Task readerLoopTask = readerLoop( mux, _logger );
        await readerLoopTask.Awaiting( x => x ).Should().ThrowAsync<OperationCanceledException>();
        await producer1;
        await producer2;
        if ( receivedCountA == 0 || receivedCountA == msgCountChannel1 ) {
            this._logger.LogError( "ERROR! receivedCountA is {ReceivedCountA}. State: {@State}",
                                   receivedCountA,
                                   new {
                                       receivedCountA,
                                       receivedCountB,
                                       operationCancelledExceptionsSeen,
                                       stopwatch.ElapsedTicks,
                                       Producer1Status     = producer1.Status,
                                       Producer2Status     = producer2.Status,
                                       MuxCompletionStatus = mux.Completion.Status
                                   } );
        }
        receivedCountA.Should().NotBe( 0 );
        receivedCountA.Should().BeLessThan( msgCountChannel1 );
        receivedCountB.Should().Be( cancelEveryCount * throwExceptionCount );
        operationCancelledExceptionsSeen.Should().Be( throwExceptionCount );
        mux.Completion.IsCompleted.Should().BeFalse(); // not completed because not all messages are read

        async Task readerLoop( ChannelMux<DataTypeA, DataTypeB> muxLocal, ILogger logger ) {
            for ( int exceptionLoop = 0 ; exceptionLoop < throwExceptionCount ; exceptionLoop++ ) {
                try {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    while ( await muxLocal.WaitToReadAsync( cts.Token ) ) {
                        if ( muxLocal.TryRead( out DataTypeA _ ) ) {
                            receivedCountA++;
                        }
                        if ( muxLocal.TryRead( out DataTypeB _ ) ) {
                            receivedCountB++;
                            if ( receivedCountB % cancelEveryCount == 0 ) {
                                cts.Cancel();
                            }
                        }
                    }
                } catch ( OperationCanceledException ) {
                    logger.LogDebug( "OperationCanceledException @exceptionLoop={ExceptionLoop} ; receivedCountB={ReceivedCountB}", exceptionLoop, receivedCountB );
                    operationCancelledExceptionsSeen++;
                    if ( exceptionLoop == throwExceptionCount - 1 ) {
                        throw;
                    }
                }
            }
        }
    }

    [ Fact ]
    public async Task CancellationToken_DuringWaitToReadAsync_NewAfterException_Test( ) {
        using BroadcastChannel<DataTypeA>      channel1                         = new ();
        using BroadcastChannel<DataTypeB>      channel2                         = new ();
        using ChannelMux<DataTypeA, DataTypeB> mux                              = new (channel1.Writer, channel2.Writer);
        const int                              throwExceptionCount              = 4;
        int                                    operationCancelledExceptionsSeen = 0;
        int                                    loops                            = 0;
        Task                                   readerLoopTask                   = readerLoop( mux, _logger );
        Func<Task>                             readerLoopTaskAwait              = async ( ) => await readerLoopTask;
        await readerLoopTaskAwait.Should().ThrowAsync<OperationCanceledException>();
        loops.Should().Be( 0 );
        operationCancelledExceptionsSeen.Should().Be( throwExceptionCount );
        mux.Completion.IsCompleted.Should().BeFalse(); // not completed because not all messages are read

        async Task readerLoop( ChannelMux<DataTypeA, DataTypeB> muxLocal, ILogger logger ) {
            for ( int exceptionLoop = 0 ; exceptionLoop < throwExceptionCount ; exceptionLoop++ ) {
                try {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter( 5 );
                    while ( await muxLocal.WaitToReadAsync( cts.Token ) ) {
                        loops++;
                    }
                } catch ( OperationCanceledException ) {
                    logger.LogDebug( "OperationCanceledException @exceptionLoop={ExceptionLoop}", exceptionLoop );
                    operationCancelledExceptionsSeen++;
                    if ( exceptionLoop == throwExceptionCount - 1 ) {
                        throw;
                    }
                }
            }
        }

        ;
    }

    #endregion


    /* **************************************************
     * Disposal Tests
     * **************************************************/

    #region Disposal tests

    [ Fact ]
    public async Task DisposalOfMuxShouldRemoveFromBroadcastChannel( ) {
        int                               msgCountChannel1 = 100;
        int                               msgCountChannel2 = 50;
        using BroadcastChannel<DataTypeA> channel1         = new ();
        using BroadcastChannel<DataTypeB> channel2         = new ();
        using ( ChannelMux<DataTypeA, DataTypeB> mux = new (channel1.Writer, channel2.Writer) ) {
            CancellationToken ct             = CancellationToken.None;
            Stopwatch         stopwatch      = Stopwatch.StartNew();
            Task              producer1      = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, i => new DataTypeA( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            Task              producer2      = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            int               receivedCountA = 0;
            int               receivedCountB = 0;

            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );

            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountA++;
                }
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    receivedCountB++;
                }
            }
            await producer1;
            await producer2;
            receivedCountA.Should().Be( msgCountChannel1 );
            receivedCountB.Should().Be( msgCountChannel2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
            if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
                Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
            }
            mux.Dispose(); // try an explicit Dispose() which will cause exiting the using block to make a second Dispose() call. make sure it doesn't error
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
        }
        TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
    }

    [ Fact ]
    public async Task ExceptionShouldRemoveFromBroadcastChannel( ) {
        int                               msgCountChannel1 = 100;
        int                               msgCountChannel2 = 50;
        using BroadcastChannel<DataTypeA> channel1         = new ();
        using BroadcastChannel<DataTypeB> channel2         = new ();
        try {
            using ChannelMux<DataTypeA, DataTypeB> mux       = new (channel1.Writer, channel2.Writer);
            CancellationToken                      ct        = CancellationToken.None;
            Stopwatch                              stopwatch = Stopwatch.StartNew();
            Task producer1 = Task.Run( ( ) => {
                try {
                    throw new DummyException();
                } catch ( Exception e ) {
                    channel1.Writer.Complete( e );
                    throw;
                }
            }, ct );
            Task producer2      = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, i => new DataTypeB( Sequence: i, WrittenTicks: stopwatch.ElapsedTicks ) ), ct );
            int  receivedCountA = 0;
            int  receivedCountB = 0;

            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 1 );

            while ( await mux.WaitToReadAsync( ct ) ) {
                if ( mux.TryRead( out DataTypeA _ ) ) {
                    receivedCountA++;
                }
                if ( mux.TryRead( out DataTypeB _ ) ) {
                    receivedCountB++;
                }
            }
            await producer1;
            await producer2;
            receivedCountA.Should().Be( msgCountChannel1 );
            receivedCountB.Should().Be( msgCountChannel2 );
            mux.Completion.IsCompleted.Should().BeTrue();
            mux.Completion.Exception.Should().BeNull();
            mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
            if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
                Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
            }
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
#pragma warning disable CA1031
        } catch ( Exception ) {
#pragma warning restore CA1031
            // nothing
        } finally {
            TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Should().HaveCount( 0 );
            _logger.LogDebug( "count of writers: {X}", TestUtils.GetPrivateField<ImmutableArray<ChannelWriter<DataTypeA>>>( channel1.Writer, "_outputWriters" ).Length );
        }
    }

    #endregion


    #region Inheritance Testing

    internal static async Task TypeInheritanceTestingOneSubOfOther( ) {
        using BroadcastChannel<BaseClass>      channel1         = new ();
        using BroadcastChannel<SubClassA>      channel2         = new ();
        using ChannelMux<BaseClass, SubClassA> mux              = new (channel1.Writer, channel2.Writer);
        int                                    msgCountChannel1 = 100;
        int                                    msgCountChannel2 = 50;
        CancellationToken                      ct               = CancellationToken.None;
        Task                                   producer1        = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, static x => new BaseClass( PropertyBase: -x - 1 ) ), ct );
        Task                                   producer2        = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, static x => new SubClassA( PropertyBase: x  + 1, PropertySubA: x + 2 ) ), ct );
        int                                    receivedCountA   = 0;
        int                                    receivedCountB   = 0;

        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( ( mux.TryRead( out BaseClass? baseClass ), mux.TryRead( out SubClassA? subClassA ) ) != ( false, false ) ) {
                if ( baseClass is { } bc ) {
                    bc.PropertyBase.Should().BeLessThan( 0 );
                    receivedCountA++;
                }
                if ( subClassA is { } subA ) {
                    subA.PropertyBase.Should().BeGreaterThan( 0 );
                    subA.PropertySubA.Should().Be( subA.PropertyBase + 1 );
                    receivedCountB++;
                }
            }
        }
        await producer1;
        await producer2;
        receivedCountA.Should().Be( msgCountChannel1 );
        receivedCountB.Should().Be( msgCountChannel2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
            Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
        }
    }

    internal static async Task TypeInheritanceTestingBothSubOfSame( ) {
        using BroadcastChannel<SubClassB>      channel1         = new ();
        using BroadcastChannel<SubClassA>      channel2         = new ();
        using ChannelMux<SubClassB, SubClassA> mux              = new (channel1.Writer, channel2.Writer);
        int                                    msgCountChannel1 = 100;
        int                                    msgCountChannel2 = 50;
        CancellationToken                      ct               = CancellationToken.None;
        Task                                   producer1        = Task.Run( ( ) => producerTaskSimple( channel1.Writer, msgCountChannel1, static x => new SubClassB( PropertyBase: -x - 1, PropertySubB: -x - 2 ) ), ct );
        Task                                   producer2        = Task.Run( ( ) => producerTaskSimple( channel2.Writer, msgCountChannel2, static x => new SubClassA( PropertyBase: x  + 1, PropertySubA: x  + 2 ) ), ct );
        int                                    receivedCountA   = 0;
        int                                    receivedCountB   = 0;

        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( ( mux.TryRead( out SubClassB? baseClass ), mux.TryRead( out SubClassA? subClassA ) ) != ( false, false ) ) {
                if ( baseClass is { } subB ) {
                    subB.PropertyBase.Should().BeLessThan( 0 );
                    subB.PropertySubB.Should().Be( subB.PropertyBase - 1 );
                    receivedCountA++;
                }
                if ( subClassA is { } subA ) {
                    subA.PropertyBase.Should().BeGreaterThan( 0 );
                    subA.PropertySubA.Should().Be( subA.PropertyBase + 1 );
                    receivedCountB++;
                }
            }
        }
        await producer1;
        await producer2;
        receivedCountA.Should().Be( msgCountChannel1 );
        receivedCountB.Should().Be( msgCountChannel2 );
        mux.Completion.IsCompleted.Should().BeTrue();
        mux.Completion.Exception.Should().BeNull();
        mux.Completion.IsCompletedSuccessfully.Should().BeTrue();
        if ( receivedCountA != msgCountChannel1 || receivedCountB != msgCountChannel2 ) {
            Assert.Fail( $"Not all messages were read. {nameof(receivedCountA)}: {receivedCountA} ; {nameof(receivedCountB)}: {receivedCountB}" );
        }
    }

    #endregion
}