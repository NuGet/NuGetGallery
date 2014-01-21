using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Api
{
    public static class ModelConverters
    {
        public static Invocation ToModel(this InvocationState self)
        {
            return ToModel(self, null);
        }

        public static Invocation ToModel(this InvocationState self, UrlHelper url)
        {
            return new Invocation()
            {
                Id = self.Id,
                Job = self.Job,
                JobInstanceName = self.JobInstanceName,
                Source = self.Source,

                Payload = self.Payload,

                Status = self.Status,
                Result = self.Result,
                ResultMessage = self.ResultMessage,
                LastUpdatedBy = self.LastUpdatedBy,
                LogUrl = url == null ? self.LogUrl : url.RouteUri(Routes.GetInvocationLog, new {
                    id = self.Id.ToString("N").ToLowerInvariant()
                }).AbsoluteUri,

                DequeueCount = self.DequeueCount,
                IsContinuation = self.IsContinuation,

                LastDequeuedAt = self.LastDequeuedAt,
                LastSuspendedAt = self.LastSuspendedAt,
                CompletedAt = self.CompletedAt,
                QueuedAt = self.QueuedAt,
                NextVisibleAt = self.NextVisibleAt,
                UpdatedAt = self.UpdatedAt
            };
        }

        public static Job ToModel(this JobDescription self)
        {
            return new Job()
            {
                Name = self.Name,
                Description = self.Description,
                Runtime = self.Runtime,
                Assembly = self.Assembly,
                EventProviderId = self.EventProviderId,
                Enabled = self.Enabled
            };
        }

        public static InvocationStatistics ToInvocationModel(this InvocationStatisticsRecord self)
        {
            return FillStatisticsModel(self, new InvocationStatistics());
        }

        public static JobStatistics ToJobModel(this InvocationStatisticsRecord self)
        {
            return FillStatisticsModel(self, new JobStatistics() { Job = self.Item });
        }

        public static InstanceStatistics ToInstanceModel(this InvocationStatisticsRecord self)
        {
            return FillStatisticsModel(self, new InstanceStatistics() { Instance = self.Item });
        }

        private static T FillStatisticsModel<T>(InvocationStatisticsRecord self, T existing)
            where T : InvocationStatistics
        {
            existing.Queued = self.Queued;
            existing.Dequeued = self.Dequeued;
            existing.Executing = self.Executing;
            existing.Executed = self.Executed;
            existing.Cancelled = self.Cancelled;
            existing.Suspended = self.Suspended;
            existing.Completed = self.Completed;
            existing.Faulted = self.Faulted;
            existing.Crashed = self.Crashed;
            existing.Aborted = self.Aborted;
            existing.Total = self.Total;
            return existing;
        }
    }
}
