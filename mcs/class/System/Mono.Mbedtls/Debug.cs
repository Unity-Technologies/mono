#if SECURITY_DEP
#if MONO_SECURITY_ALIAS
extern alias MonoSecurity;
#endif

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

#if MONO_SECURITY_ALIAS
using MonoSecurity::Mono.Security.Interface;
#else
using Mono.Security.Interface;
#endif
using Mono.Util;

namespace Mono.Mbedtls
{
	class Debug
	{
		public const int LEVEL_PRINT_DATA_BLOCK              = 9;
		public const int LEVEL_PRINT_FILENAME_AND_LINENUMBER = 99;
		public const int LEVEL_PRINT_EXCEPTIONS              = 1;

		public static int Level { get; private set; }

		static Debug()
		{
			int debugLevel;
			Int32.TryParse (Environment.GetEnvironmentVariable ("MONO_TLS_DEBUG"), out debugLevel);
			Mbedtls.unity_mbedtls_debug_set_threshold (debugLevel);
			Level = debugLevel;
		}

		[MonoPInvokeCallback (typeof (Mbedtls.mbedtls_ssl_dbg_t))]
		public static void Callback (IntPtr p_dbg, int level, IntPtr filePtr, int line, IntPtr messagePtr)
		{
			string file = filePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi (filePtr) : "<unknown>";
			string message = messagePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi (messagePtr) : "";
			if (Debug.Level < Debug.LEVEL_PRINT_FILENAME_AND_LINENUMBER)
				Debug.WriteLine (level, "[{0}]", message.Trim ());
			else
				Debug.WriteLine (level, "{0,5} {1,-12} [{2}]", line, Path.GetFileName (file), message.Trim ());
		}

		public static void WriteLine (int level, string message, params object [] args)
		{
			if (Level < level)
				return;
			Console.Error.WriteLine (message, args);
		}

		public static void WriteBlock (int level, string message, IntPtr data, int len)
		{
			if (Level < level)
				return;
			StringBuilder builder = new StringBuilder (message);
			for (int i = 0; i < len; ++i) {
				if (i % 16 == 0) {
					WriteLine (level, builder.ToString ());
					builder.Clear ();
					builder.AppendFormat ("{0:X4}:  ", i);
				}
				builder.AppendFormat (" {0:X2}", Marshal.ReadByte (data, i));
			}
			WriteLine (level, builder.ToString ());
		}

		public static int CheckAndThrow (int result, string message)
		{
			if (result < 0)
				InternalThrow(result, message);
			return result;
		}

		public static int CheckAndThrow (int result, string message, params object [] args)
		{
			if (result < 0)
				InternalThrow(result, String.Format(message, args));
			return result;
		}

		public static void Throw (AlertDescription alert, string message, params object [] args)
		{
			Throw(alert, string.Format(message, args));
		}

		static void Throw (AlertDescription alert, string message)
		{
			WriteLine (LEVEL_PRINT_EXCEPTIONS, "TLSError: {0}", message);
			throw new TlsException (alert, message);
		}

		static void InternalThrow (int result, string message)
		{
			using (NativeBuffer nativeBuffer = new NativeBuffer(1024))
			{
				Mbedtls.unity_mbedtls_strerror (result, nativeBuffer.DataPtr, new IntPtr(nativeBuffer.Size));
				string error = String.Format ("{0} [{1}]", message, Marshal.PtrToStringAuto (nativeBuffer.DataPtr));
				Throw (AlertDescription.InternalError, error);
			}
		}

		static StackFrame FindCallSite()
		{
			foreach (var frame in new StackTrace().GetFrames()) {
				if (!typeof(Debug).Equals(frame.GetMethod().ReflectedType))
					return frame;
			}
			return null;
		}
	}
}
#endif