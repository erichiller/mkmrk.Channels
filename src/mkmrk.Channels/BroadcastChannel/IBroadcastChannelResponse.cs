using JetBrains.Annotations;

namespace mkmrk.Channels;

/// <summary>
/// Response message container
/// </summary>
public interface IBroadcastChannelResponse {
    /// <summary>
    /// Set to a <see cref="System.Exception"/> if one has occurred, else leave <c>null</c>
    /// </summary>
    public System.Exception? Exception { get; init; }
}

/// <inheritdoc />
[UsedImplicitly]
public record BroadcastChannelResponse( System.Exception? Exception) : IBroadcastChannelResponse;