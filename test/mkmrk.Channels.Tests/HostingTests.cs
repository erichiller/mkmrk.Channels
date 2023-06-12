using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;

using Xunit;
using Xunit.Abstractions;

namespace mkmrk.Channels.Tests;

public sealed class HostingTests : TestBase<HostingTests>, IDisposable {
    private readonly IHost                   _host;
    private readonly CancellationTokenSource _cts      = new CancellationTokenSource();
    private const    int                     _maxTestMs = 1_000;

    public HostingTests( ITestOutputHelper testOutputHelper ) : base( testOutputHelper, logLevel: LogEventLevel.Information ) {
        _host = CreateHostBuilder( startPublisherServices: true )
            .Build();
        _cts.CancelAfter( _maxTestMs ); // don't wait more than 1 sec
    }

    /* ************************************************************************ */

    public IHostBuilder CreateHostBuilder( bool startPublisherServices, LogLevel logLevel = LogLevel.Trace ) =>
        Host.CreateDefaultBuilder( Array.Empty<string>() )
            .ConfigureServices( ( hostContext, services ) => {
                services.AddBroadcastChannels();
                var loggerConfiguration = new Serilog.LoggerConfiguration()
                                          .MinimumLevel.Is( LogEventLevel.Verbose )
                                          .WriteTo.TestOutput( this._output!,
                                                               Serilog.Events.LogEventLevel.Verbose,
                                                               // outputTemplate: @"{Timestamp:HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId,-2} {SourceContext,-45} {Scope}{NewLine}     >> {Message:lj}{NewLine}{Exception}"
                                                               outputTemplate: @"{Timestamp:HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId,-2} {SourceContext,-45} {Scope} >> {Message:lj}{NewLine}{Exception}"
                                          )
                                          .Enrich.WithThreadId()
                                          .CreateLogger()
                                          .ForContext<HostingTests>();
                services.AddLogging( logBuilder =>
                                         logBuilder
                                             .ClearProviders()
                                             // .AddJsonConsole( options => {
                                             //     options.TimestampFormat   = @"HH:mm:ss.ffff ";
                                             //     options.JsonWriterOptions = new JsonWriterOptions() { Indented = true };
                                             //     options.IncludeScopes     = true;
                                             // } )
                                             // .AddConsole( opts => opts.TimestampFormat = @"HH:mm:ss.ffff " )
                                             /*
                                             .AddSimpleConsole( options => {
                                                 options.TimestampFormat = @"HH:mm:ss.ffff ";
                                                 // options.SingleLine      = true;
                                                 options.IncludeScopes = true;
                                                 // options.ColorBehavior   = LoggerColorBehavior.Default;
                                             } )
                                             */
                                             .SetMinimumLevel( LogLevel.Trace )
                                             // Serilog can be added as well
                                             .AddSerilog( loggerConfiguration, dispose: true )
                );

                if ( startPublisherServices ) {
                    _logger.LogDebug( "Starting Publisher Services" );
                    services.AddHostedService<BroadcastPublisher<ChannelMessageSubA>>();
                    services.AddHostedService<BroadcastPublisher<ChannelMessageSubB>>();
                    services.AddHostedService<BroadcastPublisher<ChannelMessageSubC>>();
                }
            } );


    [ Fact ]
    public async Task RequestBroadcastChannelReaderSource( ) {
        var readerSourceA = _host.Services.GetRequiredService<IBroadcastChannelReaderSource<ChannelMessageSubA>>(); // I need to ALWAYS FULLY DEFINE 
        var readerSourceB = _host.Services.GetRequiredService<IBroadcastChannelReaderSource<ChannelMessageSubB>>();
        var mux           = new ChannelMux<ChannelMessageSubA, ChannelMessageSubB>( readerSourceA, readerSourceB );
        await _host.StartAsync( _cts.Token );
        int                 receivedCountA = 0;
        int                 receivedCountB = 0;
        ChannelMessageSubA? lastMsgA       = null;
        ChannelMessageSubB? lastMsgB       = null;
        while ( await mux.WaitToReadAsync( _cts.Token ) ) {
            if ( mux.TryRead( out ChannelMessageSubA? msgA ) ) {
                msgA.Id.Should().Be( receivedCountA );
                lastMsgA = msgA;
                receivedCountA++;
            }
            if ( mux.TryRead( out ChannelMessageSubB? msgB ) ) {
                msgB.Id.Should().Be( receivedCountB );
                lastMsgB = msgB;
                receivedCountB++;
            }
        }
        receivedCountA.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        receivedCountB.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        lastMsgA.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
        lastMsgB.Should().BeOfType<ChannelMessageSubB>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
    }

