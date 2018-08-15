// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class TyposquattingPackagesCheckListService : ITyposquattingPackagesCheckListService
    {
        private readonly IPackageService _packageService;

        public TyposquattingPackagesCheckListService(IPackageService packageService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public List<string> GetTyposquattingChecklist(int typosquattingCheckListLength)
        {
            var packages = _packageService.GetTyposquattingCheckList(typosquattingCheckListLength);
            return packages;
        }
    }
}