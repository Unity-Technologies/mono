#if SECURITY_DEP
#if MONO_SECURITY_ALIAS
extern alias MonoSecurity;
#endif

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

#if MONO_SECURITY_ALIAS
using MonoSecurity::Mono.Security.Interface;
#else
using Mono.Security.Interface;
#endif

using Mono.Net.Security;
using Mono.Util;

namespace Mono.Mbedtls
{
	unsafe struct mbedtls_ssl_context_handle
	{
		public readonly Mbedtls.mbedtls_ssl_context* Ptr;

		public mbedtls_ssl_context_handle(Mbedtls.mbedtls_ssl_context* ptr)
		{
			Ptr = ptr;
		}

		public static implicit operator Mbedtls.mbedtls_ssl_context*(mbedtls_ssl_context_handle h)
		{
			return h.Ptr;
		}

		public int state
		{
			get
			{
				return Mbedtls.unity_mbedtls_ssl_get_state(Ptr);
			}
		}
		
		public int minor_ver
		{
			get
			{
				return Mbedtls.unity_mbedtls_ssl_get_minor_ver(Ptr);
			}
		}
	}

	unsafe struct mbedtls_x509_crt_handle
	{
		public readonly Mbedtls.mbedtls_x509_crt* Ptr;

		public mbedtls_x509_crt_handle(Mbedtls.mbedtls_x509_crt* ptr)
		{
			Ptr = ptr;
		}

		public static implicit operator Mbedtls.mbedtls_x509_crt*(mbedtls_x509_crt_handle h)
	    {
	        return h.Ptr;
	    }

		public Mbedtls.mbedtls_x509_crt* next
		{ 
			get 
			{
				return Mbedtls.unity_mbedtls_x509_crt_get_next(Ptr);
			}
		}

		public Mbedtls.mbedtls_x509_buf raw
		{ 
			get 
			{
				return Mbedtls.unity_mbedtls_x509_crt_get_raw(Ptr);
			}
		}
	}

	unsafe struct mbedtls_pk_context_handle
	{
		public readonly Mbedtls.mbedtls_pk_context* Ptr;

		public mbedtls_pk_context_handle(Mbedtls.mbedtls_pk_context* ptr)
		{
			Ptr = ptr;
		}

		public static implicit operator Mbedtls.mbedtls_pk_context*(mbedtls_pk_context_handle h)
	    {
	        return h.Ptr;
	    }
	}

	unsafe class MbedtlsContext : MobileTlsContext
	{
		private const int MaxIOBufferSize = 16384;

		readonly static string SSL_SEED_STR = "MonoMbedTls";

		// Native structs
		mbedtls_ssl_context_handle        m_SslContext; // mbedtls_ssl_context
		Mbedtls.mbedtls_entropy_context*  m_EntropyContext; // mbedtls_entropy_context
		Mbedtls.mbedtls_ctr_drbg_context* m_RandomGeneratorContext; // mbedtls_ctr_drbg_context
		Mbedtls.mbedtls_ssl_config*       m_SslConfig; // mbedtls_ssl_config
		mbedtls_x509_crt_handle           m_RootCertificateChain; // mbedtls_x509_crt
		mbedtls_x509_crt_handle           m_OwnCertificateChain; // mbedtls_x509_crt
		mbedtls_pk_context_handle         m_OwnPrivateKeyContext; // mbedtls_pk_context

		// Delegate references to guard against garbage collection
		Mbedtls.mbedtls_ssl_send_t        m_BIOWriteCallback;
		IntPtr                            m_BIOWriteCallbackPtr;
		Mbedtls.mbedtls_ssl_recv_t        m_BIOReadCallback;
		IntPtr                            m_BIOReadCallbackPtr;
		Mbedtls.mbedtls_ssl_dbg_t         m_DebugCallback;
		IntPtr                            m_DebugCallbackPtr;
		Mbedtls.mbedtls_verify_t          m_VerifyCallback;
		IntPtr                            m_VerifyCallbackPtr;

		// States and certificates
		X509Certificate       m_LocalClientCertificate;
		X509Certificate2      m_RemoteCertificate;
		MonoTlsConnectionInfo m_Connectioninfo;
		bool                  m_IsAuthenticated;

		NativeBuffer m_NativeBuffer;

