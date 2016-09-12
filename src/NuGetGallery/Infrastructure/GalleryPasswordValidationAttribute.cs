// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery.Infrastructure
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GalleryPasswordValidationAttribute : ValidationAttribute
    {
        private readonly RegularExpressionAttribute _internalRegexAttribute;

        public GalleryPasswordValidationAttribute()
        {
            var configuration = DependencyResolver.Current.GetService<IGalleryConfigurationService>().Current;

            _internalRegexAttribute = new RegularExpressionAttribute(configuration.UserPasswordRegex)
            {
                ErrorMessage = configuration.UserPasswordHint
            };
        }

        public override bool IsValid(object value)
        {
            return _internalRegexAttribute.IsValid(value);
        }

        public override string FormatErrorMessage(string name)
        {
            return _internalRegexAttribute.FormatErrorMessage(name);
        }
    }
}