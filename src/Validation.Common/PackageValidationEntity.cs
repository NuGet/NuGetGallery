// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Jobs.Validation.Common.Validators;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationEntity
        : TableEntity
    {
        private DateTimeOffset _created;
        private Guid _validationId;

        public Guid ValidationId
        {
            get { return _validationId; }
            set
            {
                _validationId = value;

                // RowKey is our validation id
                RowKey = value.ToString();
            }
        }

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public DateTimeOffset Created
        {
            get { return _created; }
            set
            {
                _created = value;

                // PartitionKey is a reversed timestamp (entries ordered by date descending)
                PartitionKey = string.Format("{0:D19}", DateTimeOffset.MaxValue.Ticks - _created.Ticks);
            }
        }

        public DateTimeOffset? Finished { get; set; }
        public string RequestedValidators { get; set; }
        public string CompletedValidators { get; set; }
        public int ValidationResult { get; set; }

        public void ValidatorCompleted(string validator, ValidationResult result)
        {
            var completedValidators = GetCompletedValidatorsList();
            if (!completedValidators.Contains(validator))
            {
                completedValidators.Add(validator);
                CompletedValidators = string.Join(";", completedValidators.OrderBy(v => v));
            }

            if (ValidationResult >= 0)
            {
                ValidationResult = (int)result;
            }

            if (RequestedValidators == CompletedValidators)
            {
                Finished = DateTimeOffset.UtcNow;
            }
        }

        public List<string> GetCompletedValidatorsList()
        {
            return CompletedValidators.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}