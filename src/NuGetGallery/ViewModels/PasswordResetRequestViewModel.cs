﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}