using System;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace mkmrk.Channels;

/// <summary>
/// A FIFO Queue that can have 1 publisher / writer, and 0 to many subscribers / readers.
/// With a return message channel with message type of <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// If there are no readers currently, all write activity will simply return as if it was successful.
/// </remarks>
/// <typeparam name="TData">Type of the messages which <see cref="BroadcastChannelWriter{TData,TResponse}"/> writes and <see cref="BroadcastChannelReader{TData,TResponse}"/> reads.</typeparam>
/// <typeparam name="TResponse">Type of the responses which <see cref="BroadcastChannelReader{TData,TResponse}"/> writes and <see cref="BroadcastChannelWriter{TData,TResponse}"/> reads.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelwriter-1">ChannelWriter&lt;T&gt;</seealso>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1">ChannelReader&lt;T&gt;</seealso>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1">Channel&lt;T&gt;</seealso>
public class BroadcastChannel<TData, TResponse> : IBroadcastChannelAddReaderProvider<TData>, IDisposable where TResponse : IBroadcastChannelResponse {
    private          BroadcastChannelWriter<TData, TResponse>? _writer;
    private readonly ILoggerFactory?                           _loggerFactory;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}"/>
    public BroadcastChannel( ILoggerFactory? loggerFactory = null) => this._loggerFactory = loggerFactory;

    /// <summary>
    /// Get the single <see cref="BroadcastChannelWriter{TData,TResponse}"/>
    /// </summary>
    public BroadcastChannelWriter<TData, TResponse> Writer
        => _writer ??= new BroadcastChannelWriter<TData, TResponse>( _loggerFactory );

    /// <summary>
    /// Create a new <see cref="BroadcastChannelReader{TData,TResponse}"/> and return it.
    /// </summary>
    public BroadcastChannelReader<TData, TResponse> GetReader( )
        => Writer.GetReader();
    
    /// <inheritdoc />
    RemoveWriterByHashCode IBroadcastChannelAddReaderProvider<TData>.AddReader( ChannelWriter<TData> reader ) => this.Writer.AddReader( reader );

    private bool _isDisposed;

    /// <inheritdoc />
    public void Dispose( ) {
        Dispose( true );
        GC.SuppressFinalize( this );
    }

    // ReSharper disable once InconsistentNaming
    /// <inheritdoc cref="IDisposable.Dispose"/>
    protected virtual void Dispose( bool disposing ) {
        if ( this._isDisposed ) {
            return;
        }

        if ( disposing ) {
            this._writer?.Dispose();
        }

        this._isDisposed = true;
    }

    /// <inheritdoc />
    public override string ToString( ) => $"{this.GetType().GenericTypeShortDescriptor(useShortGenericName: false)} [Hash: {this.GetHashCode()}] [Writer: {this._writer}]";
}

/// <summary>
/// <see cref="BroadcastChannel{TData}"/> with a default Response type of <see cref="IBroadcastChannelResponse"/> potentially containing an <see cref="System.Exception"/>.
/// </summary>
/// <typeparam name="TData">Type of the messages which <see cref="BroadcastChannelWriter{TData,TResponse}"/> writes and <see cref="BroadcastChannelReader{TData,TResponse}"/> reads.</typeparam>
/// <inheritdoc cref="BroadcastChannel{TData,TResponse}" path="/remarks" />
public class BroadcastChannel<TData> : BroadcastChannel<TData, IBroadcastChannelResponse> {
    /// <inheritdoc cref="M:mkmrk.Common.BroadcastChannel`2.#ctor(Microsoft.Extensions.Logging.ILoggerFactory)"/>
    public BroadcastChannel( ILoggerFactory? loggerFactory = null ) : base( loggerFactory ) { }
}