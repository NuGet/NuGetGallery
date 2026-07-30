// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class StagedValidationStatusFacts
    {
        /// <summary>
        /// These values are persisted to the database as integers, and <see cref="StagedValidationStatus.Succeeded"/>
        /// is what the promotion eligibility check compares against, so reordering them would make validation-failed
        /// packages promotable. Nothing else catches this: the values live in the conceptual model only, so the
        /// migration drift test sees no change.
        /// </summary>
        [Fact]
        public void ValuesAreNotChanged()
        {
            Assert.Equal(0, (int)StagedValidationStatus.Validating);
            Assert.Equal(1, (int)StagedValidationStatus.Succeeded);
            Assert.Equal(2, (int)StagedValidationStatus.Failed);
        }
    }
}
