﻿using System;
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
	internal List<Dictionary<int, IntPtr>?> userFunctions;
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

public static unsafe class Core
{
	private static AssembliesContextManager assembliesContextManager;
	private static WeakReference assembliesContextWeakReference;
	private static List<Plugin> plugins;
	private static IntPtr sharedEvents;
	private static List<IntPtr> userEvents;
	
	private static delegate* unmanaged[Cdecl]<LogLevel, string, void> Log;

	public static void StartMod()
	{
		foreach (var userEvent in userEvents.Where(userEvent => ((IntPtr*)userEvent)[0] is not 0))
		{
			((delegate* unmanaged[Cdecl]<void>)((IntPtr*)userEvent)[0])();
		}
	}

    public static void StopMod()
    {
	    foreach (var userEvent in userEvents.Where(userEvent => ((IntPtr*)userEvent)[1] is not 0))
	    {
		    ((delegate* unmanaged[Cdecl]<void>)((IntPtr*)userEvent)[1])();
	    }
    }

    public static void ProgramStart()
    {
	    foreach (var userEvent in userEvents.Where(userEvent => ((IntPtr*)userEvent)[2] is not 0))
	    {
		    ((delegate* unmanaged[Cdecl]<void>)((IntPtr*)userEvent)[2])();
	    }
    }

    public static void UnrealInit()
    {
	    foreach (var userEvent in userEvents.Where(userEvent => ((IntPtr*)userEvent)[3] is not 0))
	    {
		    ((delegate* unmanaged[Cdecl]<void>)((IntPtr*)userEvent)[3])();
	    }
    }

    public static void Update()
    {
	    foreach (var userEvent in userEvents.Where(userEvent => ((IntPtr*)userEvent)[4] is not 0))
	    {
		    ((delegate* unmanaged[Cdecl]<void>)((IntPtr*)userEvent)[4])();
	    }
    }
	
	[UnmanagedCallersOnly]
	internal static IntPtr ManagedCommand(Command command)
	{
		switch (command.type)
		{
			case CommandType.Execute:
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
			case CommandType.Initialize:
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

						Log = (delegate* unmanaged[Cdecl]<LogLevel, string, void>)runtimeFunctions[head];
					}

					sharedEvents = buffer[position];
				}

				catch (Exception exception)
				{
					Log(LogLevel.Error, "Runtime initialization failed\r\n" + exception);
				}

				return new(0xF);
			case CommandType.LoadAssemblies:
				try
				{
					userEvents = new List<IntPtr>();
					plugins = new List<Plugin>();
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
								var curPlugin = new Plugin();
								curPlugin.loader = PluginLoader.CreateFromAssemblyFile(assembly, config =>
								{
									config.DefaultContext = assembliesContextManager.assembliesContext;
									config.IsUnloadable = true;
									config.LoadInMemory = true;
								});
								curPlugin.assembly = curPlugin.loader.LoadAssemblyFromPath(assembly);
								curPlugin.userFunctions = new List<Dictionary<int, IntPtr>?>();

								AssemblyName[] referencedAssemblies = curPlugin.assembly.GetReferencedAssemblies();

								foreach (AssemblyName referencedAssembly in referencedAssemblies)
								{
									if (referencedAssembly.Name == frameworkAssemblyName)
									{
										Assembly framework = curPlugin.loader.LoadAssembly(referencedAssembly);

										using (assembliesContextManager.assembliesContext.EnterContextualReflection())
										{
											Type sharedClass = framework.GetType(frameworkAssemblyName + ".Framework" + ".Shared");

											IntPtr events = Marshal.AllocHGlobal(sizeof(IntPtr) * 5);
											Unsafe.InitBlockUnaligned((byte*)events, 0, (uint)(sizeof(IntPtr) * 5));

											curPlugin.userFunctions.Add((Dictionary<int, IntPtr>)sharedClass
												.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)
												.Invoke(null,
													[events, curPlugin.assembly]));
                                        
											userEvents.Add(events);

											sharedClass
												.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)
												.Invoke(null,
													[sharedEvents, Assembly.GetExecutingAssembly()]);

											plugins.Add(curPlugin);

											Log(LogLevel.Default, "Framework loaded succesfuly for " + assembly);
										}
									}
								}
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
			case CommandType.UnloadAssemblies:
				UnloadAssemblies();
				break;
		}

		return default;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void UnloadAssemblies()
	{
		try
		{
			foreach (var plugin in plugins)
			{
				plugin.loader.Dispose();
			}
			
			plugins.Clear();

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