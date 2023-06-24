using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using JetBrains.Annotations;

namespace mkmrk.Channels;

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData,TResponse}"/>
// [EditorBrowsable(EditorBrowsableState.Never)]
public class BroadcastChannelReaderSource<TData, TResponse>
    : IBroadcastChannelReaderSource<TData, TResponse>
    where TResponse : IBroadcastChannelResponse {
    
    // KILL
    // /// <summary>
    // /// Reader saved for use when this is being used as an implicit conversion to a reader.
    // /// </summary>
    // [ SuppressMessage( "Design", "CA1051:Do not declare visible instance fields" ) ]
    // protected IBroadcastChannelReader<TData, TResponse>? _reader = null;
    private readonly IBroadcastChannelWriter<TData, TResponse> _broadcastChannel;

    /// <inheritdoc cref="BroadcastChannelReaderSource{TData,TResponse}" />
    // Ideally, only internal code would be able to construct, but a public constructor is needed for dependency injection.
    [ EditorBrowsable( EditorBrowsableState.Never ) ]
    public BroadcastChannelReaderSource( IBroadcastChannelWriter<TData, TResponse> broadcastChannel )
        => _broadcastChannel = broadcastChannel;

    /// <inheritdoc cref="BroadcastChannel{TData,TResponse}.CreateReader" />
    /// <remarks>
    /// The reader returned will always be the same.
    /// Only one reader can be allocated from a single <see cref="BroadcastChannelReaderSource{TData,TResponse}"/>.
    /// </remarks>
    [ PublicAPI ]
    public IBroadcastChannelReader<TData, TResponse> CreateReader( ) => _broadcastChannel.CreateReader();

    // // TODO: FUTURE? GetResponseChannel doesn't exist yet.
    // public ChannelWriter<TResponse> GetResponseChannel( ) => _responseChannel ??= _broadcastChannel.GetResponseChannel();

    /// <inheritdoc />
    [ SuppressMessage( "Design", "CA1033:Interface methods should be callable by child types", Justification = "Only being callable as an interface is intended." ) ]
    RemoveWriterByHashCode IBroadcastChannelAddReaderProvider<TData>.AddReader( ChannelWriter<TData> reader ) => _broadcastChannel.AddReader( reader );

    /// <inheritdoc />
    IBroadcastChannelReader<TData> IBroadcastChannelReaderSource<TData>.CreateReader( ) => this.CreateReader();

    // KILL
    // /// <summary>
    // /// Enables easy use as <see cref="BroadcastChannelReader{TData,TResponse}"/>
    // /// </summary>
    // [ SuppressMessage( "Usage", "CA2225:Operator overloads have named alternates" ) ]
    // public static implicit operator BroadcastChannelReader<TData, TResponse>( BroadcastChannelReaderSource<TData, TResponse> src ) {
    //     ArgumentNullException.ThrowIfNull( src );
    //     return ( src._reader ??= src.CreateReader() ) as BroadcastChannelReader<TData, TResponse> ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelReader<TData, TResponse>, BroadcastChannelReader<TData, TResponse>>( src.CreateReader() );
    // }
}

/// <inheritdoc cref="IBroadcastChannelReaderSource{TData}"/>
[ EditorBrowsable( EditorBrowsableState.Never ) ] // not meant for end user consumption, but the constructor must be accessible for dependency injection.
public class BroadcastChannelReaderSource<TData>
    : BroadcastChannelReaderSource<TData, IBroadcastChannelResponse>, IBroadcastChannelReaderSource<TData> {
    /// <inheritdoc />
    public BroadcastChannelReaderSource( IBroadcastChannelWriter<TData> broadcastChannelWriter ) :
        base( broadcastChannelWriter as IBroadcastChannelWriter<TData, IBroadcastChannelResponse>
              ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelWriter<TData>, IBroadcastChannelWriter<TData, IBroadcastChannelResponse>>( broadcastChannelWriter ) ) { }


    /// <inheritdoc />
    IBroadcastChannelReader<TData> IBroadcastChannelReaderSource<TData>.CreateReader( ) => this.CreateReader();

    // KILL
    // /// <summary>
    // /// Enables easy use as <see cref="BroadcastChannelReader{TData,TResponse}"/>
    // /// </summary>
    // [ SuppressMessage( "Usage", "CA2225:Operator overloads have named alternates" ) ]
    // public static implicit operator BroadcastChannelReader<TData>( BroadcastChannelReaderSource<TData> src ) {
    //     ArgumentNullException.ThrowIfNull( src );
    //     return ( src._reader ??= src.CreateReader() ) as BroadcastChannelReader<TData> ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannelReader<TData>, BroadcastChannelReader<TData>>( src.CreateReader() );
    // }
}