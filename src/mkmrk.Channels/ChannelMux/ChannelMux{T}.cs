using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace mkmrk.Channels;

/// <inheritdoc cref="ChannelMux"/>
/// <remarks>
/// Note that more generic parameters can easily be added by inheriting from this class and additional type params.
/// </remarks>
public class ChannelMux<T1, T2> : ChannelMux, IDisposable {
    private ChannelMuxInput<T1> _input1;
    private ChannelMuxInput<T2> _input2;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux( BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1, BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2 ) : this( channel1, channel2, totalChannels: 2 ) { }

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    /// <remarks>
    /// For construction by subclasses
    /// </remarks>
    protected ChannelMux( BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1, BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2, int totalChannels ) : base( totalChannels: totalChannels ) {
        _input1 = new ChannelMuxInput<T1>( channel1, this );
        _input2 = new ChannelMuxInput<T2>( channel2, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    [ SuppressMessage( "ReSharper", "RedundantNullableFlowAttribute" ) ]
    public bool TryRead( [ MaybeNullWhen( false ) ] out T1 item ) => _input1.TryRead( out item );

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    [ SuppressMessage( "ReSharper", "RedundantNullableFlowAttribute" ) ]
    public bool TryRead( [ MaybeNullWhen( false ) ] out T2 item ) => _input2.TryRead( out item );

    /// <summary>
    /// Replace the <see cref="Channel"/> of the same data type with <paramref name="newChannel"/>.
    ///
    /// <list type="bullet">
    ///     <item>
    ///         <description><see cref="ChannelMux._completeException"/> will be set to <c>null</c> if set by the channel being replaced.</description>
    ///     </item>
    ///     <item>
    ///         <description><see cref="ChannelMux._hasException"/> will be set to <c>false</c> if set by the channel being replaced.</description>
    ///     </item>
    ///     <item>
    ///         <description><see cref="ChannelMux._closedChannels"/> will be decremented by 1 if the channel was closed (completed and empty).</description>
    ///     </item>
    ///     <item>
    ///         <description>The <see cref="ChannelMux.Completion"/> task will be created new if <see cref="Task.IsCompleted"/>.</description>
    ///     </item>
    ///     <item>
    ///         <description>The prior <see cref="Channel"/> will have the reader associated with this <see cref="ChannelMux"/> removed.</description>
    ///     </item>
    /// </list>
    /// </summary>
    /// <param name="newChannel">
    ///     Channel that will replace the channel of the matching type.
    /// </param>
    /// <param name="force">
    ///     If set to <c>true</c>, the channel will be replaced regardless of whether <see cref="ChannelWriter{T}.Complete"/> has been called.
    /// </param>
    /// <returns>
    ///     Any data remaining in the channel being replaced.
    /// </returns>
    /// <exception cref="ChannelNotClosedException">
    ///     If the <see cref="Channel"/> being replaced is not complete.
    ///     This can be overriden by setting <paramref name="force"/> to <c>true</c>.
    /// </exception>
    public IEnumerable<T1> ReplaceChannel( BroadcastChannelWriter<T1, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input1.IsComplete ) {
            this.resetOneChannel( this._input1 );
            var oldMuxInput = Interlocked.Exchange( ref _input1, new ChannelMuxInput<T1>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T1>>();
    }

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`2.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T2> ReplaceChannel( BroadcastChannelWriter<T2, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input2.IsComplete ) {
            this.resetOneChannel( this._input2 );
            var oldMuxInput = Interlocked.Exchange( ref _input2, new ChannelMuxInput<T2>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T2>>();
    }

    /*
     * IDisposable implementation
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    public void Dispose( ) => Dispose( true );

    /// <inheritdoc cref="IDisposable.Dispose" />
    [ SuppressMessage( "ReSharper", "InconsistentNaming" ) ]
    protected virtual void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input1.Dispose();
                _input2.Dispose();
            }
            _isDisposed = true;
        }
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3> : ChannelMux<T1, T2>, IDisposable {
    private ChannelMuxInput<T3> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3
    ) : this( channel1, channel2, channel3, totalChannels: 3 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        int                                                   totalChannels
    ) : base( channel1, channel2, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T3>( channel3, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T3 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`3.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T3> ReplaceChannel( BroadcastChannelWriter<T3, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T3>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T3>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3, T4> : ChannelMux<T1, T2, T3>, IDisposable {
    private ChannelMuxInput<T4> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4
    ) : this( channel1, channel2, channel3, channel4, totalChannels: 4 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        int                                                   totalChannels
    ) : base( channel1, channel2, channel3, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T4>( channel4, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T4 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`4.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T4> ReplaceChannel( BroadcastChannelWriter<T4, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T4>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T4>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3, T4, T5> : ChannelMux<T1, T2, T3, T4>, IDisposable {
    private ChannelMuxInput<T5> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5
    ) : this( channel1, channel2, channel3, channel4, channel5, totalChannels: 5 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        int                                                   totalChannels
    ) : base( channel1, channel2, channel3, channel4, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T5>( channel5, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T5 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`5.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T5> ReplaceChannel( BroadcastChannelWriter<T5, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T5>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T5>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3, T4, T5, T6> : ChannelMux<T1, T2, T3, T4, T5>, IDisposable {
    private ChannelMuxInput<T6> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6
    ) : this( channel1, channel2, channel3, channel4, channel5, channel6, totalChannels: 6 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6,
        int                                                   totalChannels
    ) : base( channel1, channel2, channel3, channel4, channel5, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T6>( channel6, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T6 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`6.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T6> ReplaceChannel( BroadcastChannelWriter<T6, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T6>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T6>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3, T4, T5, T6, T7> : ChannelMux<T1, T2, T3, T4, T5, T6>, IDisposable {
    private ChannelMuxInput<T7> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6,
        BroadcastChannelWriter<T7, IBroadcastChannelResponse> channel7
    ) : this( channel1, channel2, channel3, channel4, channel5, channel6, channel7, totalChannels: 7 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6,
        BroadcastChannelWriter<T7, IBroadcastChannelResponse> channel7,
        int                                                   totalChannels
    ) : base( channel1, channel2, channel3, channel4, channel5, channel6, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T7>( channel7, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T7 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`7.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T7> ReplaceChannel( BroadcastChannelWriter<T7, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T7>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T7>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}

/// <inheritdoc cref="ChannelMux{T1,T2}"/>
public class ChannelMux<T1, T2, T3, T4, T5, T6, T7, T8> : ChannelMux<T1, T2, T3, T4, T5, T6, T7>, IDisposable {
    private ChannelMuxInput<T8> _input;

    /// <inheritdoc cref="ChannelMux{T1,T2}"/>
    public ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6,
        BroadcastChannelWriter<T7, IBroadcastChannelResponse> channel7,
        BroadcastChannelWriter<T8, IBroadcastChannelResponse> channel8
    ) : this( channel1, channel2, channel3, channel4, channel5, channel6, channel7, channel8, totalChannels: 8 ) { }

    /// <inheritdoc cref="M:mkmrk.Channels.ChannelMux`2.#ctor(mkmrk.Channels.BroadcastChannelWriter{`0,mkmrk.Channels.IBroadcastChannelResponse},mkmrk.Channels.BroadcastChannelWriter{`1,mkmrk.Channels.IBroadcastChannelResponse},System.Int32)"/>
    protected ChannelMux(
        BroadcastChannelWriter<T1, IBroadcastChannelResponse> channel1,
        BroadcastChannelWriter<T2, IBroadcastChannelResponse> channel2,
        BroadcastChannelWriter<T3, IBroadcastChannelResponse> channel3,
        BroadcastChannelWriter<T4, IBroadcastChannelResponse> channel4,
        BroadcastChannelWriter<T5, IBroadcastChannelResponse> channel5,
        BroadcastChannelWriter<T6, IBroadcastChannelResponse> channel6,
        BroadcastChannelWriter<T7, IBroadcastChannelResponse> channel7,
        BroadcastChannelWriter<T8, IBroadcastChannelResponse> channel8,
        int                                                   totalChannels
    ) : base( channel1, channel2, channel3, channel4, channel5, channel6, channel7, totalChannels: totalChannels ) {
        _input = new ChannelMuxInput<T8>( channel8, this );
    }

    /// <inheritdoc cref="System.Threading.Channels.ChannelReader{T}.TryRead"/>
    public bool TryRead( [ MaybeNullWhen( false ) ] out T8 item ) => _input.TryRead( out item );

    /// <inheritdoc cref="M:BroadcastChannelMux.ChannelMux`8.ReplaceChannel(BroadcastChannel.BroadcastChannelWriter{`0,BroadcastChannel.IBroadcastChannelResponse},System.Boolean)" />
    public IEnumerable<T8> ReplaceChannel( BroadcastChannelWriter<T8, IBroadcastChannelResponse> newChannel, bool force = false ) {
        if ( force || this._input.IsComplete ) {
            this.resetOneChannel( this._input );
            var oldMuxInput = Interlocked.Exchange( ref _input, new ChannelMuxInput<T8>( newChannel, this ) );
            oldMuxInput.Dispose();
            return oldMuxInput;
        }
        return ChannelNotClosedException.Throw<IEnumerable<T8>>();
    }

    /*
     * Disposal
     */

    private bool _isDisposed = false;

    /// <inheritdoc cref="IDisposable.Dispose" />
    protected override void Dispose( bool disposing ) {
        if ( !_isDisposed ) {
            if ( disposing ) {
                _input.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose( disposing );
    }
}