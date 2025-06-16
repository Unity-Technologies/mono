// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using size_t = System.IntPtr;

internal static partial class Interop
{
    internal static partial class Brotli
    {
#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliDecoderCreateInstance")]
#endif
        internal static extern SafeBrotliDecoderHandle BrotliDecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliDecoderDecompressStream")]
#endif
        internal static extern unsafe int BrotliDecoderDecompressStream(
            SafeBrotliDecoderHandle state, ref size_t availableIn, byte** nextIn,
            ref size_t availableOut, byte** nextOut, out size_t totalOut);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliDecoderDecompress")]
#endif
        internal static extern unsafe bool BrotliDecoderDecompress(size_t availableInput, byte* inBytes, ref size_t availableOutput, byte* outBytes);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliDecoderDestroyInstance")]
#endif
        internal static extern void BrotliDecoderDestroyInstance(IntPtr state);

#if UNITY_WIN_PLATFORM || UNITY_AOT
        [DllImport(Libraries.CompressionNative)]
#else
        [DllImport(Libraries.CompressionNative, CharSet = CharSet.Unicode, EntryPoint = "MonoBrotliDecoderIsFinished")]
#endif
        internal static extern bool BrotliDecoderIsFinished(SafeBrotliDecoderHandle state);
    }
}

