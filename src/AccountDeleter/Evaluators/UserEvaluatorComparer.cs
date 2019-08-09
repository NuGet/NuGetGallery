// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    public class UserEvaluatorComparer : IEqualityComparer<IUserEvaluator>
    {
        public bool Equals(IUserEvaluator x, IUserEvaluator y)
        {
            return x.EvaluatorId == y.EvaluatorId;
        }

        public int GetHashCode(IUserEvaluator obj)
        {
            return obj.EvaluatorId.GetHashCode();
        }
    }
}
