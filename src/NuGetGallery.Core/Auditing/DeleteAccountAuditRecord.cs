// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public class DeleteAccountAuditRecord : AuditRecord<AuditedDeleteAccountAction>
    {
        public enum ActionStatus
        {
            Success,
            Failure
        }

        public string Username;

        public string AdminUsername;

        public ActionStatus Status;

        public DeleteAccountAuditRecord(string username, ActionStatus status, AuditedDeleteAccountAction action)
            : this(username, status, action, adminUsername: string.Empty)
        {}

        public DeleteAccountAuditRecord(string username, ActionStatus status, AuditedDeleteAccountAction action, string adminUsername)
            : base(action)
        {
            Username = username;
            AdminUsername = adminUsername;
            Status = status;
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}
