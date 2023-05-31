using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using mkmrk.Channels.Benchmarks;

using Xunit.Abstractions;

namespace mkmrk.Channels.Tests.Cli;

public static partial class Program {
    internal static async Task SimpleTest( int count = 100, int messageCount = 100_000 ) {
        for ( int i = 0 ; i < count ; i++ ) {
            Console.WriteLine( $"========================================\n## {nameof(SimpleTest)} #{i}\n========================================\n" );
            Stopwatch                                            stopwatch      = Stopwatch.StartNew();
            long                                                 testStartTicks = stopwatch.ElapsedTicks;
            long                                                 testStartMs    = stopwatch.ElapsedMilliseconds;
            BroadcastChannel<StructA, IBroadcastChannelResponse> channel1       = new ();
            BroadcastChannel<ClassA>                             channel2       = new ();
            using ChannelMux<StructA, ClassA>                    mux            = new (channel1.Writer, channel2.Writer);
            using CancellationTokenSource                        cts            = new CancellationTokenSource();
            cts.CancelAfter( 8_000 );
            CancellationToken ct = cts.Token;
            // ct = CancellationToken.None;

            Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, messageCount, static x => new StructA {
                                                                Id   = x,
                                                                Name = @"some_text"
                                                            } ), ct );
            Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, messageCount, static x => new ClassA {
                                                                Id   = x,
                                                                Name = @"some_text"
                                                            } ), ct );
            int receivedCountStructA = 0;
            int receivedCountClassA  = 0;
            try {
                while ( await mux.WaitToReadAsync( ct ) ) {
                    while ( ( mux.TryRead( out ClassA? classA ), mux.TryRead( out StructA structA ) ) != ( false, false ) ) {
                        if ( classA is { } ) {
                            receivedCountClassA++;
                        }
                        if ( structA != default(StructA) ) {
                            receivedCountStructA++;
                        }
                    }
                }
                await producer1;
                Console.WriteLine( $"{nameof(producer1)} await complete" );
                await producer2;
                Console.WriteLine( $"{nameof(producer2)} await complete" );
            } catch ( OperationCanceledException e ) {
                Console.WriteLine( $"Operation Cancelled {e}" );
                break;
            } catch ( System.InvalidOperationException e ) {
                Console.WriteLine( $"InvalidOperationException: {e}" );
                break;
            } catch ( Exception e ) {
                Console.WriteLine( $"EXCEPTION: {e}" );
                break;
            }
            Console.WriteLine( $"[{i:00}] {nameof(CancellationToken)}.{nameof(CancellationToken.IsCancellationRequested)} is {ct.IsCancellationRequested}" );
            Console.WriteLine( $"[{i:00}] {stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)\n\t" +
                               $"{nameof(messageCount)}: {messageCount}\n\t"                                                                                                                    +
                               $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t"                                                                                                    +
                               $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t"
            );
            Console.Write( $"[{i:00}] " );
            if ( receivedCountClassA != messageCount || receivedCountStructA != messageCount ) {
                throw new System.Exception( $"Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
            }
        }

        static void producerTask<T>( in BroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is beginning, totalMessages: {totalMessages}" );
            try {
                for ( int i = 0 ; i < totalMessages ; i++ ) {
                    writer.TryWrite( objectFactory( i ) );
                }
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is {nameof(writer.Complete)}" );
                writer.Complete();
            } catch ( Exception e ) {
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} caught exception: {e}" );
                throw;
            }
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is done" );
        }
    }

    internal static async Task StressTest( ) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int       i         = 0;
        while ( stopwatch.Elapsed < TimeSpan.FromHours( 8 ) ) {
            i++;
            await SimpleTest( count: 1, messageCount: 10_000_000 );
            await CheckForOffsetCompletionErrors( false );
        }
        Console.WriteLine( $"Ran for {stopwatch.Elapsed}. {i} loops." );
    }

    internal static async Task CheckForOffsetCompletionErrors( bool writeToFile = true ) {
        Stopwatch stopwatch    = Stopwatch.StartNew();
        const int messageCount = 10_000;
        for ( int i = 0 ; i < 10_000 ; i++ ) {
            long                                                  testStartTicks = stopwatch.ElapsedTicks;
            long                                                  testStartMs    = stopwatch.ElapsedMilliseconds;
            BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1       = new ();
            BroadcastChannel<ClassA>                              channel2       = new ();
            using ChannelMux<StructA?, ClassA>                    mux            = new (channel1.Writer, channel2.Writer);
            CancellationToken                                     ct             = CancellationToken.None;
            // using CancellationTokenSource cts = new CancellationTokenSource();
            // CancellationToken             ct  = cts.Token;

            Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, messageCount, static i => new StructA {
                                                                Id   = i,
                                                                Name = @"some_text"
                                                            } ), ct );
            Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, messageCount, static i => new ClassA {
                                                                Id   = i,
                                                                Name = @"some_text"
                                                            } ), ct );
            int receivedCountStructA = 0;
            int receivedCountClassA  = 0;
            while ( await mux.WaitToReadAsync( ct ) ) {
                while ( ( mux.TryRead( out ClassA? classA ), mux.TryRead( out StructA? structA ) ) != ( false, false ) ) {
                    if ( classA is { } ) {
                        receivedCountClassA++;
                    }
                    if ( structA is { } ) {
                        receivedCountStructA++;
                    }
                }
            }
            await producer1;
            await producer2;
            if ( receivedCountClassA != messageCount || receivedCountStructA != messageCount ) {
                throw new System.Exception( $"[{i}] Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
            }
            if ( i % 10 == 0 ) {
                Console.WriteLine( $"[{i}] @ {stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)\n\t" +
                                   $"{nameof(messageCount)}: {messageCount}\n\t"                                                                                                                   +
                                   $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t"                                                                                                   +
                                   $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t"
                );
                // Thread.Sleep( 5_000 );
            }
            // break;
        }

        static void producerTask<T>( in BroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
            int i = 0;
            // Thread.Sleep( Random.Shared.Next( 10, 70 ) ); // startup
            while ( i++ < totalMessages ) {
                writer.TryWrite( objectFactory( i ) );
                if ( i % _msgsPerMsgGroup == 0 ) {
                    Thread.Sleep( Random.Shared.Next( 1, 7 ) );
                }
            }
            writer.Complete();
        }
    }

    private class BaseClass {
        public int PropertyBase { get; init; }

        /// <inheritdoc />
        public override string ToString( ) {
            return $"{nameof(PropertyBase)} = {PropertyBase}";
        }
    }

    private class SubClassA : BaseClass {
        public int PropertySubA { get; init; }

        /// <inheritdoc />
        public override string ToString( ) {
            return base.ToString() + $" ; {nameof(PropertySubA)} = {PropertySubA}";
        }
    }


    private class SubClassB : BaseClass {
        public int PropertySubB { get; init; }

        /// <inheritdoc />
        public override string ToString( ) {
            return base.ToString() + $" ; {nameof(PropertySubB)} = {PropertySubB}";
        }
    }


    internal static async Task TypeInheritanceTestingOneSubOfOther( ) {
        Console.WriteLine( $"========================================\n## {nameof(TypeInheritanceTestingOneSubOfOther)} \n========================================\n" );
        Stopwatch                              stopwatch      = Stopwatch.StartNew();
        long                                   testStartTicks = stopwatch.ElapsedTicks;
        long                                   testStartMs    = stopwatch.ElapsedMilliseconds;
        BroadcastChannel<BaseClass>            channel1       = new ();
        BroadcastChannel<SubClassA>            channel2       = new ();
        using ChannelMux<BaseClass, SubClassA> mux            = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource          cts            = new CancellationTokenSource();
        const int                              messageCount   = 100;
        cts.CancelAfter( 8_000 );
        CancellationToken ct = cts.Token;
        // ct = CancellationToken.None;

        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, messageCount, static x => new BaseClass { PropertyBase = -x - 1 } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, messageCount, static x => new SubClassA {
                                                            PropertyBase = x + 1,
                                                            PropertySubA = x + 2
                                                        } ), ct );
        int receivedCount2 = 0;
        int receivedCount1 = 0;
        try {
            while ( await mux.WaitToReadAsync( ct ) ) {
                while ( ( mux.TryRead( out BaseClass? baseClass ), mux.TryRead( out SubClassA? subClassA ) ) != ( false, false ) ) {
                    if ( baseClass is { } bc ) {
                        if ( bc.PropertyBase >= 0 ) {
                            throw new Exception( bc.ToString() );
                        }
                        receivedCount1++;
                    }
                    if ( subClassA is { } subA ) {
                        if ( subA.PropertyBase <= 0 ) {
                            throw new Exception( subA.ToString() );
                        }
                        if ( subA.PropertySubA != subA.PropertyBase + 1 ) {
                            throw new Exception( subA.ToString() );
                        }
                        receivedCount2++;
                    }
                }
            }
            await producer1;
            Console.WriteLine( $"{nameof(producer1)} await complete" );
            await producer2;
            Console.WriteLine( $"{nameof(producer2)} await complete" );
        } catch ( OperationCanceledException e ) {
            Console.WriteLine( $"Operation Cancelled {e}" );
            throw;
        } catch ( System.InvalidOperationException e ) {
            Console.WriteLine( $"InvalidOperationException: {e}" );
            throw;
        } catch ( Exception e ) {
            Console.WriteLine( $"EXCEPTION: {e}" );
            throw;
        }
        Console.WriteLine( $"{nameof(CancellationToken)}.{nameof(CancellationToken.IsCancellationRequested)} is {ct.IsCancellationRequested}" );
        Console.WriteLine( $"{stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)\n\t" +
                           $"{nameof(messageCount)}: {messageCount}\n\t"                                                                                                           +
                           $"{nameof(receivedCount2)}: {receivedCount2}\n\t"                                                                                                       +
                           $"{nameof(receivedCount1)}: {receivedCount1}\n\t"
        );
        if ( receivedCount1 != messageCount || receivedCount2 != messageCount ) {
            throw new System.Exception( $"Not all messages were read. {nameof(receivedCount1)}: {receivedCount1} ; {nameof(receivedCount2)}: {receivedCount2}" );
        }

        static void producerTask<T>( in BroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is beginning, totalMessages: {totalMessages}" );
            try {
                for ( int i = 0 ; i < totalMessages ; i++ ) {
                    writer.TryWrite( objectFactory( i ) );
                }
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is {nameof(writer.Complete)}" );
                writer.Complete();
            } catch ( Exception e ) {
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} caught exception: {e}" );
                throw;
            }
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is done" );
        }
    }

    internal static async Task TypeInheritanceTestingBothSubOfSame( ) {
        Console.WriteLine( $"========================================\n## {nameof(TypeInheritanceTestingBothSubOfSame)} \n========================================\n" );
        Stopwatch                              stopwatch      = Stopwatch.StartNew();
        long                                   testStartTicks = stopwatch.ElapsedTicks;
        long                                   testStartMs    = stopwatch.ElapsedMilliseconds;
        BroadcastChannel<SubClassB>            channel1       = new ();
        BroadcastChannel<SubClassA>            channel2       = new ();
        using ChannelMux<SubClassB, SubClassA> mux            = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource          cts            = new CancellationTokenSource();
        const int                              messageCount   = 100;
        cts.CancelAfter( 8_000 );
        CancellationToken ct = cts.Token;
        // ct = CancellationToken.None;

        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, messageCount, static x => new SubClassB {
                                                            PropertyBase = -x - 1,
                                                            PropertySubB = -x - 2
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, messageCount, static x => new SubClassA {
                                                            PropertyBase = x + 1,
                                                            PropertySubA = x + 2
                                                        } ), ct );
        int receivedCount2 = 0;
        int receivedCount1 = 0;
        try {
            while ( await mux.WaitToReadAsync( ct ) ) {
                while ( ( mux.TryRead( out SubClassB? subClassB ), mux.TryRead( out SubClassA? subClassA ) ) != ( false, false ) ) {
                    if ( subClassB is { } subB ) {
                        if ( subB.PropertyBase >= 0 ) {
                            throw new Exception( subB.ToString() );
                        }
                        if ( subB.PropertySubB != subB.PropertyBase - 1 ) {
                            throw new Exception( subB.ToString() );
                        }
                        receivedCount1++;
                    }
                    if ( subClassA is { } subA ) {
                        if ( subA.PropertyBase <= 0 ) {
                            throw new Exception( subA.ToString() );
                        }
                        if ( subA.PropertySubA != subA.PropertyBase + 1 ) {
                            throw new Exception( subA.ToString() );
                        }
                        receivedCount2++;
                    }
                }
            }
            await producer1;
            Console.WriteLine( $"{nameof(producer1)} await complete" );
            await producer2;
            Console.WriteLine( $"{nameof(producer2)} await complete" );
        } catch ( OperationCanceledException e ) {
            Console.WriteLine( $"Operation Cancelled {e}" );
            throw;
        } catch ( System.InvalidOperationException e ) {
            Console.WriteLine( $"InvalidOperationException: {e}" );
            throw;
        } catch ( Exception e ) {
            Console.WriteLine( $"EXCEPTION: {e}" );
            throw;
        }
        Console.WriteLine( $"{nameof(CancellationToken)}.{nameof(CancellationToken.IsCancellationRequested)} is {ct.IsCancellationRequested}" );
        Console.WriteLine( $"{stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)\n\t" +
                           $"{nameof(messageCount)}: {messageCount}\n\t"                                                                                                           +
                           $"{nameof(receivedCount2)}: {receivedCount2}\n\t"                                                                                                       +
                           $"{nameof(receivedCount1)}: {receivedCount1}\n\t"
        );
        if ( receivedCount1 != messageCount || receivedCount2 != messageCount ) {
            throw new System.Exception( $"Not all messages were read. {nameof(receivedCount1)}: {receivedCount1} ; {nameof(receivedCount2)}: {receivedCount2}" );
        }

        static void producerTask<T>( in BroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is beginning, totalMessages: {totalMessages}" );
            try {
                for ( int i = 0 ; i < totalMessages ; i++ ) {
                    writer.TryWrite( objectFactory( i ) );
                }
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is {nameof(writer.Complete)}" );
                writer.Complete();
            } catch ( Exception e ) {
                Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} caught exception: {e}" );
                throw;
            }
            Console.WriteLine( $"{nameof(producerTask)} for type {typeof(T).Name} is done" );
        }
    }

    private static async Task ChannelComplete_WithException_ShouldThrow_UponAwait( ) {
        var tests = new ChannelMuxTests( new ConsoleTestOutputHelper() );
        await tests.ChannelComplete_WithException_ShouldThrow_UponAwait( true );
    }

    internal static async Task ExceptionShouldRemoveFromBroadcastChannel( ) {
        var tests = new ChannelMuxTests( new ConsoleTestOutputHelper() );
        await tests.ExceptionShouldRemoveFromBroadcastChannel();
    }

    internal static async Task ChannelMuxLatencyTest( ) {
        var tests = new ChannelMuxTests( new ConsoleTestOutputHelper() );
        await tests.ChannelMuxLatencyTest( false );
    }

    private static async Task AsyncWaitLoopOnly_2Producer( ) {
        ChannelMuxBenchmarks benchmark = new () { MessageCount = 10_000 };
        const int            testCount = 1;
        Stopwatch            stopwatch = Stopwatch.StartNew();
        for ( int i = 0 ; i < testCount ; i++ ) {
            long testStartTicks = stopwatch.ElapsedTicks;
            long testStartMs    = stopwatch.ElapsedMilliseconds;
            await benchmark.AsyncWaitLoopOnly_2Producer( withCancellationToken: true );
            if ( i % 100 == 0 ) {
                Console.WriteLine( $"[{i}] @ {stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)" );
            }
        }
    }

    private static async Task LoopTryRead2_2Producer( int? count = 10_000 ) {
        // while ( !System.Diagnostics.Debugger.IsAttached )
        // Thread.Sleep( TimeSpan.FromMilliseconds( 100 ) );
        ChannelMuxBenchmarks benchmark = new ();
        Stopwatch            stopwatch = Stopwatch.StartNew();
        benchmark.MessageCount = 100_000;
        Console.WriteLine( $"will run {count} times" );
        for ( int i = 0 ; i < count ; i++ ) {
            long testStartTicks = stopwatch.ElapsedTicks;
            long testStartMs    = stopwatch.ElapsedMilliseconds;
            await benchmark.LoopTryRead2_2Producer( withCancellationToken: false );
            if ( i % 10 == 0 ) {
                Console.WriteLine( $"[{i}] @ {stopwatch.ElapsedMilliseconds:N0} ms took {stopwatch.ElapsedTicks - testStartTicks:N0} ticks ({stopwatch.ElapsedMilliseconds - testStartMs} ms)" );
            }
        }
    }

    private static async Task LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes( ) {
        ChannelMuxBenchmarks benchmark = new () { MessageCount = 10 };
        await benchmark.LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes();
    }


    public static async Task ChannelMux_LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes( ) {
        ChannelMuxBenchmarks benchmark = new () { MessageCount = 10 };
        await benchmark.LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes();
    }

    public static async Task ChannelMux_LoopTryRead2_4Producer_4Tasks_4ReferenceTypes( ) {
        ChannelMuxBenchmarks benchmark = new () { MessageCount = 10 };
        await benchmark.LoopTryRead2_4Producer_4Tasks_4ReferenceTypes();
    }

    public static async Task ChannelMux_LoopTryRead2_8Producer_8Tasks( ) {
        ChannelMuxBenchmarks benchmark = new () { MessageCount = 10 };
        await benchmark.LoopTryRead2_8Producer_8Tasks( withCancellationToken: false );
    }

    private static int _msBetweenMsgGroups = 85;
    private static int _msgsPerSecond      = 500 / 10       * 20;
    private static int _msgsPerMsgGroup    = _msgsPerSecond / ( 1000 / _msBetweenMsgGroups ); // 500 per 10 seconds

    static void producerTaskWithMsgGrouping<T>( in BroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in Stopwatch stopwatch, in int totalMessages, System.Func<int, T> objectFactory ) {
        int i = 0;
        Console.WriteLine( $"Producer Task Starting at {stopwatch.ElapsedTicks:N0} on {Thread.CurrentThread.ManagedThreadId}" );
        Thread.Sleep( 50 ); // startup
        while ( i++ < totalMessages ) {
            writer.TryWrite( objectFactory( i ) );
            if ( i % _msgsPerMsgGroup == 0 ) {
                Thread.Sleep( _msBetweenMsgGroups );
            }
        }
        Console.WriteLine( $"Producer Task Completing at {stopwatch.ElapsedTicks:N0} on {Thread.CurrentThread.ManagedThreadId}" );
        writer.Complete();
    }

    internal static async Task LatencyTest( ) {
        BroadcastChannel<StructB?>     channel1 = new ();
        BroadcastChannel<StructC?>     channel2 = new ();
        ChannelMux<StructB?, StructC?> mux      = new (channel1.Writer, channel2.Writer);
        CancellationToken              ct       = CancellationToken.None;
        // int                            MessageCount = 10_000_000;
        const int messageCount = 1_000_000;
        Stopwatch stopwatch    = Stopwatch.StartNew();
        Task      producer1    = Task.Run( ( ) => producerTaskWithMsgGrouping( channel1.Writer, stopwatch, messageCount, _ => new StructB( stopwatch.ElapsedTicks ) ), ct );
        Task      producer2    = Task.Run( ( ) => producerTaskWithMsgGrouping( channel2.Writer, stopwatch, messageCount, _ => new StructC( stopwatch.ElapsedTicks ) ), ct );
        int       received2    = 0;
        int       received1    = 0;

        Console.WriteLine( $"{nameof(_msBetweenMsgGroups)}: {_msBetweenMsgGroups:N0}\n" +
                           $"{nameof(_msgsPerSecond)}: {_msgsPerSecond:N0}\n"           +
                           $"{nameof(_msgsPerMsgGroup)}: {_msgsPerMsgGroup:N0}\n"       +
                           $"" );
        Log( $"StopWatch Frequency is: {Stopwatch.Frequency:N0}" );
        // using (StreamWriter outputFile = new StreamWriter(file.FullName)){
        Console.WriteLine( $"Beginning at Stopwatch ticks: {stopwatch.ElapsedTicks:N0} on {Thread.CurrentThread.ManagedThreadId}" );
        TimeInfo[] timeInfo1 = new TimeInfo[ messageCount ];
        TimeInfo[] timeInfo2 = new TimeInfo[ messageCount ];
        while ( await mux.WaitToReadAsync( ct ) ) {
            Console.WriteLine( $"[{( ( received1 + received2 ) / 2 ):N0}] WaitToReadAsync continued, Stopwatch ticks: {stopwatch.ElapsedTicks:N0}" );
            while ( ( mux.TryRead( out StructB? structB ), mux.TryRead( out StructC? structC ) ) != ( false, false ) ) {
                long elapsed = stopwatch.ElapsedTicks;
                if ( structB is { TimeSent: var ts1 } ) {
                    timeInfo1[ received1 ] = new (Seq: received1, Written: ts1, Delta: ( elapsed - ts1 ));
                    // long tickDelta = ( elapsed - ts1 );
                    // Log( $"{nameof(structB)} ticks since message: {tickDelta:N0} ticks ({( tickDelta / Stopwatch.Frequency / 1000 ):N3}) ms" );
                    received1++;
                }
                if ( structC is { TimeSent: var ts2 } ) {
                    timeInfo2[ received2 ] = new (Seq: received2, Written: ts2, Delta: ( elapsed - ts2 ));
                    // long tickDelta = ( elapsed - ts2 );
                    // Log( $"{nameof(structC)} ticks since message: {tickDelta:N0} ticks ({( tickDelta / Stopwatch.Frequency / 1000 ):N3}) ms" );
                    received2++;
                }
            }
        }
        Console.WriteLine( $"Finished Reading at Stopwatch ticks: {stopwatch.ElapsedTicks:N0} on {Thread.CurrentThread.ManagedThreadId}" );
        // StringBuilder sb = new ();
        Console.WriteLine( $"{timeInfo1[ 0 ].Written:N0} to {timeInfo2[ ^1 ].Written:N0}" );
        Console.WriteLine( $"{nameof(_msBetweenMsgGroups)}: {_msBetweenMsgGroups:N0}\n" +
                           $"{nameof(_msgsPerSecond)}: {_msgsPerSecond:N0}\n"           +
                           $"{nameof(_msgsPerMsgGroup)}: {_msgsPerMsgGroup:N0}\n"       +
                           $"" );
        var file = new System.IO.FileInfo( System.IO.Path.Combine( System.Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), "Downloads", "latency_test_results.csv" ) );

        await using ( StreamWriter outputFile = new StreamWriter( file.FullName ) ) {
            for ( int i = 0 ; i < messageCount ; i++ ) {
                // if ( i % 100 == 0 ) {
                await outputFile.WriteLineAsync( $"{timeInfo1[ i ].Seq},{timeInfo1[ i ].Written},{timeInfo1[ i ].Delta},{timeInfo2[ i ].Written},{timeInfo2[ i ].Delta}" ).ConfigureAwait( false );
                // }
            }
        }
        // System.IO.File.WriteAllText( file.FullName, sb.ToString() );
        await producer1;
        await producer2;
        if ( received1 != messageCount || received2 != messageCount ) {
            throw new System.Exception( $"Not all messages were read. {nameof(received1)}: {received1} ; {nameof(received2)}: {received2}" );
        }
    }
}

file readonly record struct StructB( long TimeSent );

file readonly record struct StructC( long TimeSent );

/// <summary>
/// Represents a class which can be used to provide test output.
/// </summary>
file class ConsoleTestOutputHelper : ITestOutputHelper {
    /// <summary>Adds a line of text to the output.</summary>
    /// <param name="message">The message</param>
    public void WriteLine( string message ) => Console.WriteLine( message );

    /// <summary>Formats a line of text and adds it to the output.</summary>
    /// <param name="format">The message format</param>
    /// <param name="args">The format arguments</param>
    public void WriteLine( string format, params object[] args ) => Console.WriteLine( format, args );
}

file readonly record struct TimeInfo( int Seq, long Written, long Delta );