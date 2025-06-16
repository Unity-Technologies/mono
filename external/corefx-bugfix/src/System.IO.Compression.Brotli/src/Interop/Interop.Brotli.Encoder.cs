// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.IO.Compression;
using Microsoft.Win32.SafeHandles;
using size_t = System.IntPtr;

internal static partial class Interop
{
    internal static partial class Brotli
    {
#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderCreateInstance")]
#endif
        internal static extern SafeBrotliEncoderHandle BrotliEncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderSetParameter")]
#endif
        internal static extern bool BrotliEncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, UInt32 value);
        
#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderCompressStream")]
#endif
        internal static extern unsafe bool BrotliEncoderCompressStream(
            SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref size_t availableIn,
            byte** nextIn, ref size_t availableOut, byte** nextOut, out size_t totalOut);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderHasMoreOutput")]
#endif
        internal static extern bool BrotliEncoderHasMoreOutput(SafeBrotliEncoderHandle state);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderDestroyInstance")]
#endif
        internal static extern void BrotliEncoderDestroyInstance(IntPtr state);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliEncoderCompress")]
#endif
        internal static extern unsafe bool BrotliEncoderCompress(int quality, int window, int v, size_t availableInput, byte* inBytes, ref size_t availableOutput, byte* outBytes);
    }
}

