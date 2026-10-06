// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class NamespaceReservationRequestService : INamespaceReservationRequestService
    {
        internal const string InitialPendingReason = "The request was saved; model assessment has not completed.";
        private readonly IEntityRepository<NamespaceReservationRequest> _repository;
        private readonly IUserService _userService;
        private readonly Lazy<INamespaceReservationFoundryClient> _foundryClient;
        private readonly ITelemetryService _telemetryService;
        private readonly INamespaceReservationEvidenceFactory _evidenceFactory;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly NamespaceReservationTracing _tracing;

        public NamespaceReservationRequestService(
            IEntityRepository<NamespaceReservationRequest> repository,
            IUserService userService,
            Lazy<INamespaceReservationFoundryClient> foundryClient,
            ITelemetryService telemetryService,
            INamespaceReservationEvidenceFactory evidenceFactory,
            IReservedNamespaceService reservedNamespaceService,
            NamespaceReservationTracing tracing = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _foundryClient = foundryClient ?? throw new ArgumentNullException(nameof(foundryClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _evidenceFactory = evidenceFactory ?? throw new ArgumentNullException(nameof(evidenceFactory));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _tracing = tracing ?? NamespaceReservationTracing.Disabled;
        }

        public IReadOnlyList<NamespaceReservationRequest> GetRequestsForUser(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.Key <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(user), "The user must have a positive key.");
            }

            var userKey = user.Key;
            return _repository.GetAll()
                .Where(request => request.SubmittedByUserKey == userKey)
                .OrderBy(request => request.CreatedTimestamp)
                .ThenBy(request => request.Key)
                .ToList();
        }

        public async Task<NamespaceReservationSubmissionResult> SubmitAsync(User submitter, NamespaceReservationRequestInput input)
        {
            if (submitter == null)
            {
                throw new ArgumentNullException(nameof(submitter));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true);

            if (!submitter.Confirmed)
            {
                errors.Add(new ValidationResult("Confirm your email address before requesting a namespace reservation."));
            }

            if (submitter.IsDeleted || submitter.IsLocked)
            {
                errors.Add(new ValidationResult("This account cannot request a namespace reservation."));
            }

            if (errors.Any())
            {
                _tracing.ValidationRejected();
                return new NamespaceReservationSubmissionResult(errors);
            }

            var namespaceValue = input.Namespace.Trim();
            if (!PackageIdValidator.IsValidPackageId(namespaceValue))
            {
                errors.Add(new ValidationResult(
                    "Enter a base namespace such as Contoso or Contoso.Tools, using letters, numbers, underscores, and single dots or hyphens between name segments. Do not include a wildcard or trailing dot.",
                    new[] { nameof(input.Namespace) }));
            }

            var ownerNames = input.Owner.Split(',').Select(name => name.Trim()).ToArray();
            var owners = new List<User>();
            if (ownerNames.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add(new ValidationResult("Enter owner names separated by commas, without empty entries.", new[] { nameof(input.Owner) }));
            }
            else
            {
                foreach (var ownerName in ownerNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var owner = _userService.FindByUsername(ownerName);
                    if (owner == null || owner.IsDeleted || owner.IsLocked)
                    {
                        errors.Add(new ValidationResult($"Owner '{ownerName}' does not exist or is unavailable.", new[] { nameof(input.Owner) }));
                    }
                    else if (ActionsRequiringPermissions.ManageAccount.CheckPermissions(submitter, owner) != PermissionsCheckResult.Allowed)
                    {
                        errors.Add(new ValidationResult(
                            $"You can only request a namespace for your own account or an organization you administer. You do not have permission for '{ownerName}'.",
                            new[] { nameof(input.Owner) }));
                    }
                    else
                    {
                        owners.Add(owner);
                    }
                }
            }

            if (errors.Any())
            {
                _tracing.ValidationRejected();
                return new NamespaceReservationSubmissionResult(errors);
            }

            using var trace = _tracing.StartAssessment(assessmentOnly: false);
            // Snapshot trusted scope separately from the three untrusted customer fields before any await.
            // Evidence uses authorized, canonical keys, never submitted aliases or mutable request state.
            var submitterKey = submitter.Key;
            var ownerKeys = owners.Select(owner => owner.Key).Distinct().ToArray();
            // Keep a separate immutable allocation scope; evidence providers receive an array.
            var reservationOwnerKeys = Array.AsReadOnly(ownerKeys.ToArray());
            var assessmentInput = new NamespaceReservationAssessmentInput
            {
                Namespace = namespaceValue,
                Owner = input.Owner.Trim(),
                Justification = input.Justification.Trim()
            };

            var request = new NamespaceReservationRequest
            {
                SubmittedByUserKey = submitterKey,
                Namespace = namespaceValue,
                RequestedOwnersJson = JsonConvert.SerializeObject(owners
                    .GroupBy(owner => owner.Key)
                    .Select(group => group.First())
                    .Select(owner => new { owner.Key, owner.Username })),
                Justification = input.Justification.Trim(),
                CreatedTimestamp = DateTime.UtcNow,
                Status = "Pending",
                Reason = InitialPendingReason,
                CompletedTimestamp = null
            };

            try
            {
                _repository.InsertOnCommit(request);
                await _repository.CommitChangesAsync();
                trace.Persistence(initial: true, succeeded: true);
            }
            catch (Exception ex)
            {
                trace.Persistence(initial: true, succeeded: false, exception: ex);
                throw;
            }

            var requestKey = request.Key;
            var status = "Pending";
            string reason;
            try
            {
                // Resolve lazily so invalid configuration/authentication cannot prevent saving the request.
                var client = _foundryClient.Value;
                if (!client.IsEnabled)
                {
                    reason = "Model assessment is disabled.";
                }
                else
                {
                    // Create only after durable Pending and only when enabled. Factory failures must
                    // use the safe failure path, never fall back to submission-only assessment.
                    var evidence = _evidenceFactory.Create(submitterKey, ownerKeys, namespaceValue)
                        ?? throw new InvalidOperationException("Namespace reservation evidence is unavailable.");
                    var assessment = await client.AssessAsync(assessmentInput, evidence, CancellationToken.None);
                    if (assessment == null || string.IsNullOrWhiteSpace(assessment.Reason) || assessment.Reason.Length > 4000
                        || (assessment.Status != "Approved" && assessment.Status != "Rejected"))
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    status = assessment.Status;
                    reason = assessment.Reason;
                }
            }
            catch (Exception ex)
            {
                trace.Error(ex, stage: "assessment");
                TrackSafeFailure(ex, "AssessNamespaceReservationRequest");
                status = "Pending";
                reason = ex is OperationCanceledException
                    ? "The model assessment timed out or was cancelled before a decision was accepted."
                    : "The model assessment could not be completed. No decision was accepted.";
            }

            request.Status = status;
            request.Reason = reason;
            var completed = DateTime.UtcNow;
            request.CompletedTimestamp = status == "Pending"
                ? (DateTime?)null
                : completed < request.CreatedTimestamp ? request.CreatedTimestamp : completed;
            try
            {
                // Same tracked row, including Pending reasons and safe failure explanations. The model
                // supplies neither database keys nor completion timestamps. Persist the assessment
                // before allocation, including the completion timestamp for an Approved assessment.
                await _repository.CommitChangesAsync();
                trace.Persistence(initial: false, succeeded: true);
            }
            catch (Exception ex) when (ex is DataException || ex is DbException || ex is ReadOnlyModeException)
            {
                trace.Persistence(initial: false, succeeded: false, exception: ex);
                // Restore local tracking state; do not retry inference or claim a decision was persisted.
                // A commit failure may be ambiguous, so the UI must not assert a final database status.
                request.Status = "Pending";
                request.Reason = InitialPendingReason;
                request.CompletedTimestamp = null;
                TrackSafeFailure(ex, "SaveNamespaceReservationAssessment");
                return Saved("Your namespace reservation request was saved, but we could not confirm that the assessment decision was saved. No namespace has been reserved.", isWarning: true);
            }
            catch (Exception ex)
            {
                trace.Persistence(initial: false, succeeded: false, exception: ex);
                throw;
            }

            if (status == "Approved")
            {
                try
                {
                    // The host, not the model, authorizes this write. The reservation service
                    // revalidates the saved approval and original owner scope in one transaction.
                    await _reservedNamespaceService.ReserveNamespaceForRequestAsync(
                        requestKey, namespaceValue, submitterKey, reservationOwnerKeys);
                }
                catch (Exception ex)
                {
                    trace.Error(ex, stage: "reservation");
                    TrackSafeFailure(ex, "ReserveApprovedNamespaceRequest");
                    // Commit/audit failure can be ambiguous. Never retry, compensate, or save
                    // this unit of work again. The table reads actual reservation records.
                    return Saved("Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.", isWarning: true);
                }

                trace.Complete();
                return Saved("Your namespace reservation request was approved and the namespace has been reserved for the requested owners. " + reason);
            }

            trace.Complete();
            return Saved("Your namespace reservation request was saved with status " + status + ". " + reason + " No namespace has been reserved.");
        }

        private static NamespaceReservationSubmissionResult Saved(string message, bool isWarning = false)
        {
            return new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(), message, isWarning);
        }

        private void TrackSafeFailure(Exception exception, string operation)
        {
            // Credential/model/database exception messages may contain sensitive information. Log only
            // an application-owned message and exception type, never the request or response contents.
            _telemetryService.TrackException(new InvalidOperationException("Namespace reservation assessment processing failed."), properties =>
            {
                properties["Operation"] = operation;
                properties["ExceptionType"] = exception.GetType().Name;
            });
        }
    }
}