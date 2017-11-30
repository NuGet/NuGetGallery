// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Entities
{
    public class ScopeFacts
    {
        public static IEnumerable<object[]> CloneSucceeds_Input
        {
            get
            {
                return new[]
                {
                    new object[]
                    {
                        new Scope(ownerKey: 1, subject: "abc", allowedAction: "cde")
                    },
                    new object[]
                    {
                        new Scope(owner: new User() { Key = 2 }, subject: "abc", allowedAction: "cde")
                    }
                };
            }
        }

        [MemberData(nameof(CloneSucceeds_Input))]
        [Theory]
        public void CloneSucceeds(Scope scope)
        {
            // Act
            var clone = scope.Clone();

            // Assert
            Assert.Equal(scope.AllowedAction, clone.AllowedAction);
            Assert.Equal(scope.Owner != null ? scope.Owner.Key : scope.OwnerKey, clone.OwnerKey);
            Assert.Equal(scope.Subject, clone.Subject);
        }
    }
}
