using System.Threading.Channels;

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
    /// There are some cases where a Singleton reader might be desirable, such as for a application long <c>IHostedService</c>,
    /// but this will have to be added manually using the full <see cref="BroadcastChannel{TData, TResponse}"/> form rather than an open generic.
    /// </remarks>
    public static IServiceCollection AddBroadcastChannel<TData, TResponse>( this IServiceCollection services ) where TResponse : IBroadcastChannelResponse {
        services.AddSingleton<BroadcastChannel<TData, TResponse>>();
        services.AddSingleton<BroadcastChannelWriter<TData, TResponse>>( sp => {
            BroadcastChannel<TData, TResponse> broadcastChannel = sp.GetRequiredService<BroadcastChannel<TData, TResponse>>();
            return broadcastChannel.Writer;
        } );
        services.AddTransient<BroadcastChannelReader<TData, TResponse>>( sp => sp.GetRequiredService<BroadcastChannel<TData, TResponse>>().GetReader() );
        return services;
    }

    /// <summary>
    /// Add the required services for <b><i>any</i></b> requested <see cref="BroadcastChannel{TData,TResponse}"/>
    /// </summary>
    /// <inheritdoc cref="AddBroadcastChannel{TData,TResponse}" path="/remarks" />
    public static IServiceCollection AddBroadcastChannels( this IServiceCollection services ) {
        services.AddSingleton( typeof(BroadcastChannel<,>) );
        services.AddSingleton( typeof(BroadcastChannelWriter<,>) );
        services.AddTransient( typeof(BroadcastChannelReader<,>) );
        services.AddSingleton( typeof(BroadcastChannel<>) );
        services.AddSingleton( typeof(BroadcastChannelWriter<>) );
        services.AddTransient( typeof(BroadcastChannelReader<>) );
        return services;
    }

    /// <summary>
    /// Add the required services for <b><i>any</i></b> requested <see cref="BroadcastChannel{TData,TResponse}"/>
    /// replacing any requests for <see cref="Channel{T}"/>, <see cref="ChannelWriter{T}"/>, and <see cref="ChannelReader{T}"/>. 
    /// </summary>
    /// <inheritdoc cref="AddBroadcastChannel{TData,TResponse}" path="/remarks" />
    public static IServiceCollection AddBroadcastChannelsAsChannel( this IServiceCollection services ) {
        services.AddSingleton( typeof(Channel<>), typeof(BroadcastChannel<>) );
        services.AddSingleton( typeof(ChannelWriter<>), typeof(BroadcastChannelWriter<>) );
        services.AddTransient( typeof(ChannelReader<>), typeof(BroadcastChannelReader<>) );
        return services;
    }
}