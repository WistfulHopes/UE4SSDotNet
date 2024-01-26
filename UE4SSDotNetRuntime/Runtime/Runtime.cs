using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using UE4SSDotNetRuntime.Plugins;

/*
 *  Unreal Engine .NET 6 integration
 *  Copyright (c) 2021 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

namespace UE4SSDotNetRuntime.Runtime;

internal enum LogLevel
{
	Default,
	Normal,
	Verbose,
	Warning,
	Error,
}


internal enum ArgumentType
{
	None,
	Single,
	Integer,
	Pointer,
	Callback
}

internal enum CallbackType
{
	ActorOverlapDelegate,
	ActorHitDelegate,
	ActorCursorDelegate,
	ActorKeyDelegate,
	ComponentOverlapDelegate,
	ComponentHitDelegate,
	ComponentCursorDelegate,
	ComponentKeyDelegate,
	CharacterLandedDelegate
}

internal enum CommandType
{
	Initialize = 1,
	LoadAssemblies = 2,
	UnloadAssemblies = 3,
	Find = 4,
	Execute = 5
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal unsafe struct Callback {
	[FieldOffset(0)]
	internal IntPtr* parameters;
	[FieldOffset(8)]
	internal CallbackType type;
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct Argument {
	[FieldOffset(0)]
	internal float single;
	[FieldOffset(0)]
	internal uint integer;
	[FieldOffset(0)]
	internal IntPtr pointer;
	[FieldOffset(0)]
	internal Callback callback;
	[FieldOffset(16)]
	internal ArgumentType type;
}

[StructLayout(LayoutKind.Explicit, Size = 40)]
internal unsafe struct Command {
	// Initialize
	[FieldOffset(0)]
	internal IntPtr* buffer;
	[FieldOffset(8)]
	internal int checksum;
	// Find
	[FieldOffset(0)]
	internal IntPtr method;
	[FieldOffset(8)]
	internal int optional;
	// Execute
	[FieldOffset(0)]
	internal IntPtr function;
	[FieldOffset(8)]
	internal Argument value;
	[FieldOffset(32)]
	internal CommandType type;
}

internal sealed class Plugin {
	internal PluginLoader loader;
	internal Assembly assembly;
	internal Dictionary<int, IntPtr> userFunctions;
}

internal sealed class AssembliesContextManager {
	internal AssemblyLoadContext assembliesContext;

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal WeakReference CreateAssembliesContext() {
		assembliesContext = new("UnrealEngine", true);

		return new(assembliesContext, trackResurrection: true);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal void UnloadAssembliesContext() => assembliesContext?.Unload();
}

internal static unsafe class Core
{
	private static AssembliesContextManager assembliesContextManager;
	private static WeakReference assembliesContextWeakReference;
	private static Plugin plugin;
	private static IntPtr sharedEvents;
	
	private static delegate* unmanaged[Cdecl]<LogLevel, string, void> Log;

	[UnmanagedCallersOnly]
	internal static IntPtr ManagedCommand(Command command)
	{
		if (command.type == CommandType.Execute)
		{
			try
			{
				switch (command.value.type)
				{
					case ArgumentType.None:
					{
						((delegate* unmanaged[Cdecl]<void>)command.function)();
						break;
					}

					case ArgumentType.Single:
					{
						((delegate* unmanaged[Cdecl]<float, void>)command.function)(command.value.single);
						break;
					}

					case ArgumentType.Integer:
					{
						((delegate* unmanaged[Cdecl]<uint, void>)command.function)(command.value.integer);
						break;
					}

					case ArgumentType.Pointer:
					{
						((delegate* unmanaged[Cdecl]<IntPtr, void>)command.function)(command.value.pointer);
						break;
					}

					case ArgumentType.Callback:
					{
						if (command.value.callback.type == CallbackType.ActorOverlapDelegate ||
						    command.value.callback.type == CallbackType.ComponentOverlapDelegate ||
						    command.value.callback.type == CallbackType.ActorKeyDelegate ||
						    command.value.callback.type == CallbackType.ComponentKeyDelegate)
							((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)command.function)(
								command.value.callback.parameters[0], command.value.callback.parameters[1]);
						else if (command.value.callback.type == CallbackType.ActorHitDelegate ||
						         command.value.callback.type == CallbackType.ComponentHitDelegate)
							((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)command.function)(
								command.value.callback.parameters[0], command.value.callback.parameters[1],
								command.value.callback.parameters[2], command.value.callback.parameters[3]);
						else if (command.value.callback.type == CallbackType.ActorCursorDelegate ||
						         command.value.callback.type == CallbackType.ComponentCursorDelegate ||
						         command.value.callback.type == CallbackType.CharacterLandedDelegate)
							((delegate* unmanaged[Cdecl]<IntPtr, void>)command.function)(command.value.callback
								.parameters[0]);
						else
							throw new Exception("Unknown callback type");
						break;
					}

					default:
						throw new Exception("Unknown function type");
				}
			}

			catch (Exception exception)
			{
				try
				{
					Log(LogLevel.Error, exception.ToString());
				}

				catch (FileNotFoundException fileNotFoundException)
				{
					Log(LogLevel.Error, 
						"One of the project dependencies is missed! Please, publish the project instead of building it\r\n" +
						fileNotFoundException);
				}
			}

			return default;
		}

		if (command.type == CommandType.Find)
		{
			IntPtr function = IntPtr.Zero;

			try
			{
				string method = Marshal.PtrToStringAnsi(command.method);

				if (!plugin.userFunctions.TryGetValue(method.GetHashCode(StringComparison.Ordinal), out function) &&
				    command.optional != 1)
					Log(LogLevel.Error, "Managed function was not found \"" + method + "\"");
			}

			catch (Exception exception)
			{
				Log(LogLevel.Error, exception.ToString());
			}

			return function;
		}

		if (command.type == CommandType.Initialize)
		{
			try
			{
				assembliesContextManager = new();
				assembliesContextWeakReference = assembliesContextManager.CreateAssembliesContext();

				int position = 0;
				IntPtr* buffer = command.buffer;

				unchecked
				{
					int head = 0;
					IntPtr* runtimeFunctions = (IntPtr*)buffer[position++];

					Log = (delegate* unmanaged[Cdecl]<LogLevel, string, void>)runtimeFunctions[head++];
				}

				sharedEvents = buffer[position++];
			}

			catch (Exception exception)
			{
				Log(LogLevel.Error, "Runtime initialization failed\r\n" + exception);
			}

			return new(0xF);
		}

		if (command.type == CommandType.LoadAssemblies)
		{
			try
			{
				const string frameworkAssemblyName = "UE4SSDotNetFramework";
				string assemblyPath = Assembly.GetExecutingAssembly().Location;
				string managedFolder =
					assemblyPath.Substring(0, assemblyPath.IndexOf("DotNetRuntime", StringComparison.Ordinal)) + "DotNetPlugins";
				string[] folders = Directory.GetDirectories(managedFolder);

				Array.Resize(ref folders, folders.Length + 1);

				folders[^1] = managedFolder;

				foreach (string folder in folders)
				{
					IEnumerable<string> assemblies =
						Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories);

					foreach (string assembly in assemblies)
					{
						AssemblyName name = null;
						bool loadingFailed = false;

						try
						{
							name = AssemblyName.GetAssemblyName(assembly);
						}

						catch (BadImageFormatException)
						{
							continue;
						}

						if (name?.Name != frameworkAssemblyName)
						{
							plugin = new();
							plugin.loader = PluginLoader.CreateFromAssemblyFile(assembly, config =>
							{
								config.DefaultContext = assembliesContextManager.assembliesContext;
								config.IsUnloadable = true;
								config.LoadInMemory = true;
							});
							plugin.assembly = plugin.loader.LoadAssemblyFromPath(assembly);

							AssemblyName[] referencedAssemblies = plugin.assembly.GetReferencedAssemblies();

							foreach (AssemblyName referencedAssembly in referencedAssemblies)
							{
								if (referencedAssembly.Name == frameworkAssemblyName)
								{
									Assembly framework = plugin.loader.LoadAssembly(referencedAssembly);

									using (assembliesContextManager.assembliesContext.EnterContextualReflection())
									{
										Type sharedClass = framework.GetType(frameworkAssemblyName + ".Framework" + ".Shared");

										plugin.userFunctions = (Dictionary<int, IntPtr>)sharedClass
											.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)
											.Invoke(null,
												new object[] { sharedEvents, plugin.assembly });

										Log(LogLevel.Default, "Framework loaded succesfuly for " + assembly);

										return default;
									}
								}
							}

							UnloadAssemblies();

							if (loadingFailed)
								return default;
						}
					}
				}
			}

			catch (Exception exception)
			{
				Log(LogLevel.Error, "Loading of assemblies failed\r\n" + exception);
				UnloadAssemblies();
			}

			return default;
		}

		if (command.type == CommandType.UnloadAssemblies)
			UnloadAssemblies();

		return default;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void UnloadAssemblies()
	{
		try
		{
			plugin?.loader.Dispose();
			plugin = null;

			assembliesContextManager.UnloadAssembliesContext();
			assembliesContextManager = null;

			if (assembliesContextWeakReference.IsAlive)
			{
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
				GC.WaitForPendingFinalizers();
			}

			assembliesContextManager = new();
			assembliesContextWeakReference = assembliesContextManager.CreateAssembliesContext();
		}

		catch (Exception exception)
		{
			Log(LogLevel.Error, "Unloading of assemblies failed\r\n" + exception);
		}
	}
}