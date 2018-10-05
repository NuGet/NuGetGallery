// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// A message posted on the status page to inform users of a change in the service's status.
    /// Each message is correlated to a <see cref="EventEntity"/>.
    /// See <see cref="Message"/>.
    /// </summary>
    public class MessageEntity : ChildEntity<EventEntity>
    {
        public const string DefaultPartitionKey = "messages";

        public MessageEntity()
        {
        }

        public MessageEntity(EventEntity eventEntity, DateTime time, string contents, MessageType type)
            : base(
                  DefaultPartitionKey,
                  GetRowKey(eventEntity, time),
                  eventEntity)
        {
            Time = time;
            Contents = contents;
            Type = (int)type;
        }

        public DateTime Time { get; set; }

        public string Contents { get; set; }

        /// <remarks>
        /// This should be a <see cref="MessageType"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int Type { get; set; }

        public static string GetRowKey(string eventRowKey, DateTime time)
        {
            return $"{eventRowKey}_{time.ToString("o")}";
        }

        public static string GetRowKey(EventEntity eventEntity, DateTime time)
        {
            return GetRowKey(eventEntity.RowKey, time);
        }
    }
}
