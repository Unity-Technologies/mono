// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System
{
    // Implements the Decimal data type. The Decimal data type can
    // represent values ranging from -79,228,162,514,264,337,593,543,950,335 to
    // 79,228,162,514,264,337,593,543,950,335 with 28 significant digits. The
    // Decimal data type is ideally suited to financial calculations that
    // require a large number of significant digits and no round-off errors.
    //
    // The finite set of values of type Decimal are of the form m
    // / 10e, where m is an integer such that
    // -296 <; m <; 296, and e is an integer
    // between 0 and 28 inclusive.
    //
    // Contrary to the float and double data types, decimal
    // fractional numbers such as 0.1 can be represented exactly in the
    // Decimal representation. In the float and double
    // representations, such numbers are often infinite fractions, making those
    // representations more prone to round-off errors.
    //
    // The Decimal class implements widening conversions from the
    // ubyte, char, short, int, and long types
    // to Decimal. These widening conversions never loose any information
    // and never throw exceptions. The Decimal class also implements
    // narrowing conversions from Decimal to ubyte, char,
    // short, int, and long. These narrowing conversions round
    // the Decimal value towards zero to the nearest integer, and then
    // converts that integer to the destination type. An OverflowException
    // is thrown if the result is not within the range of the destination type.
    //
    // The Decimal class provides a widening conversion from
    // Currency to Decimal. This widening conversion never loses any
    // information and never throws exceptions. The Currency class provides
    // a narrowing conversion from Decimal to Currency. This
    // narrowing conversion rounds the Decimal to four decimals and then
    // converts that number to a Currency. An OverflowException
    // is thrown if the result is not within the range of the Currency type.
    //
    // The Decimal class provides narrowing conversions to and from the
    // float and double types. A conversion from Decimal to
    // float or double may loose precision, but will not loose
    // information about the overall magnitude of the numeric value, and will never
    // throw an exception. A conversion from float or double to
    // Decimal throws an OverflowException if the value is not within
    // the range of the Decimal type.
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
#if !MONO
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
#endif
    public readonly partial struct Decimal : IFormattable, IComparable, IConvertible, IComparable<decimal>, IEquatable<decimal>, IDeserializationCallback, ISpanFormattable
    {
        // Sign mask for the flags field. A value of zero in this bit indicates a
        // positive Decimal value, and a value of one in this bit indicates a
        // negative Decimal value.
        // 
        // Look at OleAut's DECIMAL_NEG constant to check for negative values
        // in native code.
        private const int SignMask = unchecked((int)0x80000000);

        // Scale mask for the flags field. This byte in the flags field contains
        // the power of 10 to divide the Decimal value by. The scale byte must
        // contain a value between 0 and 28 inclusive.
        private const int ScaleMask = 0x00FF0000;

        // Number of bits scale is shifted by.
        private const int ScaleShift = 16;

        // Constant representing the Decimal value 0.
        public const decimal Zero = 0m;

        // Constant representing the Decimal value 1.
        public const decimal One = 1m;

        // Constant representing the Decimal value -1.
        public const decimal MinusOne = -1m;

        // Constant representing the largest possible Decimal value. The value of
        // this constant is 79,228,162,514,264,337,593,543,950,335.
        public const decimal MaxValue = 79228162514264337593543950335m;

        // Constant representing the smallest possible Decimal value. The value of
        // this constant is -79,228,162,514,264,337,593,543,950,335.
        public const decimal MinValue = -79228162514264337593543950335m;

        // The lo, mid, hi, and flags fields contain the representation of the
        // Decimal value. The lo, mid, and hi fields contain the 96-bit integer
        // part of the Decimal. Bits 0-15 (the lower word) of the flags field are
        // unused and must be zero; bits 16-23 contain must contain a value between
        // 0 and 28, indicating the power of 10 to divide the 96-bit integer part
        // by to produce the Decimal value; bits 24-30 are unused and must be zero;
        // and finally bit 31 indicates the sign of the Decimal value, 0 meaning
        // positive and 1 meaning negative.
        //
        // NOTE: Do not change the order in which these fields are declared. The
        // native methods in this class rely on this particular order. 
        // Do not rename (binary serialization).
        [FieldOffset(0)]
        private readonly int flags;
        [FieldOffset(4)]
        private readonly int hi;
        [FieldOffset(8)]
        private readonly int lo;
        [FieldOffset(12)]
        private readonly int mid;

        /// <summary>
        /// The low and mid fields combined in little-endian order
        /// </summary>
        [FieldOffset(8), NonSerialized]
        private readonly ulong ulomidLE;

        // Constructs a Decimal from an integer value.
        //
        public Decimal(int value)
#if __MonoCS__
        : this()
#endif
        {
            if (value >= 0)
            {
                flags = 0;
            }
            else
            {
                flags = SignMask;
                value = -value;
            }
            lo = value;
            mid = 0;
            hi = 0;
        }

        // Constructs a Decimal from an unsigned integer value.
        //
        [CLSCompliant(false)]
        public Decimal(uint value)
#if __MonoCS__
        : this()
#endif
        {
            flags = 0;
            lo = (int)value;
            mid = 0;
            hi = 0;
        }

        // Constructs a Decimal from a long value.
        //
        public Decimal(long value)
#if __MonoCS__
        : this()
#endif
        {
            if (value >= 0)
            {
                flags = 0;
            }
            else
            {
                flags = SignMask;
                value = -value;
            }
            lo = (int)value;
            mid = (int)(value >> 32);
            hi = 0;
        }

        // Constructs a Decimal from an unsigned long value.
        //
        [CLSCompliant(false)]
        public Decimal(ulong value)
#if __MonoCS__
        : this()
#endif
        {
            flags = 0;
            lo = (int)value;
            mid = (int)(value >> 32);
            hi = 0;
        }

        // Constructs a Decimal from a float value.
        //
        public Decimal(float value)
#if __MonoCS__
        : this()
#endif
        {
            DecCalc.VarDecFromR4(value, out AsMutable(ref this));
        }

        // Constructs a Decimal from a double value.
        //
        public Decimal(double value)
#if __MonoCS__
        : this()
#endif
        {
            DecCalc.VarDecFromR8(value, out AsMutable(ref this));
        }

        //
        // Decimal <==> Currency conversion.
        //
        // A Currency represents a positive or negative decimal value with 4 digits past the decimal point. The actual Int64 representation used by these methods
        // is the currency value multiplied by 10,000. For example, a currency value of $12.99 would be represented by the Int64 value 129,900.
        //
        public static decimal FromOACurrency(long cy)
        {
            ulong absoluteCy; // has to be ulong to accommodate the case where cy == long.MinValue.
            bool isNegative = false;
            if (cy < 0)
            {
                isNegative = true;
                absoluteCy = (ulong)(-cy);
            }
            else
            {
                absoluteCy = (ulong)cy;
            }

            // In most cases, FromOACurrency() produces a Decimal with Scale set to 4. Unless, that is, some of the trailing digits past the decimal point are zero,
            // in which case, for compatibility with .Net, we reduce the Scale by the number of zeros. While the result is still numerically equivalent, the scale does
            // affect the ToString() value. In particular, it prevents a converted currency value of $12.95 from printing uglily as "12.9500".
            int scale = 4;
            if (absoluteCy != 0)  // For compatibility, a currency of 0 emits the Decimal "0.0000" (scale set to 4).
            {
                while (scale != 0 && ((absoluteCy % 10) == 0))
                {
                    scale--;
                    absoluteCy /= 10;
                }
            }

            return new decimal((int)absoluteCy, (int)(absoluteCy >> 32), 0, isNegative, (byte)scale);
        }

        public static long ToOACurrency(decimal value)
        {
            return DecCalc.VarCyFromDec(ref AsMutable(ref value));
        }

        private static bool IsValid(int flags) => (flags & ~(SignMask | ScaleMask)) == 0 && ((uint)(flags & ScaleMask) <= (28 << ScaleShift));

        // Constructs a Decimal from an integer array containing a binary
        // representation. The bits argument must be a non-null integer
        // array with four elements. bits[0], bits[1], and
        // bits[2] contain the low, middle, and high 32 bits of the 96-bit
        // integer part of the Decimal. bits[3] contains the scale factor
        // and sign of the Decimal: bits 0-15 (the lower word) are unused and must
        // be zero; bits 16-23 must contain a value between 0 and 28, indicating
        // the power of 10 to divide the 96-bit integer part by to produce the
        // Decimal value; bits 24-30 are unused and must be zero; and finally bit
        // 31 indicates the sign of the Decimal value, 0 meaning positive and 1
        // meaning negative.
        //
        // Note that there are several possible binary representations for the
        // same numeric value. For example, the value 1 can be represented as {1,
        // 0, 0, 0} (integer value 1 with a scale factor of 0) and equally well as
        // {1000, 0, 0, 0x30000} (integer value 1000 with a scale factor of 3).
        // The possible binary representations of a particular value are all
        // equally valid, and all are numerically equivalent.
        //
        public Decimal(int[] bits)
#if __MonoCS__
        : this()
#endif
        {
            if (bits == null)
                throw new ArgumentNullException(nameof(bits));
            if (bits.Length == 4)
            {
                int f = bits[3];
                if (IsValid(f))
                {
                    lo = bits[0];
                    mid = bits[1];
                    hi = bits[2];
                    flags = f;
                    return;
                }
            }
            throw new ArgumentException(SR.Arg_DecBitCtor);
        }

        // Constructs a Decimal from its constituent parts.
        // 
        public Decimal(int lo, int mid, int hi, bool isNegative, byte scale)
#if __MonoCS__
        : this()
#endif
        {
            if (scale > 28)
                throw new ArgumentOutOfRangeException(nameof(scale), SR.ArgumentOutOfRange_DecimalScale);
            this.lo = lo;
            this.mid = mid;
            this.hi = hi;
            flags = ((int)scale) << 16;
            if (isNegative)
                flags |= SignMask;
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            // OnDeserialization is called after each instance of this class is deserialized.
            // This callback method performs decimal validation after being deserialized.
            if (!IsValid(flags))
                throw new SerializationException(SR.Overflow_Decimal);
        }

        // Constructs a Decimal from its constituent parts.
        private Decimal(int lo, int mid, int hi, int flags)
        {
            if (IsValid(flags))
            {
#if __MonoCS__
                this.ulomidLE = 0;
#endif
                this.lo = lo;
                this.mid = mid;
                this.hi = hi;
                this.flags = flags;
                return;
            }
            throw new ArgumentException(SR.Arg_DecBitCtor);
        }

        private Decimal(in decimal d, int flags)
        {
            this = d;
            this.flags = flags;
        }

        // Returns the absolute value of the given Decimal. If d is
        // positive, the result is d. If d is negative, the result
        // is -d.
        //
        internal static decimal Abs(ref decimal d)
        {
            return new decimal(d, d.flags & ~SignMask);
        }

        // Adds two Decimal values.
        //
        public static decimal Add(decimal d1, decimal d2)
        {
            DecCalc.DecAddSub(ref AsMutable(ref d1), ref AsMutable(ref d2), false);
            return d1;
        }

        // Rounds a Decimal to an integer value. The Decimal argument is rounded
        // towards positive infinity.
        public static decimal Ceiling(decimal d)
        {
            int flags = d.flags;
            if ((flags & ScaleMask) != 0)
                DecCalc.InternalRound(ref AsMutable(ref d), (byte)(flags >> ScaleShift), DecCalc.RoundingMode.Ceiling);
            return d;
        }

        // Compares two Decimal values, returning an integer that indicates their
        // relationship.
        //
        public static int Compare(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2);
        }

        // Compares this object to another object, returning an integer that
        // indicates the relationship. 
        // Returns a value less than zero if this  object
        // null is considered to be less than any instance.
        // If object is not of type Decimal, this method throws an ArgumentException.
        // 
        public int CompareTo(object value)
        {
            if (value == null)
                return 1;
            if (!(value is decimal))
                throw new ArgumentException(SR.Arg_MustBeDecimal);

            decimal other = (decimal)value;
            return DecCalc.VarDecCmp(in this, in other);
        }

        public int CompareTo(decimal value)
        {
            return DecCalc.VarDecCmp(in this, in value);
        }

        // Divides two Decimal values.
        //
        public static decimal Divide(decimal d1, decimal d2)
        {
            DecCalc.VarDecDiv(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        // Checks if this Decimal is equal to a given object. Returns true
        // if the given object is a boxed Decimal and its value is equal to the
        // value of this Decimal. Returns false otherwise.
        //
        public override bool Equals(object value)
        {
            if (value is decimal)
            {
                decimal other = (decimal)value;
                return DecCalc.VarDecCmp(in this, in other) == 0;
            }
            return false;
        }

        public bool Equals(decimal value)
        {
            return DecCalc.VarDecCmp(in this, in value) == 0;
        }

        // Returns the hash code for this Decimal.
        //
        public override int GetHashCode() => DecCalc.GetHashCode(in this);

        // Compares two Decimal values for equality. Returns true if the two
        // Decimal values are equal, or false if they are not equal.
        //
        public static bool Equals(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) == 0;
        }

        // Rounds a Decimal to an integer value. The Decimal argument is rounded
        // towards negative infinity.
        //
        public static decimal Floor(decimal d)
        {
            int flags = d.flags;
            if ((flags & ScaleMask) != 0)
                DecCalc.InternalRound(ref AsMutable(ref d), (byte)(flags >> ScaleShift), DecCalc.RoundingMode.Floor);
            return d;
        }

        // Converts this Decimal to a string. The resulting string consists of an
        // optional minus sign ("-") followed to a sequence of digits ("0" - "9"),
        // optionally followed by a decimal point (".") and another sequence of
        // digits.
        //
        public override string ToString()
        {
            return Number.FormatDecimal(this, null, NumberFormatInfo.CurrentInfo);
        }

        public string ToString(string format)
        {
            return Number.FormatDecimal(this, format, NumberFormatInfo.CurrentInfo);
        }

        public string ToString(IFormatProvider provider)
        {
            return Number.FormatDecimal(this, null, NumberFormatInfo.GetInstance(provider));
        }

        public string ToString(string format, IFormatProvider provider)
        {
            return Number.FormatDecimal(this, format, NumberFormatInfo.GetInstance(provider));
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
        {
            return Number.TryFormatDecimal(this, format, NumberFormatInfo.GetInstance(provider), destination, out charsWritten);
        }

        // Converts a string to a Decimal. The string must consist of an optional
        // minus sign ("-") followed by a sequence of digits ("0" - "9"). The
        // sequence of digits may optionally contain a single decimal point (".")
        // character. Leading and trailing whitespace characters are allowed.
        // Parse also allows a currency symbol, a trailing negative sign, and
        // parentheses in the number.
        //
        public static decimal Parse(string s)
        {
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo);
        }

        public static decimal Parse(string s, NumberStyles style)
        {
            NumberFormatInfo.ValidateParseStyleFloatingPoint(style);
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s, style, NumberFormatInfo.CurrentInfo);
        }

        public static decimal Parse(string s, IFormatProvider provider)
        {
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s, NumberStyles.Number, NumberFormatInfo.GetInstance(provider));
        }

        public static decimal Parse(string s, NumberStyles style, IFormatProvider provider)
        {
            NumberFormatInfo.ValidateParseStyleFloatingPoint(style);
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s, style, NumberFormatInfo.GetInstance(provider));
        }

        public static decimal Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Number, IFormatProvider provider = null)
        {
            NumberFormatInfo.ValidateParseStyleFloatingPoint(style);
            return Number.ParseDecimal(s, style, NumberFormatInfo.GetInstance(provider));
        }

        public static bool TryParse(string s, out decimal result)
        {
            if (s == null)
            {
                result = 0;
                return false;
            }

            return Number.TryParseDecimal(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse(ReadOnlySpan<char> s, out decimal result)
        {
            return Number.TryParseDecimal(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse(string s, NumberStyles style, IFormatProvider provider, out decimal result)
        {
            NumberFormatInfo.ValidateParseStyleFloatingPoint(style);

            if (s == null)
            {
                result = 0;
                return false;
            }

            return Number.TryParseDecimal(s, style, NumberFormatInfo.GetInstance(provider), out result);
        }

        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal result)
        {
            NumberFormatInfo.ValidateParseStyleFloatingPoint(style);
            return Number.TryParseDecimal(s, style, NumberFormatInfo.GetInstance(provider), out result);
        }

        // Returns a binary representation of a Decimal. The return value is an
        // integer array with four elements. Elements 0, 1, and 2 contain the low,
        // middle, and high 32 bits of the 96-bit integer part of the Decimal.
        // Element 3 contains the scale factor and sign of the Decimal: bits 0-15
        // (the lower word) are unused; bits 16-23 contain a value between 0 and
        // 28, indicating the power of 10 to divide the 96-bit integer part by to
        // produce the Decimal value; bits 24-30 are unused; and finally bit 31
        // indicates the sign of the Decimal value, 0 meaning positive and 1
        // meaning negative.
        //
        public static int[] GetBits(decimal d)
        {
            return new int[] { d.lo, d.mid, d.hi, d.flags };
        }

        internal static void GetBytes(in decimal d, byte[] buffer)
        {
            Debug.Assert((buffer != null && buffer.Length >= 16), "[GetBytes]buffer != null && buffer.Length >= 16");
            buffer[0] = (byte)d.lo;
            buffer[1] = (byte)(d.lo >> 8);
            buffer[2] = (byte)(d.lo >> 16);
            buffer[3] = (byte)(d.lo >> 24);

            buffer[4] = (byte)d.mid;
            buffer[5] = (byte)(d.mid >> 8);
            buffer[6] = (byte)(d.mid >> 16);
            buffer[7] = (byte)(d.mid >> 24);

            buffer[8] = (byte)d.hi;
            buffer[9] = (byte)(d.hi >> 8);
            buffer[10] = (byte)(d.hi >> 16);
            buffer[11] = (byte)(d.hi >> 24);

            buffer[12] = (byte)d.flags;
            buffer[13] = (byte)(d.flags >> 8);
            buffer[14] = (byte)(d.flags >> 16);
            buffer[15] = (byte)(d.flags >> 24);
        }

        internal static decimal ToDecimal(byte[] buffer)
        {
            Debug.Assert((buffer != null && buffer.Length >= 16), "[ToDecimal]buffer != null && buffer.Length >= 16");
            int lo = ((int)buffer[0]) | ((int)buffer[1] << 8) | ((int)buffer[2] << 16) | ((int)buffer[3] << 24);
            int mid = ((int)buffer[4]) | ((int)buffer[5] << 8) | ((int)buffer[6] << 16) | ((int)buffer[7] << 24);
            int hi = ((int)buffer[8]) | ((int)buffer[9] << 8) | ((int)buffer[10] << 16) | ((int)buffer[11] << 24);
            int flags = ((int)buffer[12]) | ((int)buffer[13] << 8) | ((int)buffer[14] << 16) | ((int)buffer[15] << 24);
            return new decimal(lo, mid, hi, flags);
        }

        // Returns the larger of two Decimal values.
        //
        internal static ref readonly decimal Max(ref decimal d1, ref decimal d2)
        {
            return ref DecCalc.VarDecCmp(d1, d2) >= 0 ? ref d1 : ref d2;
        }

        // Returns the smaller of two Decimal values.
        //
        internal static ref readonly decimal Min(ref decimal d1, ref decimal d2)
        {
            return ref DecCalc.VarDecCmp(d1, d2) < 0 ? ref d1 : ref d2;
        }

        public static decimal Remainder(decimal d1, decimal d2)
        {
            DecCalc.VarDecMod(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        // Multiplies two Decimal values.
        //
        public static decimal Multiply(decimal d1, decimal d2)
        {
            DecCalc.VarDecMul(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        // Returns the negated value of the given Decimal. If d is non-zero,
        // the result is -d. If d is zero, the result is zero.
        //
        public static decimal Negate(decimal d)
        {
            return new decimal(in d, d.flags ^ SignMask);
        }

        // Rounds a Decimal value to a given number of decimal places. The value
        // given by d is rounded to the number of decimal places given by
        // decimals. The decimals argument must be an integer between
        // 0 and 28 inclusive.
        //
        // By default a mid-point value is rounded to the nearest even number. If the mode is
        // passed in, it can also round away from zero.

        public static decimal Round(decimal d) => Round(ref d, 0, MidpointRounding.ToEven);
        public static decimal Round(decimal d, int decimals) => Round(ref d, decimals, MidpointRounding.ToEven);
        public static decimal Round(decimal d, MidpointRounding mode) => Round(ref d, 0, mode);
        public static decimal Round(decimal d, int decimals, MidpointRounding mode) => Round(ref d, decimals, mode);

        private static decimal Round(ref decimal d, int decimals, MidpointRounding mode)
        {
            if ((uint)decimals > 28)
                throw new ArgumentOutOfRangeException(nameof(decimals), SR.ArgumentOutOfRange_DecimalRound);
            if ((uint)mode > (uint)MidpointRounding.AwayFromZero)
                throw new ArgumentException(SR.Format(SR.Argument_InvalidEnumValue, mode, nameof(MidpointRounding)), nameof(mode));

            int scale = d.Scale - decimals;
            if (scale > 0)
                DecCalc.InternalRound(ref AsMutable(ref d), (uint)scale, (DecCalc.RoundingMode)mode);
            return d;
        }

        internal static int Sign(ref decimal d) => (d.lo | d.mid | d.hi) == 0 ? 0 : (d.flags >> 31) | 1;

        // Subtracts two Decimal values.
        //
        public static decimal Subtract(decimal d1, decimal d2)
        {
            DecCalc.DecAddSub(ref AsMutable(ref d1), ref AsMutable(ref d2), true);
            return d1;
        }

        // Converts a Decimal to an unsigned byte. The Decimal value is rounded
        // towards zero to the nearest integer value, and the result of this
        // operation is returned as a byte.
        //
        public static byte ToByte(decimal value)
        {
            uint temp;
            try
            {
                temp = ToUInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Byte, e);
            }
            if (temp != (byte)temp) throw new OverflowException(SR.Overflow_Byte);
            return (byte)temp;
        }

        // Converts a Decimal to a signed byte. The Decimal value is rounded
        // towards zero to the nearest integer value, and the result of this
        // operation is returned as a byte.
        //
        [CLSCompliant(false)]
        public static sbyte ToSByte(decimal value)
        {
            int temp;
            try
            {
                temp = ToInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_SByte, e);
            }
            if (temp != (sbyte)temp) throw new OverflowException(SR.Overflow_SByte);
            return (sbyte)temp;
        }

        // Converts a Decimal to a short. The Decimal value is
        // rounded towards zero to the nearest integer value, and the result of
        // this operation is returned as a short.
        //
        public static short ToInt16(decimal value)
        {
            int temp;
            try
            {
                temp = ToInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Int16, e);
            }
            if (temp != (short)temp) throw new OverflowException(SR.Overflow_Int16);
            return (short)temp;
        }

        // Converts a Decimal to a double. Since a double has fewer significant
        // digits than a Decimal, this operation may produce round-off errors.
        //
        public static double ToDouble(decimal d)
        {
            return DecCalc.VarR8FromDec(in d);
        }

        // Converts a Decimal to an integer. The Decimal value is rounded towards
        // zero to the nearest integer value, and the result of this operation is
        // returned as an integer.
        //
        public static int ToInt32(decimal d)
        {
            Truncate(ref d);
            if ((d.hi | d.mid) == 0)
            {
                int i = d.lo;
                if (!d.IsNegative)
                {
                    if (i >= 0) return i;
                }
                else
                {
                    i = -i;
                    if (i <= 0) return i;
                }
            }
            throw new OverflowException(SR.Overflow_Int32);
        }

        // Converts a Decimal to a long. The Decimal value is rounded towards zero
        // to the nearest integer value, and the result of this operation is
        // returned as a long.
        //
        public static long ToInt64(decimal d)
        {
            Truncate(ref d);
            if (d.hi == 0)
            {
                long l = (long)d.Low64;
                if (!d.IsNegative)
                {
                    if (l >= 0) return l;
                }
                else
                {
                    l = -l;
                    if (l <= 0) return l;
                }
            }
            throw new OverflowException(SR.Overflow_Int64);
        }

        // Converts a Decimal to an ushort. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as an ushort.
        //
        [CLSCompliant(false)]
        public static ushort ToUInt16(decimal value)
        {
            uint temp;
            try
            {
                temp = ToUInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_UInt16, e);
            }
            if (temp != (ushort)temp) throw new OverflowException(SR.Overflow_UInt16);
            return (ushort)temp;
        }

        // Converts a Decimal to an unsigned integer. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as an unsigned integer.
        //
        [CLSCompliant(false)]
        public static uint ToUInt32(decimal d)
        {
            Truncate(ref d);
            if ((d.hi | d.mid) == 0)
            {
                uint i = d.Low;
                if (!d.IsNegative || i == 0)
                    return i;
            }
            throw new OverflowException(SR.Overflow_UInt32);
        }

        // Converts a Decimal to an unsigned long. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as a long.
        //
        [CLSCompliant(false)]
        public static ulong ToUInt64(decimal d)
        {
            Truncate(ref d);
            if (d.hi == 0)
            {
                ulong l = d.Low64;
                if (!d.IsNegative || l == 0)
                    return l;
            }
            throw new OverflowException(SR.Overflow_UInt64);
        }

        // Converts a Decimal to a float. Since a float has fewer significant
        // digits than a Decimal, this operation may produce round-off errors.
        //
        public static float ToSingle(decimal d)
        {
            return DecCalc.VarR4FromDec(in d);
        }

        // Truncates a Decimal to an integer value. The Decimal argument is rounded
        // towards zero to the nearest integer value, corresponding to removing all
        // digits after the decimal point.
        //
        public static decimal Truncate(decimal d)
        {
            Truncate(ref d);
            return d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Truncate(ref decimal d)
        {
            int flags = d.flags;
            if ((flags & ScaleMask) != 0)
                DecCalc.InternalRound(ref AsMutable(ref d), (byte)(flags >> ScaleShift), DecCalc.RoundingMode.Truncate);
        }

        public static implicit operator decimal(byte value)
        {
            return new decimal((uint)value);
        }

        [CLSCompliant(false)]
        public static implicit operator decimal(sbyte value)
        {
            return new decimal(value);
        }

        public static implicit operator decimal(short value)
        {
            return new decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator decimal(ushort value)
        {
            return new decimal((uint)value);
        }

        public static implicit operator decimal(char value)
        {
            return new decimal((uint)value);
        }

        public static implicit operator decimal(int value)
        {
            return new decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator decimal(uint value)
        {
            return new decimal(value);
        }

        public static implicit operator decimal(long value)
        {
            return new decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator decimal(ulong value)
        {
            return new decimal(value);
        }


        public static explicit operator decimal(float value)
        {
            return new decimal(value);
        }

        public static explicit operator decimal(double value)
        {
            return new decimal(value);
        }

        public static explicit operator byte(decimal value)
        {
            return ToByte(value);
        }

        [CLSCompliant(false)]
        public static explicit operator sbyte(decimal value)
        {
            return ToSByte(value);
        }

        public static explicit operator char(decimal value)
        {
            ushort temp;
            try
            {
                temp = ToUInt16(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Char, e);
            }
            return (char)temp;
        }

        public static explicit operator short(decimal value)
        {
            return ToInt16(value);
        }

        [CLSCompliant(false)]
        public static explicit operator ushort(decimal value)
        {
            return ToUInt16(value);
        }

        public static explicit operator int(decimal value)
        {
            return ToInt32(value);
        }

        [CLSCompliant(false)]
        public static explicit operator uint(decimal value)
        {
            return ToUInt32(value);
        }

        public static explicit operator long(decimal value)
        {
            return ToInt64(value);
        }

        [CLSCompliant(false)]
        public static explicit operator ulong(decimal value)
        {
            return ToUInt64(value);
        }

        public static explicit operator float(decimal value)
        {
            return ToSingle(value);
        }

        public static explicit operator double(decimal value)
        {
            return ToDouble(value);
        }

        public static decimal operator +(decimal d)
        {
            return d;
        }

        public static decimal operator -(decimal d)
        {
            return new decimal(in d, d.flags ^ SignMask);
        }

        public static decimal operator ++(decimal d)
        {
            return Add(d, One);
        }

        public static decimal operator --(decimal d)
        {
            return Subtract(d, One);
        }

        public static decimal operator +(decimal d1, decimal d2)
        {
            DecCalc.DecAddSub(ref AsMutable(ref d1), ref AsMutable(ref d2), false);
            return d1;
        }

        public static decimal operator -(decimal d1, decimal d2)
        {
            DecCalc.DecAddSub(ref AsMutable(ref d1), ref AsMutable(ref d2), true);
            return d1;
        }

        public static decimal operator *(decimal d1, decimal d2)
        {
            DecCalc.VarDecMul(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        public static decimal operator /(decimal d1, decimal d2)
        {
            DecCalc.VarDecDiv(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        public static decimal operator %(decimal d1, decimal d2)
        {
            DecCalc.VarDecMod(ref AsMutable(ref d1), ref AsMutable(ref d2));
            return d1;
        }

        public static bool operator ==(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) == 0;
        }

        public static bool operator !=(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) != 0;
        }

        public static bool operator <(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) < 0;
        }

        public static bool operator <=(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) <= 0;
        }

        public static bool operator >(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) > 0;
        }

        public static bool operator >=(decimal d1, decimal d2)
        {
            return DecCalc.VarDecCmp(in d1, in d2) >= 0;
        }

        //
        // IConvertible implementation
        //

        public TypeCode GetTypeCode()
        {
            return TypeCode.Decimal;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Decimal", "Char"));
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return this;
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Decimal", "DateTime"));
        }

        object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }



#if __MonoCS__

        // Low level accessors used by a DecCalc and formatting
        internal uint High => (uint)hi;
        internal uint Low => (uint)lo;
        internal uint Mid => (uint)mid;

        internal bool IsNegative => flags < 0;

        internal int Scale => (byte)(flags >> ScaleShift);

        // Mono note: Mono doesn't use the BIGENDIAN define, as both endians consume the same bootstrap tarball.
        // As such, runtime checks should be used. Both Mono and CoreCLR will inline endianness checks as an intrinsic.
        private ulong Low64 => BitConverter.IsLittleEndian ? (ulong)ulomidLE : ((ulong)Mid << 32) | Low;

        private static ref DecCalc AsMutable(ref decimal d) => ref Unsafe.As<decimal, DecCalc>(ref d);

        #region APIs need by number formatting.

        internal static uint DecDivMod1E9(ref decimal value)
        {
            return DecCalc.DecDivMod1E9(ref AsMutable(ref value));
        }

        #endregion

        /// <summary>
        /// Class that contains all the mathematical calculations for decimal. Most of which have been ported from oleaut32.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct DecCalc
        {
            // NOTE: Do not change the offsets of these fields. This structure must have the same layout as Decimal.
            [FieldOffset(0)]
            private uint uflags;
            [FieldOffset(4)]
            private uint uhi;
            [FieldOffset(8)]
            private uint ulo;
            [FieldOffset(12)]
            private uint umid;

            /// <summary>
            /// The low and mid fields combined in little-endian order
            /// </summary>
            [FieldOffset(8)]
            private ulong ulomidLE;

            private uint High
            {
                get => uhi;
                set => uhi = value;
            }

            private uint Low
            {
                get => ulo;
                set => ulo = value;
            }

            private uint Mid
            {
                get => umid;
                set => umid = value;
            }

            private bool IsNegative => (int)uflags < 0;

            private int Scale => (byte)(uflags >> ScaleShift);

            private ulong Low64
            {
                get { return BitConverter.IsLittleEndian ? ulomidLE : (((ulong)umid << 32) | ulo); }
                set
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        ulomidLE = value;
                    }
                    else
                    {
                        umid = (uint)(value >> 32);
                        ulo = (uint)value;
                    }
                }
            }

            private const uint SignMask = 0x80000000;
            private const uint ScaleMask = 0x00FF0000;

            private const int DEC_SCALE_MAX = 28;

            private const uint TenToPowerNine = 1000000000;
            private const ulong TenToPowerEighteen = 1000000000000000000;

            // The maximum power of 10 that a 32 bit integer can store
            private const int MaxInt32Scale = 9;
            // The maximum power of 10 that a 64 bit integer can store
            private const int MaxInt64Scale = 19;

            // Fast access for 10^n where n is 0-9
            private static readonly uint[] s_powers10 = new uint[] {
                1,
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000
            };

            // Fast access for 10^n where n is 1-19
            private static readonly ulong[] s_ulongPowers10 = new ulong[] {
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000,
                10000000000,
                100000000000,
                1000000000000,
                10000000000000,
                100000000000000,
                1000000000000000,
                10000000000000000,
                100000000000000000,
                1000000000000000000,
                10000000000000000000,
            };

            private static readonly double[] s_doublePowers10 = new double[] {
                1, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9,
                1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
                1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28, 1e29,
                1e30, 1e31, 1e32, 1e33, 1e34, 1e35, 1e36, 1e37, 1e38, 1e39,
                1e40, 1e41, 1e42, 1e43, 1e44, 1e45, 1e46, 1e47, 1e48, 1e49,
                1e50, 1e51, 1e52, 1e53, 1e54, 1e55, 1e56, 1e57, 1e58, 1e59,
                1e60, 1e61, 1e62, 1e63, 1e64, 1e65, 1e66, 1e67, 1e68, 1e69,
                1e70, 1e71, 1e72, 1e73, 1e74, 1e75, 1e76, 1e77, 1e78, 1e79,
                1e80
            };

            #region Decimal Math Helpers

            private static unsafe uint GetExponent(float f)
            {
                // Based on pulling out the exp from this single struct layout
                //typedef struct {
                //    ULONG mant:23;
                //    ULONG exp:8;
                //    ULONG sign:1;
                //} SNGSTRUCT;

                return (byte)(*(uint*)&f >> 23);
            }

            private static unsafe uint GetExponent(double d)
            {
                // Based on pulling out the exp from this double struct layout
                //typedef struct {
                //   DWORDLONG mant:52;
                //   DWORDLONG signexp:12;
                // } DBLSTRUCT;

                return (uint)(*(ulong*)&d >> 52) & 0x7FFu;
            }

            private static ulong UInt32x32To64(uint a, uint b)
            {
                return (ulong)a * (ulong)b;
            }

            private static void UInt64x64To128(ulong a, ulong b, ref DecCalc result)
            {
                ulong low = UInt32x32To64((uint)a, (uint)b); // lo partial prod
                ulong mid = UInt32x32To64((uint)a, (uint)(b >> 32)); // mid 1 partial prod
                ulong high = UInt32x32To64((uint)(a >> 32), (uint)(b >> 32));
                high += mid >> 32;
                low += mid <<= 32;
                if (low < mid)  // test for carry
                    high++;

                mid = UInt32x32To64((uint)(a >> 32), (uint)b);
                high += mid >> 32;
                low += mid <<= 32;
                if (low < mid)  // test for carry
                    high++;

                if (high > uint.MaxValue)
                    throw new OverflowException(SR.Overflow_Decimal);
                result.Low64 = low;
                result.High = (uint)high;
            }

            /// <summary>
            /// Do full divide, yielding 96-bit result and 32-bit remainder.
            /// </summary>
            /// <param name="bufNum">96-bit dividend as array of uints, least-sig first</param>
            /// <param name="den">32-bit divisor</param>
            /// <returns>Returns remainder. Quotient overwrites dividend.</returns>
            private static uint Div96By32(ref Buf12 bufNum, uint den)
            {
                // TODO: https://github.com/dotnet/coreclr/issues/3439
                ulong tmp, div;
                if (bufNum.U2 != 0)
                {
                    tmp = bufNum.High64;
                    div = tmp / den;
                    bufNum.High64 = div;
                    tmp = ((tmp - (uint)div * den) << 32) | bufNum.U0;
                    if (tmp == 0)
                        return 0;
                    uint div32 = (uint)(tmp / den);
                    bufNum.U0 = div32;
                    return (uint)tmp - div32 * den;
                }

                tmp = bufNum.Low64;
                if (tmp == 0)
                    return 0;
                div = tmp / den;
                bufNum.Low64 = div;
                return (uint)(tmp - div * den);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool Div96ByConst(ref ulong high64, ref uint low, uint pow)
            {
#if BIT64
                ulong div64 = high64 / pow;
                uint div = (uint)((((high64 - div64 * pow) << 32) + low) / pow);
                if (low == div * pow)
                {
                    high64 = div64;
                    low = div;
                    return true;
                }
#else
                // 32-bit RyuJIT doesn't convert 64-bit division by constant into multiplication by reciprocal. Do half-width divisions instead.
                Debug.Assert(pow <= ushort.MaxValue);
                uint num, mid32, low16, div;
                if (high64 <= uint.MaxValue)
                {
                    num = (uint)high64;
                    mid32 = num / pow;
                    num = (num - mid32 * pow) << 16;

                    num += low >> 16;
                    low16 = num / pow;
                    num = (num - low16 * pow) << 16;

                    num += (ushort)low;
                    div = num / pow;
                    if (num == div * pow)
                    {
                        high64 = mid32;
                        low = (low16 << 16) + div;
                        return true;
                    }
                }
                else
                {
                    num = (uint)(high64 >> 32);
                    uint high32 = num / pow;
                    num = (num - high32 * pow) << 16;

                    num += (uint)high64 >> 16;
                    mid32 = num / pow;
                    num = (num - mid32 * pow) << 16;

                    num += (ushort)high64;
                    div = num / pow;
                    num = (num - div * pow) << 16;
                    mid32 = div + (mid32 << 16);

                    num += low >> 16;
                    low16 = num / pow;
                    num = (num - low16 * pow) << 16;

                    num += (ushort)low;
                    div = num / pow;
                    if (num == div * pow)
                    {
                        high64 = ((ulong)high32 << 32) | mid32;
                        low = (low16 << 16) + div;
                        return true;
                    }
                }
#endif
                return false;
            }

            /// <summary>
            /// Normalize (unscale) the number by trying to divide out 10^8, 10^4, 10^2, and 10^1.
            /// If a division by one of these powers returns a zero remainder, then we keep the quotient.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Unscale(ref uint low, ref ulong high64, ref int scale)
            {
                // Since 10 = 2 * 5, there must be a factor of 2 for every power of 10 we can extract.
                // We use this as a quick test on whether to try a given power.

#if BIT64
                while ((byte)low == 0 && scale >= 8 && Div96ByConst(ref high64, ref low, 100000000))
                    scale -= 8;

                if ((low & 0xF) == 0 && scale >= 4 && Div96ByConst(ref high64, ref low, 10000))
                    scale -= 4;
#else
                while ((low & 0xF) == 0 && scale >= 4 && Div96ByConst(ref high64, ref low, 10000))
                    scale -= 4;
#endif

                if ((low & 3) == 0 && scale >= 2 && Div96ByConst(ref high64, ref low, 100))
                    scale -= 2;

                if ((low & 1) == 0 && scale >= 1 && Div96ByConst(ref high64, ref low, 10))
                    scale--;
            }

            /// <summary>
            /// Do partial divide, yielding 32-bit result and 64-bit remainder.
            /// Divisor must be larger than upper 64 bits of dividend.
            /// </summary>
            /// <param name="bufNum">96-bit dividend as array of uints, least-sig first</param>
            /// <param name="den">64-bit divisor</param>
            /// <returns>Returns quotient. Remainder overwrites lower 64-bits of dividend.</returns>
            private static uint Div96By64(ref Buf12 bufNum, ulong den)
            {
                Debug.Assert(den > bufNum.High64);
                uint quo;
                ulong num;
                uint num2 = bufNum.U2;
                if (num2 == 0)
                {
                    num = bufNum.Low64;
                    if (num < den)
                        // Result is zero.  Entire dividend is remainder.
                        return 0;

                    // TODO: https://github.com/dotnet/coreclr/issues/3439
                    quo = (uint)(num / den);
                    num -= quo * den; // remainder
                    bufNum.Low64 = num;
                    return quo;
                }

                uint denHigh32 = (uint)(den >> 32);
                if (num2 >= denHigh32)
                {
                    // Divide would overflow.  Assume a quotient of 2^32, and set
                    // up remainder accordingly.
                    //
                    num = bufNum.Low64;
                    num -= den << 32;
                    quo = 0;

                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    do
                    {
                        quo--;
                        num += den;
                    } while (num >= den);

                    bufNum.Low64 = num;
                    return quo;
                }

                // Hardware divide won't overflow
                //
                ulong num64 = bufNum.High64;
                if (num64 < denHigh32)
                    // Result is zero.  Entire dividend is remainder.
                    //
                    return 0;

                // TODO: https://github.com/dotnet/coreclr/issues/3439
                quo = (uint)(num64 / denHigh32);
                num = bufNum.U0 | ((num64 - quo * denHigh32) << 32); // remainder

                // Compute full remainder, rem = dividend - (quo * divisor).
                //
                ulong prod = UInt32x32To64(quo, (uint)den); // quo * lo divisor
                num -= prod;

                if (num > ~prod)
                {
                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    do
                    {
                        quo--;
                        num += den;
                    } while (num >= den);
                }

                bufNum.Low64 = num;
                return quo;
            }

            /// <summary>
            /// Do partial divide, yielding 32-bit result and 96-bit remainder.
            /// Top divisor uint must be larger than top dividend uint. This is
            /// assured in the initial call because the divisor is normalized
            /// and the dividend can't be. In subsequent calls, the remainder
            /// is multiplied by 10^9 (max), so it can be no more than 1/4 of
            /// the divisor which is effectively multiplied by 2^32 (4 * 10^9).
            /// </summary>
            /// <param name="bufNum">128-bit dividend as array of uints, least-sig first</param>
            /// <param name="bufDen">96-bit divisor</param>
            /// <returns>Returns quotient. Remainder overwrites lower 96-bits of dividend.</returns>
            private static uint Div128By96(ref Buf16 bufNum, ref Buf12 bufDen)
            {
                Debug.Assert(bufDen.U2 > bufNum.U3);
                ulong dividend = bufNum.High64;
                uint den = bufDen.U2;
                if (dividend < den)
                    // Result is zero.  Entire dividend is remainder.
                    //
                    return 0;

                // TODO: https://github.com/dotnet/coreclr/issues/3439
                uint quo = (uint)(dividend / den);
                uint remainder = (uint)dividend - quo * den;

                // Compute full remainder, rem = dividend - (quo * divisor).
                //
                ulong prod1 = UInt32x32To64(quo, bufDen.U0); // quo * lo divisor
                ulong prod2 = UInt32x32To64(quo, bufDen.U1); // quo * mid divisor
                prod2 += prod1 >> 32;
                prod1 = (uint)prod1 | (prod2 << 32);
                prod2 >>= 32;

                ulong num = bufNum.Low64;
                num -= prod1;
                remainder -= (uint)prod2;

                // Propagate carries
                //
                if (num > ~prod1)
                {
                    remainder--;
                    if (remainder < ~(uint)prod2)
                        goto PosRem;
                }
                else if (remainder <= ~(uint)prod2)
                    goto PosRem;
                {
                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    prod1 = bufDen.Low64;

                    for (;;)
                    {
                        quo--;
                        num += prod1;
                        remainder += den;

                        if (num < prod1)
                        {
                            // Detected carry. Check for carry out of top
                            // before adding it in.
                            //
                            if (remainder++ < den)
                                break;
                        }
                        if (remainder < den)
                            break; // detected carry
                    }
                }
PosRem:

                bufNum.Low64 = num;
                bufNum.U2 = remainder;
                return quo;
            }

            /// <summary>
            /// Multiply the two numbers. The low 96 bits of the result overwrite
            /// the input. The last 32 bits of the product are the return value.
            /// </summary>
            /// <param name="bufNum">96-bit number as array of uints, least-sig first</param>
            /// <param name="power">Scale factor to multiply by</param>
            /// <returns>Returns highest 32 bits of product</returns>
            private static uint IncreaseScale(ref Buf12 bufNum, uint power)
            {
                ulong tmp = UInt32x32To64(bufNum.U0, power);
                bufNum.U0 = (uint)tmp;
                tmp >>= 32;
                tmp += UInt32x32To64(bufNum.U1, power);
                bufNum.U1 = (uint)tmp;
                tmp >>= 32;
                tmp += UInt32x32To64(bufNum.U2, power);
                bufNum.U2 = (uint)tmp;
                return (uint)(tmp >> 32);
            }

            private static void IncreaseScale64(ref Buf12 bufNum, uint power)
            {
                ulong tmp = UInt32x32To64(bufNum.U0, power);
                bufNum.U0 = (uint)tmp;
                tmp >>= 32;
                tmp += UInt32x32To64(bufNum.U1, power);
                bufNum.High64 = tmp;
            }

            /// <summary>
            /// See if we need to scale the result to fit it in 96 bits.
            /// Perform needed scaling. Adjust scale factor accordingly.
            /// </summary>
            /// <param name="bufRes">Array of uints with value, least-significant first</param>
            /// <param name="hiRes">Index of last non-zero value in bufRes
            /// <param name="scale">Scale factor for this value, range 0 - 2 * DEC_SCALE_MAX</param>
            /// <returns>Returns new scale factor. bufRes updated in place, always 3 uints.</returns>
            private static unsafe int ScaleResult(Buf24* bufRes, uint hiRes, int scale)
            {
                Debug.Assert(hiRes < bufRes->Length);
                uint* result = (uint*)bufRes;

                // See if we need to scale the result.  The combined scale must
                // be <= DEC_SCALE_MAX and the upper 96 bits must be zero.
                //
                // Start by figuring a lower bound on the scaling needed to make
                // the upper 96 bits zero.  hiRes is the index into result[]
                // of the highest non-zero uint.
                //
                int newScale = 0;
                if (hiRes > 2)
                {
                    newScale = (int)hiRes * 32 - 64 - 1;
                    newScale -= LeadingZeroCount(result[hiRes]);

                    // Multiply bit position by log10(2) to figure it's power of 10.
                    // We scale the log by 256.  log(2) = .30103, * 256 = 77.  Doing this
                    // with a multiply saves a 96-byte lookup table.  The power returned
                    // is <= the power of the number, so we must add one power of 10
                    // to make it's integer part zero after dividing by 256.
                    //
                    // Note: the result of this multiplication by an approximation of
                    // log10(2) have been exhaustively checked to verify it gives the
                    // correct result.  (There were only 95 to check...)
                    //
                    newScale = ((newScale * 77) >> 8) + 1;

                    // newScale = min scale factor to make high 96 bits zero, 0 - 29.
                    // This reduces the scale factor of the result.  If it exceeds the
                    // current scale of the result, we'll overflow.
                    //
                    if (newScale > scale)
                        goto ThrowOverflow;
                }

                // Make sure we scale by enough to bring the current scale factor
                // into valid range.
                //
                if (newScale < scale - DEC_SCALE_MAX)
                    newScale = scale - DEC_SCALE_MAX;

                if (newScale != 0)
                {
                    // Scale by the power of 10 given by newScale.  Note that this is
                    // NOT guaranteed to bring the number within 96 bits -- it could
                    // be 1 power of 10 short.
                    //
                    scale -= newScale;
                    uint sticky = 0;
                    uint quotient, remainder = 0;

                    for (;;)
                    {
                        sticky |= remainder; // record remainder as sticky bit

                        uint power;
                        // Scaling loop specialized for each power of 10 because division by constant is an order of magnitude faster (especially for 64-bit division that's actually done by 128bit DIV on x64)
                        switch (newScale)
                        {
                            case 1:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 10);
                                break;
                            case 2:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 100);
                                break;
                            case 3:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 1000);
                                break;
                            case 4:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 10000);
                                break;
#if BIT64
                            case 5:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 100000);
                                break;
                            case 6:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 1000000);
                                break;
                            case 7:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 10000000);
                                break;
                            case 8:
                                power = DivByConst(result, hiRes, out quotient, out remainder, 100000000);
                                break;
                            default:
                                power = DivByConst(result, hiRes, out quotient, out remainder, TenToPowerNine);
                                break;
#else
                            default:
                                goto case 4;
#endif
                        }
                        result[hiRes] = quotient;
                        // If first quotient was 0, update hiRes.
                        //
                        if (quotient == 0 && hiRes != 0)
                            hiRes--;

#if BIT64
                        newScale -= MaxInt32Scale;
#else
                        newScale -= 4;
#endif
                        if (newScale > 0)
                            continue; // scale some more

                        // If we scaled enough, hiRes would be 2 or less.  If not,
                        // divide by 10 more.
                        //
                        if (hiRes > 2)
                        {
                            if (scale == 0)
                                goto ThrowOverflow;
                            newScale = 1;
                            scale--;
                            continue; // scale by 10
                        }

                        // Round final result.  See if remainder >= 1/2 of divisor.
                        // If remainder == 1/2 divisor, round up if odd or sticky bit set.
                        //
                        power >>= 1;  // power of 10 always even
                        if (power <= remainder && (power < remainder || ((result[0] & 1) | sticky) != 0) && ++result[0] == 0)
                        {
                            uint cur = 0;
                            do
                            {
                                Debug.Assert(cur + 1 < bufRes->Length);
                            }
                            while (++result[++cur] == 0);

                            if (cur > 2)
                            {
                                // The rounding caused us to carry beyond 96 bits.
                                // Scale by 10 more.
                                //
                                if (scale == 0)
                                    goto ThrowOverflow;
                                hiRes = cur;
                                sticky = 0;    // no sticky bit
                                remainder = 0; // or remainder
                                newScale = 1;
                                scale--;
                                continue; // scale by 10
                            }
                        }

                        break;
                    } // for(;;)
                }
                return scale;

