// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Stores the string label for the Validator to be stored in DB and 
    /// to allow validator instantiation by that label
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ValidatorNameAttribute: Attribute
    {
        public string Name { get; }

        public ValidatorNameAttribute(string name)
        {
            Name = name;
        }
    }
}
