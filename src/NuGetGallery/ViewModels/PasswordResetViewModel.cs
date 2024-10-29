// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class PasswordResetViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        [PasswordValidation]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}