ThrowOverflow:
                throw new OverflowException(SR.Overflow_Decimal);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe uint DivByConst(uint* result, uint hiRes, out uint quotient, out uint remainder, uint power)
            {
                uint high = result[hiRes];
                remainder = high - (quotient = high / power) * power;
                for (uint i = hiRes - 1; (int)i >= 0; i--)
                {
#if BIT64
                    ulong num = result[i] + ((ulong)remainder << 32);
                    remainder = (uint)num - (result[i] = (uint)(num / power)) * power;
#else
                    // 32-bit RyuJIT doesn't convert 64-bit division by constant into multiplication by reciprocal. Do half-width divisions instead.
                    Debug.Assert(power <= ushort.MaxValue);
                    int low16 = BitConverter.IsLittleEndian ? 0 : 2, high16 = BitConverter.IsLittleEndian ? 2 : 0;
                    // byte* is used here because Roslyn doesn't do constant propagation for pointer arithmetic
                    uint num = *(ushort*)((byte*)result + i * 4 + high16) + (remainder << 16);
                    uint div = num / power;
                    remainder = num - div * power;
                    *(ushort*)((byte*)result + i * 4 + high16) = (ushort)div;

                    num = *(ushort*)((byte*)result + i * 4 + low16) + (remainder << 16);
                    div = num / power;
                    remainder = num - div * power;
                    *(ushort*)((byte*)result + i * 4 + low16) = (ushort)div;
#endif
                }
                return power;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int LeadingZeroCount(uint value)
            {
                Debug.Assert(value > 0);
                int c = 1;
                if ((value & 0xFFFF0000) == 0)
                {
                    value <<= 16;
                    c += 16;
                }
                if ((value & 0xFF000000) == 0)
                {
                    value <<= 8;
                    c += 8;
                }
                if ((value & 0xF0000000) == 0)
                {
                    value <<= 4;
                    c += 4;
                }
                if ((value & 0xC0000000) == 0)
                {
                    value <<= 2;
                    c += 2;
                }
                return c + ((int)value >> 31);
            }

            /// <summary>
            /// Adjust the quotient to deal with an overflow.
            /// We need to divide by 10, feed in the high bit to undo the overflow and then round as required.
            /// </summary>
            private static int OverflowUnscale(ref Buf12 bufQuo, int scale, bool sticky)
            {
                if (--scale < 0)
                    throw new OverflowException(SR.Overflow_Decimal);

                Debug.Assert(bufQuo.U2 == 0);

                // We have overflown, so load the high bit with a one.
                const ulong highbit = 1UL << 32;
                bufQuo.U2 = (uint)(highbit / 10);
                ulong tmp = ((highbit % 10) << 32) + bufQuo.U1;
                uint div = (uint)(tmp / 10);
                bufQuo.U1 = div;
                tmp = ((tmp - div * 10) << 32) + bufQuo.U0;
                div = (uint)(tmp / 10);
                bufQuo.U0 = div;
                uint remainder = (uint)(tmp - div * 10);
                // The remainder is the last digit that does not fit, so we can use it to work out if we need to round up
                if (remainder > 5 || remainder == 5 && (sticky || (bufQuo.U0 & 1) != 0))
                    Add32To96(ref bufQuo, 1);
                return scale;
            }

            /// <summary>
            /// Determine the max power of 10, &lt;= 9, that the quotient can be scaled
            /// up by and still fit in 96 bits.
            /// </summary>
            /// <param name="bufQuo">96-bit quotient</param>
            /// <param name="scale ">Scale factor of quotient, range -DEC_SCALE_MAX to DEC_SCALE_MAX-1</param>
            /// <returns>power of 10 to scale by</returns>
            private static int SearchScale(ref Buf12 bufQuo, int scale)
            {
                const uint OVFL_MAX_9_HI = 4;
                const uint OVFL_MAX_8_HI = 42;
                const uint OVFL_MAX_7_HI = 429;
                const uint OVFL_MAX_6_HI = 4294;
                const uint OVFL_MAX_5_HI = 42949;
                const uint OVFL_MAX_4_HI = 429496;
                const uint OVFL_MAX_3_HI = 4294967;
                const uint OVFL_MAX_2_HI = 42949672;
                const uint OVFL_MAX_1_HI = 429496729;
                const ulong OVFL_MAX_9_MIDLO = 5441186219426131129;

                uint resHi = bufQuo.U2;
                ulong resMidLo = bufQuo.Low64;
                int curScale = 0;

                // Quick check to stop us from trying to scale any more.
                //
                if (resHi > OVFL_MAX_1_HI)
                {
                    goto HaveScale;
                }

                var powerOvfl = PowerOvflValues;
                if (scale > DEC_SCALE_MAX - 9)
                {
                    // We can't scale by 10^9 without exceeding the max scale factor.
                    // See if we can scale to the max.  If not, we'll fall into
                    // standard search for scale factor.
                    //
                    curScale = DEC_SCALE_MAX - scale;
                    if (resHi < powerOvfl[curScale - 1].Hi)
                        goto HaveScale;
                }
                else if (resHi < OVFL_MAX_9_HI || resHi == OVFL_MAX_9_HI && resMidLo <= OVFL_MAX_9_MIDLO)
                    return 9;

                // Search for a power to scale by < 9.  Do a binary search.
                //
                if (resHi > OVFL_MAX_5_HI)
                {
                    if (resHi > OVFL_MAX_3_HI)
                    {
                        curScale = 2;
                        if (resHi > OVFL_MAX_2_HI)
                            curScale--;
                    }
                    else
                    {
                        curScale = 4;
                        if (resHi > OVFL_MAX_4_HI)
                            curScale--;
                    }
                }
                else
                {
                    if (resHi > OVFL_MAX_7_HI)
                    {
                        curScale = 6;
                        if (resHi > OVFL_MAX_6_HI)
                            curScale--;
                    }
                    else
                    {
                        curScale = 8;
                        if (resHi > OVFL_MAX_8_HI)
                            curScale--;
                    }
                }

                // In all cases, we already found we could not use the power one larger.
                // So if we can use this power, it is the biggest, and we're done.  If
                // we can't use this power, the one below it is correct for all cases
                // unless it's 10^1 -- we might have to go to 10^0 (no scaling).
                //
                if (resHi == powerOvfl[curScale - 1].Hi && resMidLo > powerOvfl[curScale - 1].MidLo)
                    curScale--;

                HaveScale:
                // curScale = largest power of 10 we can scale by without overflow,
                // curScale < 9.  See if this is enough to make scale factor
                // positive if it isn't already.
                //
                if (curScale + scale < 0)
                    throw new OverflowException(SR.Overflow_Decimal);

                return curScale;
            }

            /// <summary>
            /// Add a 32-bit uint to an array of 3 uints representing a 96-bit integer.
            /// </summary>
            /// <returns>Returns false if there is an overflow</returns>
            private static bool Add32To96(ref Buf12 bufNum, uint value)
            {
                if ((bufNum.Low64 += value) < value)
                {
                    if (++bufNum.U2 == 0)
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Adds or subtracts two decimal values.
            /// On return, d1 contains the result of the operation and d2 is trashed.
            /// </summary>
            /// <param name="sign">True means subtract and false means add.</param>
            internal static unsafe void DecAddSub(ref DecCalc d1, ref DecCalc d2, bool sign)
            {
                ulong low64 = d1.Low64;
                uint high = d1.High, flags = d1.uflags, d2flags = d2.uflags;

                uint xorflags = d2flags ^ flags;
                sign ^= (xorflags & SignMask) != 0;

                if ((xorflags & ScaleMask) == 0)
                {
                    // Scale factors are equal, no alignment necessary.
                    //
                    goto AlignedAdd;
                }
                else
                {
                    // Scale factors are not equal.  Assume that a larger scale
                    // factor (more decimal places) is likely to mean that number
                    // is smaller.  Start by guessing that the right operand has
                    // the larger scale factor.  The result will have the larger
                    // scale factor.
                    //
                    uint d1flags = flags;
                    flags = d2flags & ScaleMask | flags & SignMask; // scale factor of "smaller",  but sign of "larger"
                    int scale = (int)(flags - d1flags) >> ScaleShift;

                    if (scale < 0)
                    {
                        // Guessed scale factor wrong. Swap operands.
                        //
                        scale = -scale;
                        flags = d1flags;
                        if (sign)
                            flags ^= SignMask;
                        low64 = d2.Low64;
                        high = d2.High;
                        d2 = d1;
                    }

                    uint power;
                    ulong tmp64, tmpLow;

                    // d1 will need to be multiplied by 10^scale so
                    // it will have the same scale as d2.  We could be
                    // extending it to up to 192 bits of precision.

                    // Scan for zeros in the upper words.
                    //
                    if (high == 0)
                    {
                        if (low64 <= uint.MaxValue)
                        {
                            if ((uint)low64 == 0)
                            {
                                // Left arg is zero, return right.
                                //
                                uint signFlags = flags & SignMask;
                                if (sign)
                                    signFlags ^= SignMask;
                                d1 = d2;
                                d1.uflags = d2.uflags & ScaleMask | signFlags;
                                return;
                            }

                            do
                            {
                                if (scale <= MaxInt32Scale)
                                {
                                    low64 = UInt32x32To64((uint)low64, s_powers10[scale]);
                                    goto AlignedAdd;
                                }
                                scale -= MaxInt32Scale;
                                low64 = UInt32x32To64((uint)low64, TenToPowerNine);
                            } while (low64 <= uint.MaxValue);
                        }

                        do
                        {
                            power = TenToPowerNine;
                            if (scale < MaxInt32Scale)
                                power = s_powers10[scale];
                            tmpLow = UInt32x32To64((uint)low64, power);
                            tmp64 = UInt32x32To64((uint)(low64 >> 32), power) + (tmpLow >> 32);
                            low64 = (uint)tmpLow + (tmp64 << 32);
                            high = (uint)(tmp64 >> 32);
                            if ((scale -= MaxInt32Scale) <= 0)
                                goto AlignedAdd;
                        } while (high == 0);
                    }

                    while (true)
                    {
                        // Scaling won't make it larger than 4 uints
                        //
                        power = TenToPowerNine;
                        if (scale < MaxInt32Scale)
                            power = s_powers10[scale];
                        tmpLow = UInt32x32To64((uint)low64, power);
                        tmp64 = UInt32x32To64((uint)(low64 >> 32), power) + (tmpLow >> 32);
                        low64 = (uint)tmpLow + (tmp64 << 32);
                        tmp64 >>= 32;
                        tmp64 += UInt32x32To64(high, power);

                        scale -= MaxInt32Scale;
                        if (tmp64 > uint.MaxValue)
                            break;

                        high = (uint)tmp64;
                        // Result fits in 96 bits.  Use standard aligned add.
                        if (scale <= 0)
                            goto AlignedAdd;
                    }

                    // Have to scale by a bunch. Move the number to a buffer where it has room to grow as it's scaled.
                    //
                    Buf24 bufNum;
                    _ = &bufNum; // workaround for CS0165
                    bufNum.Low64 = low64;
                    bufNum.Mid64 = tmp64;
                    uint hiProd = 3;

                    // Scaling loop, up to 10^9 at a time. hiProd stays updated with index of highest non-zero uint.
                    //
                    for (; scale > 0; scale -= MaxInt32Scale)
                    {
                        power = TenToPowerNine;
                        if (scale < MaxInt32Scale)
                            power = s_powers10[scale];
                        tmp64 = 0;
                        uint* rgulNum = (uint*)&bufNum;
                        for (uint cur = 0; ;)
                        {
                            Debug.Assert(cur < bufNum.Length);
                            tmp64 += UInt32x32To64(rgulNum[cur], power);
                            rgulNum[cur] = (uint)tmp64;
                            cur++;
                            tmp64 >>= 32;
                            if (cur > hiProd)
                                break;
                        }

                        if ((uint)tmp64 != 0)
                        {
                            // We're extending the result by another uint.
                            Debug.Assert(hiProd + 1 < bufNum.Length);
                            rgulNum[++hiProd] = (uint)tmp64;
                        }
                    }

                    // Scaling complete, do the add.  Could be subtract if signs differ.
                    //
                    tmp64 = bufNum.Low64;
                    low64 = d2.Low64;
                    uint tmpHigh = bufNum.U2;
                    high = d2.High;

                    if (sign)
                    {
                        // Signs differ, subtract.
                        //
                        low64 = tmp64 - low64;
                        high = tmpHigh - high;

                        // Propagate carry
                        //
                        if (low64 > tmp64)
                        {
                            high--;
                            if (high < tmpHigh)
                                goto NoCarry;
                        }
                        else if (high <= tmpHigh)
                            goto NoCarry;

                        // Carry the subtraction into the higher bits.
                        // 
                        uint* number = (uint*)&bufNum;
                        uint cur = 3;
                        do
                        {
                            Debug.Assert(cur < bufNum.Length);
                        } while (number[cur++]-- == 0);
                        Debug.Assert(hiProd < bufNum.Length);
                        if (number[hiProd] == 0 && --hiProd <= 2)
                            goto ReturnResult;
                    }
                    else
                    {
                        // Signs the same, add.
                        //
                        low64 += tmp64;
                        high += tmpHigh;

                        // Propagate carry
                        //
                        if (low64 < tmp64)
                        {
                            high++;
                            if (high > tmpHigh)
                                goto NoCarry;
                        }
                        else if (high >= tmpHigh)
                            goto NoCarry;

                        uint* number = (uint*)&bufNum;
                        for (uint cur = 3; ++number[cur++] == 0;)
                        {
                            Debug.Assert(cur < bufNum.Length);
                            if (hiProd < cur)
                            {
                                number[cur] = 1;
                                hiProd = cur;
                                break;
                            }
                        }
                    }
NoCarry:

                    bufNum.Low64 = low64;
                    bufNum.U2 = high;
                    scale = ScaleResult(&bufNum, hiProd, (byte)(flags >> ScaleShift));
                    flags = (flags & ~ScaleMask) | ((uint)scale << ScaleShift);
                    low64 = bufNum.Low64;
                    high = bufNum.U2;
                    goto ReturnResult;
                }

SignFlip:
                {
                    // Got negative result.  Flip its sign.
                    flags ^= SignMask;
                    high = ~high;
                    low64 = (ulong)-(long)low64;
                    if (low64 == 0)
                        high++;
                    goto ReturnResult;
                }

AlignedScale:
                {
                    // The addition carried above 96 bits.
                    // Divide the value by 10, dropping the scale factor.
                    //
                    if ((flags & ScaleMask) == 0)
                        throw new OverflowException(SR.Overflow_Decimal);
                    flags -= 1 << ScaleShift;

                    const uint den = 10;
                    ulong num = high + (1UL << 32);
                    high = (uint)(num / den);
                    num = ((num - high * den) << 32) + (low64 >> 32);
                    uint div = (uint)(num / den);
                    num = ((num - div * den) << 32) + (uint)low64;
                    low64 = div;
                    low64 <<= 32;
                    div = (uint)(num / den);
                    low64 += div;
                    div = (uint)num - div * den;

                    // See if we need to round up.
                    //
                    if (div >= 5 && (div > 5 || (low64 & 1) != 0))
                    {
                        if (++low64 == 0)
                            high++;
                    }
                    goto ReturnResult;
                }

AlignedAdd:
                {
                    ulong d1Low64 = low64;
                    uint d1High = high;
                    if (sign)
                    {
                        // Signs differ - subtract
                        //
                        low64 = d1Low64 - d2.Low64;
                        high = d1High - d2.High;

                        // Propagate carry
                        //
                        if (low64 > d1Low64)
                        {
                            high--;
                            if (high >= d1High)
                                goto SignFlip;
                        }
                        else if (high > d1High)
                            goto SignFlip;
                    }
                    else
                    {
                        // Signs are the same - add
                        //
                        low64 = d1Low64 + d2.Low64;
                        high = d1High + d2.High;

                        // Propagate carry
                        //
                        if (low64 < d1Low64)
                        {
                            high++;
                            if (high <= d1High)
                                goto AlignedScale;
                        }
                        else if (high < d1High)
                            goto AlignedScale;
                    }
                    goto ReturnResult;
                }

ReturnResult:
                d1.uflags = flags;
                d1.High = high;
                d1.Low64 = low64;
                return;
            }

#endregion

            /// <summary>
            /// Convert Decimal to Currency (similar to OleAut32 api.)
            /// </summary>
            internal static long VarCyFromDec(ref DecCalc pdecIn)
            {
                long value;

                int scale = pdecIn.Scale - 4;
                // Need to scale to get 4 decimal places.  -4 <= scale <= 24.
                //
                if (scale < 0)
                {
                    if (pdecIn.High != 0)
                        goto ThrowOverflow;
                    uint pwr = s_powers10[-scale];
                    ulong high = UInt32x32To64(pwr, pdecIn.Mid);
                    if (high > uint.MaxValue)
                        goto ThrowOverflow;
                    ulong low = UInt32x32To64(pwr, pdecIn.Low);
                    low += high <<= 32;
                    if (low < high)
                        goto ThrowOverflow;
                    value = (long)low;
                }
                else
                {
                    if (scale != 0)
                        InternalRound(ref pdecIn, (uint)scale, RoundingMode.ToEven);
                    if (pdecIn.High != 0)
                        goto ThrowOverflow;
                    value = (long)pdecIn.Low64;
                }

                if (value < 0 && (value != long.MinValue || !pdecIn.IsNegative))
                    goto ThrowOverflow;

                if (pdecIn.IsNegative)
                    value = -value;

                return value;

ThrowOverflow:
                throw new OverflowException(SR.Overflow_Currency);
            }

            /// <summary>
            /// Decimal Compare updated to return values similar to ICompareTo
            /// </summary>
            internal static int VarDecCmp(in decimal d1, in decimal d2)
            {
                if ((d2.Low | d2.Mid | d2.High) == 0)
                {
                    if ((d1.Low | d1.Mid | d1.High) == 0)
                        return 0;
                    return (d1.flags >> 31) | 1;
                }
                if ((d1.Low | d1.Mid | d1.High) == 0)
                    return -((d2.flags >> 31) | 1);

                int sign = (d1.flags >> 31) - (d2.flags >> 31);
                if (sign != 0)
                    return sign;
                return VarDecCmpSub(in d1, in d2);
            }

            private static int VarDecCmpSub(in decimal d1, in decimal d2)
            {
                int flags = d2.flags;
                int sign = (flags >> 31) | 1;
                int scale = flags - d1.flags;

                ulong low64 = d1.Low64;
                uint high = d1.High;

                ulong d2Low64 = d2.Low64;
                uint d2High = d2.High;

                if (scale != 0)
                {
                    scale >>= ScaleShift;

                    // Scale factors are not equal. Assume that a larger scale factor (more decimal places) is likely to mean that number is smaller.
                    // Start by guessing that the right operand has the larger scale factor.
                    if (scale < 0)
                    {
                        // Guessed scale factor wrong. Swap operands.
                        scale = -scale;
                        sign = -sign;

                        ulong tmp64 = low64;
                        low64 = d2Low64;
                        d2Low64 = tmp64;

                        uint tmp = high;
                        high = d2High;
                        d2High = tmp;
                    }

                    // d1 will need to be multiplied by 10^scale so it will have the same scale as d2.
                    // Scaling loop, up to 10^9 at a time.
                    do
                    {
                        uint power = scale >= MaxInt32Scale ? TenToPowerNine : s_powers10[scale];
                        ulong tmpLow = UInt32x32To64((uint)low64, power);
                        ulong tmp = UInt32x32To64((uint)(low64 >> 32), power) + (tmpLow >> 32);
                        low64 = (uint)tmpLow + (tmp << 32);
                        tmp >>= 32;
                        tmp += UInt32x32To64(high, power);
                        // If the scaled value has more than 96 significant bits then it's greater than d2
                        if (tmp > uint.MaxValue)
                            return sign;
                        high = (uint)tmp;
                    } while ((scale -= MaxInt32Scale) > 0);
                }

                uint cmpHigh = high - d2High;
                if (cmpHigh != 0)
                {
                    // check for overflow
                    if (cmpHigh > high)
                        sign = -sign;
                    return sign;
                }

                ulong cmpLow64 = low64 - d2Low64;
                if (cmpLow64 == 0)
                    sign = 0;
                // check for overflow
                else if (cmpLow64 > low64)
                    sign = -sign;
                return sign;
            }

            /// <summary>
            /// Decimal Multiply
            /// </summary>
            internal static unsafe void VarDecMul(ref DecCalc d1, ref DecCalc d2)
            {
                int scale = (byte)(d1.uflags + d2.uflags >> ScaleShift);

                ulong tmp;
                uint hiProd;
                Buf24 bufProd;
                _ = &bufProd; // workaround for CS0165

                if ((d1.High | d1.Mid) == 0)
                {
                    if ((d2.High | d2.Mid) == 0)
                    {
                        // Upper 64 bits are zero.
                        //
                        ulong low64 = UInt32x32To64(d1.Low, d2.Low);
                        if (scale > DEC_SCALE_MAX)
                        {
                            // Result scale is too big.  Divide result by power of 10 to reduce it.
                            // If the amount to divide by is > 19 the result is guaranteed
                            // less than 1/2.  [max value in 64 bits = 1.84E19]
                            //
                            if (scale > DEC_SCALE_MAX + MaxInt64Scale)
                                goto ReturnZero;

                            scale -= DEC_SCALE_MAX + 1;
                            ulong power = s_ulongPowers10[scale];

                            // TODO: https://github.com/dotnet/coreclr/issues/3439
                            tmp = low64 / power;
                            ulong remainder = low64 - tmp * power;
                            low64 = tmp;

                            // Round result.  See if remainder >= 1/2 of divisor.
                            // Divisor is a power of 10, so it is always even.
                            //
                            power >>= 1;
                            if (remainder >= power && (remainder > power || ((uint)low64 & 1) > 0))
                                low64++;

                            scale = DEC_SCALE_MAX;
                        }
                        d1.Low64 = low64;
                        d1.uflags = ((d2.uflags ^ d1.uflags) & SignMask) | ((uint)scale << ScaleShift);
                        return;
                    }
                    else
                    {
                        // Left value is 32-bit, result fits in 4 uints
                        tmp = UInt32x32To64(d1.Low, d2.Low);
                        bufProd.U0 = (uint)tmp;

                        tmp = UInt32x32To64(d1.Low, d2.Mid) + (tmp >> 32);
                        bufProd.U1 = (uint)tmp;
                        tmp >>= 32;

                        if (d2.High != 0)
                        {
                            tmp += UInt32x32To64(d1.Low, d2.High);
                            if (tmp > uint.MaxValue)
                            {
                                bufProd.Mid64 = tmp;
                                hiProd = 3;
                                goto SkipScan;
                            }
                        }
                        if ((uint)tmp != 0)
                        {
                            bufProd.U2 = (uint)tmp;
                            hiProd = 2;
                            goto SkipScan;
                        }
                        hiProd = 1;
                    }
                }
                else if ((d2.High | d2.Mid) == 0)
                {
                    // Right value is 32-bit, result fits in 4 uints
                    tmp = UInt32x32To64(d2.Low, d1.Low);
                    bufProd.U0 = (uint)tmp;

                    tmp = UInt32x32To64(d2.Low, d1.Mid) + (tmp >> 32);
                    bufProd.U1 = (uint)tmp;
                    tmp >>= 32;

                    if (d1.High != 0)
                    {
                        tmp += UInt32x32To64(d2.Low, d1.High);
                        if (tmp > uint.MaxValue)
                        {
                            bufProd.Mid64 = tmp;
                            hiProd = 3;
                            goto SkipScan;
                        }
                    }
                    if ((uint)tmp != 0)
                    {
                        bufProd.U2 = (uint)tmp;
                        hiProd = 2;
                        goto SkipScan;
                    }
                    hiProd = 1;
                }
                else
                {
                    // Both operands have bits set in the upper 64 bits.
                    //
                    // Compute and accumulate the 9 partial products into a
                    // 192-bit (24-byte) result.
                    //
                    //        [l-h][l-m][l-l]      left high, middle, low
                    //         x    [r-h][r-m][r-l]      right high, middle, low
                    // ------------------------------
                    //
                    //             [0-h][0-l]      l-l * r-l
                    //        [1ah][1al]      l-l * r-m
                    //        [1bh][1bl]      l-m * r-l
                    //       [2ah][2al]          l-m * r-m
                    //       [2bh][2bl]          l-l * r-h
                    //       [2ch][2cl]          l-h * r-l
                    //      [3ah][3al]          l-m * r-h
                    //      [3bh][3bl]          l-h * r-m
                    // [4-h][4-l]              l-h * r-h
                    // ------------------------------
                    // [p-5][p-4][p-3][p-2][p-1][p-0]      prod[] array
                    //

                    tmp = UInt32x32To64(d1.Low, d2.Low);
                    bufProd.U0 = (uint)tmp;

                    ulong tmp2 = UInt32x32To64(d1.Low, d2.Mid) + (tmp >> 32);

                    tmp = UInt32x32To64(d1.Mid, d2.Low);
                    tmp += tmp2; // this could generate carry
                    bufProd.U1 = (uint)tmp;
                    if (tmp < tmp2) // detect carry
                        tmp2 = (tmp >> 32) | (1UL << 32);
                    else
                        tmp2 = tmp >> 32;

                    tmp = UInt32x32To64(d1.Mid, d2.Mid) + tmp2;

                    if ((d1.High | d2.High) > 0)
                    {
                        // Highest 32 bits is non-zero.     Calculate 5 more partial products.
                        //
                        tmp2 = UInt32x32To64(d1.Low, d2.High);
                        tmp += tmp2; // this could generate carry
                        uint tmp3 = 0;
                        if (tmp < tmp2) // detect carry
                            tmp3 = 1;

                        tmp2 = UInt32x32To64(d1.High, d2.Low);
                        tmp += tmp2; // this could generate carry
                        bufProd.U2 = (uint)tmp;
                        if (tmp < tmp2) // detect carry
                            tmp3++;
                        tmp2 = ((ulong)tmp3 << 32) | (tmp >> 32);

                        tmp = UInt32x32To64(d1.Mid, d2.High);
                        tmp += tmp2; // this could generate carry
                        tmp3 = 0;
                        if (tmp < tmp2) // detect carry
                            tmp3 = 1;

                        tmp2 = UInt32x32To64(d1.High, d2.Mid);
                        tmp += tmp2; // this could generate carry
                        bufProd.U3 = (uint)tmp;
                        if (tmp < tmp2) // detect carry
                            tmp3++;
                        tmp = ((ulong)tmp3 << 32) | (tmp >> 32);

                        bufProd.High64 = UInt32x32To64(d1.High, d2.High) + tmp;

                        hiProd = 5;
                    }
                    else if (tmp != 0)
                    {
                        bufProd.Mid64 = tmp;
                        hiProd = 3;
                    }
                    else
                        hiProd = 1;
                }

                // Check for leading zero uints on the product
                //
                uint* product = (uint*)&bufProd;
                while (product[(int)hiProd] == 0)
                {
                    if (hiProd == 0)
                        goto ReturnZero;
                    hiProd--;
                }

SkipScan:
                if (hiProd > 2 || scale > DEC_SCALE_MAX)
                {
                    scale = ScaleResult(&bufProd, hiProd, scale);
                }

                d1.Low64 = bufProd.Low64;
                d1.High = bufProd.U2;
                d1.uflags = ((d2.uflags ^ d1.uflags) & SignMask) | ((uint)scale << ScaleShift);
                return;

ReturnZero:
                d1 = default;
            }

            /// <summary>
            /// Convert float to Decimal
            /// </summary>
            internal static void VarDecFromR4(float input, out DecCalc result)
            {
                result = default;

                // The most we can scale by is 10^28, which is just slightly more
                // than 2^93.  So a float with an exponent of -94 could just
                // barely reach 0.5, but smaller exponents will always round to zero.
                //
                const uint SNGBIAS = 126;
                int exp = (int)(GetExponent(input) - SNGBIAS);
                if (exp < -94)
                    return; // result should be zeroed out

                if (exp > 96)
                    throw new OverflowException(SR.Overflow_Decimal);

                uint flags = 0;
                if (input < 0)
                {
                    input = -input;
                    flags = SignMask;
                }

                // Round the input to a 7-digit integer.  The R4 format has
                // only 7 digits of precision, and we want to keep garbage digits
                // out of the Decimal were making.
                //
                // Calculate max power of 10 input value could have by multiplying
                // the exponent by log10(2).  Using scaled integer multiplcation,
                // log10(2) * 2 ^ 16 = .30103 * 65536 = 19728.3.
                //
                double dbl = input;
                int power = 6 - ((exp * 19728) >> 16);
                // power is between -22 and 35

                if (power >= 0)
                {
                    // We have less than 7 digits, scale input up.
                    //
                    if (power > DEC_SCALE_MAX)
                        power = DEC_SCALE_MAX;

                    dbl *= s_doublePowers10[power];
                }
                else
                {
                    if (power != -1 || dbl >= 1E7)
                        dbl /= s_doublePowers10[-power];
                    else
                        power = 0; // didn't scale it
                }

                Debug.Assert(dbl < 1E7);
                if (dbl < 1E6 && power < DEC_SCALE_MAX)
                {
                    dbl *= 10;
                    power++;
                    Debug.Assert(dbl >= 1E6);
                }

                // Round to integer
                //
                uint mant;
                mant = (uint)(int)dbl;
                dbl -= (int)mant;  // difference between input & integer
                if (dbl > 0.5 || dbl == 0.5 && (mant & 1) != 0)
                    mant++;
                if (mant == 0)
                    return;  // result should be zeroed out

                if (power < 0)
                {
                    // Add -power factors of 10, -power <= (29 - 7) = 22.
                    //
                    power = -power;
                    if (power < 10)
                    {
                        result.Low64 = UInt32x32To64(mant, s_powers10[power]);
                    }
                    else
                    {
                        // Have a big power of 10.
                        //
                        if (power > 18)
                        {
                            ulong low64 = UInt32x32To64(mant, s_powers10[power - 18]);
                            UInt64x64To128(low64, TenToPowerEighteen, ref result);
                        }
                        else
                        {
                            ulong low64 = UInt32x32To64(mant, s_powers10[power - 9]);
                            ulong hi64 = UInt32x32To64(TenToPowerNine, (uint)(low64 >> 32));
                            low64 = UInt32x32To64(TenToPowerNine, (uint)low64);
                            result.Low = (uint)low64;
                            hi64 += low64 >> 32;
                            result.Mid = (uint)hi64;
                            hi64 >>= 32;
                            result.High = (uint)hi64;
                        }
                    }
                }
                else
                {
                    // Factor out powers of 10 to reduce the scale, if possible.
                    // The maximum number we could factor out would be 6.  This
                    // comes from the fact we have a 7-digit number, and the
                    // MSD must be non-zero -- but the lower 6 digits could be
                    // zero.  Note also the scale factor is never negative, so
                    // we can't scale by any more than the power we used to
                    // get the integer.
                    //
                    int lmax = power;
                    if (lmax > 6)
                        lmax = 6;

                    if ((mant & 0xF) == 0 && lmax >= 4)
                    {
                        const uint den = 10000;
                        uint div = mant / den;
                        if (mant == div * den)
                        {
                            mant = div;
                            power -= 4;
                            lmax -= 4;
                        }
                    }

                    if ((mant & 3) == 0 && lmax >= 2)
                    {
                        const uint den = 100;
                        uint div = mant / den;
                        if (mant == div * den)
                        {
                            mant = div;
                            power -= 2;
                            lmax -= 2;
                        }
                    }

                    if ((mant & 1) == 0 && lmax >= 1)
                    {
                        const uint den = 10;
                        uint div = mant / den;
                        if (mant == div * den)
                        {
                            mant = div;
                            power--;
                        }
                    }

                    flags |= (uint)power << ScaleShift;
                    result.Low = mant;
                }

                result.uflags = flags;
            }

            /// <summary>
            /// Convert double to Decimal
            /// </summary>
            internal static void VarDecFromR8(double input, out DecCalc result)
            {
                result = default;

                // The most we can scale by is 10^28, which is just slightly more
                // than 2^93.  So a float with an exponent of -94 could just
                // barely reach 0.5, but smaller exponents will always round to zero.
                //
                const uint DBLBIAS = 1022;
                int exp = (int)(GetExponent(input) - DBLBIAS);
                if (exp < -94)
                    return; // result should be zeroed out

                if (exp > 96)
                    throw new OverflowException(SR.Overflow_Decimal);

                uint flags = 0;
                if (input < 0)
                {
                    input = -input;
                    flags = SignMask;
                }

                // Round the input to a 15-digit integer.  The R8 format has
                // only 15 digits of precision, and we want to keep garbage digits
                // out of the Decimal were making.
                //
                // Calculate max power of 10 input value could have by multiplying
                // the exponent by log10(2).  Using scaled integer multiplcation,
                // log10(2) * 2 ^ 16 = .30103 * 65536 = 19728.3.
                //
                double dbl = input;
                int power = 14 - ((exp * 19728) >> 16);
                // power is between -14 and 43

                if (power >= 0)
                {
                    // We have less than 15 digits, scale input up.
                    //
                    if (power > DEC_SCALE_MAX)
                        power = DEC_SCALE_MAX;

                    dbl *= s_doublePowers10[power];
                }
                else
                {
                    if (power != -1 || dbl >= 1E15)
                        dbl /= s_doublePowers10[-power];
                    else
                        power = 0; // didn't scale it
                }

                Debug.Assert(dbl < 1E15);
                if (dbl < 1E14 && power < DEC_SCALE_MAX)
                {
                    dbl *= 10;
                    power++;
                    Debug.Assert(dbl >= 1E14);
                }

                // Round to int64
                //
                ulong mant;
                mant = (ulong)(long)dbl;
                dbl -= (long)mant;  // difference between input & integer
                if (dbl > 0.5 || dbl == 0.5 && (mant & 1) != 0)
                    mant++;

                if (mant == 0)
                    return;  // result should be zeroed out

                if (power < 0)
                {
                    // Add -power factors of 10, -power <= (29 - 15) = 14.
                    //
                    power = -power;
                    if (power < 10)
                    {
                        var pow10 = s_powers10[power];
                        ulong low64 = UInt32x32To64((uint)mant, pow10);
                        ulong hi64 = UInt32x32To64((uint)(mant >> 32), pow10);
                        result.Low = (uint)low64;
                        hi64 += low64 >> 32;
                        result.Mid = (uint)hi64;
                        hi64 >>= 32;
                        result.High = (uint)hi64;
                    }
                    else
                    {
                        // Have a big power of 10.
                        //
                        Debug.Assert(power <= 14);
                        UInt64x64To128(mant, s_ulongPowers10[power - 1], ref result);
                    }
                }
                else
                {
                    // Factor out powers of 10 to reduce the scale, if possible.
                    // The maximum number we could factor out would be 14.  This
                    // comes from the fact we have a 15-digit number, and the
                    // MSD must be non-zero -- but the lower 14 digits could be
                    // zero.  Note also the scale factor is never negative, so
                    // we can't scale by any more than the power we used to
                    // get the integer.
                    //
                    int lmax = power;
                    if (lmax > 14)
                        lmax = 14;

                    if ((byte)mant == 0 && lmax >= 8)
                    {
                        const uint den = 100000000;
                        ulong div = mant / den;
                        if ((uint)mant == (uint)(div * den))
                        {
                            mant = div;
                            power -= 8;
                            lmax -= 8;
                        }
                    }

                    if (((uint)mant & 0xF) == 0 && lmax >= 4)
                    {
                        const uint den = 10000;
                        ulong div = mant / den;
                        if ((uint)mant == (uint)(div * den))
                        {
                            mant = div;
                            power -= 4;
                            lmax -= 4;
                        }
                    }

                    if (((uint)mant & 3) == 0 && lmax >= 2)
                    {
                        const uint den = 100;
                        ulong div = mant / den;
                        if ((uint)mant == (uint)(div * den))
                        {
                            mant = div;
                            power -= 2;
                            lmax -= 2;
                        }
                    }

                    if (((uint)mant & 1) == 0 && lmax >= 1)
                    {
                        const uint den = 10;
                        ulong div = mant / den;
                        if ((uint)mant == (uint)(div * den))
                        {
                            mant = div;
                            power--;
                        }
                    }

                    flags |= (uint)power << ScaleShift;
                    result.Low64 = mant;
                }

                result.uflags = flags;
            }

            /// <summary>
            /// Convert Decimal to float
            /// </summary>
            internal static float VarR4FromDec(in decimal value)
            {
                return (float)VarR8FromDec(in value);
            }

            /// <summary>
            /// Convert Decimal to double
            /// </summary>
            internal static double VarR8FromDec(in decimal value)
            {
                // Value taken via reverse engineering the double that corresponds to 2^64. (oleaut32 has ds2to64 = DEFDS(0, 0, DBLBIAS + 65, 0))
                const double ds2to64 = 1.8446744073709552e+019;

                double dbl = ((double)value.Low64 +
                    (double)value.High * ds2to64) / s_doublePowers10[value.Scale];

                if (value.IsNegative)
                    dbl = -dbl;

                return dbl;
            }

            internal static int GetHashCode(in decimal d)
            {
                if ((d.Low | d.Mid | d.High) == 0)
                    return 0;

                uint flags = (uint)d.flags;
                if ((flags & ScaleMask) == 0 || (d.Low & 1) != 0)
                    return (int)(flags ^ d.High ^ d.Mid ^ d.Low);

                int scale = (byte)(flags >> ScaleShift);
                uint low = d.Low;
                ulong high64 = ((ulong)d.High << 32) | d.Mid;

                Unscale(ref low, ref high64, ref scale);

                flags = ((flags) & ~ScaleMask) | (uint)scale << ScaleShift;
                return (int)(flags ^ (uint)(high64 >> 32) ^ (uint)high64 ^ low);
            }

            /// <summary>
            /// Divides two decimal values.
            /// On return, d1 contains the result of the operation.
            /// </summary>
            internal static unsafe void VarDecDiv(ref DecCalc d1, ref DecCalc d2)
            {
                Buf12 bufQuo;
                _ = &bufQuo; // workaround for CS0165
                uint power;
                int curScale;

                int scale = (sbyte)(d1.uflags - d2.uflags >> ScaleShift);
                bool unscale = false;
                uint tmp;

                if ((d2.High | d2.Mid) == 0)
                {
                    // Divisor is only 32 bits.  Easy divide.
                    //
                    uint den = d2.Low;
                    if (den == 0)
                        throw new DivideByZeroException();

                    bufQuo.Low64 = d1.Low64;
                    bufQuo.U2 = d1.High;
                    uint remainder = Div96By32(ref bufQuo, den);

                    for (;;)
                    {
                        if (remainder == 0)
                        {
                            if (scale < 0)
                            {
                                curScale = Math.Min(9, -scale);
                                goto HaveScale;
                            }
                            break;
                        }

                        // We need to unscale if and only if we have a non-zero remainder
                        unscale = true;

                        // We have computed a quotient based on the natural scale
                        // ( <dividend scale> - <divisor scale> ).  We have a non-zero
                        // remainder, so now we should increase the scale if possible to
                        // include more quotient bits.
                        //
                        // If it doesn't cause overflow, we'll loop scaling by 10^9 and
                        // computing more quotient bits as long as the remainder stays
                        // non-zero.  If scaling by that much would cause overflow, we'll
                        // drop out of the loop and scale by as much as we can.
                        //
                        // Scaling by 10^9 will overflow if bufQuo[2].bufQuo[1] >= 2^32 / 10^9
                        // = 4.294 967 296.  So the upper limit is bufQuo[2] == 4 and
                        // bufQuo[1] == 0.294 967 296 * 2^32 = 1,266,874,889.7+.  Since
                        // quotient bits in bufQuo[0] could be all 1's, then 1,266,874,888
                        // is the largest value in bufQuo[1] (when bufQuo[2] == 4) that is
                        // assured not to overflow.
                        //
                        if (scale == DEC_SCALE_MAX || (curScale = SearchScale(ref bufQuo, scale)) == 0)
                        {
                            // No more scaling to be done, but remainder is non-zero.
                            // Round quotient.
                            //
                            tmp = remainder << 1;
                            if (tmp < remainder || tmp >= den && (tmp > den || (bufQuo.U0 & 1) != 0))
                                goto RoundUp;
                            break;
                        }

                        HaveScale:
                        power = s_powers10[curScale];
                        scale += curScale;

                        if (IncreaseScale(ref bufQuo, power) != 0)
                            goto ThrowOverflow;

                        ulong num = UInt32x32To64(remainder, power);
                        // TODO: https://github.com/dotnet/coreclr/issues/3439
                        uint div = (uint)(num / den);
                        remainder = (uint)num - div * den;

                        if (!Add32To96(ref bufQuo, div))
                        {
                            scale = OverflowUnscale(ref bufQuo, scale, remainder != 0);
                            break;
                        }
                    } // for (;;)
                }
                else
                {
                    // Divisor has bits set in the upper 64 bits.
                    //
                    // Divisor must be fully normalized (shifted so bit 31 of the most
                    // significant uint is 1).  Locate the MSB so we know how much to
                    // normalize by.  The dividend will be shifted by the same amount so
                    // the quotient is not changed.
                    //
                    tmp = d2.High;
                    if (tmp == 0)
                        tmp = d2.Mid;

                    curScale = LeadingZeroCount(tmp);

                    // Shift both dividend and divisor left by curScale.
                    //
                    Buf16 bufRem;
                    _ = &bufRem; // workaround for CS0165
                    bufRem.Low64 = d1.Low64 << curScale;
                    bufRem.High64 = (d1.Mid + ((ulong)d1.High << 32)) >> (32 - curScale);

                    ulong divisor = d2.Low64 << curScale;

                    if (d2.High == 0)
                    {
                        // Have a 64-bit divisor in sdlDivisor.  The remainder
                        // (currently 96 bits spread over 4 uints) will be < divisor.
                        //

                        bufQuo.U1 = Div96By64(ref *(Buf12*)&bufRem.U1, divisor);
                        bufQuo.U0 = Div96By64(ref *(Buf12*)&bufRem, divisor);

                        for (;;)
                        {
                            if (bufRem.Low64 == 0)
                            {
                                if (scale < 0)
                                {
                                    curScale = Math.Min(9, -scale);
                                    goto HaveScale64;
                                }
                                break;
                            }

                            // We need to unscale if and only if we have a non-zero remainder
                            unscale = true;

                            // Remainder is non-zero.  Scale up quotient and remainder by
                            // powers of 10 so we can compute more significant bits.
                            //
                            if (scale == DEC_SCALE_MAX || (curScale = SearchScale(ref bufQuo, scale)) == 0)
                            {
                                // No more scaling to be done, but remainder is non-zero.
                                // Round quotient.
                                //
                                ulong tmp64 = bufRem.Low64;
                                if ((long)tmp64 < 0 || (tmp64 <<= 1) > divisor ||
                                  (tmp64 == divisor && (bufQuo.U0 & 1) != 0))
                                    goto RoundUp;
                                break;
                            }

                            HaveScale64:
                            power = s_powers10[curScale];
                            scale += curScale;

                            if (IncreaseScale(ref bufQuo, power) != 0)
                                goto ThrowOverflow;

                            IncreaseScale64(ref *(Buf12*)&bufRem, power);
                            tmp = Div96By64(ref *(Buf12*)&bufRem, divisor);
                            if (!Add32To96(ref bufQuo, tmp))
                            {
                                scale = OverflowUnscale(ref bufQuo, scale, bufRem.Low64 != 0);
                                break;
                            }
                        } // for (;;)
                    }
                    else
                    {
                        // Have a 96-bit divisor in bufDivisor.
                        //
                        // Start by finishing the shift left by curScale.
                        //
                        Buf12 bufDivisor;
                        _ = &bufDivisor; // workaround for CS0165
                        bufDivisor.Low64 = divisor;
                        bufDivisor.U2 = (uint)((d2.Mid + ((ulong)d2.High << 32)) >> (32 - curScale));

                        // The remainder (currently 96 bits spread over 4 uints) will be < divisor.
                        //
                        bufQuo.Low64 = Div128By96(ref bufRem, ref bufDivisor);

                        for (;;)
                        {
                            if ((bufRem.Low64 | bufRem.U2) == 0)
                            {
                                if (scale < 0)
                                {
                                    curScale = Math.Min(9, -scale);
                                    goto HaveScale96;
                                }
                                break;
                            }

                            // We need to unscale if and only if we have a non-zero remainder
                            unscale = true;

                            // Remainder is non-zero.  Scale up quotient and remainder by
                            // powers of 10 so we can compute more significant bits.
                            //
                            if (scale == DEC_SCALE_MAX || (curScale = SearchScale(ref bufQuo, scale)) == 0)
                            {
                                // No more scaling to be done, but remainder is non-zero.
                                // Round quotient.
                                //
                                if ((int)bufRem.U2 < 0)
                                {
                                    goto RoundUp;
                                }

                                tmp = bufRem.U1 >> 31;
                                bufRem.Low64 <<= 1;
                                bufRem.U2 = (bufRem.U2 << 1) + tmp;

                                if (bufRem.U2 > bufDivisor.U2 || bufRem.U2 == bufDivisor.U2 &&
                                  (bufRem.Low64 > bufDivisor.Low64 || bufRem.Low64 == bufDivisor.Low64 &&
                                  (bufQuo.U0 & 1) != 0))
                                    goto RoundUp;
                                break;
                            }

                            HaveScale96:
                            power = s_powers10[curScale];
                            scale += curScale;

                            if (IncreaseScale(ref bufQuo, power) != 0)
                                goto ThrowOverflow;

                            bufRem.U3 = IncreaseScale(ref *(Buf12*)&bufRem, power);
                            tmp = Div128By96(ref bufRem, ref bufDivisor);
                            if (!Add32To96(ref bufQuo, tmp))
                            {
                                scale = OverflowUnscale(ref bufQuo, scale, (bufRem.Low64 | bufRem.High64) != 0);
                                break;
                            }
                        } // for (;;)
                    }
                }

Unscale:
                if (unscale)
                {
                    uint low = bufQuo.U0;
                    ulong high64 = bufQuo.High64;
                    Unscale(ref low, ref high64, ref scale);
                    d1.Low = low;
                    d1.Mid = (uint)high64;
                    d1.High = (uint)(high64 >> 32);
                }
                else
                {
                    d1.Low64 = bufQuo.Low64;
                    d1.High = bufQuo.U2;
                }

                d1.uflags = ((d1.uflags ^ d2.uflags) & SignMask) | ((uint)scale << ScaleShift);
                return;

RoundUp:
                {
                    if (++bufQuo.Low64 == 0 && ++bufQuo.U2 == 0)
                    {
                        scale = OverflowUnscale(ref bufQuo, scale, true);
                    }
                    goto Unscale;
                }

ThrowOverflow:
                throw new OverflowException(SR.Overflow_Decimal);
            }

            /// <summary>
            /// Computes the remainder between two decimals.
            /// On return, d1 contains the result of the operation and d2 is trashed.
            /// </summary>
            internal static void VarDecMod(ref DecCalc d1, ref DecCalc d2)
            {
                if ((d2.ulo | d2.umid | d2.uhi) == 0)
                    throw new DivideByZeroException();

                if ((d1.ulo | d1.umid | d1.uhi) == 0)
                    return;

                // In the operation x % y the sign of y does not matter. Result will have the sign of x.
                d2.uflags = (d2.uflags & ~SignMask) | (d1.uflags & SignMask);

                int cmp = VarDecCmpSub(in Unsafe.As<DecCalc, decimal>(ref d1), in Unsafe.As<DecCalc, decimal>(ref d2));
                if (cmp == 0)
                {
                    d1.ulo = 0;
                    d1.umid = 0;
                    d1.uhi = 0;
                    if (d2.uflags > d1.uflags)
                        d1.uflags = d2.uflags;
                    return;
                }
                if ((cmp ^ (int)(d1.uflags & SignMask)) < 0)
                    return;

                // The divisor is smaller than the dividend and both are non-zero. Calculate the integer remainder using the larger scaling factor. 

                int scale = (sbyte)(d1.uflags - d2.uflags >> ScaleShift);
                if (scale > 0)
                {
                    // Divisor scale can always be increased to dividend scale for remainder calculation.
                    do
                    {
                        uint power = scale >= MaxInt32Scale ? TenToPowerNine : s_powers10[scale];
                        ulong tmp = UInt32x32To64(d2.Low, power);
                        d2.Low = (uint)tmp;
                        tmp >>= 32;
                        tmp += (d2.Mid + ((ulong)d2.High << 32)) * power;
                        d2.Mid = (uint)tmp;
                        d2.High = (uint)(tmp >> 32);
                    } while ((scale -= MaxInt32Scale) > 0);
                    scale = 0;
                }

                do
                {
                    if (scale < 0)
                    {
                        d1.uflags = d2.uflags;
                        // Try to scale up dividend to match divisor.
                        Buf12 bufQuo;
                        unsafe
                        { _ = &bufQuo; } // workaround for CS0165
                        bufQuo.Low64 = d1.Low64;
                        bufQuo.U2 = d1.High;
                        do
                        {
                            int iCurScale = SearchScale(ref bufQuo, DEC_SCALE_MAX + scale);
                            if (iCurScale == 0)
                                break;
                            uint power = iCurScale >= MaxInt32Scale ? TenToPowerNine : s_powers10[iCurScale];
                            scale += iCurScale;
                            ulong tmp = UInt32x32To64(bufQuo.U0, power);
                            bufQuo.U0 = (uint)tmp;
                            tmp >>= 32;
                            bufQuo.High64 = tmp + bufQuo.High64 * power;
                            if (power != TenToPowerNine)
                                break;
                        }
                        while (scale < 0);
                        d1.Low64 = bufQuo.Low64;
                        d1.High = bufQuo.U2;
                    }

                    if (d1.High == 0)
                    {
                        Debug.Assert(d2.High == 0);
                        Debug.Assert(scale == 0);
                        d1.Low64 %= d2.Low64;
                        return;
                    }
                    else if ((d2.High | d2.Mid) == 0)
                    {
                        uint den = d2.Low;
                        ulong tmp = ((ulong)d1.High << 32) | d1.Mid;
                        tmp = ((tmp % den) << 32) | d1.Low;
                        d1.Low64 = tmp % den;
                        d1.High = 0;
                    }
                    else
                    {
                        VarDecModFull(ref d1, ref d2, scale);
                        return;
                    }
                } while (scale < 0);
            }

            private static unsafe void VarDecModFull(ref DecCalc d1, ref DecCalc d2, int scale)
            {
                // Divisor has bits set in the upper 64 bits.
                //
                // Divisor must be fully normalized (shifted so bit 31 of the most significant uint is 1). 
                // Locate the MSB so we know how much to normalize by. 
                // The dividend will be shifted by the same amount so the quotient is not changed.
                //
                uint tmp = d2.High;
                if (tmp == 0)
                    tmp = d2.Mid;
                int shift = LeadingZeroCount(tmp);

                Buf28 b;
                _ = &b; // workaround for CS0165
                b.Buf24.Low64 = d1.Low64 << shift;
                b.Buf24.Mid64 = (d1.Mid + ((ulong)d1.High << 32)) >> (32 - shift);

                // The dividend might need to be scaled up to 221 significant bits.
                // Maximum scaling is required when the divisor is 2^64 with scale 28 and is left shifted 31 bits
                // and the dividend is decimal.MaxValue: (2^96 - 1) * 10^28 << 31 = 221 bits.
                uint high = 3;
                while (scale < 0)
                {
                    uint power = scale <= -MaxInt32Scale ? TenToPowerNine : s_powers10[-scale];
                    uint* buf = (uint*)&b;
                    ulong tmp64 = UInt32x32To64(b.Buf24.U0, power);
                    b.Buf24.U0 = (uint)tmp64;
                    for (int i = 1; i <= high; i++)
                    {
                        tmp64 >>= 32;
                        tmp64 += UInt32x32To64(buf[i], power);
                        buf[i] = (uint)tmp64;
                    }
                    // The high bit of the dividend must not be set.
                    if (tmp64 > int.MaxValue)
                    {
                        Debug.Assert(high + 1 < b.Length);
                        buf[++high] = (uint)(tmp64 >> 32);
                    }

                    scale += MaxInt32Scale;
                }

                if (d2.High == 0)
                {
                    ulong divisor = d2.Low64 << shift;
                    switch (high)
                    {
                        case 6:
                            Div96By64(ref *(Buf12*)&b.Buf24.U4, divisor);
                            goto case 5;
                        case 5:
                            Div96By64(ref *(Buf12*)&b.Buf24.U3, divisor);
                            goto case 4;
                        case 4:
                            Div96By64(ref *(Buf12*)&b.Buf24.U2, divisor);
                            break;
                    }
                    Div96By64(ref *(Buf12*)&b.Buf24.U1, divisor);
                    Div96By64(ref *(Buf12*)&b, divisor);

                    d1.Low64 = b.Buf24.Low64 >> shift;
                    d1.High = 0;
                }
                else
                {
                    Buf12 bufDivisor;
                    _ = &bufDivisor; // workaround for CS0165
                    bufDivisor.Low64 = d2.Low64 << shift;
                    bufDivisor.U2 = (uint)((d2.Mid + ((ulong)d2.High << 32)) >> (32 - shift));

                    switch (high)
                    {
                        case 6:
                            Div128By96(ref *(Buf16*)&b.Buf24.U3, ref bufDivisor);
                            goto case 5;
                        case 5:
                            Div128By96(ref *(Buf16*)&b.Buf24.U2, ref bufDivisor);
                            goto case 4;
                        case 4:
                            Div128By96(ref *(Buf16*)&b.Buf24.U1, ref bufDivisor);
                            break;
                    }
                    Div128By96(ref *(Buf16*)&b, ref bufDivisor);

                    d1.Low64 = (b.Buf24.Low64 >> shift) + ((ulong)b.Buf24.U2 << (32 - shift) << 32);
                    d1.High = b.Buf24.U2 >> shift;
                }
            }

            internal enum RoundingMode
            {
                ToEven = 0,
                AwayFromZero = 1,
                Truncate = 2,
                Floor = 3,
                Ceiling = 4,
            }

            /// <summary>
            /// Does an in-place round by the specified scale
            /// </summary>
            internal static void InternalRound(ref DecCalc d, uint scale, RoundingMode mode)
            {
                // the scale becomes the desired decimal count
                d.uflags -= scale << ScaleShift;

                uint remainder, sticky = 0, power;
                // First divide the value by constant 10^9 up to three times
                while (scale >= MaxInt32Scale)
                {
                    scale -= MaxInt32Scale;

                    const uint divisor = TenToPowerNine;
                    uint n = d.uhi;
                    if (n == 0)
                    {
                        ulong tmp = d.Low64;
                        ulong div = tmp / divisor;
                        d.Low64 = div;
                        remainder = (uint)(tmp - div * divisor);
                    }
                    else
                    {
                        uint q;
                        d.uhi = q = n / divisor;
                        remainder = n - q * divisor;
                        n = d.umid;
                        if ((n | remainder) != 0)
                        {
                            d.umid = q = (uint)((((ulong)remainder << 32) | n) / divisor);
                            remainder = n - q * divisor;
                        }
                        n = d.ulo;
                        if ((n | remainder) != 0)
                        {
                            d.ulo = q = (uint)((((ulong)remainder << 32) | n) / divisor);
                            remainder = n - q * divisor;
                        }
                    }
                    power = divisor;
                    if (scale == 0)
                        goto checkRemainder;
                    sticky |= remainder;
                }

                {
                    power = s_powers10[scale];
                    // TODO: https://github.com/dotnet/coreclr/issues/3439
                    uint n = d.uhi;
                    if (n == 0)
                    {
                        ulong tmp = d.Low64;
                        if (tmp == 0)
                        {
                            if (mode <= RoundingMode.Truncate)
                                goto done;
                            remainder = 0;
                            goto checkRemainder;
                        }
                        ulong div = tmp / power;
                        d.Low64 = div;
                        remainder = (uint)(tmp - div * power);
                    }
                    else
                    {
                        uint q;
                        d.uhi = q = n / power;
                        remainder = n - q * power;
                        n = d.umid;
                        if ((n | remainder) != 0)
                        {
                            d.umid = q = (uint)((((ulong)remainder << 32) | n) / power);
                            remainder = n - q * power;
                        }
                        n = d.ulo;
                        if ((n | remainder) != 0)
                        {
                            d.ulo = q = (uint)((((ulong)remainder << 32) | n) / power);
                            remainder = n - q * power;
                        }
                    }
                }

checkRemainder:
                if (mode == RoundingMode.Truncate)
                    goto done;
                else if (mode == RoundingMode.ToEven)
                {
                    // To do IEEE rounding, we add LSB of result to sticky bits so either causes round up if remainder * 2 == last divisor.
                    remainder <<= 1;
                    if ((sticky | d.ulo & 1) != 0)
                        remainder++;
                    if (power >= remainder)
                        goto done;
                }
                else if (mode == RoundingMode.AwayFromZero)
                {
                    // Round away from zero at the mid point.
                    remainder <<= 1;
                    if (power > remainder)
                        goto done;
                }
                else if (mode == RoundingMode.Floor)
                {
                    // Round toward -infinity if we have chopped off a non-zero amount from a negative value.
                    if ((remainder | sticky) == 0 || !d.IsNegative)
                        goto done;
                }
                else
                {
                    Debug.Assert(mode == RoundingMode.Ceiling);
                    // Round toward infinity if we have chopped off a non-zero amount from a positive value.
                    if ((remainder | sticky) == 0 || d.IsNegative)
                        goto done;
                }
                if (++d.Low64 == 0)
                    d.uhi++;
done:
                return;
            }

            internal static uint DecDivMod1E9(ref DecCalc value)
            {
                ulong high64 = ((ulong)value.uhi << 32) + value.umid;
                ulong div64 = high64 / TenToPowerNine;
                value.uhi = (uint)(div64 >> 32);
                value.umid = (uint)div64;

                ulong num = ((high64 - (uint)div64 * TenToPowerNine) << 32) + value.ulo;
                uint div = (uint)(num / TenToPowerNine);
                value.ulo = div;
                return (uint)num - div * TenToPowerNine;
            }

            struct PowerOvfl
            {
                public readonly uint Hi;
                public readonly ulong MidLo;

                public PowerOvfl(uint hi, uint mid, uint lo)
                {
                    Hi = hi;
                    MidLo = ((ulong)mid << 32) + lo;
                }
            }

            static readonly PowerOvfl[] PowerOvflValues = new[]
            {
                // This is a table of the largest values that can be in the upper two
                // uints of a 96-bit number that will not overflow when multiplied
                // by a given power.  For the upper word, this is a table of
                // 2^32 / 10^n for 1 <= n <= 8.  For the lower word, this is the
                // remaining fraction part * 2^32.  2^32 = 4294967296.
                //
                new PowerOvfl(429496729, 2576980377, 2576980377),  // 10^1 remainder 0.6
                new PowerOvfl(42949672,  4123168604, 687194767),   // 10^2 remainder 0.16
                new PowerOvfl(4294967,   1271310319, 2645699854),  // 10^3 remainder 0.616
                new PowerOvfl(429496,    3133608139, 694066715),   // 10^4 remainder 0.1616
                new PowerOvfl(42949,     2890341191, 2216890319),  // 10^5 remainder 0.51616
                new PowerOvfl(4294,      4154504685, 2369172679),  // 10^6 remainder 0.551616
                new PowerOvfl(429,       2133437386, 4102387834),  // 10^7 remainder 0.9551616
                new PowerOvfl(42,        4078814305, 410238783),   // 10^8 remainder 0.09991616
            };

            [StructLayout(LayoutKind.Explicit)]
            private struct Buf12
            {
                [FieldOffset(0 * 4)]
                public uint U0;
                [FieldOffset(1 * 4)]
                public uint U1;
                [FieldOffset(2 * 4)]
                public uint U2;

                [FieldOffset(0)]
                private ulong ulo64LE;
                [FieldOffset(4)]
                private ulong uhigh64LE;

                public ulong Low64
                {
                    get => BitConverter.IsLittleEndian ? ulo64LE : (((ulong)U1 << 32) | U0);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            ulo64LE = value;
                        }
                        else
                        {
                            U1 = (uint)(value >> 32);
                            U0 = (uint)value;
                        }
                    }
                }

                /// <summary>
                /// U1-U2 combined (overlaps with Low64)
                /// </summary>
                public ulong High64
                {
                    get => BitConverter.IsLittleEndian ? uhigh64LE : (((ulong)U2 << 32) | U1);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            uhigh64LE = value;
                        }
                        else
                        {
                            U2 = (uint)(value >> 32);
                            U1 = (uint)value;
                        }
                    }
                }
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct Buf16
            {
                [FieldOffset(0 * 4)]
                public uint U0;
                [FieldOffset(1 * 4)]
                public uint U1;
                [FieldOffset(2 * 4)]
                public uint U2;
                [FieldOffset(3 * 4)]
                public uint U3;

                [FieldOffset(0 * 8)]
                private ulong ulo64LE;
                [FieldOffset(1 * 8)]
                private ulong uhigh64LE;

                public ulong Low64
                {
                    get => BitConverter.IsLittleEndian ? ulo64LE : (((ulong)U1 << 32) | U0);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            ulo64LE = value;
                        }
                        else
                        {
                            U1 = (uint)(value >> 32);
                            U0 = (uint)value;
                        }
                    }
                }

                public ulong High64
                {
                    get => BitConverter.IsLittleEndian ? uhigh64LE : (((ulong)U3 << 32) | U2);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            uhigh64LE = value;
                        }
                        else
                        {
                            U3 = (uint)(value >> 32);
                            U2 = (uint)value;
                        }
                    }
                }
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct Buf24
            {
                [FieldOffset(0 * 4)]
                public uint U0;
                [FieldOffset(1 * 4)]
                public uint U1;
                [FieldOffset(2 * 4)]
                public uint U2;
                [FieldOffset(3 * 4)]
                public uint U3;
                [FieldOffset(4 * 4)]
                public uint U4;
                [FieldOffset(5 * 4)]
                public uint U5;

                [FieldOffset(0 * 8)]
                private ulong ulo64LE;
                [FieldOffset(1 * 8)]
                private ulong umid64LE;
                [FieldOffset(2 * 8)]
                private ulong uhigh64LE;

                public ulong Low64
                {
                    get => BitConverter.IsLittleEndian ? ulo64LE : (((ulong)U1 << 32) | U0);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            ulo64LE = value;
                        }
                        else
                        {
                            U1 = (uint)(value >> 32);
                            U0 = (uint)value;
                        }
                    }
                }

                public ulong Mid64
                {
                    get => BitConverter.IsLittleEndian ? umid64LE : (((ulong)U3 << 32) | U2);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            umid64LE = value;
                        }
                        else
                        {
                            U3 = (uint)(value >> 32);
                            U2 = (uint)value;
                        }
                    }
                }

                public ulong High64
                {
                    get => BitConverter.IsLittleEndian ? uhigh64LE : (((ulong)U5 << 32) | U4);
                    set
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            uhigh64LE = value;
                        }
                        else
                        {
                            U5 = (uint)(value >> 32);
                            U4 = (uint)value;
                        }
                    }
                }

                public int Length => 6;
            }

            private struct Buf28
            {
                public Buf24 Buf24;
                public uint U6;

                public int Length => 7;
            }
        }
#endif
    }
}
