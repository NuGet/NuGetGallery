// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    public class AlwayRejectEvaluator : IUserEvaluator
    {
        private readonly Guid _id;

        public AlwayRejectEvaluator()
        {
            _id = new Guid();
        }

        public string EvaluatorId
        {
            get
            {
                return _id.ToString();
            }
        }

        public bool CanUserBeDeleted(User user)
        {
            return false;
        }

    }
}
