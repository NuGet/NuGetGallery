// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter
{
    public interface IUserEvaluatorFactory
    {
        IUserEvaluator GetEvaluatorForSource(string source);

        bool AddEvaluatorByKey(string key, IUserEvaluator evaluator);
    }
}
