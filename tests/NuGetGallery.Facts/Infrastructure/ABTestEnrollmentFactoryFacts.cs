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
                Assert.Equal(1, enrollment.SchemaVersion);
                Assert.InRange(enrollment.PreviewSearchBucket, 1, 100);
            }
        }

        public class Serialize : Facts
        {
            [Fact]
            public void ProducesExpectedString()
            {
                var enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    schemaVersion: 1,
                    previewSearchBucket: 42);

                var serialized = Target.Serialize(enrollment);

                Assert.Equal(@"{""v"":1,""ps"":42}", serialized);
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
            public void ParsesValid(string input, int previewSearchBucket)
            {
                var success = Target.TryDeserialize(input, out var enrollment);

                Assert.True(success, "The derialization should have succeeded.");
                Assert.NotNull(enrollment);
                Assert.Equal(ABTestEnrollmentState.Active, enrollment.State);
                Assert.Equal(1, enrollment.SchemaVersion);
                Assert.Equal(previewSearchBucket, enrollment.PreviewSearchBucket);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                TelemetryService = new Mock<ITelemetryService>();
                Logger = new Mock<ILogger<ABTestEnrollmentFactory>>();

                Target = new ABTestEnrollmentFactory(TelemetryService.Object, Logger.Object);
            }

            public Mock<ITelemetryService> TelemetryService { get; }
            public Mock<ILogger<ABTestEnrollmentFactory>> Logger { get; }
            public ABTestEnrollmentFactory Target { get; }
        }
    }
}
