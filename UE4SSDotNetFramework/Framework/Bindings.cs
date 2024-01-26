using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UE4SSDotNetFramework.Framework;

namespace UE4SSDotNetFramework.Framework;

internal static class Shared
{
	internal const int checksum = 0x2F0;
	internal static Dictionary<int, IntPtr> userFunctions = new();
	private const string dynamicTypesAssemblyName = "UnrealEngine.DynamicTypes";

	private static readonly ModuleBuilder moduleBuilder = AssemblyBuilder
		.DefineDynamicAssembly(new(dynamicTypesAssemblyName), AssemblyBuilderAccess.RunAndCollect)
		.DefineDynamicModule(dynamicTypesAssemblyName);

	private static readonly Type[] delegateCtorSignature = { typeof(object), typeof(IntPtr) };
	private static Dictionary<string, Delegate> delegatesCache = new();
	private static Dictionary<string, Type> delegateTypesCache = new();

	private const MethodAttributes ctorAttributes =
		MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;

	private const MethodImplAttributes implAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;

	private const MethodAttributes invokeAttributes = MethodAttributes.Public | MethodAttributes.HideBySig |
	                                                  MethodAttributes.NewSlot | MethodAttributes.Virtual;

	private const TypeAttributes delegateTypeAttributes = TypeAttributes.Class | TypeAttributes.Public |
	                                                      TypeAttributes.Sealed | TypeAttributes.AnsiClass |
	                                                      TypeAttributes.AutoClass;

