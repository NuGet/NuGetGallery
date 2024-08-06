// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface IStringTemplateProcessor<TInput>
    {
        /// <summary>
        /// Returns a string that was produced as a result of substituting
        /// template arguments with data passed through the <paramref name="input"/> argument.
        /// </summary>
        /// <param name="input">Data source for the parameter substitution.</param>
        /// <returns>String with placeholders filled with actual data.</returns>
        string Process(TInput input);
    }
}