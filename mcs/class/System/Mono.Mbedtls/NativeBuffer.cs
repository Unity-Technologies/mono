#if SECURITY_DEP
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Mono.Mbedtls
{
	class NativeBuffer : IDisposable
	{
		public IntPtr DataPtr { get; private set; }
		public int    Size    { get; private set; }

		public NativeBuffer(int size = 0)
		{
			DataPtr = IntPtr.Zero;
			Size = 0;
			EnsureSize(size);
		}

		public void Dispose()
		{
			if (DataPtr != IntPtr.Zero)
				Marshal.FreeHGlobal (DataPtr);
			DataPtr = IntPtr.Zero;
			Size = 0;
		}
		
		public int ToNative(byte [] data, int index, int count)
		{
			EnsureSize (count);
			Marshal.Copy (data, index, DataPtr, count);
			return count;
		}

		public int ToNative (byte [] data, int count)
		{
			return ToNative(data, 0, count);
		}

		public int ToNative (byte [] data)
		{
			return ToNative (data, data.Length);
		}

		internal int ToNative (string str)
		{
			byte[] data = Encoding.UTF8.GetBytes (str);
			EnsureSize (data.Length + 1);
			Marshal.Copy (data, 0, DataPtr, data.Length);
			Marshal.WriteByte (DataPtr, data.Length, 0);
			return data.Length + 1;
		}

		public void ToManaged (byte [] data, int index, int count)
		{
			Marshal.Copy (DataPtr, data, index, count);
		}

		public void EnsureSize (int size)
		{
			if (Size >= size)
				return;

			Debug.WriteLine (99, "NativeBuffer {0}=>{1}", Size, size);

			if (DataPtr != IntPtr.Zero)
				Marshal.FreeHGlobal (DataPtr);

			Size = size;
			DataPtr = Marshal.AllocHGlobal (Size);
			if (DataPtr == IntPtr.Zero)
				throw new OutOfMemoryException ();
		}
	}
}
#endif