		NativeBuffer m_NativeIOReadBuffer;
		NativeBuffer m_NativeIOWriteBuffer;
		NativeBuffer m_NativeEnabledCiphers;

		byte [] m_ManagedBuffer = new byte[0];

		GCHandle m_handle;

		[MonoTODO ("Move stuff to StartHandshake")]
		internal MbedtlsContext (
		    MobileAuthenticatedStream parent,
		    bool serverMode, string targetHost,
		    SslProtocols enabledProtocols, X509Certificate serverCertificate,
		    X509CertificateCollection clientCertificates, bool askForClientCert)
		    : base (parent, serverMode, targetHost, enabledProtocols,
			    serverCertificate, clientCertificates, askForClientCert)
		{
			m_handle = GCHandle.Alloc(this);

			// Setup native buffers
			m_NativeBuffer        = new NativeBuffer();
			m_NativeIOReadBuffer  = new NativeBuffer();
			m_NativeIOWriteBuffer = new NativeBuffer();
			m_NativeEnabledCiphers      = new NativeBuffer();

			// Initialize native structs
			m_SslContext = new mbedtls_ssl_context_handle (Mbedtls.unity_mbedtls_ssl_init ());
			m_SslConfig = Mbedtls.unity_mbedtls_ssl_config_init ();
			m_RandomGeneratorContext = Mbedtls.unity_mbedtls_ctr_drbg_init ();
			m_EntropyContext = Mbedtls.unity_mbedtls_entropy_init ();
			m_RootCertificateChain = new mbedtls_x509_crt_handle (Mbedtls.unity_mbedtls_x509_crt_init ());
			m_OwnCertificateChain = new mbedtls_x509_crt_handle (Mbedtls.unity_mbedtls_x509_crt_init ());
			m_OwnPrivateKeyContext = new mbedtls_pk_context_handle (Mbedtls.unity_mbedtls_pk_init ());

			Mono.Mbedtls.Debug.CheckAndThrow (Mbedtls.unity_mbedtls_ssl_config_defaults (m_SslConfig,
			    IsServer ? Mbedtls.MBEDTLS_SSL_IS_SERVER : Mbedtls.MBEDTLS_SSL_IS_CLIENT,
			    Mbedtls.MBEDTLS_SSL_TRANSPORT_STREAM,
			    Mbedtls.MBEDTLS_SSL_PRESET_DEFAULT),
				"SSL default configuration failed");

			int seedStringLength = m_NativeBuffer.ToNative (SSL_SEED_STR);
			Mono.Mbedtls.Debug.CheckAndThrow (Mbedtls.unity_mbedtls_ctr_drbg_seed (m_RandomGeneratorContext,
			    m_EntropyContext, m_NativeBuffer.DataPtr, new IntPtr(seedStringLength)),
			    "Unable to create random generator");
			Mbedtls.unity_mbedtls_ssl_conf_rng (m_SslConfig,
			    m_RandomGeneratorContext);

			Mbedtls.unity_mbedtls_ssl_conf_dbg (m_SslConfig, m_DebugCallbackPtr = Marshal.GetFunctionPointerForDelegate (m_DebugCallback = Mono.Mbedtls.Debug.Callback), IntPtr.Zero);
			Mbedtls.unity_mbedtls_ssl_conf_ca_chain (m_SslConfig, m_RootCertificateChain, IntPtr.Zero);
			Mbedtls.unity_mbedtls_ssl_conf_own_cert (m_SslConfig, m_OwnCertificateChain, m_OwnPrivateKeyContext);
			Mbedtls.unity_mbedtls_ssl_set_bio (m_SslContext, (IntPtr)m_handle, m_BIOWriteCallbackPtr = Marshal.GetFunctionPointerForDelegate (m_BIOWriteCallback = BIOWriteCallback), m_BIOReadCallbackPtr = Marshal.GetFunctionPointerForDelegate (m_BIOReadCallback = BIOReadCallback), IntPtr.Zero);
			Mbedtls.unity_mbedtls_ssl_conf_verify (m_SslConfig, m_VerifyCallbackPtr = Marshal.GetFunctionPointerForDelegate (m_VerifyCallback = VerifyCallback), (IntPtr)m_handle);

			Mono.Mbedtls.Debug.CheckAndThrow (Mbedtls.unity_mbedtls_ssl_setup (m_SslContext, m_SslConfig),
			    "SSL context setup failed");

			TlsProtocolCode min, max;
			GetProtocolVersions (out min, out max);
			Mbedtls.unity_mbedtls_ssl_conf_min_version (m_SslConfig, 3, GetProtocol(min));
			Mbedtls.unity_mbedtls_ssl_conf_max_version (m_SslConfig, 3, GetProtocol(max));

			if (Settings != null && Settings.EnabledCiphers != null) {
				m_NativeEnabledCiphers.EnsureSize((Settings.EnabledCiphers.Length + 1) * sizeof(uint));
				for (int i = 0; i < Settings.EnabledCiphers.Length; ++i)
					Marshal.WriteInt32 (m_NativeEnabledCiphers.DataPtr, i*4, (int)Settings.EnabledCiphers[i]);
				Marshal.WriteInt32 (m_NativeEnabledCiphers.DataPtr, Settings.EnabledCiphers.Length*4, 0);
				Mbedtls.unity_mbedtls_ssl_conf_ciphersuites (m_SslConfig, m_NativeBuffer.DataPtr);
			}
		}

