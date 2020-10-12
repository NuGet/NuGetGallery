// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class ABTestEnrollmentFactoryFacts
    {
        public class Initialize : Facts
        {
            [Fact]
            public void CreatesANewEnrollmentInstance()
            {
                var enrollment = Target.Initialize();

                Assert.NotNull(enrollment);
                Assert.Equal(ABTestEnrollmentState.FirstHit, enrollment.State);
                Assert.Equal(2, enrollment.SchemaVersion);
                Assert.InRange(enrollment.PreviewSearchBucket, 1, 100);
                Assert.InRange(enrollment.PackageDependentBucket, 1, 100);
            }
        }

        public class Serialize : Facts
        {
            [Fact]
            public void ProducesExpectedString()
            {
                var enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    schemaVersion: 2,
                    previewSearchBucket: 42,
                    packageDependentBucket: 82);

                var serialized = Target.Serialize(enrollment);

                Assert.Equal(@"{""v"":2,""ps"":42,""pd"":82}", serialized);
            }

            [Fact]
            public void ProducesExpectedStringVersionTwo()
            {
                var enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    schemaVersion: 2,
                    previewSearchBucket: 42,
                    packageDependentBucket: 74);

                var serialized = Target.Serialize(enrollment);

                Assert.Equal(@"{""v"":2,""ps"":42,""pd"":74}", serialized);
            }
        }

        public class Deserialize : Facts
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            [InlineData(" \t ")]
            [InlineData("{}")]
            [InlineData("[]")]
            [InlineData("null")]
            [InlineData(@"{""ps"":42}")]
            [InlineData(@"{""v"":2,""ps"":42}")]
            [InlineData(@"{""v"":1}")]
            [InlineData(@"{""v"":1,""ps"":-1}")]
            [InlineData(@"{""v"":1,""ps"":0}")]
            [InlineData(@"{""v"":1,""ps"":101}")]
            [InlineData(@"{""v"":2,""ps"":10,""pd"":101}")]
            [InlineData(@"{""v"":2}")]
            [InlineData(@"{""pd"":42}")]
            [InlineData(@"{""v"":2,""ps"":1,""pd"":-1}")]
            [InlineData(@"{""v"":2,""ps"":10,""pd"":0}")]
            [InlineData(@"{""v"":3,""ps"":42,""pd"":53}")]
            [InlineData(@"{""v"":2,""ps"":-1,""pd"":24}")]
            [InlineData(@"{""v"":2,""ps"":0,""pd"":35}")]
            [InlineData(@"{""v"":2,""ps"":101,""pd"":56}")]
            [InlineData(@"{""v"":2,""ps"":200,""pd"":82}")]
            [InlineData(@"{""ps"":20,""pd"":82}")]
            public void RejectsInvalid(string input)
            {
                var success = Target.TryDeserialize(input, out var enrollment);

                Assert.False(success, "The derialization should have failed.");
                Assert.Null(enrollment);
            }

            [Theory]
            [InlineData(@"{""v"":1,""ps"":42}", 42)]
            [InlineData(@"{""v"":1,""ps"":42,""zzz"":false}", 42)]
            [InlineData(@"{""v"":1,""ps"":1}", 1)]
            [InlineData(@"{""v"":1,""ps"":100}", 100)]
            public void UpgradesValidVersionV1(string input, int previewSearchBucket)
            {
                var success = Target.TryDeserialize(input, out var enrollment);

                Assert.True(success, "The derialization should have succeeded.");
                Assert.NotNull(enrollment);
                Assert.Equal(ABTestEnrollmentState.Upgraded, enrollment.State);
                Assert.Equal(2, enrollment.SchemaVersion);
                Assert.Equal(previewSearchBucket, enrollment.PreviewSearchBucket);
                Assert.InRange(enrollment.PackageDependentBucket, 1, 100);
            }

            [Theory]
            [InlineData(@"{""v"":2,""ps"":42,""pd"":57}", 42, 57)]
            [InlineData(@"{""v"":2,""ps"":1,""pd"":1}", 1, 1)]
            [InlineData(@"{""v"":2,""ps"":100,""pd"":100}", 100, 100)]
            [InlineData(@"{""v"":2,""ps"":1,""pd"":32}", 1, 32)]
            [InlineData(@"{""v"":2,""ps"":100,""pd"":57}", 100, 57)]
            [InlineData(@"{""v"":2,""ps"":52,""pd"":1}", 52, 1)]
            [InlineData(@"{""v"":2,""ps"":68,""pd"":100}", 68, 100)]
            public void ParsesValidVersion2(string input, int previewSearchBucket, int packageDependentBucket)
            {
                var success = Target.TryDeserialize(input, out var enrollment);

                Assert.True(success, "The derialization should have succeeded.");
                Assert.NotNull(enrollment);
                Assert.Equal(ABTestEnrollmentState.Active, enrollment.State);
                Assert.Equal(2, enrollment.SchemaVersion);
                Assert.Equal(previewSearchBucket, enrollment.PreviewSearchBucket);
                Assert.Equal(packageDependentBucket, enrollment.PackageDependentBucket);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                TelemetryService = new Mock<ITelemetryService>();
                Target = new ABTestEnrollmentFactory(TelemetryService.Object);
            }

            public Mock<ITelemetryService> TelemetryService { get; }
            public Mock<ILogger<ABTestEnrollmentFactory>> Logger { get; }
            public ABTestEnrollmentFactory Target { get; }
        }
    }
}
