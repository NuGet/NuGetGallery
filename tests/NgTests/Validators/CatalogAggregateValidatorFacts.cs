// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using Xunit;

namespace NgTests.Validators
{
    public class CatalogAggregateValidatorFacts
    {
        [Fact]
        public async Task DoesntValidateSignatureIfDisabled()
        {
            var mock = Mock.Of<IDictionary<FeedType, SourceRepository>>();
            var loggerFactory = Mock.Of<ILoggerFactory>();
            var factory = new ValidatorFactory(mock, loggerFactory);

            var target = new CatalogAggregateValidator(factory, requireSignature: false);

            var result = await target.ValidateAsync(new ValidationContext());

            Assert.Equal(0, result.ValidationResults.Count());
        }

        [Fact]
        public async Task ValidatesSignature()
        {
            var feedToSource = new Mock<IDictionary<FeedType, SourceRepository>>();
            var loggerFactory = Mock.Of<ILoggerFactory>();
            var factory = new ValidatorFactory(feedToSource.Object, loggerFactory);

            feedToSource.Setup(x => x[It.IsAny<FeedType>()]).Returns(new Mock<SourceRepository>().Object);

            var target = new CatalogAggregateValidator(factory, requireSignature: true);

            var result = await target.ValidateAsync(new ValidationContext());

            Assert.Equal(1, result.ValidationResults.Count());
        }
    }
}
