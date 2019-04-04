// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsFlightViewModel : IFeatureFlagsViewModel
    {
        public FeatureFlagsFlightViewModel()
        {
        }

        public FeatureFlagsFlightViewModel(
            FeatureFlagsFlightViewModel flight)
        {
            Name = flight.Name;
            All = flight.All;
            SiteAdmins = flight.SiteAdmins;
            Accounts = flight.Accounts;
            Domains = flight.Domains;
        }

        public FeatureFlagsFlightViewModel(
            string name,
            Flight flight)
        {
            Name = name;
            All = flight.All;
            SiteAdmins = flight.SiteAdmins;
            Accounts = flight.Accounts;
            Domains = flight.Domains;
        }

        [Required]
        public string Name { get; set; }

        public bool All { get; set; }

        [Display(Name = "Site Admins")]
        public bool SiteAdmins { get; set; }

        public IEnumerable<string> Accounts { get; set; }

        public IEnumerable<string> Domains { get; set; }
    }
}