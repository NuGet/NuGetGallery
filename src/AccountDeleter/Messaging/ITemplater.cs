// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter
{
    public interface ITemplater
    {
        /// <summary>
        /// Add a new replacement to the templater.
        /// </summary>
        /// <param name="toReplace">Token to replace</param>
        /// <param name="replaceWith">Value to replace Token with.</param>
        /// <returns>True if new substitution was added. False if new addition failed for some reason. Note that any values added with this method will override any previous values.</returns>
        bool AddReplacement(string toReplace, string replaceWith);

        /// <summary>
        /// Replaces all known template values
        /// </summary>
        /// <param name="template">String with placeholder values</param>
        /// <returns>String with placeholder values replaced.</returns>
        string FillTemplate(string template);
    }
}
