// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Exception to indicate that an unknown evaluator was requested by configuration.
    /// </summary>
    public class UnknownEvaluatorException : Exception
    {
        private const string _messageTemplate = "{0} requested unknown evaluator {1}";

        public UnknownEvaluatorException(string requestedEvaluator, string source)
            : base(String.Format(_messageTemplate, source, requestedEvaluator))
        {
            
        }

        public UnknownEvaluatorException() : base() { }
    }
}
