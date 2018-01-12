// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    public class ValidatorIssue : BaseValidationIssue
    {
        /// <summary>
        /// The validation ID that this issue is related to. This is a foreign key to the <see cref="ValidatorStatus"/>
        /// entity.
        /// </summary>
        public Guid ValidationId { get; set; }

        /// <summary>
        /// The <see cref="ValidatorStatus"/> that has this issue.
        /// </summary>
        public ValidatorStatus ValidatorStatus { get; set; }
    }
}
