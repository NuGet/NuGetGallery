// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.FeatureFlags;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class FeatureFlagServiceFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsDerOrderingEnforcementStatus(bool isEnabled)
        {
            Mock<IFeatureFlagClient> client = new();
            client
                .Setup(featureFlags => featureFlags.IsEnabled(
                    "Validation.DerOrderingEnforcement",
                    defaultValue: false))
                .Returns(isEnabled);
            FeatureFlagService service = new(client.Object);

            bool actual = service.IsDerOrderingEnforcementEnabled();

            Assert.Equal(isEnabled, actual);
        }
    }
}
