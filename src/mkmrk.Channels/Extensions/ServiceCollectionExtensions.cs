using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection.Extensions;

using mkmrk.Channels;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/> to add the required services for <see cref="BroadcastChannel{TData,TResponse}"/>
/// </summary>
public static class BroadcastChannelServiceCollectionExtensions {
    /// <summary>
    /// Add the required services for <see cref="BroadcastChannel{TData,TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// <list type="deflist">
    /// <item><term><see cref="BroadcastChannel{TData, TResponse}"/></term><description>will be added as a Singleton</description></item>
    /// <item><term><see cref="BroadcastChannelWriter{TData, TResponse}"/></term><description>will be added as a Singleton</description></item>
    /// <item><term><see cref="BroadcastChannelReader{TData, TResponse}"/></term><description>will be added as Transient</description></item>
    /// </list>
    /// A single writer should be used throughout the life of an application. When readers are disposed of, the object will consume essentially no memory.
    /// There are some cases where a Singleton reader might be desirable, such as for long lived <c>IHostedService</c>,
    /// but this will have to be added manually using the full <see cref="BroadcastChannel{TData, TResponse}"/> form rather than an open generic.
    /// </remarks>
    public static IServiceCollection AddBroadcastChannel<TData, TResponse>( this IServiceCollection services ) where TResponse : IBroadcastChannelResponse {
        services.TryAddSingleton<IBroadcastChannel<TData, TResponse>, BroadcastChannel<TData, TResponse>>();
        services.TryAddSingleton<IBroadcastChannelWriter<TData, TResponse>, BroadcastChannelWriter<TData, TResponse>>();
        services.TryAddTransient<IBroadcastChannelReader<TData, TResponse>, BroadcastChannelReader<TData, TResponse>>();
        /*
         * AddSingleton instead of TryAddSingleton here because there could be more than one IBroadcastChannelWriter<TData> registered with different TResponses,
         * so keep them all and let the user determine which they want.
         */
        services.AddSingleton<IBroadcastChannel<TData>>( sp => {
            IBroadcastChannel<TData> broadcastChannel = sp.GetRequiredService<IBroadcastChannel<TData, TResponse>>() as IBroadcastChannel<TData>
                                                        ?? ThrowHelper.ThrowInvalidCastException<IBroadcastChannel<TData, TResponse>, IBroadcastChannel<TData>>( sp.GetRequiredService<IBroadcastChannel<TData, TResponse>>() );
            return broadcastChannel;
        } );
        services.AddSingleton<IBroadcastChannelWriter<TData>>( sp => {
            IBroadcastChannelWriter<TData, TResponse> broadcastChannelWriter = sp.GetRequiredService<IBroadcastChannelWriter<TData, TResponse>>();
            return broadcastChannelWriter;
        } );
        services.AddTransient<IBroadcastChannelReader<TData>>( sp => {
            IBroadcastChannelReader<TData, TResponse> broadcastChannelReader = sp.GetRequiredService<IBroadcastChannelWriter<TData, TResponse>>().GetReader();
            return broadcastChannelReader;
        } );

        // reader sources
        services.TryAddTransient<BroadcastChannelReaderSource<TData, TResponse>, BroadcastChannelReaderSource<TData, TResponse>>(); // concrete so that the implicit conversion to BroadcastChannelReader can be used.
        services.TryAddTransient<IBroadcastChannelReaderSource<TData, TResponse>, BroadcastChannelReaderSource<TData, TResponse>>();
        services.TryAddTransient<IBroadcastChannelReaderSource<TData>, BroadcastChannelReaderSource<TData, TResponse>>();
        services.TryAddTransient<IBroadcastChannelAddReaderProvider<TData>, BroadcastChannelReaderSource<TData, TResponse>>();


        return services;
    }

    /// <summary>
    /// Add the required services for <b><i>any</i></b> requested <see cref="BroadcastChannel{TData,TResponse}"/>
    /// </summary>
    /// <remarks>
    /// It is important to note that requesting <c>BroadcastChannel&lt;TData&gt;</c> will
    /// not result in the same instance as requesting <c>BroadcastChannel&lt;TData,IBroadcastChannelResponse&gt;</c>.
    /// <br/>
    /// For Example:
    /// <code>
    /// var writerResponseTypeSpecified = _host.Services.GetRequiredService&lt;BroadcastChannelReader&lt;ChannelMessageSubA,IBroadcastChannelResponse&gt;&gt;();
    /// var readerNoResponseTypeSpecified = _host.Services.GetRequiredService&lt;BroadcastChannelReader&lt;ChannelMessageSubA&gt;&gt;();
    /// Console.WriteLine(writerResponseTypeSpecified.ReaderCount); // 0
    /// var writerNoResponseTypeSpecified = _host.Services.GetRequiredService&lt;BroadcastChannelReader&lt;ChannelMessageSubA&gt;&gt;();
    /// Console.WriteLine(writerNoResponseTypeSpecified.ReaderCount); // 1
    /// </code>
    /// </remarks>
    /// <inheritdoc cref="AddBroadcastChannel{TData,TResponse}" path="/remarks" />
    public static IServiceCollection AddBroadcastChannels( this IServiceCollection services ) {
        // Data and response generic type parameters
        services.TryAddSingleton( typeof(IBroadcastChannel<>), typeof(BroadcastChannel<>) );
        services.TryAddSingleton( typeof(IBroadcastChannelWriter<>), typeof(BroadcastChannelWriter<>) );
        services.TryAddTransient( typeof(IBroadcastChannelReader<>), typeof(BroadcastChannelReader<>) );
        services.TryAddTransient( typeof(IBroadcastChannelReaderSource<>), typeof(BroadcastChannelReaderSource<>) );
        services.TryAddTransient( typeof(IBroadcastChannelAddReaderProvider<>), typeof(BroadcastChannelReaderSource<>) );


        // ChannelMux
        services.TryAddTransient( typeof(ChannelMux<,>) );
        services.TryAddTransient( typeof(ChannelMux<,,>) );
        services.TryAddTransient( typeof(ChannelMux<,,,>) );
        services.TryAddTransient( typeof(ChannelMux<,,,,>) );
        services.TryAddTransient( typeof(ChannelMux<,,,,,>) );
        services.TryAddTransient( typeof(ChannelMux<,,,,,,>) );
        services.TryAddTransient( typeof(ChannelMux<,,,,,,,>) );
        return services;
    }

    /// <summary>
    /// Add the required services for <b><i>any</i></b> requested <see cref="BroadcastChannel{TData,TResponse}"/>
    /// replacing any requests for <see cref="Channel{T}"/>, <see cref="ChannelWriter{T}"/>, and <see cref="ChannelReader{T}"/>. 
    /// </summary>
    /// <inheritdoc cref="AddBroadcastChannel{TData,TResponse}" path="/remarks" />
    public static IServiceCollection AddBroadcastChannelsAsChannel( this IServiceCollection services ) {
        services.TryAddSingleton( typeof(Channel<>), typeof(BroadcastChannel<>) );
        services.TryAddSingleton( typeof(ChannelWriter<>), typeof(BroadcastChannelWriter<>) );
        services.TryAddTransient( typeof(ChannelReader<>), typeof(BroadcastChannelReader<>) );
        return services;
    }
}