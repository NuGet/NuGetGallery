// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NuGetGallery.Services.UserManagement;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ModifyFeatureFlagsFlightViewModel 
        : FeatureFlagsFlightViewModel, IModifyFeatureFlagsViewModel<FeatureFlagsFlightViewModel>
    {
        public ModifyFeatureFlagsFlightViewModel()
        {
        }

        public ModifyFeatureFlagsFlightViewModel(
            FeatureFlagsFlightViewModel flight,
            string contentId)
            : base(flight)
        {
            ContentId = contentId;
        }

        [Required]
        public string ContentId { get; set; }

        public string PrettyName => "flight";

        public List<FeatureFlagsFlightViewModel> GetExistingList(FeatureFlagsViewModel model)
        {
            return model.Flights;
        }

        public string GetValidationError(IUserService userService)
        {
            if (Accounts?.Any() ?? false)
            {
                var missingAccounts = new List<string>();
                foreach (var accountName in Accounts)
                {
                    var user = userService.FindByUsername(accountName);
                    if (user == null)
                    {
                        missingAccounts.Add(accountName);
                    }
                }

                if (missingAccounts.Any())
                {
                    return $"Some accounts specified by the flight '{Name}' ({string.Join(", ", missingAccounts)}) do not exist. A flight cannot specify accounts that do not exist.";
                }
            }

            return null;
        }

        public void ApplyTo(FeatureFlagsFlightViewModel target)
        {
            target.All = All;
            target.SiteAdmins = SiteAdmins;
            target.Accounts = Accounts;
            target.Domains = Domains;
        }
    }
}