// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.Validation.Issues
{
    /// <summary>
    /// A validation issue used for testing purposes.
    /// </summary>
    [Obsolete("This issue should only be used for testing")]
    public class ObsoleteTestingIssue : ValidationIssue
    {
        public ObsoleteTestingIssue(string a, int b)
        {
            A = a;
            B = b;
        }

#pragma warning disable 618
        public override ValidationIssueCode IssueCode => ValidationIssueCode.ObsoleteTesting;
#pragma warning restore 618

        [JsonProperty(Required = Required.AllowNull)]
        public string A { get; set; }

        [JsonProperty(Required = Required.AllowNull)]
        public int B { get; set; }
    }
}