	internal static unsafe Dictionary<int, IntPtr> Load(IntPtr* events, IntPtr functions, Assembly pluginAssembly)
	{
		int position = 0;
		IntPtr* buffer = (IntPtr*)functions;

		unchecked
		{
			int head = 0;
			IntPtr* debugFunctions = (IntPtr*)buffer[position++];

			Debug.log = (delegate* unmanaged[Cdecl]<LogLevel, byte[], void>)debugFunctions[head++];
		}

		unchecked
		{
			int head = 0;
			IntPtr* objectFunctions = (IntPtr*)buffer[position++];

			Object.isValid = (delegate* unmanaged[Cdecl]<IntPtr, bool>)objectFunctions[head++];
			Object.invoke = (delegate* unmanaged[Cdecl]<IntPtr, byte[], bool>)objectFunctions[head++];
			Object.find = (delegate* unmanaged[Cdecl]<byte[], IntPtr>)objectFunctions[head++];
			Object.getName = (delegate* unmanaged[Cdecl]<IntPtr, byte[], void>)objectFunctions[head++];
			Object.getBool = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref bool, bool>)objectFunctions[head++];
			Object.getByte = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref byte, bool>)objectFunctions[head++];
			Object.getShort = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref short, bool>)objectFunctions[head++];
			Object.getInt = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref int, bool>)objectFunctions[head++];
			Object.getLong = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref long, bool>)objectFunctions[head++];
			Object.getUShort =
				(delegate* unmanaged[Cdecl]<IntPtr, byte[], ref ushort, bool>)objectFunctions[head++];
			Object.getUInt = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref uint, bool>)objectFunctions[head++];
			Object.getULong = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref ulong, bool>)objectFunctions[head++];
			Object.getFloat = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref float, bool>)objectFunctions[head++];
			Object.getDouble =
				(delegate* unmanaged[Cdecl]<IntPtr, byte[], ref double, bool>)objectFunctions[head++];
			Object.getEnum = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ref int, bool>)objectFunctions[head++];
			Object.getString = (delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool>)objectFunctions[head++];
			Object.getText = (delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool>)objectFunctions[head++];
			Object.setBool = (delegate* unmanaged[Cdecl]<IntPtr, byte[], bool, bool>)objectFunctions[head++];
			Object.setByte = (delegate* unmanaged[Cdecl]<IntPtr, byte[], byte, bool>)objectFunctions[head++];
			Object.setShort = (delegate* unmanaged[Cdecl]<IntPtr, byte[], short, bool>)objectFunctions[head++];
			Object.setInt = (delegate* unmanaged[Cdecl]<IntPtr, byte[], int, bool>)objectFunctions[head++];
			Object.setLong = (delegate* unmanaged[Cdecl]<IntPtr, byte[], long, bool>)objectFunctions[head++];
			Object.setUShort = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ushort, bool>)objectFunctions[head++];
			Object.setUInt = (delegate* unmanaged[Cdecl]<IntPtr, byte[], uint, bool>)objectFunctions[head++];
			Object.setULong = (delegate* unmanaged[Cdecl]<IntPtr, byte[], ulong, bool>)objectFunctions[head++];
			Object.setFloat = (delegate* unmanaged[Cdecl]<IntPtr, byte[], float, bool>)objectFunctions[head++];
			Object.setDouble = (delegate* unmanaged[Cdecl]<IntPtr, byte[], double, bool>)objectFunctions[head++];
			Object.setEnum = (delegate* unmanaged[Cdecl]<IntPtr, byte[], int, bool>)objectFunctions[head++];
			Object.setString = (delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool>)objectFunctions[head++];
			Object.setText = (delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool>)objectFunctions[head++];
		}
		unchecked {
			Type[] types = pluginAssembly.GetTypes();

			foreach (Type type in types) {
				MethodInfo[] methods = type.GetMethods();

				if (type.Name == "Main" && type.IsPublic) {
					foreach (MethodInfo method in methods) {
						if (method.IsPublic && method.IsStatic && !method.IsGenericMethod) {
							ParameterInfo[] parameterInfos = method.GetParameters();

							if (parameterInfos.Length <= 1) {
								if (method.Name == "StartMod") {
									if (parameterInfos.Length == 0)
										events[0] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}

								if (method.Name == "StopMod") {
									if (parameterInfos.Length == 0)
										events[1] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "ProgramStart") {
									if (parameterInfos.Length == 0)
										events[2] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "UnrealInit") {
									if (parameterInfos.Length == 0)
										events[3] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "Update") {
									if (parameterInfos.Length == 0)
										events[4] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");
								}
							}
						}
					}
				}

				foreach (MethodInfo method in methods) {
					if (method.IsPublic && method.IsStatic && !method.IsGenericMethod) {
						ParameterInfo[] parameterInfos = method.GetParameters();

						if (parameterInfos.Length <= 1) {
							if (parameterInfos.Length == 1 && parameterInfos[0].ParameterType != typeof(ObjectReference))
								continue;

							string name = type.FullName + "." + method.Name;

							userFunctions.Add(name.GetHashCode(StringComparison.Ordinal), GetFunctionPointer(method));
						}
					}
				}
			}
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();

		return userFunctions;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetTypeName(Type type) => type.FullName.Replace(".", string.Empty, StringComparison.Ordinal);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetMethodName(Type[] parameters, Type returnType) {
		string name = GetTypeName(returnType);

		foreach (Type type in parameters) {
			name += '_' + GetTypeName(type);
		}

		return name;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type GetDelegateType(Type[] parameters, Type returnType) {
		string methodName = GetMethodName(parameters, returnType);

		return delegateTypesCache.GetOrAdd(methodName, () => MakeDelegate(parameters, returnType, methodName));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type MakeDelegate(Type[] types, Type returnType, string name) {
		TypeBuilder builder = moduleBuilder.DefineType(name, delegateTypeAttributes, typeof(MulticastDelegate));

		builder.DefineConstructor(ctorAttributes, CallingConventions.Standard, delegateCtorSignature).SetImplementationFlags(implAttributes);
		builder.DefineMethod("Invoke", invokeAttributes, returnType, types).SetImplementationFlags(implAttributes);

		return builder.CreateTypeInfo();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IntPtr GetFunctionPointer(MethodInfo method) {
		string methodName = $"{ method.DeclaringType.FullName }.{ method.Name }";

		Delegate dynamicDelegate = delegatesCache.GetOrAdd(methodName, () => {
			ParameterInfo[] parameterInfos = method.GetParameters();
			Type[] parameterTypes = new Type[parameterInfos.Length];

			for (int i = 0; i < parameterTypes.Length; i++) {
				parameterTypes[i] = parameterInfos[i].ParameterType;
			}

			return method.CreateDelegate(GetDelegateType(parameterTypes, method.ReturnType));
		});

		return Collector.GetFunctionPointer(dynamicDelegate);
	}
}

static unsafe partial class Debug {
	internal static delegate* unmanaged[Cdecl]<LogLevel, byte[], void> log;
}

internal static unsafe class Object {
	internal static delegate* unmanaged[Cdecl]<IntPtr, bool> isValid;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], bool> invoke;
	internal static delegate* unmanaged[Cdecl]<byte[], IntPtr> find;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], void> getName;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref bool, bool> getBool;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref byte, bool> getByte;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref short, bool> getShort;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref int, bool> getInt;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref long, bool> getLong;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref ushort, bool> getUShort;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref uint, bool> getUInt;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref ulong, bool> getULong;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref float, bool> getFloat;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref double, bool> getDouble;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ref int, bool> getEnum;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool> getString;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool> getText;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], bool, bool> setBool;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], byte, bool> setByte;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], short, bool> setShort;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], int, bool> setInt;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], long, bool> setLong;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ushort, bool> setUShort;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], uint, bool> setUInt;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], ulong, bool> setULong;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], float, bool> setFloat;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], double, bool> setDouble;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], int, bool> setEnum;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool> setString;
	internal static delegate* unmanaged[Cdecl]<IntPtr, byte[], byte[], bool> setText;
}