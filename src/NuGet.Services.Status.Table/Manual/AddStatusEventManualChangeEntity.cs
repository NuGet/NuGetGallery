using System;

namespace NuGet.Services.Status.Table.Manual
{
    public class AddStatusEventManualChangeEntity : ManualStatusChangeEntity
    {
        public AddStatusEventManualChangeEntity()
        {
        }

        public AddStatusEventManualChangeEntity(
            string eventAffectedComponentPath,
            ComponentStatus eventAffectedComponentStatus,
            string messageContents,
            bool eventIsActive)
            : base(ManualStatusChangeType.AddStatusEvent)
        {
            EventAffectedComponentPath = eventAffectedComponentPath ?? throw new ArgumentNullException(nameof(eventAffectedComponentPath));
            EventAffectedComponentStatus = (int)eventAffectedComponentStatus;
            MessageContents = messageContents ?? throw new ArgumentNullException(nameof(messageContents));
            EventIsActive = eventIsActive;
        }

        public string EventAffectedComponentPath { get; set; }

        /// <remarks>
        /// This should be a <see cref="ComponentStatus"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int EventAffectedComponentStatus { get; set; }

        public string MessageContents { get; set; }

        public bool EventIsActive { get; set; }
    }
}
