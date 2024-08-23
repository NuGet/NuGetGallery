// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;

namespace NuGet.Services.Configuration
{
    public static class ConfigurationUtility
    {
        /// <summary>
        /// Converts a string into T.
        /// </summary>
        /// <typeparam name="T">Type that value will be converted into.</typeparam>
        /// <param name="value">String to convert.</param>
        /// <returns>Value converted into T.</returns>
        /// <exception cref="NotSupportedException">Thrown when a conversion from string to T is impossible.</exception>
        public static T ConvertFromString<T>(string value)
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                return (T)converter.ConvertFromString(value);
            }

            throw new NotSupportedException("No converter exists from string to " + typeof(T).Name + "!");
        }
    }
}
