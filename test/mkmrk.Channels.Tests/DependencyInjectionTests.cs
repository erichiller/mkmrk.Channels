using System;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog.Events;

using Xunit;
using Xunit.Abstractions;

namespace mkmrk.Channels.Tests;

public sealed class DependencyInjectionTests : TestBase<DependencyInjectionTests> {
    /// <inheritdoc />
    public DependencyInjectionTests( ITestOutputHelper? output, ILogger? logger = null, LogEventLevel logLevel = LogEventLevel.Verbose ) : base( output, logger, logLevel ) { }

    private IHostBuilder getHost( ) => Host.CreateDefaultBuilder( Array.Empty<string>() )
                                           .ConfigureServices( ( _, services ) => {
                                               services.AddLogging( logBuilder =>
                                                                        logBuilder
                                                                            .ClearProviders()
                                                                            .AddSimpleConsole( options => {
                                                                                options.TimestampFormat = @"HH:mm:ss.ffff ";
                                                                                options.IncludeScopes   = true;
                                                                            } )
                                                                            .SetMinimumLevel( LogLevel.Trace )
                                               );
                                           } );

    [ Fact ]
    public void Requests_ShouldReceiveSame_BroadcastChannel( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        host.Services.GetRequiredService<IBroadcastChannel<ChannelMessageSubA, ChannelResponse>>()
            .Should()
            .BeSameAs(
                host.Services.GetRequiredService<IBroadcastChannel<ChannelMessageSubA>>()
            );
    }

    [ Fact ]
    public void Requests_ShouldReceiveSame_BroadcastChannelWriter_WhenUsingBroadcastChannelFirst( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        host.Services.GetRequiredService<IBroadcastChannel<ChannelMessageSubA, ChannelResponse>>().Writer
            .Should()
            .BeSameAs(
                host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA>>()
            );
    }

    [ Fact ]
    public void Requests_ShouldReceiveSame_BroadcastChannelWriter_WhenUsingBroadcastChannelLast( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA>>()
            .Should()
            .BeSameAs(
                host.Services.GetRequiredService<IBroadcastChannel<ChannelMessageSubA, ChannelResponse>>().Writer
            );
    }

    [ Fact ]
    public void Requests_ShouldReceiveSame_BroadcastChannelWriter( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>()
            .Should()
            .BeSameAs(
                host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA>>()
            );
    }

    [ Fact ]
    public void ReadersOfSameType_ShouldHaveSame_BroadcastChannelWriter( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        var writer = host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>();
        writer.Should().BeSameAs( host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>() );
        writer.ReaderCount.Should().Be( 0 );
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA, ChannelResponse>>();
        writer.ReaderCount.Should().Be( 1 );
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA>>();
        writer.ReaderCount.Should().Be( 2 );
        BroadcastChannelReader<ChannelMessageSubA, ChannelResponse> readerFromConcreteSource = host.Services.GetRequiredService<BroadcastChannelReaderSource<ChannelMessageSubA, ChannelResponse>>();
        writer.ReaderCount.Should().Be( 3 );
        IBroadcastChannelReader<ChannelMessageSubA, ChannelResponse> readerFromInterfaceSource = host.Services.GetRequiredService<IBroadcastChannelReaderSource<ChannelMessageSubA, ChannelResponse>>().ToReader();
        writer.ReaderCount.Should().Be( 4 );
    }

    [ Fact ]
    public void IBroadcastChannelReaderSource_ShouldBe_ConvertibleTo_IBroadcastChannelReader( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        var writer = host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>();
        writer.Should().BeSameAs( host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>() );
        writer.ReaderCount.Should().Be( 0 );
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA, ChannelResponse>>();
        writer.ReaderCount.Should().Be( 1 );
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA>>();
        writer.ReaderCount.Should().Be( 2 );
        IBroadcastChannelReader<ChannelMessageSubA> readerFromInterfaceSource = host.Services.GetRequiredService<IBroadcastChannelReaderSource<ChannelMessageSubA>>().ToReader();
        writer.ReaderCount.Should().Be( 3 );
    }

    [ Fact ]
    public void ReadersOfSameType_ShouldHaveSame_BroadcastChannelWriter_WhenRequestedLast( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA, ChannelResponse>>();
        host.Services.GetRequiredService<IBroadcastChannelReader<ChannelMessageSubA>>();
        BroadcastChannelReader<ChannelMessageSubA, ChannelResponse>  readerFromConcreteSource  = host.Services.GetRequiredService<BroadcastChannelReaderSource<ChannelMessageSubA, ChannelResponse>>();
        IBroadcastChannelReader<ChannelMessageSubA, ChannelResponse> readerFromInterfaceSource = host.Services.GetRequiredService<IBroadcastChannelReaderSource<ChannelMessageSubA, ChannelResponse>>().ToReader();
        var                                                          writer                    = host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>();
        writer.Should().BeSameAs( host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>() );
        writer.ReaderCount.Should().Be( 4 );
    }


    [ Fact ]
    public void MuxOfSameType_ShouldHaveSame_BroadcastChannelWriter( ) {
        using IHost host = getHost().ConfigureServices(
            services => {
                services.AddBroadcastChannel<ChannelMessageSubA, ChannelResponse>();
                services.AddBroadcastChannels();
            } ).Build();
        var                                                writer = host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubA, ChannelResponse>>();
        ChannelMux<ChannelMessageSubA, ChannelMessageSubB> mux    = host.Services.GetRequiredService<ChannelMux<ChannelMessageSubA, ChannelMessageSubB>>();
        writer.ReaderCount.Should().Be( 1 );
        host.Services.GetRequiredService<IBroadcastChannelWriter<ChannelMessageSubB>>().ReaderCount.Should().Be( 1 );
    }
}