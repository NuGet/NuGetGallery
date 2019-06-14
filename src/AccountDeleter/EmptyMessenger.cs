// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public class EmptyMessenger : IMessenger
    {
        public EmptyMessenger()
        {

        }

        public async Task<bool> SendMessageAsync(string userName, int userKey)
        {
            return true;
        }
    }
}
