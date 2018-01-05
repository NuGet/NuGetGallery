// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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

        public string A { get; set; }

        public int B { get; set; }

        public override string GetMessage() => Strings.UnknownIssueMessage;
    }
}
