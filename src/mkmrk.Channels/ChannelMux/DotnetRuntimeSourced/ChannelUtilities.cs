// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
 * Original file: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Threading.Channels/src/System/Threading/Channels/ChannelUtilities.cs
 */
// ReSharper disable InconsistentNaming

using System.Threading.Tasks;

namespace System.Threading.Channels;

/// <summary>Provides internal helper methods for implementing channels.</summary>
internal static class ChannelUtilities {
    /// <summary>Sentinel object used to indicate being done writing.</summary>
    internal static readonly Exception s_doneWritingSentinel = new Exception(nameof(s_doneWritingSentinel));

    /// <summary>Completes the specified TaskCompletionSource.</summary>
    /// <param name="tcs">The source to complete.</param>
    /// <param name="error">
    /// The optional exception with which to complete.
    /// If this is null or the DoneWritingSentinel, the source will be completed successfully.
    /// If this is an OperationCanceledException, it'll be completed with the exception's token.
    /// Otherwise, it'll be completed as faulted with the exception.
    /// </param>
    internal static void Complete(TaskCompletionSource tcs, Exception? error = null) {
        if (error is OperationCanceledException oce) {
            tcs.TrySetCanceled(oce.CancellationToken);
        }
        else if (error != null && error != s_doneWritingSentinel) {
            if (tcs.TrySetException(error)) {
                // Suppress unobserved exceptions from Completion tasks, as the exceptions will generally
                // have been surfaced elsewhere (which may end up making a consumer not consume the completion
                // task), and even if they weren't, they're created by a producer who will have "seen" them (in
                // contrast to them being created by some method call failing as part of user code).
                _ = tcs.Task.Exception;
            }
        }
        else {
            tcs.TrySetResult();
        }
    }

    /// <summary>Creates and returns an exception object to indicate that a channel has been closed.</summary>
    internal static Exception CreateInvalidCompletionException(Exception? inner = null) =>
        inner is OperationCanceledException             ? inner :
        inner != null && inner != s_doneWritingSentinel ? new ChannelClosedException(inner) :
                                                          new ChannelClosedException();
}