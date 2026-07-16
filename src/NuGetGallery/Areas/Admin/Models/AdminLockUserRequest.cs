// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminLockUserRequest : IValidatableObject
    {
        public List<AdminUserIdentity> Users { get; set; }

        [Required(ErrorMessage = "The locked field is required.")]
        public bool? Locked { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "The reason field is required.")]
        public string Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Users == null)
            {
                yield return new ValidationResult("The users field is required.", [nameof(Users)]);
                yield break;
            }

            if (Users.Count == 0)
            {
                yield return new ValidationResult("The users field must contain at least one entry.", [nameof(Users)]);
            }

            if (Users.Count > AdminRequestLimits.MaxUserCount)
            {
                yield return new ValidationResult($"The users field must contain at most {AdminRequestLimits.MaxUserCount} entries.", [nameof(Users)]);
            }

            for (var i = 0; i < Users.Count; i++)
            {
                if (Users[i] == null)
                {
                    yield return new ValidationResult("User entry must not be null.", [$"{nameof(Users)}[{i}]"]);
                }
            }
        }
    }
}
