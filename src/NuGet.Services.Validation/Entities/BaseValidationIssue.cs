// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Services.Validation
{
    public abstract class BaseValidationIssue
    {
        /// <summary>
        /// The unique key that identifies this error.
        /// </summary>
        public long Key { get; set; }
        
        /// <summary>
        /// The code that this error represents. The NuGet Gallery should map this error
        /// to an actual error message using this code.
        /// </summary>
        public ValidationIssueCode IssueCode { get; set; }

        /// <summary>
        /// The error message's serialized data.
        /// </summary>
        public string Data { get; set; }
    }
}
