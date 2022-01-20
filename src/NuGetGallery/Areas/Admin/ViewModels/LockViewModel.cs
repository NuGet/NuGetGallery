// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Areas.Admin.Controllers;
using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public abstract class LockViewModel
    {
        public LockViewModel(string controllerName, string entitiesLabel, string identifersLabel)
        {
            ControllerName = controllerName;
            EntitiesLabel = entitiesLabel;
            IdentifersLabel = identifersLabel;
        }

        public string ControllerName { get; set; }

        /// <summary>
        /// Should be plural and capitalized, e.g. "Packages"
        /// </summary>
        public string EntitiesLabel { get; set; }

        /// <summary>
        /// Should be plural and capitalized, e.g. "IDs"
        /// </summary>
        public string IdentifersLabel { get; set; }

        public string Query { get; set; }

        public bool HasQuery => !string.IsNullOrEmpty(Query);

        public bool HasResults => LockStates?.Count > 0;

        public IList<LockState> LockStates { get; set; }
    }
}