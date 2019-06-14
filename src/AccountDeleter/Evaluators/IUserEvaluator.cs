// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    public interface IUserEvaluator
    {
        /// <summary>
        /// Unique Id for evaluator. Used to track results.
        /// </summary>
        string EvaluatorId { get; }

        /// <summary>
        /// Determines if as user can be deleted as per the criteria defined by this evaluator
        /// </summary>
        /// <param name="user"></param>
        /// <returns>True if user can be deleted. False otherwise</returns>
        bool CanUserBeDeleted(User user);
    }
}
