#if SECURITY_DEP
#if MONO_SECURITY_ALIAS
extern alias MonoSecurity;
#endif

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#if MONO_SECURITY_ALIAS
using MonoSecurity::Mono.Security.Cryptography;
#else
using Mono.Security.Cryptography;
#endif

namespace Mono.Mbedtls
{
	unsafe class CertificateHelper
	{
		static NativeBuffer nativeBuffer;
		static CertificateHelper()
		{
			nativeBuffer = new NativeBuffer();
		}

		enum CertDataFormat
		{
			DATATYPE_STRING = 0,
			DATATYPE_INTPTR = 1,
			DATATYPE_FILE = 2
		}

		[DllImport ("__Internal")]
		static extern bool UnityPalSystemCertificatesEnumSystemCertificates(IntPtr certStore, ref IntPtr iter, out CertDataFormat format, out int size, out IntPtr data);
		[DllImport ("__Internal")]
		static extern IntPtr UnityPalSystemCertificatesOpenSystemRootStore();
		[DllImport ("__Internal")]
		static extern void UnityPalSystemCertificatesCloseSystemRootStore(IntPtr cStore);

		public static void AddSystemCertificates(Mbedtls.mbedtls_x509_crt* chain)
		{
			IntPtr store = UnityPalSystemCertificatesOpenSystemRootStore();
			CertDataFormat format;
			int size;
			IntPtr data;
			IntPtr iter = IntPtr.Zero;
			while (UnityPalSystemCertificatesEnumSystemCertificates(store, ref iter, out format, out size, out data)) {
				if (format == CertDataFormat.DATATYPE_INTPTR)
				{
					AddToChain(chain, data, size);
				}
				else if (format == CertDataFormat.DATATYPE_FILE)
				{
					string path = Marshal.PtrToStringAuto(data);
					if (!String.IsNullOrEmpty(path))
					{
						AddFileToChain(chain, path.Trim());
					}
				}
				else // DATATYPE_STRING
				{
					string cert = Marshal.PtrToStringAuto(data);
					if (!String.IsNullOrEmpty(cert))
					{
						AddToChain(chain, cert.Trim());
					}
				}
			}

			UnityPalSystemCertificatesCloseSystemRootStore(store);
		}

		public static void AddToChain (Mbedtls.mbedtls_x509_crt* chain, X509Certificate crt)
		{
			if (crt == null)
				return;
			lock (nativeBuffer)
			{
				int crtLength = nativeBuffer.ToNative (crt.GetRawCertData ());
				AddToChain (chain, nativeBuffer.DataPtr, crtLength);
			}
		}

		public static void AddToChain (Mbedtls.mbedtls_x509_crt* chain, string crt)
		{
			if (String.IsNullOrEmpty(crt))
				return;
			lock (nativeBuffer)
			{
				int crtLength = nativeBuffer.ToNative (crt);
				AddToChain (chain, nativeBuffer.DataPtr, crtLength);
			}
		}

		public static void AddToChain (Mbedtls.mbedtls_x509_crt* chain, IntPtr crt, int crtLength)
		{
			if (crt == IntPtr.Zero)
				return;
			Mbedtls.unity_mbedtls_x509_crt_parse (chain, crt, (IntPtr)crtLength);
		}

		public static void AddFileToChain (Mbedtls.mbedtls_x509_crt* chain, string path)
		{
			if (String.IsNullOrEmpty(path) || !File.Exists(path))
				return;

			lock (nativeBuffer)
			{
				nativeBuffer.ToNative(path);
				Mbedtls.unity_mbedtls_x509_crt_parse_file(chain, nativeBuffer.DataPtr);
			}
		}

		public static void SetPrivateKey (Mbedtls.mbedtls_pk_context* ctx, X509Certificate crt)
		{
			X509Certificate2 crt2 = crt as X509Certificate2;
			if (crt2 == null)
				return;

			SetPrivateKey (ctx, crt2.PrivateKey);
		}

		public static void SetPrivateKey (Mbedtls.mbedtls_pk_context* ctx, AsymmetricAlgorithm key)
		{
			if (key == null)
				return;

			int keyLength, result;
			lock (nativeBuffer)
			{
				keyLength = nativeBuffer.ToNative (PKCS8.PrivateKeyInfo.Encode (key));
				result = Mbedtls.unity_mbedtls_pk_parse_key (ctx, nativeBuffer.DataPtr, new IntPtr(keyLength), IntPtr.Zero, IntPtr.Zero);
			}
			Debug.CheckAndThrow (result, "Unable to parse private key");
		}

		public static X509Certificate2 AsX509 (Mbedtls.mbedtls_x509_crt* crt)
		{
			if ((IntPtr)crt == IntPtr.Zero)
				return null;

			return AsX509 (new mbedtls_x509_crt_handle(crt).raw);
		}

		public static X509Certificate2 AsX509 (Mbedtls.mbedtls_x509_buf crt)
		{
			Debug.WriteLine (9, "X509 tag {0} len {1}", crt.tag, crt.len);
			Debug.WriteBlock (99, "X509 DER", crt.data, (int)crt.len);

			byte [] certificateData = new byte [(int)crt.len];
			Marshal.Copy (crt.data, certificateData, 0, (int)crt.len);
			return new X509Certificate2 (certificateData);
		}
	}
}
#endif