		public override bool HasContext {
			get { return true; }
		}

		public override bool IsAuthenticated {
			get { return m_IsAuthenticated; }
		}

		public override MonoTlsConnectionInfo ConnectionInfo {
			get { return m_Connectioninfo; }
		}
		internal override bool IsRemoteCertificateAvailable {
			get { return m_RemoteCertificate != null; }
		}
		internal override X509Certificate LocalClientCertificate {
			get { return m_LocalClientCertificate; }
		}
		public override X509Certificate RemoteCertificate {
			get { return m_RemoteCertificate; }
		}
		public override TlsProtocols NegotiatedProtocol {
			get { return ConnectionInfo.ProtocolVersion; }
		}

		[MonoTODO ("Handle intermediate certificates")]
		void SetPrivateCertificate (X509Certificate cert)
		{
			if (cert == null)
				return;

			CertificateHelper.AddToChain (m_OwnCertificateChain, cert);

			X509Certificate2 privateKeyCert = cert as X509Certificate2;
			if (privateKeyCert == null)
				return;

			CertificateHelper.SetPrivateKey (m_OwnPrivateKeyContext, privateKeyCert.PrivateKey);
		}

		public override void Flush ()
		{
			// NO-OP
		}

		public override int Read (byte [] buffer, int offset, int count, out bool wouldBlock)
		{
			wouldBlock = false;

			int bufferSize = System.Math.Min(MaxIOBufferSize, count);
			m_NativeIOReadBuffer.EnsureSize (bufferSize);
			int result = Mbedtls.unity_mbedtls_ssl_read (m_SslContext, m_NativeIOReadBuffer.DataPtr, bufferSize);
			if (result == Mbedtls.MBEDTLS_ERR_SSL_WANT_READ) {
				wouldBlock = true;
				return 0;
			}
			Mono.Mbedtls.Debug.CheckAndThrow (result, "Read error");
			m_NativeIOReadBuffer.ToManaged(buffer, offset, result);
			return result;
		}

		public override int Write (byte [] buffer, int offset, int count, out bool wouldBlock)
		{
			wouldBlock = false;

			int bufferSize = System.Math.Min(MaxIOBufferSize, count);
			m_NativeIOWriteBuffer.EnsureSize (bufferSize);
			Marshal.Copy (buffer, offset, m_NativeIOWriteBuffer.DataPtr, bufferSize);
			int result = Mbedtls.unity_mbedtls_ssl_write (m_SslContext, m_NativeIOWriteBuffer.DataPtr, new IntPtr(bufferSize));
			if (result == Mbedtls.MBEDTLS_ERR_SSL_WANT_WRITE) {
				wouldBlock = true;
				return 0;
			}
			Mono.Mbedtls.Debug.CheckAndThrow (result, "Write error");
			return result;
		}

		public override void Close ()
		{
			// free native resources
			Mbedtls.unity_mbedtls_ssl_free (m_SslContext);
			Mbedtls.unity_mbedtls_ssl_config_free (m_SslConfig);
			Mbedtls.unity_mbedtls_ctr_drbg_free (m_RandomGeneratorContext);
			Mbedtls.unity_mbedtls_entropy_free (m_EntropyContext);
			Mbedtls.unity_mbedtls_x509_crt_free (m_RootCertificateChain);
			Mbedtls.unity_mbedtls_x509_crt_free (m_OwnCertificateChain);
			Mbedtls.unity_mbedtls_pk_free (m_OwnPrivateKeyContext);
		}

