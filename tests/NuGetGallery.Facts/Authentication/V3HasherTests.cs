// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class V3HasherTests
    {
        [Fact]
        public void WhenStringIsHashedItCanBeVerified()
        {
            // Arrange
            string password = "arew234235wfsdq2321edfewt";

            // Act
            string hash = V3Hasher.GenerateHash(password);

            // Assert
            Assert.True(V3Hasher.VerifyHash(hash, password));
        }

        [Fact]
        public void WhenWrongStringIsVerifiedThenVerificationFails()
        {
            // Arrange
            string password = "arew234235wfsdq2321edfewt";
            string hash = V3Hasher.GenerateHash(password);

            // Act
            bool verify = V3Hasher.VerifyHash(hash, password+"1");

            // Assert
            Assert.False(verify);
        }

        [Fact]
        public void WhenHashIsInvalidVerificationFails()
        {
            // Arrange
            string password = "arew234235wfsdq2321edfewt";
            string hash = V3Hasher.GenerateHash(password);
            byte[] badHash = Convert.FromBase64String(hash);
            badHash[0] = 0x0;

            // Act
            // The first bit should be 0x01 in the algorithm we use. Make sure we fail if it's not.
            bool verify = V3Hasher.VerifyHash(Convert.ToBase64String(badHash), password);

            // Assert
            Assert.False(verify);
        }

        [Fact(Skip = "This test is not deterministic. Lets not run it as part of CI")]
        public void ProcessingTimesForSuccessfulAuthAndFailedAuthAreSimilar()
        {
            // Arrange
            double allowedDiffPercent = 0.05;
            int repetitions = 1000;

            string password = "arew234235wfsdq2321edfewt";
            string hash = V3Hasher.GenerateHash(password);

            // Act
            var successStopWatch = new Stopwatch();
            var failureStopWatch = new Stopwatch();

            successStopWatch.Start();

            for (int i = 0; i < repetitions; i++)
            {
                V3Hasher.VerifyHash(hash, password);
            }

            successStopWatch.Stop();

            failureStopWatch.Start();

            for (int i = 0; i < repetitions; i++)
            {
                V3Hasher.VerifyHash(hash, password + "1");
            }

            failureStopWatch.Stop();

            double diffPercent = ((double)successStopWatch.ElapsedTicks - (double)failureStopWatch.ElapsedTicks)/
                                (double)successStopWatch.ElapsedTicks;

            Assert.True(Math.Abs(diffPercent) < allowedDiffPercent);
        }
    }
}
