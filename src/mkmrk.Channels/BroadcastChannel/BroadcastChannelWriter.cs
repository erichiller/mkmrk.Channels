using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannelWriter{TData,TResponse}" />
public class BroadcastChannelWriter<TData, TResponse> : ChannelWriter<TData>, IBroadcastChannelWriter<TData, TResponse>, IDisposable where TResponse : IBroadcastChannelResponse {
    private readonly ChannelReader<TResponse>                          _responseReader;
    private readonly Channel<TResponse>                                _responseChannel;
    private          ImmutableArray<ChannelWriter<TData>>              _outputWriters = ImmutableArray<ChannelWriter<TData>>.Empty;
    private readonly object                                            _readersLock   = new object();
    private readonly ILoggerFactory?                                   _loggerFactory;
    private readonly ILogger<BroadcastChannelWriter<TData, TResponse>> _logger;

    /// <inheritdoc />
    public int ReaderCount {
        get {
            lock ( this._readersLock ) {
                return this._outputWriters.Length;
            }
        }
    }

    /// <summary>
    /// Necessary resources to be written to by this <see cref="BroadcastChannelWriter{TData,TResponse}"/>
    /// </summary>
    /// <param name="DataChannelReader"><see cref="ChannelReader{T}"/> for <c>TData</c> messages</param>
    /// <param name="WriterHash">Hash code of writer to use for <paramref name="RemoveOutputWriterCallback"/></param>
    /// <param name="RemoveOutputWriterCallback">Deregister / Dispose callback</param>
    /// <param name="ResponseChannelWriter"><see cref="ChannelWriter{T}"/> for <c>TResponse</c> messages</param>
    /// <param name="Logger"><see cref="BroadcastChannelReader{TData,TResponse}"/> specific Logger</param>
    internal record ReaderConfiguration(
        ChannelReader<TData>                              DataChannelReader,
        int                                               WriterHash,
        RemoveWriterByHashCode                            RemoveOutputWriterCallback,
        ChannelWriter<TResponse>                          ResponseChannelWriter,
        ILogger<BroadcastChannelReader<TData, TResponse>> Logger
    );

    /// <summary>
    /// This is only for Dependency Injection and internal creation by <see cref="BroadcastChannel{TData,TResponse}.Writer"/>
    /// and should not be used directly, instead use <see cref="BroadcastChannel{TData,TResponse}.Writer"/>.
    /// </summary>
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    // ReSharper disable once UnusedParameter.Local
    public BroadcastChannelWriter( ILoggerFactory? loggerFactory = null ) {
        this._responseChannel = Channel.CreateUnbounded<TResponse>(
            new UnboundedChannelOptions() {
                SingleReader = true,
                SingleWriter = false
            }
        );
        this._loggerFactory  = loggerFactory;
        this._logger         = loggerFactory?.CreateLogger<BroadcastChannelWriter<TData, TResponse>>() ?? NullLogger<BroadcastChannelWriter<TData, TResponse>>.Instance;
        this._responseReader = this._responseChannel.Reader;
        this._logger.LogTrace( "Constructed: {Writer}", this );
    }

    /* ************************************************** */

    /// <inheritdoc cref="IBroadcastChannelWriter{TData,TResponse}.GetNewReaderConfiguration" />
    internal ReaderConfiguration GetNewReaderConfiguration( ) {
        Channel<TData> dataChannel = Channel.CreateUnbounded<TData>( new UnboundedChannelOptions() {
                                                                         SingleReader = true,
                                                                         SingleWriter = true
                                                                     } );
        lock ( this._readersLock ) {
            this._outputWriters = this._outputWriters.Add( dataChannel.Writer );
        }

        this._logger.LogTrace( $"{nameof(GetNewReaderConfiguration)} {nameof(ReaderConfiguration.WriterHash)} is {{WriterHash}}", dataChannel.Writer.GetHashCode() );
        return new ReaderConfiguration( DataChannelReader: dataChannel.Reader,
                                        WriterHash: dataChannel.Writer.GetHashCode(),
                                        RemoveOutputWriterCallback: this.removeReader,
                                        ResponseChannelWriter: this._responseChannel.Writer,
                                        Logger: this._loggerFactory?.CreateLogger<BroadcastChannelReader<TData, TResponse>>()
                                                ?? NullLogger<BroadcastChannelReader<TData, TResponse>>.Instance );
    }