		protected override void Dispose (bool disposing)
		{
			try {
				if (disposing) {

					// free native resources
					Mbedtls.unity_mbedtls_ssl_free (m_SslContext);
					Mbedtls.unity_mbedtls_ssl_config_free (m_SslConfig);
					Mbedtls.unity_mbedtls_ctr_drbg_free (m_RandomGeneratorContext);
					Mbedtls.unity_mbedtls_entropy_free (m_EntropyContext);
					Mbedtls.unity_mbedtls_x509_crt_free (m_RootCertificateChain);
					Mbedtls.unity_mbedtls_x509_crt_free (m_OwnCertificateChain);
					Mbedtls.unity_mbedtls_pk_free (m_OwnPrivateKeyContext);

					// reset callbacks
		            m_BIOWriteCallback = null;
		            m_BIOReadCallback = null;
		            m_DebugCallback = null;

		            // reset states
					m_LocalClientCertificate = null;
					m_RemoteCertificate = null;
					m_Connectioninfo = null;
					m_IsAuthenticated = false;

					m_NativeBuffer.Dispose();
					m_NativeIOWriteBuffer.Dispose();
					m_NativeIOReadBuffer.Dispose();
					m_NativeEnabledCiphers.Dispose();
					m_ManagedBuffer = null;
				}

				m_handle.Free();

			} finally {
				base.Dispose (disposing);
			}
		}

		[MonoPInvokeCallback (typeof (Mbedtls.mbedtls_ssl_send_t))]
		static int VerifyCallback (IntPtr p_vrfy, IntPtr _crt, int depth, ref uint flags)
		{
			var handle = (GCHandle)p_vrfy;
			var context = (MbedtlsContext)handle.Target;

			return context.Verify((Mbedtls.mbedtls_x509_crt*)_crt, depth, ref flags);
		}

		int Verify (Mbedtls.mbedtls_x509_crt* _crt, int depth, ref uint flags)
		{
			// Skip intermediate and root certificates
			if (depth != 0)
				return 0;

			// Assemble a collection of the entire trust chain
			X509CertificateCollection certificates = new X509CertificateCollection ();
			mbedtls_x509_crt_handle  crt    = new mbedtls_x509_crt_handle (_crt);
			while (true)
			{
				certificates.Add(CertificateHelper.AsX509(crt));
				if ((IntPtr)crt.next == IntPtr.Zero)
					break;
				crt = new mbedtls_x509_crt_handle (crt.next);
			}
			if (ValidateCertificate (certificates))
				flags = 0;
			else
				flags |= Mbedtls.MBEDTLS_X509_BADCERT_NOT_TRUSTED;
			return 0;
		}

		public override void StartHandshake ()
		{
			if (m_SslContext.state > 0)
				Mono.Mbedtls.Debug.Throw (AlertDescription.InternalError, "Handshake started already");

			if (IsServer) {
				SetPrivateCertificate (LocalServerCertificate);
			}
			if (!IsServer) {
				m_NativeBuffer.ToNative (ServerName);
				Mono.Mbedtls.Debug.CheckAndThrow (Mbedtls.unity_mbedtls_ssl_set_hostname (m_SslContext, m_NativeBuffer.DataPtr), "Unable to set hostname");
			}
			if (!IsServer || AskForClientCertificate) {
				Mbedtls.unity_mbedtls_ssl_conf_authmode (m_SslConfig, Mbedtls.MBEDTLS_SSL_VERIFY_REQUIRED);
			}
			CertificateHelper.AddSystemCertificates(m_RootCertificateChain);
		}

