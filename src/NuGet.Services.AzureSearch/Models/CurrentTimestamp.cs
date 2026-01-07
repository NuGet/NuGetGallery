// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A "last updated" timestamp set on an Azure Search document is approximate since there is some undefined time
    /// between when the document is initialized, then serialized, then pushed to Azure Search, then applied to the
    /// index, then made available in a search result. The only period of time that we can mitigate is time between
    /// initializing the document and when it is serialized to be sent to Azure Search. To do this, we introduce this
    /// stateful type that can capture the current timestamp in <see cref="Value"/> when it is next read. This is so
    /// the current timestamp is captured as we are serializing the document to JSON to be sent to the Azure Search
    /// REST API.
    /// </summary>
    public class CurrentTimestamp
    {
        public const int FalseInt = 0;
        public const int TrueInt = 1;

        private int _setOnNextRead = FalseInt;
        private DateTimeOffset? _value;

        /// <summary>
        /// Defaults to <c>null</c>. After <see cref="SetOnNextRead"/> is called, the next time the getter of
        /// <see cref="Value"/> is the current timestamp is captured then returned. The setter can be used to set the
        /// the value but does not undo any calls to <see cref="SetOnNextRead"/>. In other words, if <see cref="SetOnNextRead"/> is
        /// called, then <see cref="Value"/> is set to an arbitrary timestamp, the the getter is called, the current
        /// timestamp will still be captured.
        /// </summary>
        public DateTimeOffset? Value
        {
            get
            {
                var existingValue = Interlocked.CompareExchange(
                    ref _setOnNextRead,
                    FalseInt,
                    TrueInt);
                if (existingValue == TrueInt)
                {
                    _value = DateTimeOffset.UtcNow;
                }

                return _value;
            }

            set
            {
                _value = value;
            }
        }

        public void SetOnNextRead()
        {
            _setOnNextRead = TrueInt;
        }
    }
}
