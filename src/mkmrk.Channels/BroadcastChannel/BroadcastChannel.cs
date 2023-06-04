using System;
using System.ComponentModel;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannel{TData,TResponse}" />
public class BroadcastChannel<TData, TResponse> : IBroadcastChannel<TData, TResponse>, IBroadcastChannel<TData> where TResponse : IBroadcastChannelResponse {
    private          IBroadcastChannelWriter<TData, TResponse>? _writer;
    private readonly ILoggerFactory?                            _loggerFactory;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}"/>
    public BroadcastChannel( ILoggerFactory? loggerFactory = null ) => this._loggerFactory = loggerFactory;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}"/>
    /// <remarks>For Dependency Injection and should not be used directly.</remarks>
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannel( IBroadcastChannelWriter<TData, TResponse> broadcastChannelWriter, ILoggerFactory? loggerFactory = null ) {
        this._loggerFactory = loggerFactory;
        this._writer        = broadcastChannelWriter;
    }

    /// <summary>
    /// Get the single <see cref="BroadcastChannelWriter{TData,TResponse}"/>
    /// </summary>
    public IBroadcastChannelWriter<TData, TResponse> Writer
        => _writer ??= new BroadcastChannelWriter<TData, TResponse>( _loggerFactory );

    /// <summary>
    /// Create a new <see cref="BroadcastChannelReader{TData,TResponse}"/> and return it.
    /// </summary>
    public IBroadcastChannelReader<TData, TResponse> GetReader( )
        => Writer.GetReader();

    IBroadcastChannelWriter<TData> IBroadcastChannel<TData>.Writer       => this.Writer;
    IBroadcastChannelReader<TData> IBroadcastChannel<TData>.GetReader( ) => this.GetReader();


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
    public override string ToString( ) => $"{this.GetType().GenericTypeShortDescriptor( useShortGenericName: false )} [Hash: {this.GetHashCode()}] [Writer: {this._writer}]";
}

/// <summary>
/// <see cref="BroadcastChannel{TData}"/> with a default Response type of <see cref="IBroadcastChannelResponse"/> potentially containing an <see cref="System.Exception"/>.
/// </summary>
/// <typeparam name="TData">Type of the messages which <see cref="BroadcastChannelWriter{TData,TResponse}"/> writes and <see cref="BroadcastChannelReader{TData,TResponse}"/> reads.</typeparam>
/// <inheritdoc cref="BroadcastChannel{TData,TResponse}" path="/remarks" />
public class BroadcastChannel<TData> : BroadcastChannel<TData, IBroadcastChannelResponse> {
    /// <inheritdoc cref="M:mkmrk.Common.BroadcastChannel`2.#ctor(Microsoft.Extensions.Logging.ILoggerFactory)"/>
    public BroadcastChannel( ILoggerFactory? loggerFactory = null ) : base( loggerFactory ) { }

    /// <inheritdoc cref="BroadcastChannel{TData}"/>
    /// <remarks>For Dependency Injection and should not be used directly.</remarks>
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannel( IBroadcastChannelWriter<TData> broadcastChannelWriter, ILoggerFactory? loggerFactory = null ) :
        base( broadcastChannelWriter as IBroadcastChannelWriter<TData, IBroadcastChannelResponse>
              ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelWriter<TData>, IBroadcastChannelWriter<TData, IBroadcastChannelResponse>>( broadcastChannelWriter ), loggerFactory ) { }
}