// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Validation.Tests.Entities
{
    public class EndCertificateUseTests
    {
        private static readonly IReadOnlyDictionary<int, EndCertificateUse> Expected = new Dictionary<int, EndCertificateUse>
        {
            { 1, EndCertificateUse.CodeSigning },
            { 2, EndCertificateUse.Timestamping },
        };

        [Theory]
        [MemberData(nameof(HasExpectedValuesData))]
        public void HasExpectedValues(int expected, EndCertificateUse actual)
        {
            Assert.Equal((EndCertificateUse)expected, actual);
        }

        [Fact]
        public void HasAllValuesTested()
        {
            Assert.Equal(
                Expected.Values.OrderBy(x => x),
                Enum.GetValues(typeof(EndCertificateUse)).Cast<EndCertificateUse>().OrderBy(x => x));
        }

        /// <summary>
        /// It's not clear whether we will want to support a certificate used for both code signing and timestamping in
        /// the future. If we do need that, we could feasibly represent it with <see cref="FlagsAttribute"/>.
        /// </summary>
        [Fact]
        public void SupportsFlags()
        {
            var values = Enum
                .GetValues(typeof(EndCertificateUse))
                .Cast<int>()
                .ToList();

            // No duplicate values.
            Assert.Equal(values.Distinct().Count(), values.Count);

            // All values must be powers of 2.
            Assert.All(values, x => Assert.Equal(0, x & (x - 1)));
            
            // No zero values.
            Assert.All(values, x => Assert.NotEqual(0, x));
        }

        public static IEnumerable<object[]> HasExpectedValuesData => Expected
            .Select(x => new object[] { x.Key, x.Value });
    }
}
