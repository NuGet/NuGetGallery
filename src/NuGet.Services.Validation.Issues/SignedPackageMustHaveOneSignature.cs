// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.Validation.Issues
{
    public class SignedPackageMustHaveOneSignature : ValidationIssue
    {
        [JsonConstructor]
        public SignedPackageMustHaveOneSignature(int count)
        {
            if (count == 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "The count must not be 1.");
            }

            Count = count;
        }

        public override ValidationIssueCode IssueCode => ValidationIssueCode.SignedPackageMustHaveOneSignature;

        [JsonProperty("c", Required = Required.Always)]
        public int Count { get; }
    }
}
