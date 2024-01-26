﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UE4SSDotNetFramework.Framework 
{
	internal static class ArrayPool {
		[ThreadStatic]
		private static byte[] stringBuffer;

		public static byte[] GetStringBuffer() {
			if (stringBuffer == null)
				stringBuffer = GC.AllocateUninitializedArray<byte>(8192, pinned: true);

			Array.Clear(stringBuffer, 0, stringBuffer.Length);

			return stringBuffer;
		}
	}

	internal static class Collector {
		[ThreadStatic]
		private static List<object> references;

		public static IntPtr GetFunctionPointer(Delegate reference) {
			if (references == null)
				references = new();

			references.Add(reference);

			return Marshal.GetFunctionPointerForDelegate(reference);
		}
	}

	internal static class Extensions {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static T GetOrAdd<S, T>(this IDictionary<S, T> dictionary, S key, Func<T> valueCreator) => dictionary.TryGetValue(key, out var value) ? value : dictionary[key] = valueCreator();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte[] StringToBytes(this string value) {
			if (value != null)
				return Encoding.UTF8.GetBytes(value);

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string BytesToString(this byte[] buffer) {
			int end;

			for (end = 0; end < buffer.Length && buffer[end] != 0; end++);

			unsafe {
				fixed (byte* pinnedBuffer = buffer) {
					return new((sbyte*)pinnedBuffer, 0, end);
				}
			}
		}
	}
	
	// Public

	public partial struct UnArray
	{
		public Type GetDataType()
		{
			switch (Type)
			{
				case PropertyType.ObjectProperty:
					return typeof(ObjectReference);
				case PropertyType.Int8Property:
					return typeof(sbyte);
				case PropertyType.Int16Property:
					return typeof(short);
				case PropertyType.IntProperty:
					return typeof(int);
				case PropertyType.Int64Property:
					return typeof(long);
				case PropertyType.ByteProperty:
					return typeof(byte);
				case PropertyType.UInt16Property:
					return typeof(ushort);
				case PropertyType.UInt32Property:
					return typeof(uint);
				case PropertyType.UInt64Property:
					return typeof(ulong);
				case PropertyType.StructProperty:
					return typeof(StructReference);
				case PropertyType.ArrayProperty:
					return typeof(UnArray);
				case PropertyType.FloatProperty:
					return typeof(float);
				case PropertyType.DoubleProperty:
					return typeof(double);
				case PropertyType.BoolProperty:
					return typeof(bool);
				case PropertyType.EnumProperty:
					return typeof(Enum);
				case PropertyType.WeakObjectProperty:
					return typeof(WeakObjectPtr);
				case PropertyType.NameProperty:
				case PropertyType.StrProperty:
				case PropertyType.TextProperty:
					return typeof(string);
			}

			return typeof(object);
		}
		
		public T[] DataToArray<T>()
		{
			var size = Marshal.SizeOf(typeof(T));
			var managedArray = new T[Length];

			for (int i = 0; i < (int)Length; i++)
			{
				IntPtr ins = new IntPtr(Data.ToInt64() + i * size);
				managedArray[i] = Marshal.PtrToStructure<T>(ins);
			}

			return managedArray;
		}

		public void ArrayToData<T>(T[] Array)
		{
			long longPtr = Data.ToInt64(); // Must work both on x86 and x64
			for (int I = 0; I < Array.Length; I++)
			{
				IntPtr rectPtr = new IntPtr(longPtr);
				Marshal.StructureToPtr(Array[I], rectPtr, false); // You do not need to erase struct in this case
				longPtr += Marshal.SizeOf(typeof(T));
			}
		}
	}
	
	/// <summary>
	/// Defines the log level for an output log message
	/// </summary>
	public enum LogLevel
	{
		Default,
		Normal,
		Verbose,
		Warning,
		Error,
	}

	/// <summary>
	/// Functionality for debugging
	/// </summary>
	public static unsafe partial class Debug
	{
		[ThreadStatic] private static StringBuilder stringBuffer = new(8192);

		/// <summary>
		/// Logs a message in accordance to the specified level, omitted in builds with the <a href="https://docs.unrealengine.com/en-US/Programming/Development/BuildConfigurations/index.html#buildconfigurationdescriptions">Shipping</a> configuration
		/// </summary>
		public static void Log(LogLevel level, string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(level, message.StringToBytes());
		}
	}

	/// <summary>
	/// A representation of the engine's object reference
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public unsafe class ObjectReference : IEquatable<ObjectReference> {
		private IntPtr pointer;
		
		internal ObjectReference(IntPtr pointer) => Pointer = pointer;

		internal IntPtr Pointer {
			get {
				if (!IsCreated)
					throw new InvalidOperationException();

				return pointer;
			}

			set {
				if (value == IntPtr.Zero)
					throw new InvalidOperationException();

				pointer = value;
			}
		}

		/// <summary>
		/// Tests for equality between two objects
		/// </summary>
		public static bool operator ==(ObjectReference left, ObjectReference right) => left.Equals(right);

		/// <summary>
		/// Tests for inequality between two objects
		/// </summary>
		public static bool operator !=(ObjectReference left, ObjectReference right) => !left.Equals(right);

		/// <summary>
		/// Returns <c>true</c> if the object is created
		/// </summary>
		public bool IsCreated => pointer != IntPtr.Zero && Object.IsValid(pointer);

		/// <summary>
		/// Returns the name of the object
		/// </summary>
		public string Name {
			get {
				byte[] stringBuffer = ArrayPool.GetStringBuffer();

				Object.GetName(Pointer, stringBuffer);

				return stringBuffer.BytesToString();
			}
		}

		/// <summary>
		/// Finds an object by name
		/// </summary>
		/// <returns>An object or <c>null</c> on failure</returns>
		public static ObjectReference? Find(string name)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			IntPtr pointer = Object.Find(name.StringToBytes());

			if (pointer != IntPtr.Zero)
				return new(pointer);

			return null;
		}
		
		/// <summary>
		/// Invokes a command, function, or an event with optional arguments
		/// </summary>
		public bool Invoke(ObjectReference function, IntPtr @params) => Object.Invoke(Pointer, function.Pointer, @params);

		/// <summary>
		/// Retrieves the value of the object property
		/// </summary>
		public ObjectReference? GetObjectReference(string name) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			IntPtr valuePtr = 0;
			
			if (Object.GetObjectReference(Pointer, name.StringToBytes(), ref valuePtr))
			{
				return new ObjectReference(valuePtr);
			}

			return null;
		}

		/// <summary>
		/// Retrieves the value of the object property
		/// </summary>
		public ObjectReference? GetFunction(string name) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			IntPtr valuePtr = 0;
			
			if (Object.GetFunction(Pointer, name.StringToBytes(), ref valuePtr))
			{
				return new ObjectReference(valuePtr);
			}

			return null;
		}

		/// <summary>
		/// Retrieves the value of the bool property
		/// </summary>
		public bool GetBool(string name, ref bool value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetBool(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the byte property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetByte(string name, ref byte value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetByte(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the short property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetShort(string name, ref short value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetShort(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the integer property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetInt(string name, ref int value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetInt(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the long property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetLong(string name, ref long value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetLong(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the unsigned short property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetUShort(string name, ref ushort value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetUShort(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the unsigned integer property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetUInt(string name, ref uint value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetUInt(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the unsigned long property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetULong(string name, ref ulong value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetULong(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the struct property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public StructReference? GetStruct(string name) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			IntPtr valuePtr = 0;
			
			if (Object.GetStruct(Pointer, name.StringToBytes(), ref valuePtr))
			{
				return new StructReference(valuePtr);
			}

			return null;
		}

		/// <summary>
		/// Retrieves the value of the array property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetArray(string name, ref UnArray value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			IntPtr valuePtr = 0;
			
			return Object.GetArray(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the float property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetFloat(string name, ref float value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetFloat(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the double property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetDouble(string name, ref double value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetDouble(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the enum property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetEnum<T>(string name, ref T value) where T : Enum {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			int data = 0;

			if (Object.GetEnum(Pointer, name.StringToBytes(), ref data)) {
				value = (T)Enum.ToObject(typeof(T), data);

				return true;
			}

			return false;
		}

		public bool GetWeakObject(string name, ref WeakObjectPtr value)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.GetWeakObject(Pointer, name.StringToBytes(), ref value);
		}

		/// <summary>
		/// Retrieves the value of the string property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetString(string name, ref string value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			byte[] stringBuffer = ArrayPool.GetStringBuffer();

			if (Object.GetString(Pointer, name.StringToBytes(), stringBuffer)) {
				value = stringBuffer.BytesToString();

				return true;
			}

			return false;
		}

		/// <summary>
		/// Retrieves the value of the text property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool GetText(string name, ref string value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			byte[] stringBuffer = ArrayPool.GetStringBuffer();

			if (Object.GetText(Pointer, name.StringToBytes(), stringBuffer)) {
				value = stringBuffer.BytesToString();

				return true;
			}

			return false;
		}

		/// <summary>
		/// Sets the value of the bool property
		/// </summary>
		public bool SetBool(string name, bool value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetBool(Pointer, name.StringToBytes(), value);
		}
		
		/// <summary>
		/// Sets the value of the object property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetObjectReference(string name, ObjectReference value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetObjectReference(Pointer, name.StringToBytes(), value.Pointer);
		}

		/// <summary>
		/// Sets the value of the byte property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetByte(string name, byte value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetByte(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the short property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetShort(string name, short value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetShort(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the integer property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetInt(string name, int value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetInt(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the long property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetLong(string name, long value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetLong(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the unsigned short property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetUShort(string name, ushort value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetUShort(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the unsigned integer property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetUInt(string name, uint value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetUInt(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the unsigned long property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetULong(string name, ulong value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetULong(Pointer, name.StringToBytes(), value);
		}
		
		/// <summary>
		/// Sets the value of the object property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetStruct(string name, StructReference value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetStruct(Pointer, name.StringToBytes(), value.Pointer);
		}
		
		/// <summary>
		/// Sets the value of the object property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetArray(string name, UnArray value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetArray(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the float property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetFloat(string name, float value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetFloat(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the double property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetDouble(string name, double value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetDouble(Pointer, name.StringToBytes(), value);
		}

		/// <summary>
		/// Sets the value of the enum property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetEnum<T>(string name, T value) where T : Enum {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return Object.SetEnum(Pointer, name.StringToBytes(), Convert.ToInt32(value));
		}

		/// <summary>
		/// Sets the value of the string property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetString(string name, string value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return Object.SetString(Pointer, name.StringToBytes(), value.StringToBytes());
		}

		/// <summary>
		/// Sets the value of the text property
		/// </summary>
		/// <returns><c>true</c> on success</returns>
		public bool SetText(string name, string value) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return Object.SetText(Pointer, name.StringToBytes(), value.StringToBytes());
		}
		
		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public bool Equals(ObjectReference other) => IsCreated && pointer == other.pointer;

		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public override bool Equals(object value) {
			if (value == null)
				return false;

			if (!ReferenceEquals(value.GetType(), typeof(ObjectReference)))
				return false;

			return Equals((ObjectReference)value);
		}

		/// <summary>
		/// Returns a hash code for the object
		/// </summary>
		public override int GetHashCode() => pointer.GetHashCode();
	}

	public unsafe class StructReference : ObjectReference, IEquatable<StructReference>
	{
		internal StructReference(IntPtr pointer) : base(pointer)
		{
			Pointer = pointer;
		}

		public ClassReference GetSuperStruct()
		{
			IntPtr valuePtr = Struct.GetSuperStruct(Pointer);
			return new ClassReference(valuePtr);
		}

		public void ForEachFunction(delegate* unmanaged[Cdecl]<IntPtr, void> callback)
		{
			Struct.ForEachFunction(Pointer, callback);
		}

		public void ForEachProperty(delegate* unmanaged[Cdecl]<IntPtr, void> callback)
		{
			Struct.ForEachProperty(Pointer, callback);
		}
		
		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public bool Equals(StructReference other) => IsCreated && Pointer == other.Pointer;

		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public override bool Equals(object value) {
			if (value == null)
				return false;

			if (!ReferenceEquals(value.GetType(), typeof(StructReference)))
				return false;

			return Equals((StructReference)value);
		}

		/// <summary>
		/// Returns a hash code for the object
		/// </summary>
		public override int GetHashCode() => Pointer.GetHashCode();
	}

	public class ClassReference : StructReference, IEquatable<ClassReference>
	{
		internal ClassReference(IntPtr pointer) : base(pointer)
		{
			Pointer = pointer;
		}

		public ObjectReference GetCDO()
		{
			IntPtr valuePtr = 0;

			Class.GetCDO(Pointer, ref valuePtr);
			return new ObjectReference(valuePtr);
		}

		public bool IsChildOf(ClassReference parent)
		{
			return Class.IsChildOf(Pointer, parent.Pointer);
		}
		
		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public bool Equals(ClassReference other) => IsCreated && Pointer == other.Pointer;

		/// <summary>
		/// Indicates equality of objects
		/// </summary>
		public override bool Equals(object value) {
			if (value == null)
				return false;

			if (!ReferenceEquals(value.GetType(), typeof(ClassReference)))
				return false;

			return Equals((ClassReference)value);
		}

		/// <summary>
		/// Returns a hash code for the object
		/// </summary>
		public override int GetHashCode() => Pointer.GetHashCode();
	}
	
	public partial struct WeakObjectPtr
	{
		public WeakObjectPtr(int objectIndex, int objectSerialNumber)
		{
			ObjectIndex = objectIndex;
			ObjectSerialNumber = objectSerialNumber;
		}
	}
}