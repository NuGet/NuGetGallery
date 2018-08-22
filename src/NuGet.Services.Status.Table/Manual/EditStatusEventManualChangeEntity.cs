using System;

namespace NuGet.Services.Status.Table.Manual
{
    public class EditStatusEventManualChangeEntity : ManualStatusChangeEntity
    {
        public EditStatusEventManualChangeEntity()
        {
        }

        public EditStatusEventManualChangeEntity(
            string eventAffectedComponentPath,
            ComponentStatus eventAffectedComponentStatus,
            DateTime eventStartTime,
            bool eventIsActive)
            : base(ManualStatusChangeType.EditStatusEvent)
        {
            EventAffectedComponentPath = eventAffectedComponentPath ?? throw new ArgumentNullException(nameof(eventAffectedComponentPath));
            EventAffectedComponentStatus = (int)eventAffectedComponentStatus;
            EventStartTime = eventStartTime;
            EventIsActive = eventIsActive;
        }

        public string EventAffectedComponentPath { get; set; }

        /// <remarks>
        /// This should be a <see cref="ComponentStatus"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int EventAffectedComponentStatus { get; set; }

        public DateTime EventStartTime { get; set; }

        public bool EventIsActive { get; set; }
    }
}
