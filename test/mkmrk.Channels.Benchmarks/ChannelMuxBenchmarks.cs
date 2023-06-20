using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace mkmrk.Channels.Benchmarks;

public class UnexpectedCountsException : Exception {
    public UnexpectedCountsException( string message ) : base( message ) { }
}

public class ChannelMuxBenchmarks {
    [ Params( 100_000 ) ]
    // ReSharper disable once UnassignedField.Global
    public int MessageCount;

    /*
     * Use below for debugger
     */

    // [ GlobalSetup ]
    // public void Setup( ) {
    //     System.Diagnostics.Debugger.Launch();
    // }
    // [ GlobalSetup ]
    // public void Setup( ) {
    //     while ( !System.Diagnostics.Debugger.IsAttached )
    //         Thread.Sleep( TimeSpan.FromMilliseconds( 100 ) );
    // }

    private static void producerTask<T>( in IBroadcastChannelWriter<T, IBroadcastChannelResponse> writer, in int totalMessages, System.Func<int, T> objectFactory ) {
        int i = 0;
        while ( i++ < totalMessages ) {
            writer.TryWrite( objectFactory( i ) );
        }
        writer.Complete();
    }

    [ Arguments( true ) ]
    [ Arguments( false ) ]
    [ Benchmark ]
    [ SuppressMessage( "ReSharper", "NotAccessedOutParameterVariable" ) ]
    public async Task AsyncWaitLoopOnly_2Producer( bool withCancellationToken ) {
        using BroadcastChannel<StructA, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                             channel2 = new ();
        using ChannelMux<StructA, ClassA>                          mux      = new (channel1.Writer, channel2.Writer); // { EnableLogging = false };
        using CancellationTokenSource                              cts      = new CancellationTokenSource();
        CancellationToken                                          ct       = CancellationToken.None;
        if ( withCancellationToken ) {
            ct = cts.Token;
            if ( !ct.CanBeCanceled ) {
                throw new Exception( "CancellationToken needs to be able to be cancelled to properly test." );
            }
        }
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;

        ClassA? classA;
        StructA classB;
        while ( await mux.WaitToReadAsync( ct ) ) {
            if ( mux.TryRead( out classA ) ) {
                receivedCountClassA++;
            }
            if ( mux.TryRead( out classB ) ) {
                receivedCountStructA++;
            }
        }
        await producer1;
        await producer2;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
        }
    }

    [ Arguments( true ) ]
    [ Arguments( false ) ]
    [ Benchmark ]
    public async Task LoopTryRead_2Producer( bool withCancellationToken ) {
        using BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                              channel2 = new ();
        using ChannelMux<StructA?, ClassA>                          mux      = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource                               cts      = new CancellationTokenSource();
        CancellationToken                                           ct       = CancellationToken.None;
        if ( withCancellationToken ) {
            ct = cts.Token;
            if ( !ct.CanBeCanceled ) {
                throw new Exception( "CancellationToken needs to be able to be cancelled to properly test." );
            }
        }
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int      receivedCountStructA = 0;
        int      receivedCountClassA  = 0;
        StructA? structA              = null;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( mux.TryRead( out ClassA? classA ) || mux.TryRead( out structA ) ) {
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( structA is { } ) {
                    receivedCountStructA++;
                    structA = null;
                }
            }
        }
        await producer1;
        await producer2;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
        }
    }

    [ Arguments( true ) ]
    [ Arguments( false ) ]
    [ Benchmark ]
    public async Task LoopTryRead2_2Producer( bool withCancellationToken ) {
        using BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                              channel2 = new ();
        using ChannelMux<StructA?, ClassA>                          mux      = new (channel1.Writer, channel2.Writer);
        using CancellationTokenSource                               cts      = new CancellationTokenSource();
        CancellationToken                                           ct       = CancellationToken.None;
        if ( withCancellationToken ) {
            ct = cts.Token;
            if ( !ct.CanBeCanceled ) {
                throw new Exception( "CancellationToken needs to be able to be cancelled to properly test." );
            }
        }
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
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
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
        }
    }

    [ Benchmark ]
    public async Task LoopTryRead2_3Producer( ) {
        using BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                              channel2 = new ();
        using BroadcastChannel<ClassB>                              channel3 = new ();
        using ChannelMux<StructA?, ClassA, ClassB>                  mux      = new (channel1.Writer, channel2.Writer, channel3.Writer);
        CancellationToken                                           ct       = CancellationToken.None;
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer3 = Task.Run( ( ) => producerTask( channel3.Writer, MessageCount, i => new ClassB {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;
        int receivedCountClassB  = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out StructA? structA ),
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ) ) != ( false, false, false ) ) {
                if ( structA is { } ) {
                    receivedCountStructA++;
                }
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
            }
        }
        await producer1;
        await producer2;
        await producer3;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount || receivedCountClassB != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"    +
                                                 $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t" +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t"   +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t"
            );
        }
    }

    [ Benchmark ]
    public async Task LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes( ) {
        using BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                              channel2 = new ();
        using BroadcastChannel<ClassB>                              channel3 = new ();
        using BroadcastChannel<ClassC>                              channel4 = new ();
        using ChannelMux<StructA?, ClassA, ClassB, ClassC>          mux      = new (channel1.Writer, channel2.Writer, channel3.Writer, channel4.Writer);
        CancellationToken                                           ct       = CancellationToken.None;
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer3 = Task.Run( ( ) => producerTask( channel3.Writer, MessageCount, i => new ClassB {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer4 = Task.Run( ( ) => producerTask( channel4.Writer, MessageCount, i => new ClassC {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;
        int receivedCountClassB  = 0;
        int receivedCountClassC  = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out StructA? structA ),
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ),
                       mux.TryRead( out ClassC? classC ) ) != ( false, false, false, false ) ) {
                if ( structA is { } ) {
                    receivedCountStructA++;
                }
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
                if ( classC is { } ) {
                    receivedCountClassC++;
                }
            }
        }
        await producer1;
        await producer2;
        await producer3;
        await producer4;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount || receivedCountClassB != MessageCount || receivedCountClassC != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"    +
                                                 $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t" +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t"   +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t"   +
                                                 $"{nameof(receivedCountClassC)}: {receivedCountClassC}\n\t"
            );
        }
    }

    [ Benchmark ]
    public async Task LoopTryRead2_4Producer_4Tasks_4ReferenceTypes( ) {
        using BroadcastChannel<ClassA>                   channel1 = new ();
        using BroadcastChannel<ClassB>                   channel2 = new ();
        using BroadcastChannel<ClassC>                   channel3 = new ();
        using BroadcastChannel<ClassD>                   channel4 = new ();
        using ChannelMux<ClassA, ClassB, ClassC, ClassD> mux      = new (channel1.Writer, channel2.Writer, channel3.Writer, channel4.Writer);
        CancellationToken                                ct       = CancellationToken.None;
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassB {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer3 = Task.Run( ( ) => producerTask( channel3.Writer, MessageCount, i => new ClassC {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer4 = Task.Run( ( ) => producerTask( channel4.Writer, MessageCount, i => new ClassD {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountClassA = 0;
        int receivedCountClassB = 0;
        int receivedCountClassC = 0;
        int receivedCountClassD = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ),
                       mux.TryRead( out ClassC? classC ),
                       mux.TryRead( out ClassD? classD ) ) != ( false, false, false, false ) ) {
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
                if ( classC is { } ) {
                    receivedCountClassC++;
                }
                if ( classD is { } ) {
                    receivedCountClassD++;
                }
            }
        }
        await producer1;
        await producer2;
        await producer3;
        await producer4;
        if ( receivedCountClassA != MessageCount || receivedCountClassB != MessageCount || receivedCountClassC != MessageCount || receivedCountClassD != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"  +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t" +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t" +
                                                 $"{nameof(receivedCountClassC)}: {receivedCountClassC}\n\t" +
                                                 $"{nameof(receivedCountClassD)}: {receivedCountClassD}\n\t"
            );
        }
    }

    [ Benchmark ]
    public async Task LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes( ) {
        using BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using BroadcastChannel<ClassA>                              channel2 = new ();
        using BroadcastChannel<ClassB>                              channel3 = new ();
        using BroadcastChannel<ClassC>                              channel4 = new ();
        using ChannelMux<StructA?, ClassA, ClassB, ClassC>          mux      = new (channel1.Writer, channel2.Writer, channel3.Writer, channel4.Writer);
        CancellationToken                                           ct       = CancellationToken.None;
        Task                                                        producer = Task.Run( producerTaskMultiChannel, ct );

        void producerTaskMultiChannel( ) {
            int i = 0;
            while ( i++ < MessageCount ) {
                channel1.Writer.TryWrite( new StructA {
                                              Id   = i,
                                              Name = @"some_text"
                                          } );
                channel2.Writer.TryWrite( new ClassA {
                                              Id   = i,
                                              Name = @"some_text"
                                          } );
                channel3.Writer.TryWrite( new ClassB {
                                              Id   = i,
                                              Name = @"some_text"
                                          } );
                channel4.Writer.TryWrite( new ClassC {
                                              Id   = i,
                                              Name = @"some_text"
                                          } );
            }
            channel1.Writer.Complete();
            channel2.Writer.Complete();
            channel3.Writer.Complete();
            channel4.Writer.Complete();
        }

        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;
        int receivedCountClassB  = 0;
        int receivedCountClassC  = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out StructA? structA ),
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ),
                       mux.TryRead( out ClassC? classC ) ) != ( false, false, false, false ) ) {
                if ( structA is { } ) {
                    receivedCountStructA++;
                }
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
                if ( classC is { } ) {
                    receivedCountClassC++;
                }
            }
        }
        await producer;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount || receivedCountClassB != MessageCount || receivedCountClassC != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"    +
                                                 $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t" +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t"   +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t"   +
                                                 $"{nameof(receivedCountClassC)}: {receivedCountClassC}\n\t"
            );
        }
    }

    [ Benchmark ]
    public async Task LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes_WriteAsync( ) {
        using      BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1 = new ();
        using       BroadcastChannel<ClassA>                             channel2 = new ();
        using        BroadcastChannel<ClassB>                            channel3 = new ();
        using       BroadcastChannel<ClassC>                             channel4 = new ();
        using    ChannelMux<StructA?, ClassA, ClassB, ClassC>            mux      = new (channel1.Writer, channel2.Writer, channel3.Writer, channel4.Writer);
        CancellationToken                                                ct       = CancellationToken.None;
        Task                                                             producer = Task.Run( producerTaskMultiChannel, ct );

        async Task producerTaskMultiChannel( ) {
            int i = 0;
            while ( i++ < MessageCount ) {
                await channel1.Writer.WriteAsync( new StructA {
                                                      Id   = i,
                                                      Name = @"some_text"
                                                  }, ct ).ConfigureAwait( false );
                await channel2.Writer.WriteAsync( new ClassA {
                                                      Id   = i,
                                                      Name = @"some_text"
                                                  }, ct ).ConfigureAwait( false );
                await channel3.Writer.WriteAsync( new ClassB {
                                                      Id   = i,
                                                      Name = @"some_text"
                                                  }, ct ).ConfigureAwait( false );
                await channel4.Writer.WriteAsync( new ClassC {
                                                      Id   = i,
                                                      Name = @"some_text"
                                                  }, ct ).ConfigureAwait( false );
            }
            channel1.Writer.Complete();
            channel2.Writer.Complete();
            channel3.Writer.Complete();
            channel4.Writer.Complete();
        }

        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;
        int receivedCountClassB  = 0;
        int receivedCountClassC  = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out StructA? structA ),
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ),
                       mux.TryRead( out ClassC? classC ) ) != ( false, false, false, false ) ) {
                if ( structA is { } ) {
                    receivedCountStructA++;
                }
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
                if ( classC is { } ) {
                    receivedCountClassC++;
                }
            }
        }
        await producer;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount || receivedCountClassB != MessageCount || receivedCountClassC != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"    +
                                                 $"{nameof(receivedCountStructA)}: {receivedCountStructA}\n\t" +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t"   +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t"   +
                                                 $"{nameof(receivedCountClassC)}: {receivedCountClassC}\n\t"
            );
        }
    }


    [ Arguments( true ) ]
    [ Arguments( false ) ]
    [ Benchmark ]
    public async Task LoopTryRead2_8Producer_8Tasks( bool withCancellationToken ) {
        using      BroadcastChannel<ClassA> channel1 = new ();
        using BroadcastChannel<ClassB>      channel2 = new ();
        using BroadcastChannel<ClassC>      channel3 = new ();
        using BroadcastChannel<ClassD>      channel4 = new ();
        using BroadcastChannel<ClassE>      channel5 = new ();
        using BroadcastChannel<ClassF>      channel6 = new ();
        using BroadcastChannel<ClassG>      channel7 = new ();
        using BroadcastChannel<ClassH>      channel8 = new ();
        using      ChannelMux<ClassA, ClassB, ClassC, ClassD, ClassE, ClassF, ClassG, ClassH> mux = new (
            channel1.Writer, channel2.Writer, channel3.Writer, channel4.Writer,
            channel5.Writer, channel6.Writer, channel7.Writer, channel8.Writer);
        using CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken             ct  = withCancellationToken ? cts.Token : CancellationToken.None;
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassB {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer3 = Task.Run( ( ) => producerTask( channel3.Writer, MessageCount, i => new ClassC {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer4 = Task.Run( ( ) => producerTask( channel4.Writer, MessageCount, i => new ClassD {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer5 = Task.Run( ( ) => producerTask( channel5.Writer, MessageCount, i => new ClassE {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer6 = Task.Run( ( ) => producerTask( channel6.Writer, MessageCount, i => new ClassF {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer7 = Task.Run( ( ) => producerTask( channel7.Writer, MessageCount, i => new ClassG {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer8 = Task.Run( ( ) => producerTask( channel8.Writer, MessageCount, i => new ClassH {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountClassA = 0;
        int receivedCountClassB = 0;
        int receivedCountClassC = 0;
        int receivedCountClassD = 0;
        int receivedCountClassE = 0;
        int receivedCountClassF = 0;
        int receivedCountClassG = 0;
        int receivedCountClassH = 0;
        while ( await mux.WaitToReadAsync( ct ) ) {
            while ( (
                       mux.TryRead( out ClassA? classA ),
                       mux.TryRead( out ClassB? classB ),
                       mux.TryRead( out ClassC? classC ),
                       mux.TryRead( out ClassD? classD ),
                       mux.TryRead( out ClassE? classE ),
                       mux.TryRead( out ClassF? classF ),
                       mux.TryRead( out ClassG? classG ),
                       mux.TryRead( out ClassH? classH ) ) != ( false, false, false, false, false, false, false, false ) ) {
                if ( classA is { } ) {
                    receivedCountClassA++;
                }
                if ( classB is { } ) {
                    receivedCountClassB++;
                }
                if ( classC is { } ) {
                    receivedCountClassC++;
                }
                if ( classD is { } ) {
                    receivedCountClassD++;
                }
                if ( classE is { } ) {
                    receivedCountClassE++;
                }
                if ( classF is { } ) {
                    receivedCountClassF++;
                }
                if ( classG is { } ) {
                    receivedCountClassG++;
                }
                if ( classH is { } ) {
                    receivedCountClassH++;
                }
            }
        }
        await producer1;
        await producer2;
        await producer3;
        await producer4;
        await producer5;
        await producer6;
        await producer7;
        await producer8;
        if ( receivedCountClassA    != MessageCount || receivedCountClassB != MessageCount || receivedCountClassC != MessageCount || receivedCountClassD != MessageCount
             || receivedCountClassE != MessageCount || receivedCountClassF != MessageCount || receivedCountClassG != MessageCount || receivedCountClassH != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. Expected {MessageCount}\n\t"  +
                                                 $"{nameof(receivedCountClassA)}: {receivedCountClassA}\n\t" +
                                                 $"{nameof(receivedCountClassB)}: {receivedCountClassB}\n\t" +
                                                 $"{nameof(receivedCountClassC)}: {receivedCountClassC}\n\t" +
                                                 $"{nameof(receivedCountClassD)}: {receivedCountClassD}\n\t" +
                                                 $"{nameof(receivedCountClassE)}: {receivedCountClassE}\n\t" +
                                                 $"{nameof(receivedCountClassF)}: {receivedCountClassF}\n\t" +
                                                 $"{nameof(receivedCountClassG)}: {receivedCountClassG}\n\t" +
                                                 $"{nameof(receivedCountClassH)}: {receivedCountClassH}\n\t"
            );
        }
    }


    /*
     * 
     */

    [ Benchmark ]
    public async Task BroadcastChannelOnly( ) {
        using      BroadcastChannel<StructA?, IBroadcastChannelResponse> channel1       = new ();
        using       BroadcastChannel<ClassA>                  channel2       = new ();
        var                                                   channelReader1 = channel1.CreateReader(); // these must be setup BEFORE the producer begins
        var                                                   channelReader2 = channel2.CreateReader();
        CancellationToken                                     ct             = CancellationToken.None;
        Task producer1 = Task.Run( ( ) => producerTask( channel1.Writer, MessageCount, i => new StructA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        Task producer2 = Task.Run( ( ) => producerTask( channel2.Writer, MessageCount, i => new ClassA {
                                                            Id   = i,
                                                            Name = @"some_text"
                                                        } ), ct );
        int receivedCountStructA = 0;
        int receivedCountClassA  = 0;
        // ReSharper disable UnusedVariable
        Task reader1 = Task.Run( async ( ) => {
            while ( await channelReader1.WaitToReadAsync( ct ) ) {
                if ( channelReader1.TryRead( out StructA? structA ) ) {
                    receivedCountStructA++;
                }
            }
        }, ct );
        Task reader2 = Task.Run( async ( ) => {
            while ( await channelReader2.WaitToReadAsync( ct ) ) {
                if ( channelReader2.TryRead( out ClassA? classA ) ) {
                    receivedCountClassA++;
                }
            }
        }, ct );
        // ReSharper restore UnusedVariable
        await producer1;
        await producer2;
        await reader1;
        await reader2;
        if ( receivedCountClassA != MessageCount || receivedCountStructA != MessageCount ) {
            throw new UnexpectedCountsException( $"Not all messages were read. {nameof(receivedCountClassA)}: {receivedCountClassA} ; {nameof(receivedCountStructA)}: {receivedCountStructA}" );
        }
    }
}

public readonly record struct StructA( int Id, string Name, string? Description = null ) {
    public override string ToString( ) => $"{nameof(StructA)} {{ Id = {Id}, Name = {Name}, Description = {Description} }}";
}

public class ClassA {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassA)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassB {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassB)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassC {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassC)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassD {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassD)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassE {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassE)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassF {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassF)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassG {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassG)} {{ Id = {Id}, Name = {Name} }}";
}

public class ClassH {
    public          int    Id;
    public          string Name = String.Empty;
    public override string ToString( ) => $"{nameof(ClassH)} {{ Id = {Id}, Name = {Name} }}";
}