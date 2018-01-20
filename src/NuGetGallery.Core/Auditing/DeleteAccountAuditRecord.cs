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

        public string UserName;

        public string AdminUserName;

        public ActionStatus Status;

        public DeleteAccountAuditRecord(string userName, ActionStatus status, AuditedDeleteAccountAction action)
            : this(userName, status, action, adminUserName: string.Empty)
        {}

        public DeleteAccountAuditRecord(string userName, ActionStatus status, AuditedDeleteAccountAction action, string adminUserName)
            : base(action)
        {
            UserName = userName;
            AdminUserName = adminUserName;
            Status = status;
        }

        public override string GetPath()
        {
            return UserName.ToLowerInvariant();
        }
    }
}
