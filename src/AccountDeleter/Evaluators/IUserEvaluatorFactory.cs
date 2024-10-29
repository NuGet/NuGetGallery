// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter
{
    public interface IUserEvaluatorFactory
    {
        /// <summary>
        /// Returns an <see cref="IUserEvaluator"/> instance that corresponds to the requested name
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="UnknownEvaluatorException"></exception>
        IUserEvaluator GetEvaluatorForSource(string source);
    }
}
