using System;
using System.Reflection;

namespace mkmrk.Channels.Tests;

internal static class TestUtils {
    //
    // internal static TReturn invokePrivateMethod<TInstance, TReturn>( object?[] constructorArgs, string methodName, object?[] methodArgs ) {
    //     Type type     = typeof(TInstance);
    //     var  instance = Activator.CreateInstance( type, constructorArgs );
    //     if ( instance is null ) {
    //         throw new InstanceCreationException<TInstance>();
    //     }
    //     return invokePrivateMethod<TReturn>( instance, methodName, methodArgs );
    // }

    // internal static TReturn invokePrivateMethod<TReturn>( object instance, string methodName, params object?[] methodArgs ) {
    //     MethodInfo method = instance.GetType()
    //                                 .GetMethods( BindingFlags.NonPublic | BindingFlags.Instance )
    //                                 // .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
    //                                 .First( x => x.Name == methodName && x.IsPrivate );
    //     return ( TReturn )method.Invoke( instance, methodArgs )!;
    // }
    //
    // internal static TReturn getPrivateProperty<TReturn>( object instance, string methodName ) {
    //     PropertyInfo property = instance.GetType()
    //                                     .GetProperty( methodName, BindingFlags.NonPublic | BindingFlags.Instance ) ?? throw new Exception();
    //     return ( TReturn )property.GetValue( instance )!;
    // }

    internal static TReturn GetPrivateField<TReturn>( object instance, string fieldName ) {
        FieldInfo field = instance.GetType().GetField( fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default ) ?? throw new Exception( $"Could not get field '{fieldName}'" );
        return ( TReturn )field.GetValue( instance )!;
    }

    internal static TReturn GetPrivateField<TFieldOwner, TReturn>( object instance, string fieldName ) {
        FieldInfo field = typeof(TFieldOwner).GetField( fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default ) ?? throw new Exception( $"Could not get field '{fieldName}'" );
        return ( TReturn )field.GetValue( instance )!;
    }

    // internal static TReturn invokePrivateStaticMethod<TReturn>( Type type, string methodName, params object[] inputParams ) {
    //     MethodInfo method = type
    //                         .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
    //                         .First( x => x.Name == methodName && x.IsPrivate );
    //     return ( TReturn )method.Invoke( null, inputParams )!;
    // }
}