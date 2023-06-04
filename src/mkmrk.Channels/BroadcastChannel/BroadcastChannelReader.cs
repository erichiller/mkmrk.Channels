using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannelReader{TData,TResponse}" />
public class BroadcastChannelReader<TData, TResponse> : ChannelReader<TData>, IBroadcastChannelReader<TData, TResponse> where TResponse : IBroadcastChannelResponse {
    private readonly RemoveWriterByHashCode                            _removeReader;
    private readonly ChannelWriter<TResponse>                          _responseWriter;
    private readonly ChannelReader<TData>                              _dataReader;
    private          bool                                              _isDisposed;
    private readonly int                                               _writerHash;
    private readonly ILogger<BroadcastChannelReader<TData, TResponse>> _logger;

    internal BroadcastChannelReader(
        ChannelReader<TData>                              dataReader,
        int                                               inputDataWriterHashCode,
        ChannelWriter<TResponse>                          responseWriter,
        RemoveWriterByHashCode                            removeReaderFunction,
        ILogger<BroadcastChannelReader<TData, TResponse>> logger
    ) {
        this._writerHash     = inputDataWriterHashCode;
        this._logger         = logger;
        this._removeReader   = removeReaderFunction;
        this._responseWriter = responseWriter;
        this._dataReader     = dataReader;
    }

    /// <summary>
    /// This is only for Dependency Injection purposes and should not be used by the user. Instead use <see cref="IBroadcastChannelWriter{TData,TResponse}.GetReader"/>
    /// </summary>
    /// <param name="broadcastChannelWriter"></param>
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannelReader( IBroadcastChannelWriter<TData, TResponse> broadcastChannelWriter ) {
        ArgumentNullException.ThrowIfNull( broadcastChannelWriter );
        ( this._dataReader, this._writerHash, this._removeReader, this._responseWriter, this._logger ) = broadcastChannelWriter.GetNewReaderConfiguration();
        this._logger.LogTrace( "Registered with Writer: {Writer}", broadcastChannelWriter );
    }

    /* ************************************************** */

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public override bool TryRead( [ MaybeNullWhen( false ) ] out TData item ) => !this._isDisposed ? this._dataReader.TryRead( out item ) : ThrowHelper.ThrowObjectDisposedException( nameof(BroadcastChannelReader<TData, TResponse>), out item );

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryPeek"/>
    public override bool TryPeek( [ MaybeNullWhen( false ) ] out TData item ) => !this._isDisposed ? this._dataReader.TryPeek( out item ) : ThrowHelper.ThrowObjectDisposedException( nameof(BroadcastChannelReader<TData, TResponse>), out item );

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.WaitToReadAsync"/>
    public override ValueTask<bool> WaitToReadAsync( CancellationToken cancellationToken = default ) =>
        !this._isDisposed
            ? this._dataReader.WaitToReadAsync( cancellationToken )
            : ThrowHelper.ThrowObjectDisposedException<ValueTask<bool>>( nameof(BroadcastChannelReader<TData, TResponse>) );

    // warning occurs because there is no `yield` statement, but this is a direct return for ChannelReader<T>.ReadAllAsync
#pragma warning disable CS8424
    // TODO: try: does [AggressiveInlining] help here?
    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.ReadAllAsync"/>
    public override IAsyncEnumerable<TData> ReadAllAsync( [ EnumeratorCancellation ] CancellationToken cancellationToken = default )
        => !this._isDisposed ? this._dataReader.ReadAllAsync( cancellationToken ) : ThrowHelper.ThrowObjectDisposedException<IAsyncEnumerable<TData>>( nameof(BroadcastChannelReader<TData, TResponse>) );

#pragma warning restore CS8424

    /// <inheritdoc cref="ChannelWriter{T}.WriteAsync" />
    public ValueTask WriteResponseAsync( TResponse response, CancellationToken cancellationToken = default ) => this._responseWriter.WriteAsync( response, cancellationToken );


    /// <inheritdoc />
    public override Task Completion => !this._isDisposed ? this._dataReader.Completion : ThrowHelper.ThrowObjectDisposedException<Task>( nameof(BroadcastChannelReader<TData, TResponse>) );

    /// <inheritdoc />
    public override int Count => !this._isDisposed ? this._dataReader.Count : ThrowHelper.ThrowObjectDisposedException<int>( nameof(BroadcastChannelReader<TData, TResponse>) );

    /// <inheritdoc />
    public override bool CanCount => !this._isDisposed ? this._dataReader.CanCount : ThrowHelper.ThrowObjectDisposedException<bool>( nameof(BroadcastChannelReader<TData, TResponse>) );

    /// <inheritdoc />
    public override bool CanPeek => !this._isDisposed ? this._dataReader.CanPeek : ThrowHelper.ThrowObjectDisposedException<bool>( nameof(BroadcastChannelReader<TData, TResponse>) );


    /* *
     * As long as I have Dispose (){ if (_disposed) ... } it doesn't matter if a transient service is disposed by the dependent instance, because the _disposed check will only allow it to be disposed once. MAKE SURE THAT AFTER THE DISPOSAL METHODS CANT BE CALLED
     */

    /// <summary>
    /// Removes reader from BroadcastChannel
    /// </summary>
    /// <remarks>
    /// This method is only needed because if used in Dependency Injection, it might not be disposed when done using,
    /// which means the Channel would continually be written to without being read,
    /// wasting potentially significant amounts of memory.
    /// <p/>
    /// While the documentation says that a dependent/requesting type should never Dispose of an injected type
    /// that was created by the ServiceProvider (and the factory pattern can not be used with Open Generic Types),
    /// it is still ok (and really <b>MUST</b> be done) for the dependent type to Dispose this <see cref="BroadcastChannelReader{TData,TResponse}"/>,
    /// as the Disposed status is tracked and it will not be disposed of twice.
    /// </remarks>
    public void Dispose( ) {
        this.Dispose( true );
        GC.SuppressFinalize( this );
    }

    // ReSharper disable once InconsistentNaming
    /// <inheritdoc cref="IDisposable.Dispose"/>
    protected virtual void Dispose( bool disposing ) {
        this._logger.LogTrace( "Dispose({Disposing}) {Type}", disposing, this.GetType().GenericTypeShortDescriptor( useShortGenericName: false ) );
        if ( this._isDisposed ) {
            return;
        }

        if ( disposing ) {
            this._removeReader( _writerHash );
        }

        this._isDisposed = true;
    }

    /// <inheritdoc />
    // NULL checking is required here, as this could be called from within the constructor before the properties are set.
    // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    public override string ToString( ) => $"{this.GetType().GenericTypeShortDescriptor( useShortGenericName: false )} [Hash: {this.GetHashCode()}] [Writer Hash: {_writerHash}] [Data Reader: {this._dataReader?.GetHashCode()}] [Response Writer: {this._responseWriter?.GetHashCode()}]";
    // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
}

/// <inheritdoc />
/// <remarks>
/// <see cref="BroadcastChannelReader{TData}"/> with a default Response type of <see cref="IBroadcastChannelResponse"/> potentially containing an <see cref="System.Exception"/>.
/// </remarks>
public class BroadcastChannelReader<TData> : BroadcastChannelReader<TData, IBroadcastChannelResponse> {
    /// <inheritdoc />
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannelReader( IBroadcastChannelWriter<TData> broadcastChannelWriter )
        : base( broadcastChannelWriter as IBroadcastChannelWriter<TData, IBroadcastChannelResponse>
                ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelWriter<TData>, IBroadcastChannelWriter<TData, IBroadcastChannelResponse>>( broadcastChannelWriter ) ) { }
}