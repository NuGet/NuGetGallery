// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Contains a result and the access condition used to modify the result in storage.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public class ResultAndAccessCondition<T>
    {
        public ResultAndAccessCondition(T result, IAccessCondition accessCondition)
        {
            Result = result;
            AccessCondition = accessCondition;
        }

        public T Result { get; }
        public IAccessCondition AccessCondition { get; }
    }
}
