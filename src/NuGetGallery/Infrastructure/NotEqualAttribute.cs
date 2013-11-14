using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class NotEqualAttribute : ValidationAttribute, IClientValidatable
    {
        public object DisallowedValue { get; private set; }
        public NotEqualAttribute(object disallowedValue) : base()
        {
            DisallowedValue = disallowedValue;
        }

        public override bool IsValid(object value)
        {
            return !Equals(value, DisallowedValue);
        }

        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(ModelMetadata metadata, ControllerContext context)
        {
            yield return new ModelClientValidationRule()
            {
                ValidationType = "notequal",
                ValidationParameters = { { "disallowed", DisallowedValue.ToString() } },
                ErrorMessage = ErrorMessageString
            };
        }
    }
}