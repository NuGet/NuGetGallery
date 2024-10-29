// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// Processes templates by running a series of <see cref="string.Replace(string, string)"/> calls against a template.
    /// Not very efficient but simple.
    /// </summary>
    /// <typeparam name="TInput">The data source type for placeholder substitution.</typeparam>
    /// <remarks>
    /// Known issue:
    /// 
    /// The order in which <see cref="string.Replace(string, string)"/> calls are performed is undefined, so
    /// if substitution results in producing some other placeholder, the result would depend on order of operation
    /// and thus be undefined as well. Need to keep that in mind when picking the placeholder representation and consider
    /// what they can be substituted with.
    /// </remarks>
    public class StringReplaceTemplateProcessor<TInput> : IStringTemplateProcessor<TInput>
    {
        private readonly string _template;
        private readonly IReadOnlyDictionary<string, Func<TInput, string>> _placeholderProcessors;

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="template">String template. Can be null, in that case <see cref="Process(TInput)"/> will always produce null, too.</param>
        /// <param name="placeholderProcessors">The map of placeholders to delegates that extract the appropriate value 
        /// to substitute with from the <typeparamref name="TInput"/> object. Placeholders are case-sensitive.</param>
        public StringReplaceTemplateProcessor(string template, IReadOnlyDictionary<string, Func<TInput, string>> placeholderProcessors)
        {
            _template = template;
            _placeholderProcessors = placeholderProcessors ?? throw new ArgumentNullException(nameof(placeholderProcessors));
            foreach (var placeholderAndProcessor in _placeholderProcessors)
            {
                if (string.IsNullOrEmpty(placeholderAndProcessor.Key))
                {
                    throw new ArgumentException(
                        $"{nameof(placeholderProcessors)} contains null or empty key.",
                        nameof(placeholderProcessors));
                }
                if (placeholderAndProcessor.Value == null)
                {
                    throw new ArgumentException(
                        $"{nameof(placeholderProcessors)} contains null processor for key {placeholderAndProcessor.Key}",
                        nameof(placeholderProcessors));
                }
            }
        }

        public string Process(TInput input)
        {
            if (_template == null)
            {
                return null;
            }
            var result = _template;
            foreach (var placeholderAndProcessor in _placeholderProcessors)
            {
                result = result.Replace(placeholderAndProcessor.Key, placeholderAndProcessor.Value(input));
            }

            return result;
        }
    }
}
