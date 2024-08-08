// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public class DeleteAccountAuditRecord(string username, DeleteAccountAuditRecord.ActionStatus status, AuditedDeleteAccountAction action, string adminUsername) : AuditRecord<AuditedDeleteAccountAction>(action)
    {
        public enum ActionStatus
        {
            Success,
            Failure
        }

        public string Username = username;

        public string AdminUsername = adminUsername;

        public ActionStatus Status = status;

        public DeleteAccountAuditRecord(string username, ActionStatus status, AuditedDeleteAccountAction action)
            : this(username, status, action, adminUsername: string.Empty)
        {}

        public override string GetPath() => Username.ToLowerInvariant();
    }
}
