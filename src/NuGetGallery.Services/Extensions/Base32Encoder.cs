// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Infrastructure.Authentication
{
    public static class Base32Encoder
    {
        private const byte Bitmask = 0x1F;
        private static readonly char[] _encodingChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567=".ToCharArray();
        private static readonly byte PaddingCharacterIndex = (byte)Array.IndexOf(_encodingChars, '=');
        private const char PaddingChar = '=';
        private static readonly string _paddingstring = new string(PaddingChar, 1);

        public static string ToBase32String(this byte[] data)
        {
            return Encode(data);
        }

        public static bool TryParseBase32String(this string base32String, out byte[] result)
        {
            try
            {
                result = Decode(base32String);
                return true;
            }
            catch (ArgumentException)
            {
                result = Array.Empty<byte>();
                return false;
            }
        }

        public static byte[] FromBase32String(this string base32String)
        {
            return Decode(base32String);
        }

        public static string RemoveBase32Padding(this string base32String)
        {
            return base32String.Replace(_paddingstring, string.Empty);
        }

        public static string AppendBase32Padding(this string input)
        {
            int requiredPaddingCount = 8 - input.Length % 8;
            string padding = new string(PaddingChar, requiredPaddingCount);
            return input + padding;
        }

        /// <summary>
        /// Encodes a byte array into Base32 (RFC 4648)
        /// </summary>
        public static string Encode(byte[] data)
        {
            if (data == null)
            {
                throw new NullReferenceException(nameof(data));
            }

            int ncTokens = GetTokenCount(data);

            char[] output = new char[ncTokens];
            for (int i = 0; i < ncTokens; i++)
            {
                output[i] = GetToken(data, i);
            }

            return new string(output);
        }

        /// <summary>
        /// Decodes a Base32 string (RFC 4648) into its original byte array
        /// </summary>
        public static byte[] Decode(string base32String)
        {
            if (base32String == null)
            {
                throw new NullReferenceException(nameof(base32String));
            }

            // Validate base32 format
            if (base32String.Length % 8 != 0)
            {
                throw new ArgumentException($"{nameof(base32String)} is not a valid base32 encoding");
            }

            // Initialized with all zeros
            byte[] output = new byte[GetByteCount(base32String)];

            int bitLocation = 0;

            foreach (Char c in base32String)
            {
                int byteOffset = bitLocation / 8;
                int bitOffset = bitLocation % 8;

                int index = Array.IndexOf(_encodingChars, c);

                if (index == -1)
                {
                    throw new ArgumentException($"{nameof(base32String)} is not a valid base32 encoding");
                }

                byte val = (byte)index;

                // If we hit an equals sign, we need to stop processing
                if (val == PaddingCharacterIndex) { break; }

                // Locate bits in val correcty respective to the byte
                int shift = 3 - bitOffset;
                if (shift < 0)
                {
                    output[byteOffset] |= (byte)(val >> (-shift));
                }
                else
                {
                    output[byteOffset] |= (byte)(val << shift);
                }

                if ((shift < 0) && (byteOffset < output.Length - 1))
                {
                    // remaining bits go into next byte
                    output[byteOffset + 1] |= (byte)(val << (8 + shift));
                }

                bitLocation += 5;
            }

            // truncate array to actual length (will rarely do anything)
            Array.Resize<byte>(ref output, bitLocation / 8);

            return output;
        }

        /// <summary>
        /// Calculates the number of Base32 tokens (output chars) in a byte array
        /// includes the padding tokens '='
        /// </summary>
        private static int GetTokenCount(byte[] data)
        {
            return (((data.Length * 8) + 39) / 40) * 8;
        }

        /// <summary>
        /// Calculates the number of bytes that the Base32 string will convert into
        /// when decoded.
        /// </summary>
        private static int GetByteCount(string base32String)
        {
            return base32String.RemoveBase32Padding().Length * 5 / 8;
        }

        /// <summary>
        /// Gets the next Base32 token from the array
        /// WARNING:  ~80% of the time of this function was index bounding, so
        /// the expensive part of that has been removed, bound your calls to this
        /// and ensure that you dont increment index forever.....
        /// </summary>
        private static char GetToken(byte[] data, int index)
        {
            if (index < 0)
            {
                throw new IndexOutOfRangeException($"{nameof(index)} can't be negative!");
            }

            // 0x20 is returned if the requested index is past the end of data
            // equates to padding char "="
            byte retval = PaddingCharacterIndex;

            // Get location of token in bits
            int byteOffset = (index * 5) / 8;

            // Is this input or padding?
            if (byteOffset < data.Length)
            {
                // Calculate which bits are used from the byte
                int bitOffset = (index * 5) % 8;
                int shift1 = bitOffset - 3;

                retval = data[byteOffset];
                if (shift1 < 0)
                {
                    // Shift right
                    retval >>= (-shift1);
                }
                else if (shift1 > 0)
                {
                    // Shift left
                    retval <<= shift1;
                    
                    // If not last byte in input, include necessary bits from next byte in token
                    if (byteOffset + 1 < data.Length)
                    {
                        int shift2 = 8 - shift1;
                        byte b = data[byteOffset + 1];
                        b >>= shift2;
                        retval |= b;
                    }
                } // (shift1 == 0) {  do nothing }

                // Mask to right 5 bits
                retval &= Bitmask;
            } /* else {
                // this is in "else" to prevent running GetTokenCount() more than necessary
                if (index >= GetTokenCount(data)) {
                    throw new IndexOutOfRangeException();
                }
            } */

            return _encodingChars[retval];
        }
    }
}