// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    public sealed class MandatoryAttribute : RequiredAttribute, IClientValidatable
    {
        public override bool IsValid(object value)
        {
            return (value != null && (bool)value);
        }

        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(ModelMetadata metadata, ControllerContext context)
        {
            yield return new ModelClientValidationRule() { ValidationType = "mandatory", ErrorMessage = this.ErrorMessage };
        }
    }
}