    /// <inheritdoc />
    ReaderConfiguration IBroadcastChannelWriter<TData, TResponse>.GetNewReaderConfiguration( ) => this.GetNewReaderConfiguration();

    IBroadcastChannelReader<TData, TResponse> IBroadcastChannelWriter<TData, TResponse>.GetReader( ) {
        Channel<TData> dataChannel = Channel.CreateUnbounded<TData>( new UnboundedChannelOptions() {
                                                                         SingleReader = true,
                                                                         SingleWriter = true
                                                                     } );
        BroadcastChannelReader<TData, TResponse> reader = new BroadcastChannelReader<TData, TResponse>(
            dataChannel.Reader,
            dataChannel.Writer.GetHashCode(),
            this._responseChannel.Writer,
            this.removeReader,
            this._loggerFactory?.CreateLogger<BroadcastChannelReader<TData, TResponse>>() ?? NullLogger<BroadcastChannelReader<TData, TResponse>>.Instance );
        this._logger.LogTrace( "Created Reader: {Reader}", reader );
        lock ( this._readersLock ) {
            this._outputWriters = this._outputWriters.Add( dataChannel.Writer );
        }

        this._logger.LogTrace( "Created Reader, Reader count is now {Count}", ReaderCount );

        return reader;
    }

    /// <inheritdoc />
    RemoveWriterByHashCode IBroadcastChannelAddReaderProvider<TData>.AddReader( ChannelWriter<TData> reader ) => this.AddReader( reader );

    internal RemoveWriterByHashCode AddReader( ChannelWriter<TData> reader ) {
        this._logger.LogTrace( "Created Reader: {Reader}", reader.ToString() );
        lock ( this._readersLock ) {
            this._outputWriters = this._outputWriters.Add( reader );
        }

        this._logger.LogTrace( "Created Reader, Reader count is now {Count}", ReaderCount );

        return this.removeReader;
    }


    private void removeReader( in int writerHash ) {
        ChannelWriter<TData>? writerFound = null;
        lock ( this._readersLock ) {
            for ( int i = 0 ; i < _outputWriters.Length ; i++ ) {
                if ( _outputWriters[ i ].GetHashCode() == writerHash ) {
                    writerFound         = _outputWriters[ i ];
                    this._outputWriters = this._outputWriters.Remove( writerFound );
                    writerFound.TryComplete();
                }
            }
        }
        if ( writerFound is null ) {
            ThrowHelper.ThrowKeyNotFoundException( $"ChannelWriter with hash {writerHash} was not found." );
        }
    }


    private bool _isDisposed;

    /// <inheritdoc />
    public void Dispose( ) {
        this.Dispose( true );
        GC.SuppressFinalize( this );
    }

    // ReSharper disable once InconsistentNaming
    /// <inheritdoc cref="IDisposable.Dispose"/>
    protected virtual void Dispose( bool disposing ) {
        this._logger.LogTrace( "Dispose({Disposing})", disposing );
        if ( this._isDisposed ) {
            return;
        }

        if ( disposing ) {
            lock ( this._readersLock ) {
                foreach ( var channelWriter in this._outputWriters ) {
                    channelWriter.TryComplete();
                }
            }

            this._responseChannel.Writer.TryComplete();
        }

        this._isDisposed = true;
    }

    /* **** Response **** */

    /// <summary>
    /// Return <see cref="ChannelReader{T}"/> for <typeparamref name="TResponse"/>.
    /// </summary>
    public ChannelReader<TResponse> Responses => this._responseReader;

    /// <inheritdoc cref="ChannelReader{T}.ReadAllAsync"/>
    public IAsyncEnumerable<TResponse> ReadAllResponsesAsync( CancellationToken ct ) => this._responseReader.ReadAllAsync( ct );

    /// <inheritdoc cref="ChannelReader{T}.ReadAsync"/>
    public ValueTask<TResponse> ReadResponseAsync( CancellationToken ct ) => this._responseReader.ReadAsync( ct );

    /// <inheritdoc cref="ChannelReader{T}.TryPeek"/>
    public bool TryPeekResponse( [ MaybeNullWhen( false ) ] out TResponse response ) => this._responseReader.TryPeek( out response );

