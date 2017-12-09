#if SECURITY_DEP
#if MONO_SECURITY_ALIAS
extern alias MonoSecurity;
#endif

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

#if MONO_SECURITY_ALIAS
using MonoSecurity::Mono.Security.Interface;
#else
using Mono.Security.Interface;
#endif

using MNS = Mono.Net.Security;

namespace Mono.Mbedtls
{
	unsafe class MbedtlsProvider : MonoTlsProvider
	{
		static readonly Guid id = new Guid ("d7ad9d8d-5df9-4455-a34a-70fae79471fb");

		public override Guid ID {
			get { return id; }
		}
		public override string Name {
			get { return "mbedtls"; }
		}

		public override bool SupportsSslStream {
			get { return true; }
		}

		public override bool SupportsMonoExtensions {
			get { return true; }
		}

		public override bool SupportsConnectionInfo {
			get { return true; }
		}

		public override SslProtocols SupportedProtocols {
			get { return SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls; }
		}


		public override IMonoSslStream CreateSslStream (
			Stream innerStream, bool leaveInnerStreamOpen,
			MonoTlsSettings settings = null)
		{
			return new MbedtlsStream (innerStream, leaveInnerStreamOpen, settings, this);
		}

		internal override bool ValidateCertificate (
			ICertificateValidator2 validator, string targetHost, bool serverMode,
			X509CertificateCollection certificates, bool wantsChain, ref X509Chain chain,
			ref MonoSslPolicyErrors errors, ref int status11)
		{

			if (wantsChain)
				chain = MNS.SystemCertificateValidator.CreateX509Chain (certificates);

			if (certificates == null || certificates.Count == 0) {
				errors |= MonoSslPolicyErrors.RemoteCertificateNotAvailable;
				return false;
			}

			// fixup targetHost name
			if (!string.IsNullOrEmpty (targetHost)) {
				var pos = targetHost.IndexOf (':');
				if (pos > 0)
					targetHost = targetHost.Substring (0, pos);
			}

			// convert (back) to native
			Mbedtls.mbedtls_x509_crt* crt_ca, trust_ca;
			crt_ca = Mbedtls.unity_mbedtls_x509_crt_init ();
			trust_ca = Mbedtls.unity_mbedtls_x509_crt_init ();

			foreach (X509Certificate certificate in certificates)
				CertificateHelper.AddToChain(crt_ca, certificate);

			CertificateHelper.AddSystemCertificates(trust_ca);

			uint flags = 0;
			int result = 0;
			using (var targetHostPtr = new Mono.SafeStringMarshal (targetHost))
				result = Mbedtls.unity_mbedtls_x509_crt_verify(crt_ca, trust_ca, IntPtr.Zero, targetHostPtr.Value, ref flags);

			Mbedtls.unity_mbedtls_x509_crt_free (crt_ca);
			Mbedtls.unity_mbedtls_x509_crt_free (trust_ca);

			return result == 0 && flags == 0;
		}
	}
}
#endif