		public override bool ProcessHandshake ()
		{
			if (m_SslContext.state == Mbedtls.MBEDTLS_SSL_HANDSHAKE_OVER)
				Mono.Mbedtls.Debug.Throw (AlertDescription.InternalError, "Handshake is over");

			Int32 result = Mbedtls.unity_mbedtls_ssl_handshake_step (m_SslContext);
			if (result == Mbedtls.MBEDTLS_ERR_SSL_WANT_READ ||
			    result == Mbedtls.MBEDTLS_ERR_SSL_WANT_WRITE)
				return false;

			Mono.Mbedtls.Debug.CheckAndThrow (result, "Handshake failed");
			Mono.Mbedtls.Debug.WriteLine (1, "State {0}", m_SslContext.state);
			switch (m_SslContext.state) {
			case Mbedtls.MBEDTLS_SSL_CERTIFICATE_REQUEST:
					m_RemoteCertificate = CertificateHelper.AsX509 (Mbedtls.unity_mbedtls_ssl_get_peer_cert (m_SslContext));
					m_LocalClientCertificate = SelectClientCertificate (m_RemoteCertificate, null);
					SetPrivateCertificate (m_LocalClientCertificate);
					break;

			case Mbedtls.MBEDTLS_SSL_HANDSHAKE_OVER:
				return true;
			}
			return false;
		}

		public override void FinishHandshake ()
		{
			if (IsServer && AskForClientCertificate) {
				if (!ValidateCertificate (null, null))
					Mono.Mbedtls.Debug.Throw (AlertDescription.CertificateUnknown, "unknown certificate");
			}

			IntPtr cipher = Mbedtls.unity_mbedtls_ssl_get_ciphersuite (m_SslContext);
			m_Connectioninfo = new MonoTlsConnectionInfo () {
				CipherSuiteCode = (CipherSuiteCode)Mbedtls.unity_mbedtls_ssl_get_ciphersuite_id (cipher),
				ProtocolVersion = GetProtocol (m_SslContext.minor_ver),
				PeerDomainName = ServerName
			};
			m_IsAuthenticated = true;
		}

		static TlsProtocols GetProtocol (int protocol)
		{
			switch (protocol) {
			case 1:
				return TlsProtocols.Tls10;
			case 2:
				return TlsProtocols.Tls11;
			case 3:
				return TlsProtocols.Tls12;
			default:
				throw new NotSupportedException ();
			}
		}

		static int GetProtocol (TlsProtocolCode protocol)
		{
			switch (protocol) {
			case TlsProtocolCode.Tls10:
				return 1;
			case TlsProtocolCode.Tls11:
				return 2;
			case TlsProtocolCode.Tls12:
				return 3;
			default:
				throw new NotSupportedException ();
			}
		}

		[MonoPInvokeCallback (typeof (Mbedtls.mbedtls_ssl_send_t))]
		static int BIOWriteCallback (IntPtr stream, IntPtr buffer, IntPtr len)
		{
			var handle = (GCHandle)stream;
			var context = (MbedtlsContext)handle.Target;

			return context.BIOWrite(buffer, len);
		}

		int BIOWrite (IntPtr buffer, IntPtr len)
		{
			bool wouldBlock = false;
			int length = len.ToInt32();
			ManagedBufferEnsureSize (length);

			Marshal.Copy (buffer, m_ManagedBuffer, 0, length);

			if (!Parent.InternalWrite (m_ManagedBuffer, 0, length))
				return -1;
			int result = length;
			if (result < 0)
				return Mbedtls.MBEDTLS_ERR_NET_SEND_FAILED;
			if (wouldBlock)
				return Mbedtls.MBEDTLS_ERR_SSL_WANT_WRITE;
			return result;
		}

		[MonoPInvokeCallback (typeof (Mbedtls.mbedtls_ssl_send_t))]
		static int BIOReadCallback (IntPtr stream, IntPtr buffer, IntPtr len)
		{
			var handle = (GCHandle)stream;
			var context = (MbedtlsContext)handle.Target;

			return context.BIORead(buffer, len);
		}

		int BIORead (IntPtr buffer, IntPtr len)
		{
			bool wouldBlock;
			int length = len.ToInt32();
			ManagedBufferEnsureSize (length);

			int result = Parent.InternalRead (m_ManagedBuffer, 0, length, out wouldBlock);
			if (result < 0)
				return Mbedtls.MBEDTLS_ERR_NET_RECV_FAILED;
			if (wouldBlock)
				return Mbedtls.MBEDTLS_ERR_SSL_WANT_READ;

			Marshal.Copy (m_ManagedBuffer, 0, buffer, result);
			return result;
		}

		void ManagedBufferEnsureSize(int size)
		{
			if (size <= m_ManagedBuffer.Length)
				return;
			m_ManagedBuffer = new byte[size];
		}
	}
}
#endif