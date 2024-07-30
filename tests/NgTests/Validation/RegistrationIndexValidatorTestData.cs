// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public interface IRegistrationIndexValidatorTestData
    {
        /// <summary>
        /// Creates the <see cref="RegistrationIndexValidator"/> to run this data against.
        /// </summary>
        RegistrationIndexValidator CreateValidator();

        /// <summary>
        /// <see cref="PackageRegistrationIndexMetadata"/>s to use for tests.
        /// 
        /// This data must follow the following rules: 
        /// 1 - If one element is compared against the same element, it must succeed.
        /// 2 - If one element is compared against a different element, it must fail.
        /// 3 - Validation should always run against any pair of these elements.
        /// </summary>
        IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes { get; }

        /// <summary>
        /// <see cref="PackageRegistrationIndexMetadata"/>s to use for tests.
        /// 
        /// Validation should never run when any of these elements are included in a test.
        /// </summary>
        IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes { get; }

        /// <summary>
        /// <see cref="PackageRegistrationIndexMetadata"/>s to use for tests.
        /// 
        /// Each tuple of elements represents a pairing that should pass if true and fail otherwise.
        /// </summary>
        IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes { get; }
    }

    public abstract class RegistrationIndexValidatorTestData<T> : IRegistrationIndexValidatorTestData
        where T : RegistrationIndexValidator
    {
        public RegistrationIndexValidator CreateValidator()
        {
            return CreateValidator(Mock.Of<ILogger<T>>());
        }

        protected abstract T CreateValidator(ILogger<T> logger);

        public abstract IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes { get; }

        public abstract IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes { get; }

        public virtual IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes => 
            Enumerable.Empty<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>>();
    }
}
