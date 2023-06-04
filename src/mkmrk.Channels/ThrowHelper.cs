using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace mkmrk.Channels;

/// <summary>
/// Utility class for throwing exceptions in an efficient way.
/// <para />
/// While there are <a href="https://stackoverflow.com/questions/1980044/when-should-i-use-a-throwhelper-method-instead-of-throwing-directly">arguments against this</a>
/// the consensus amongst .NET runtime developers is that it is the correct way to go.
/// The runtime docs say <a href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/ThrowHelper.cs#L33">
/// <i>It is very important we do this for generic classes because we can easily generate the same code multiple times for different instantiation.</i></a>
/// <br/>
/// To facilitate use in methods or locations that must return a value, use one of the Generic helper methods, eg <c>public static T ThrowException{T}</c>,
/// See <a href="https://learn.microsoft.com/en-us/dotnet/communitytoolkit/diagnostics/throwhelper#:~:text=within%20expressions%20that%20require%20a%20return%20type%20of%20a%20specific%20type">.NET Community Toolkit - ThrowHelper</a>
///
/// <para/>There is one large downside, <b>the stack trace will start at the ThrowHelper method, not the method that called it</b>.
/// <para />
/// For background see:
///         <list type="bullet">
///     <item>
///         <description>https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/ThrowHelper.cs</description>
///     </item>
///     <item>
///         <description>https://learn.microsoft.com/en-us/dotnet/communitytoolkit/diagnostics/throwhelper</description>
///     </item>
///     <item>
///         <description>https://dunnhq.com/posts/2022/throw-helper/</description>
///     </item>
/// </list>
/// </summary>
[ StackTraceHidden ]
internal static class ThrowHelper {
    /// <inheritdoc cref="ObjectDisposedException" />
    [ DoesNotReturn ]
    internal static TReturn ThrowObjectDisposedException<TReturn>( string typeNameDisposed ) =>
        throw new ObjectDisposedException( typeNameDisposed );

    /// <inheritdoc cref="ObjectDisposedException" />
    [ DoesNotReturn ]
    internal static bool ThrowObjectDisposedException<TOut>( string typeNameDisposed, out TOut data ) =>
        throw new ObjectDisposedException( typeNameDisposed );

    /// <inheritdoc cref="KeyNotFoundException" />
    [ DoesNotReturn ]
    internal static void ThrowKeyNotFoundException( string message ) =>
        throw new System.Collections.Generic.KeyNotFoundException( message );
    
    /// <summary>
    /// Throw <see cref="InvalidCastException"/>, supplying the destination type for the message
    /// </summary>
    [ DoesNotReturn ]
    public static TCast ThrowInvalidCastException<TInput, TCast>( )
        => throw new InvalidCastException( $"Unable to cast type {typeof(TInput).Name} to type {typeof(TCast).Name}" );
    
    /// <summary>
    /// Throw <see cref="InvalidCastException"/>, supplying the destination type for the message
    /// </summary>
    [ DoesNotReturn ]
    public static TCast ThrowInvalidCastException<TInput, TCast>( TInput? variable )
        => throw new InvalidCastException( $"Unable to cast type {typeof(TInput).Name} with value {variable} to type {typeof(TCast).Name}" );
}