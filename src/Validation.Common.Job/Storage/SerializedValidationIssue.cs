// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Storage
{
    public class SerializedValidationIssue : IValidationIssue
    {
        private readonly string _data;

        public SerializedValidationIssue(ValidationIssueCode issueCode, string data)
        {
            IssueCode = issueCode;
            _data = data;
        }

        public ValidationIssueCode IssueCode { get; }
        public string Serialize() => _data;
    }
}
