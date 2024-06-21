#if SECURITY_DEP

using System;
using System.Collections.Generic;
using System.Text;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using size_t = System.IntPtr;

namespace Mono.Unity
{
	// Follows mostly X509ChainImplBtls
	unsafe class X509ChainImplUnityTls : X509ChainImpl
	{
		private X509ChainElementCollection elements;
		private UnityTls.unitytls_x509list* ownedList;
		private UnityTls.unitytls_x509list_ref nativeCertificateChain;
		private X509ChainPolicy policy = new X509ChainPolicy ();
		private List<X509ChainStatus> chainStatusList;
		private bool reverseOrder;

		internal X509ChainImplUnityTls (UnityTls.unitytls_x509list_ref nativeCertificateChain, bool reverseOrder = false)
		{
			this.elements = null;
			this.ownedList = null;
			this.nativeCertificateChain = nativeCertificateChain;
			this.reverseOrder = reverseOrder;
		}

		internal X509ChainImplUnityTls (UnityTls.unitytls_x509list* ownedList, UnityTls.unitytls_errorstate* errorState, bool reverseOrder = false)
		{
			this.elements = null;
			this.ownedList = ownedList;
			this.nativeCertificateChain = UnityTls.NativeInterface.unitytls_x509list_get_ref(ownedList, errorState);
			this.reverseOrder = reverseOrder;
		}

		public override bool IsValid {
			get { return nativeCertificateChain.handle != UnityTls.NativeInterface.UNITYTLS_INVALID_HANDLE; }
		}

		public override IntPtr Handle {
			get { return new IntPtr((long)nativeCertificateChain.handle); }
		}

		internal UnityTls.unitytls_x509list_ref NativeCertificateChain => nativeCertificateChain;

		public override X509ChainElementCollection ChainElements {
			get {
				ThrowIfContextInvalid ();
				if (elements != null)
					return elements;

				unsafe
				{
					elements = new X509ChainElementCollection ();
					UnityTls.unitytls_errorstate errorState = UnityTls.NativeInterface.unitytls_errorstate_create ();
					var cert = UnityTls.NativeInterface.unitytls_x509list_get_x509 (nativeCertificateChain, (size_t)0, &errorState);
					for (int i = 1; cert.handle != UnityTls.NativeInterface.UNITYTLS_INVALID_HANDLE; ++i) {
						size_t certBufferSize = UnityTls.NativeInterface.unitytls_x509_export_der (cert, null, (size_t)0, &errorState);
						var certBuffer = new byte[(int)certBufferSize];	// Need to reallocate every time since X509Certificate constructor takes no length but only a byte array.
						fixed(byte* certBufferPtr = certBuffer) {
							UnityTls.NativeInterface.unitytls_x509_export_der (cert, certBufferPtr, certBufferSize, &errorState);
						}
						elements.Add (new X509Certificate2 (certBuffer));

						cert = UnityTls.NativeInterface.unitytls_x509list_get_x509 (nativeCertificateChain, (size_t)i, &errorState);
					}
				}

				if (reverseOrder) {
					var reversed = new X509ChainElementCollection ();
					for (int i=elements.Count - 1; i>=0; --i)
						reversed.Add(elements[i].Certificate);
					elements = reversed;
				}

				return elements;
			}
		}

		public override void AddStatus (X509ChainStatusFlags error) 
		{
			if (chainStatusList == null)
				chainStatusList = new List<X509ChainStatus>();
			chainStatusList.Add (new X509ChainStatus(error));
		}

		public override X509ChainPolicy ChainPolicy {
			get { return policy; }
			set { policy = value; }
		}

		public override X509ChainStatus[] ChainStatus => chainStatusList?.ToArray() ?? new X509ChainStatus[0];


		public override bool Build (X509Certificate2 certificate)
		{
			return false;
		}

		public override void Reset ()
		{
			if (elements != null) {
				nativeCertificateChain.handle = UnityTls.NativeInterface.UNITYTLS_INVALID_HANDLE;
				elements.Clear ();
				elements = null;
			}
			if (ownedList != null) {
				UnityTls.NativeInterface.unitytls_x509list_free (ownedList);
				ownedList = null;
			}
		}

		protected override void Dispose (bool disposing)
		{
			Reset();
			base.Dispose (disposing);
		}
	}
}

#endif
