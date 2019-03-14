// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;

namespace NuGetGallery.TestUtils
{
    /// <summary>
    /// A number generator using RNGCryptoServiceProvider.
    /// Based on http://csharphelper.com/blog/2014/08/use-a-cryptographic-random-number-generator-in-c/
    /// </summary>
    public class SecureRandomNumberGenerator
    {
        // The random number provider.
        private readonly RNGCryptoServiceProvider _rand;

        public SecureRandomNumberGenerator()
        {
            _rand = new RNGCryptoServiceProvider();
        }

        /// <summary>
        /// Returns a random integer between a min and max value.
        /// </summary>
        /// <param name="minValue">Min value</param>
        /// <param name="maxValue">Max value</param>
        /// <returns></returns>
        public int Next(int minValue, int maxValue)
        {
            if (minValue < 0 || maxValue < 0 || maxValue <= minValue)
            {
                throw new ArgumentException("Min and max values need to be positive integers and min needs to be smaller than max.");
            }

            uint scale = uint.MaxValue;
            // Get a UInt32 that is not MaxValue
            while (scale == uint.MaxValue)
            {
                // Get four random bytes.
                byte[] four_bytes = new byte[4];
                _rand.GetBytes(four_bytes);

                // Convert that into an uint.
                scale = BitConverter.ToUInt32(four_bytes, 0);
            }

            // Add min to the scaled difference between max and min.
            return (int)(minValue + (maxValue - minValue) *(scale / (double)uint.MaxValue));
        }

        /// <summary>
        ///  Returns a random integer between 0(zero) and max value.
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns>A random number grater than zero and smaller than maxValue</returns>
        public int Next(int maxValue)
        {
            if (maxValue < 0)
            {
                throw new ArgumentException("Max value needs to be greater than zero.");
            }
            return Next(0, maxValue);
        }

        /// <summary>
        /// Get random bytes
        /// </summary>
        /// <param name="bytes">The array to po</param>
        public void NextBytes(byte[] bytes)
        {
            _rand.GetBytes(bytes);
        }
    }
}
