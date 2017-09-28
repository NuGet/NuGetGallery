// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq.Language;
using Moq.Language.Flow;

namespace NuGetGallery
{
    public static class CoreMockExtensions
    {
        public static IReturnsResult<TMock> Completes<TMock>(this IReturns<TMock, Task> self)
            where TMock : class
        {
            return self.Returns(Task.FromResult((object)null));
        }
    }
}