    [ Fact ]
    public async Task RequestChannelMux2Service( ) {
        ChannelMux<ChannelMessageSubA, ChannelMessageSubB> mux = _host.Services.GetRequiredService<ChannelMux<ChannelMessageSubA, ChannelMessageSubB>>();
        await _host.StartAsync( _cts.Token );
        int                 receivedCountA = 0;
        int                 receivedCountB = 0;
        ChannelMessageSubA? lastMsgA       = null;
        ChannelMessageSubB? lastMsgB       = null;
        while ( await mux.WaitToReadAsync( _cts.Token ) ) {
            if ( mux.TryRead( out ChannelMessageSubA? msgA ) ) {
                msgA.Id.Should().Be( receivedCountA );
                lastMsgA = msgA;
                receivedCountA++;
            }
            if ( mux.TryRead( out ChannelMessageSubB? msgB ) ) {
                msgB.Id.Should().Be( receivedCountB );
                lastMsgB = msgB;
                receivedCountB++;
            }
        }
        receivedCountA.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        receivedCountB.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        lastMsgA.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
        lastMsgB.Should().BeOfType<ChannelMessageSubB>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
    }

    [ Fact ]
    public async Task RequestChannelMux3Service( ) {
        var mux = _host.Services.GetRequiredService<ChannelMux<ChannelMessageSubA, ChannelMessageSubB, ChannelMessageSubC>>();
        await _host.StartAsync( _cts.Token );
        int                 receivedCountA = 0;
        int                 receivedCountB = 0;
        int                 receivedCountC = 0;
        ChannelMessageSubA? lastMsgA       = null;
        ChannelMessageSubB? lastMsgB       = null;
        ChannelMessageSubC? lastMsgC       = null;
        while ( await mux.WaitToReadAsync( _cts.Token ) ) {
            if ( mux.TryRead( out ChannelMessageSubA? msgA ) ) {
                msgA.Id.Should().Be( receivedCountA );
                lastMsgA = msgA;
                receivedCountA++;
            }
            if ( mux.TryRead( out ChannelMessageSubB? msgB ) ) {
                msgB.Id.Should().Be( receivedCountB );
                lastMsgB = msgB;
                receivedCountB++;
            }
            if ( mux.TryRead( out var msgC ) ) {
                msgC.Id.Should().Be( receivedCountC );
                lastMsgC = msgC;
                receivedCountC++;
            }
        }
        receivedCountA.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        receivedCountB.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        receivedCountC.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        lastMsgA.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
        lastMsgB.Should().BeOfType<ChannelMessageSubB>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
        lastMsgC.Should().BeOfType<ChannelMessageSubC>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
    }

    [ Fact ]
    public async Task SimpleReaderRequest( ) {
        var readerA = _host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA>>();
        var logger  = _host.Services.GetRequiredService<ILogger<HostingTests>>();
        logger.LogInformation( "!!!!!!!!!!!!!!!!!!!!!!!!!! \n\tServices Starting. Reader: {Reader}", readerA );
        await _host.StartAsync( _cts.Token );
        _logger.LogInformation( "!!!!!!!!!!!!!!!!!!!!!!!!!! \n\tServices Started. Waiting to read from {Reader}", readerA );
        await readerA.WaitToReadAsync( _cts.Token );
        int expectedId = 0;

        readerA.TryRead( out var item ).Should().BeTrue();
        item.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( expectedId++ );
        await readerA.WaitToReadAsync( _cts.Token );
        logger.LogInformation( "!!!!!!!!!!!!!!!!!!!!!!!!!! \n\tData read from {Reader}", readerA );

        readerA.TryRead( out item ).Should().BeTrue();
        item.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( expectedId++ );

        int readCount = expectedId;
        while ( await readerA.WaitToReadAsync( _cts.Token ) ) {
            readCount++;
            readerA.TryRead( out item ).Should().BeTrue();
            item.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( expectedId++ );
            if ( readCount % 100 == 0 ) {
                logger.LogDebug( "Read {Count} messages", readCount );
            }
        }

        readCount.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT );
        item.Should().BeOfType<ChannelMessageSubA>().Subject.Id.Should().Be( BroadcastPublisher.MAX_MESSAGE_COUNT - 1 );
    }

    /// <inheritdoc />
    public void Dispose( ) {
        _host.StopAsync( _cts.Token ).Wait();
        _host.Dispose();
        _cts.Dispose();
    }
}

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelMessageSubA : ChannelMessage {
    public ChannelMessageSubA( ) { }
    public ChannelMessageSubA( int id ) => Id = id;
}

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelMessageSubB : ChannelMessage {
    public ChannelMessageSubB( ) { }
    public ChannelMessageSubB( int id ) => Id = id;
}

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record ChannelMessageSubC : ChannelMessage {
    public ChannelMessageSubC( ) { }
    public ChannelMessageSubC( int id ) => Id = id;
}