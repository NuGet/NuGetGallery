// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Class used to serialize a <see cref="Message"/> in a table.
    /// </summary>
    public class MessageEntity : TableEntity
    {
        public const string DefaultPartitionKey = "messages";

        public MessageEntity()
        {
        }

        public MessageEntity(EventEntity eventEntity, DateTime time, string contents)
            : base(DefaultPartitionKey, GetRowKey(eventEntity, time))
        {
            EventRowKey = eventEntity.RowKey;
            Time = time;
            Contents = contents;
        }

        public string EventRowKey { get; set; }
        public DateTime Time { get; set; }
        public string Contents { get; set; }

        public Message AsMessage()
        {
            return new Message(Time, Contents);
        }

        private static string GetRowKey(EventEntity eventEntity, DateTime time)
        {
            return $"{eventEntity.RowKey}_{time.ToString("o")}";
        }
    }
}
