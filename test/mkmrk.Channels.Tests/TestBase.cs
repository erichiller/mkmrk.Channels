using System;

using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;

using Xunit.Abstractions;

namespace mkmrk.Channels.Tests;

/// <summary>
/// Base class for tests to ease construction
/// </summary>
public class TestBase<T> {
    protected delegate void TestOutputHelperWriteLine( string msg );

    private protected readonly TestOutputHelperWriteLine _writeLine;
    private protected readonly ITestOutputHelper?        _output;

    private protected readonly Microsoft.Extensions.Logging.ILogger _logger;

    private static ILogger<TLogger> createLogger<TLogger>( ) {
        using Serilog.Extensions.Logging.SerilogLoggerFactory loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory();
        return loggerFactory.CreateLogger<TLogger>();
    }

    private static ILogger<TLogger> configureLogging<TLogger>( ITestOutputHelper output, LogEventLevel logLevel = LogEventLevel.Verbose ) {
        Log.Logger = new Serilog.LoggerConfiguration()
                     .MinimumLevel.Is( logLevel )
                     .WriteTo.TestOutput( output,
                                          restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose,
                                          outputTemplate: @"{Timestamp:HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId,-2} >> {Message:lj}{NewLine}{Exception}",
                                          formatProvider: null
                     )
                     .Enrich.WithThreadId()
                     .CreateLogger()
                     .ForContext<TLogger>();
        return createLogger<TLogger>();
    }

    protected TestBase(
        ITestOutputHelper?                    output,
        Microsoft.Extensions.Logging.ILogger? logger   = null,
        LogEventLevel                         logLevel = LogEventLevel.Verbose
    ) {
        _output    = output;
        _logger    = logger ?? ( output is not null ? configureLogging<T>( output, logLevel ) : throw new ArgumentNullException( nameof(output) ) );
        _writeLine = output is not null ? output.WriteLine : System.Console.Out.WriteLine;
    }
}