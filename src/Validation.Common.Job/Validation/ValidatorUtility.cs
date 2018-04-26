// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Validator-related utility methods
    /// </summary>
    public static class ValidatorUtility
    {
        /// <summary>
        /// Checks whether given type has <see cref="ValidatorNameAttribute"/> attribute.
        /// </summary>
        public static bool HasValidatorNameAttribute(Type type)
            => type.CustomAttributes.Any(a => a.AttributeType == typeof(ValidatorNameAttribute));

        /// <summary>
        /// Retrieves the value of the <see cref="ValidatorNameAttribute.Name"/> property set 
        /// for <see cref="ValidatorNameAttribute"/> set on a specified class.
        /// </summary>
        public static string GetValidatorName(Type type)
            => GetCustomAttribute<ValidatorNameAttribute>(type).Name;

        private static T GetCustomAttribute<T>(Type type)
            where T : Attribute
            => (T)Attribute.GetCustomAttribute(type, typeof(T));
    }
}
