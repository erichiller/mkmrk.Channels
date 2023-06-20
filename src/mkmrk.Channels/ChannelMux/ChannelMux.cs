/*
 * Some of the below is modified code from
 * https://github.com/dotnet/runtime/blob/main/src/libraries/System.Threading.Channels/src/System/Threading/Channels/SingleConsumerUnboundedChannel.cs
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// ReSharper disable MemberCanBePrivate.Global

namespace mkmrk.Channels;

/// <summary>
/// <see cref="ChannelMux"/> is meant to aggregate multiple <see cref="BroadcastChannel{T}"/> into a single, awaitable object.
/// It is a generic type and each type parameter has a dedicated `TryRead(out T data)` method.
/// <see cref="ChannelMuxInput{T}"/> acts presents as a writer to <see cref="BroadcastChannelWriter{T}"/> and each has a
/// <see cref="SingleProducerSingleConsumerQueue{T}"/>.
/// </summary>
/// <remarks>
/// Note that each <see cref="ChannelMuxInput{T}"/> is a single input, single output where <i>single</i> means both a single instance writing
/// and a single instance reading, and thus can be optimized using <see cref="SingleProducerSingleConsumerQueue{T}"/>.
/// </remarks>
public abstract class ChannelMux {
    /// <summary>A waiting reader (e.g. WaitForReadAsync) if there is one.</summary>
    private AsyncOperation<bool>? _waitingReader;
    private volatile bool                 _isReaderWaiting = false;
    private readonly AsyncOperation<bool> _waiterSingleton;
    private readonly bool                 _runContinuationsAsynchronously;
    private readonly object               _waiterLockObj                    = new ();
    private readonly object               _closedChannelLockObj             = new ();
    private          Exception?           _completeException                = null;
    private          Type?                _completeExceptionChannelDataType = null;
    private volatile bool                 _hasException                     = false;
    private volatile int                  _readableItems                    = 0;
    private volatile int                  _closedChannels                   = 0;
    private readonly int                  _totalChannels;
    private          bool                 _areAllChannelsComplete => _closedChannels >= _totalChannels;

    /// <summary>Task that indicates the channel has completed.</summary>
    private TaskCompletionSource _completion;

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.Completion"/>
    public Task Completion => _completion.Task;

    private TaskCompletionSource createCompletionTask( ) => new TaskCompletionSource( _runContinuationsAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None );

    /// <summary>
    /// Common functionality for <see cref="ChannelMux{T1,T2}.ReplaceChannel(mkmrk.Channels.IBroadcastChannelAddReaderProvider{T1},bool)"/>
    /// </summary>
    /// <param name="muxInput"></param>
    /// <typeparam name="TData"></typeparam>
    protected void resetOneChannel<TData>( ChannelMuxInput<TData> muxInput ) {
        ArgumentNullException.ThrowIfNull( muxInput );
        if ( muxInput.IsClosed ) {
            Interlocked.Decrement( ref _closedChannels );
        }
        if ( _completion.Task.IsCompleted ) {
            _completion = createCompletionTask();
        }
        if ( _completeExceptionChannelDataType == typeof(TData) ) {
            _completeException = null;
            _hasException      = false;
        }
    }

    /// <summary>
    /// Return <c>Exception</c> if the entire <see cref="ChannelMux"/> and all associated ChannelReaders (<see cref="ChannelMuxInput{TData}"/>) should be ended. (else return <c>null</c>).
    /// <list type="bullet">
    ///     <item>
    ///         <description>Exits <see cref="WaitToReadAsync"/> and then returns <see cref="ChannelMux._completeException"/> on any new calls.</description>
    ///     </item>
    ///     <item>
    ///         <description>Ends <see cref="Completion"/> Task (once all items have been read).</description>
    ///     </item>
    ///     <item>
    ///         <description><see cref="ChannelMuxInput{T}.TryWrite"/> for any other channels is closed (immediately).</description>
    ///     </item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Note that <see cref="ChannelMuxInput{TData}.TryRead"/>  will still be allowed until the queue is empty, but because is ended, the queue will not continue to be added to.
    /// </remarks>
    public delegate Exception? ChannelCompleteHandler( Type reportingChannelType, Exception? exception );

    /// <inheritdoc cref="ChannelCompleteHandler" />
    public ChannelCompleteHandler? OnChannelComplete { get; init; }

    /// <inheritdoc cref="ChannelMux" />
    protected ChannelMux( int totalChannels, bool runContinuationsAsynchronously = default ) {
        _runContinuationsAsynchronously = runContinuationsAsynchronously;
        _completion                     = createCompletionTask();
        _waiterSingleton                = new AsyncOperation<bool>( runContinuationsAsynchronously, pooled: true );
        _totalChannels                  = totalChannels;
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.WaitToReadAsync"/>
    public ValueTask<bool> WaitToReadAsync( CancellationToken cancellationToken ) {
        _isReaderWaiting = false;
        // Outside of the lock, check if there are any items waiting to be read.  If there are, we're done.
        if ( cancellationToken.IsCancellationRequested ) {
            return new ValueTask<bool>( Task.FromCanceled<bool>( cancellationToken ) );
        }

        if ( _hasException && _completeException is { } ) {
            // if an exception is present, return a cancelled ValueTask with the exception.
            return new ValueTask<bool>( Task.FromException<bool>( _completeException ) );
        }

        if ( _readableItems > 0 ) {
            return new ValueTask<bool>( true );
        }
        AsyncOperation<bool>? oldWaitingReader, newWaitingReader;
        lock ( _waiterLockObj ) {
            // Again while holding the lock, check to see if there are any items available.
            if ( _readableItems > 0 ) {
                return new ValueTask<bool>( true );
            }
            // There aren't any items; if we're done writing, there never will be more items.
            if ( _areAllChannelsComplete ) {
                // if an exception is present, return a cancelled ValueTask with the exception.
                return _completeException is { } exception ? new ValueTask<bool>( Task.FromException<bool>( exception ) ) : default;
            }
            // Try to use the singleton waiter.  If it's currently being used, then the channel
            // is being used erroneously, and we cancel the outstanding operation.
            oldWaitingReader = _waitingReader;
            if ( !cancellationToken.CanBeCanceled && _waiterSingleton.TryOwnAndReset() ) {
                newWaitingReader = _waiterSingleton;
                if ( newWaitingReader == oldWaitingReader ) {
                    // The previous operation completed, so null out the "old" waiter
                    // so we don't end up canceling the new operation.
                    oldWaitingReader = null;
                }
            } else {
                newWaitingReader = new AsyncOperation<bool>( _runContinuationsAsynchronously, cancellationToken ); // TODO: This is the source of a large number of assignments to the Small Object Heap
            }
            _isReaderWaiting = true;
            _waitingReader   = newWaitingReader;
        }

        if ( _readableItems > 0 ) {
            return new ValueTask<bool>( true );
        }

        oldWaitingReader?.TrySetCanceled( default );
        return newWaitingReader.ValueTaskOfT;
    }

    /*
     * ChannelMuxInput
     */

    /// <summary>
    /// <see cref="ChannelMuxInput{T}"/> acts presents as a writer to <see cref="BroadcastChannelWriter{T}"/> and each has a
    /// <see cref="SingleProducerSingleConsumerQueue{T}"/>.
    /// </summary>
    protected sealed class ChannelMuxInput<TData> : ChannelWriter<TData>, IDisposable, IEnumerable<TData> {
        private readonly ChannelMux                               _parent;
        private readonly RemoveWriterByHashCode                   _removeWriterCallback;
        private readonly SingleProducerSingleConsumerQueue<TData> _queue            = new SingleProducerSingleConsumerQueue<TData>();
        private volatile bool                                     _isComplete       = false;
        private volatile bool                                     _emptyAndComplete = false;
        private volatile bool                                     _isClosed         = false; // set once the parent's _closedChannels has been incremented by this input

        internal ChannelMuxInput( IBroadcastChannelAddReaderProvider<TData> channel, ChannelMux parent ) {
            _removeWriterCallback = channel.AddReader( this );
            _parent               = parent;
        }

        /// <inheritdoc />
        public override bool TryWrite( TData item ) {
            if ( _isComplete || _parent._hasException ) {
                return false;
            }

            _queue.Enqueue( item );
            Interlocked.Increment( ref _parent._readableItems );
            if ( !_parent._isReaderWaiting ) {
                return true;
            }
            AsyncOperation<bool>? waitingReader = null;
            if ( Monitor.TryEnter( _parent._waiterLockObj ) ) {
                try {
                    waitingReader = _parent._waitingReader;
                    if ( waitingReader == null ) {
                        return true;
                    }
                    _parent._isReaderWaiting = false;
                    _parent._waitingReader   = null;
                } finally {
                    // Ensure that the lock is released.
                    Monitor.Exit( _parent._waiterLockObj );
                }
            }
            if ( waitingReader != null ) {
                // Waiting reader is present, set its result so that it ends and the waiting reader continues.
                waitingReader.TrySetResult( item: true );
            }
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This will always return immediately.
        /// </remarks>
        public override ValueTask<bool> WaitToWriteAsync( CancellationToken cancellationToken = new CancellationToken() ) {
            Exception? completeException = _parent._completeException; // URGENT: maybe setting to a local is something I need to do elsewhere?
            return cancellationToken.IsCancellationRequested ? new ValueTask<bool>( Task.FromCanceled<bool>( cancellationToken ) ) :
                !_isComplete                                 ? new ValueTask<bool>( true ) :
                completeException is { }                     ? new ValueTask<bool>( Task.FromException<bool>( completeException ) ) :
                                                               default;
        }


        /// <inheritdoc />
        /// <remarks>
        /// Any waiting readers will only be exited if the queue is empty.
        /// </remarks>
        public override bool TryComplete( Exception? error = null ) {
            AsyncOperation<bool>? waitingReader = null;

            // If we're already marked as complete, there's nothing more to do.
            if ( _isComplete ) {
                return false;
            }

            // allow the user to ignore or modify the Exception
            error = _parent.OnChannelComplete?.Invoke( typeof(TData), error );
            error?.Data.Add( nameof(ChannelMux) + " Type", typeof(TData) );
            if ( error is { } ) {
                _parent._hasException = true;
                Interlocked.Exchange( ref _parent._completeException, error );
                Interlocked.Exchange( ref _parent._completeExceptionChannelDataType, typeof(TData) );
            }

            lock ( _parent._closedChannelLockObj ) {
                // Mark as complete for writing.
                _isComplete = true;
                if ( !_queue.IsEmpty ) {
                    return true;
                }
                _emptyAndComplete = true;
                Interlocked.Increment( ref _parent._closedChannels );
                _isClosed = true;
            }
            // if all channels are closed, or if this complete was reported with an exception, close everything so long as the _queue IsEmpty
            if ( ( _parent._closedChannels >= _parent._totalChannels || error is { } ) ) {
                // If we have no more items remaining, then the channel needs to be marked as completed
                // and readers need to be informed they'll never get another item.  All of that needs
                // to happen outside of the lock to avoid invoking continuations under the lock.
                lock ( _parent._waiterLockObj ) {
                    if ( _parent._waitingReader != null ) {
                        waitingReader            = _parent._waitingReader;
                        _parent._waitingReader   = null;
                        _parent._isReaderWaiting = false;
                    }
                }
                ChannelUtilities.Complete( _parent._completion, error );
                // Complete a waiting reader if there is one (this is only encountered when _queue.IsEmpty is true
                if ( waitingReader != null ) {
                    if ( error != null ) {
                        waitingReader.TrySetException( error );
                    } else {
                        waitingReader.TrySetResult( item: false );
                    }
                }
            }

            // Successfully completed the channel
            return true;
        }

        /// <inheritdoc cref="ChannelReader{T}.TryRead" />
        [ SuppressMessage( "ReSharper", "RedundantNullableFlowAttribute" ) ]
        public bool TryRead( [ MaybeNullWhen( false ) ] out TData? item ) {
            if ( _queue.TryDequeue( out item ) ) {
                Interlocked.Decrement( ref _parent._readableItems );
                if ( _isComplete ) {
                    lock ( _parent._closedChannelLockObj ) {
                        if ( !_queue.IsEmpty || _emptyAndComplete ) {
                            return true;
                        }
                        _emptyAndComplete = true;
                        Interlocked.Increment( ref _parent._closedChannels );
                        _isClosed = true;
                    }
                    if ( _parent._areAllChannelsComplete || _parent._hasException ) {
                        ChannelUtilities.Complete( _parent._completion, _parent._completeException );
                    }
                }
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public override ValueTask WriteAsync( TData item, CancellationToken cancellationToken = default ) =>
            // Writing always succeeds (unless we've already completed writing or cancellation has been requested),
            // so just TryWrite and return a completed task.
            cancellationToken.IsCancellationRequested ? new ValueTask( Task.FromCanceled( cancellationToken ) ) :
            TryWrite( item )                          ? default :
                                                        new ValueTask( Task.FromException( ChannelUtilities.CreateInvalidCompletionException( _parent._completeException ) ) );


        /*
         * IEnumerable implementation
         */

        /// <inheritdoc cref="P:BroadcastChannelMux.SingleProducerSingleConsumerQueue`1.IsEmpty" />
        public bool IsEmpty => this._queue.IsEmpty;

        /// <summary>
        /// Whether the Channel is has had <see cref="ChannelMuxInput{TData}.TryComplete"/> called.
        /// </summary>
        public bool IsComplete => this._isComplete;

        /// <summary>
        /// Whether when the input has incremented its parent's <see cref="ChannelMux._closedChannels"/>.
        /// </summary>
        public bool IsClosed {
            get {
                lock ( _parent._closedChannelLockObj ) {
                    return this._isClosed;
                }
            }
        }

        /*
         * IEnumerable implementation
         */

        /// <inheritdoc />
        public IEnumerator<TData> GetEnumerator( ) => this._queue.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator( ) {
            return GetEnumerator();
        }

        /*
         * IDisposable implementation
         */

        private bool _isDisposed = false;

        /// <inheritdoc />
        public void Dispose( ) {
            if ( !_isDisposed ) {
                _removeWriterCallback.Invoke( this.GetHashCode() );
                _isDisposed = true;
            }
        }
    }
}
