// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Gallery.CredentialExpiration.Models;
using NuGet.Jobs.Configuration;

namespace Gallery.CredentialExpiration
{
    public class GalleryCredentialExpiration : ICredentialExpirationExporter
    {
        private readonly Job _job;
        private readonly CredentialExpirationJobMetadata _jobMetadata;

        public GalleryCredentialExpiration(Job job, CredentialExpirationJobMetadata jobMetadata)
        {
            _job = job;
            _jobMetadata = jobMetadata;
        }

        /// <summary>
        /// Used for the expiring credentials.
        /// </summary>
        /// <param name="jobMetadata"></param>
        /// <returns></returns>
        public DateTimeOffset GetMaxNotificationDate()
        {
            return _jobMetadata.JobRunTime.AddDays(_jobMetadata.WarnDaysBeforeExpiration);
        }

        /// <summary>
        /// Used for the Expired credentials.
        /// </summary>
        /// <param name="jobMetadata"></param>
        /// <returns></returns>
        public DateTimeOffset GetMinNotificationDate()
        {
            // In case that the job failed to run for more than 1 day, go back more than the WarnDaysBeforeExpiration value 
            // with the number of days that the job did not run
            return  _jobMetadata.JobCursor.JobCursorTime;
        }

        public async Task<List<ExpiredCredentialData>> GetCredentialsAsync(TimeSpan timeout)
        {
            // Set the day interval for the accounts that will be queried for expiring /expired credentials.
            var maxNotificationDate = ConvertToString(GetMaxNotificationDate());
            var minNotificationDate = ConvertToString(GetMinNotificationDate());

            // Connect to database
            using (var galleryConnection = await _job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                // Fetch credentials that expire in _warnDaysBeforeExpiration days 
                // + the user's e-mail address
                return  (await galleryConnection.QueryWithRetryAsync<ExpiredCredentialData>(
                    Strings.GetExpiredCredentialsQuery,
                    param: new { MaxNotificationDate = maxNotificationDate, MinNotificationDate = minNotificationDate },
                    maxRetries: 3,
                    commandTimeout: timeout)).ToList();
            }
        }

        /// <summary>
        /// Send email of credential expired during the time interval [_jobMetadata.CursorTime, _jobMetadata.JobRunTime) 
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        public List<ExpiredCredentialData> GetExpiredCredentials(List<ExpiredCredentialData> credentialSet)
        {
            // Send email to the accounts that had credentials expired from the last execution.
            // The second condition is meant only for far cases that the SQL query for data filtering was modified by mistake and more credentials were included.
            return credentialSet.Where(x => (x.Expires < _jobMetadata.JobRunTime) && (x.Expires >= _jobMetadata.JobCursor.JobCursorTime)).ToList();
        }

        /// <summary>
        /// Returns the expiring credentials.
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        public List<ExpiredCredentialData> GetExpiringCredentials(List<ExpiredCredentialData> credentialSet)
        {
            // Send email to the accounts that will have credentials expiring in the next _warnDaysBeforeExpiration days and did not have any warning email sent yet.
            // Avoid cases when the cursor is out of date and MaxProcessedCredentialsTime < JobRuntime
            var sendEmailsDateLeftBoundary = (_jobMetadata.JobCursor.MaxProcessedCredentialsTime > _jobMetadata.JobRunTime)
                ? _jobMetadata.JobCursor.MaxProcessedCredentialsTime
                : _jobMetadata.JobRunTime;

            return credentialSet.Where( x => x.Expires > sendEmailsDateLeftBoundary).ToList();
        }

        /// <summary>
        /// Converts a <see cref="DateTimeOffset"/> string with the "yyyy-MM-dd HH:mm:ss" format.
        /// </summary>
        /// <param name="value">The <see cref="DateTimeOffset"/> to be converterd.</param>
        /// <returns></returns>
        private string ConvertToString(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
