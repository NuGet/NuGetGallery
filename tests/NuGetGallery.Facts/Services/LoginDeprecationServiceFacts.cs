// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public class LoginDeprecationServiceFacts
    {
        public class TheIsLoginDiscontinuedAsyncMethod
        {
            public Task IfPasswordLoginAndUserIsOnListReturnsTrue()
            {
                
            }

            public Task IfPasswordLoginAndUserIsNotOnListReturnsFalse()
            {

            }

            public Task IfPasswordLoginAndUserIsOnListWithExceptionReturnsFalse()
            {

            }

            public Task IfNotPasswordLoginReturnsFalse()
            {

            }

            private Task<ILoginDeprecationService> Setup(bool isOnDomainList, bool isOnExceptionList)
            {

            }
        }
    }
}