    /// <inheritdoc cref="ChannelReader{T}.TryRead"/>
    public bool TryReadResponse( [ MaybeNullWhen( false ) ] out TResponse response ) => this._responseReader.TryRead( out response );

    /// <inheritdoc cref="ChannelReader{T}.WaitToReadAsync"/>
    public ValueTask<bool> WaitToReadResponseAsync( CancellationToken ct = default ) => this._responseReader.WaitToReadAsync( ct );


    /* **** Data **** */

    /// <inheritdoc cref="IBroadcastChannelWriter{TData}.TryComplete"/>
    public override bool TryComplete( Exception? error = null ) {
        bool result = true;
        lock ( this._readersLock ) {
            foreach ( ChannelWriter<TData> channelWriter in this._outputWriters ) {
                result &= channelWriter.TryComplete( error );
            }
        }

        return result;
    }

    /// <inheritdoc cref="IBroadcastChannelWriter{TData}.TryWrite(TData)"/>
    public override bool TryWrite( TData item ) {
        lock ( this._readersLock ) {
            if ( this._outputWriters.Length == 1 ) {
                return this._outputWriters[ 0 ].TryWrite( item );
            }

            if ( this._outputWriters.Length == 0 ) { return true; } // this returns true as if it had written regardless of if there was an actual reader to read it

            bool result = true;
            foreach ( var channelWriter in this._outputWriters ) {
                result &= channelWriter.TryWrite( item );
            }

            return result;
        }
    }


    /// <inheritdoc />
    public bool TryWrite( IEnumerable<TData> items ) {
        lock ( this._readersLock ) {
            TData[] itemsArray = items as TData[] ?? items.ToArray();
            bool    result     = true;
            this._logger.LogTrace( "Writing {Count} items to {ReadersCount} readers", itemsArray.Length, this._outputWriters.Length );
            if ( this._outputWriters.Length == 1 ) {
                foreach ( TData item in itemsArray ) {
                    result &= this._outputWriters[ 0 ].TryWrite( item );
                }

                return result;
            }

            if ( this._outputWriters.Length == 0 ) { return true; } // this returns true as if it had written regardless of if there was an actual reader to read it

            foreach ( var channelWriter in this._outputWriters ) {
                foreach ( TData item in itemsArray ) {
                    result &= channelWriter.TryWrite( item );
                }
            }

            return result;
        }
    }

    /// <inheritdoc cref="IBroadcastChannelWriter{TData}.WaitToWriteAsync"/>
    public override ValueTask<bool> WaitToWriteAsync( CancellationToken cancellationToken = default )
        => new ValueTask<bool>( true );

    /// <inheritdoc cref="IBroadcastChannelWriter{TData}.WriteAsync"/>
    /// <remarks>This runs slower than using <see cref="WaitToWriteAsync"/> and <see cref="TryWrite(TData)"/>.</remarks> 
    public override ValueTask WriteAsync( TData item, CancellationToken cancellationToken = default ) {
        lock ( this._readersLock ) {
            if ( this._outputWriters.Length == 0 ) {
                return ValueTask.CompletedTask;
            }

            if ( this._outputWriters.Length == 1 ) {
                return this._outputWriters[ 0 ].WriteAsync( item, cancellationToken );
            }

            return this._outputWriters.Select( r => r.WriteAsync( item, cancellationToken ) ).ToArray().WhenAll();
        }
    }

    /// <inheritdoc />
    public override string ToString( ) {
        lock ( this._readersLock ) {
            return $"{this.GetType().GenericTypeShortDescriptor( useShortGenericName: false )} [Hash: {this.GetHashCode()}] [Readers: ({ReaderCount}): {this._outputWriters.Select( w => w.GetHashCode() ).ToCommaSeparatedString()}]";
        }
    }
}

/// <inheritdoc />
/// <remarks>
/// <see cref="BroadcastChannelWriter{TData}"/> with a default Response type of <see cref="IBroadcastChannelResponse"/> potentially containing an <see cref="System.Exception"/>.
/// </remarks>
public class BroadcastChannelWriter<TData> : BroadcastChannelWriter<TData, IBroadcastChannelResponse> {
    /// <inheritdoc />
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannelWriter( ILoggerFactory loggerFactory ) :
        base( loggerFactory ) { }
}