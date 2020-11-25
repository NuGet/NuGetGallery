// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;

namespace Stats.LogInterpretation
{
    internal class PackageTranslation
    {
        public string IncorrectPackageId { get; set; }
        public Regex IncorrectPackageVersionPattern { get; set; }
        public string CorrectedPackageId { get; set; }
        public string CorrectedPackageVersionPattern { get; set; }
    }
}