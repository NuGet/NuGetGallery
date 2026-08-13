(function () {
    'use strict';

    const CreateErrorMessage = "An error occurred while creating a new Trusted Publisher Policy. Please try again.";
    const EditErrorMessage = "An error occurred while editing a Trusted Publisher Policy. Please try again.";
    const EnableErrorMessage = "An error occurred while enabling the Trusted Publisher Policy. Please try again.";
    const ConfirmDeleteMessage = "Are you sure you want to remove the Trusted Publisher Policy?";
    const DeleteErrorMessage = "An error occurred while deleting the Trusted Publisher Policy. Please try again.";

    const GitHubActionsPublisherName = "GitHubActions"; // must match the PublisherType in GitHubPolicyDetailsViewModel.cs


    ko.bindingHandlers.trimmedValue = {
        init: function(element, valueAccessor, allBindings, viewModel, bindingContext) {
            // Handle the initial value and user input
            var observable = valueAccessor();
            var interceptor = ko.pureComputed({
                read: observable,
                write: function(value) {
                    observable(typeof value === "string" ? value.trim() : value);
                }
            });
            
            // Use the standard value binding with our interceptor
            ko.bindingHandlers.value.init(element, function() { return interceptor; }, allBindings, viewModel, bindingContext);
        },
        update: function(element, valueAccessor, allBindings, viewModel, bindingContext) {
            // Use the standard value binding for updates
            ko.bindingHandlers.value.update(element, valueAccessor, allBindings, viewModel, bindingContext);
        }
    };

    $(function () {
        function computedUid(self, prefix, suffix, defaultId) {
            return ko.pureComputed(function () {
                var id = self.Key();
                if (id === 0 && defaultId) {
                    id = defaultId;
                }
                else {
                    if (prefix) {
                        id = prefix + "-" + id;
                    }
                    if (suffix) {
                        id = id + "-" + suffix;
                    }
                }

                return "__" + id;
            }, self);
        }

        var _gitHubDetails = {};
        _gitHubDetails.Initialize = function (self) {
            // Create a gitHub object to hold all GitHub-related properties
            self.gitHub = {
                IsPermamentlyEnabled: ko.observable(),
                EnabledDaysLeft: ko.observable(),

                RepositoryOwner: ko.observable(),
                PendingRepositoryOwner: ko.observable(),
                RepositoryOwnerUid: computedUid(self, "github-repository-owner"),

                RepositoryOwnerId: ko.observable(),

                Repository: ko.observable(),
                PendingRepository: ko.observable(),
                RepositoryUid: computedUid(self, "github-repository"),

                RepositoryId: ko.observable(),

                WorkflowFile: ko.observable(),
                PendingWorkflowFile: ko.observable(),
                WorkflowFileUid: computedUid(self, "github-workflow-file"),

                Environment: ko.observable(),
                PendingEnvironment: ko.observable(),
                EnvironmentUid: computedUid(self, "github-environment"),
            };
        }

        _gitHubDetails.Update = function (self, data) {
            const details = data.PublisherName !== GitHubActionsPublisherName ? {} : data.PolicyDetails || {};
            const gitHub = self.gitHub;
            if (data.Key) {
                gitHub.IsPermamentlyEnabled(details.IsPermanentlyEnabled || false);
                gitHub.EnabledDaysLeft(details.EnabledDaysLeft || 0);
            } else {
                // Ignore the IsPermanentlyEnabled and EnabledDaysLeft for new items
                gitHub.IsPermamentlyEnabled(true);
                gitHub.EnabledDaysLeft(1); // any positive number is okay
            }

            gitHub.RepositoryOwner(details.RepositoryOwner || '');
            gitHub.PendingRepositoryOwner(details.RepositoryOwner || '');

            gitHub.RepositoryOwnerId(details.RepositoryOwnerId || '');

            gitHub.Repository(details.Repository || '');
            gitHub.PendingRepository(details.Repository || '');

            gitHub.RepositoryId(details.RepositoryId || '');

            gitHub.WorkflowFile(details.WorkflowFile || '');
            gitHub.PendingWorkflowFile(details.WorkflowFile || '');

            gitHub.Environment(details.Environment || '');
            gitHub.PendingEnvironment(details.Environment || '');
        }

        _gitHubDetails.CancelEdit = function (self) {
            const gitHub = self.gitHub;
            gitHub.PendingRepositoryOwner(gitHub.RepositoryOwner());
            gitHub.PendingRepository(gitHub.Repository());
            gitHub.PendingWorkflowFile(gitHub.WorkflowFile());
            gitHub.PendingEnvironment(gitHub.Environment());
        }

        _gitHubDetails.AttachExtensions = function (self, validator) {
            // Validate required fields only
            const gitHub = self.gitHub;
            validator.submitted[gitHub.RepositoryOwnerUid()] = null;
            validator.submitted[gitHub.RepositoryUid()] = null;
            validator.submitted[gitHub.WorkflowFileUid()] = null;
        }

        _gitHubDetails.Valid = function (self) {
            const gitHub = self.gitHub;
            const owner = gitHub.PendingRepositoryOwner();
            const repository = gitHub.PendingRepository();
            const workflowFile = gitHub.PendingWorkflowFile();

            return owner && repository && workflowFile;
        }

        _gitHubDetails.LookupGitHubIdentifiers = function (self, existingPolicies, callback) {
            const gitHub = self.gitHub;
            const owner = gitHub.PendingRepositoryOwner().toLowerCase();
            const repository = gitHub.PendingRepository().toLowerCase();

            // Validate inputs
            if (!owner || !repository) {
                callback();
                return;
            }

            // Check if we already have the IDs
            if (gitHub.RepositoryOwnerId() && gitHub.RepositoryId()) {
                callback();
                return;
            }

            // First, check if we can find the repository IDs from existing policies
            if (existingPolicies && existingPolicies.length > 0) {
                for (var i = 0; i < existingPolicies.length; i++) {
                    var existing = existingPolicies[i].gitHub;
                    if (existing && existing !== gitHub &&
                        existing.RepositoryOwner().toLowerCase() === owner &&
                        existing.Repository().toLowerCase() === repository &&
                        existing.RepositoryOwnerId() &&
                        existing.RepositoryId()) {

                        gitHub.RepositoryOwnerId(existing.RepositoryOwnerId());
                        gitHub.RepositoryId(existing.RepositoryId());
                        callback();
                        return;
                    }
                }
            }

            // Build GitHub API URL
            var apiUrl = "https://api.github.com/repos/" + encodeURIComponent(owner) + "/" + encodeURIComponent(repository);
            var properties = { apiUrl: apiUrl };

            // Make AJAX request to GitHub API
            $.ajax({
                url: apiUrl,
                type: 'GET',
                dataType: 'json',
                timeout: 10000, // 10 second timeout
                headers: {
                    'Accept': 'application/vnd.github.v3+json'
                },
                success: function (data) {
                    // Extract repository info from response
                    gitHub.RepositoryOwnerId(data.owner && data.owner.id ? data.owner.id.toString() : '');
                    gitHub.RepositoryId(data.id ? data.id.toString() : '');
                    properties.httpStatus = 200; // success
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    // Track the error to Application Insights
                    properties.httpStatus = jqXHR.status;
                    properties.responseText = jqXHR.responseText;
                },
                complete: function() {
                    window.nuget.sendMetric('GitHubRepositoryLookup', 1, properties);
                    callback();
                }
            });
        };

        _gitHubDetails.CreatePendingCriteria = function (self) {
            // MUST MATCH GitHub details deserialization in GitHubPolicyDetailsViewModel.cs.
            const gitHub = self.gitHub;
            var githubData = {
                Name: GitHubActionsPublisherName,
                RepositoryOwner: gitHub.PendingRepositoryOwner() || '',
                Repository: gitHub.PendingRepository() || '',
                WorkflowFile: gitHub.PendingWorkflowFile() || '',
                Environment: gitHub.PendingEnvironment() || ''
            };

            // Include IDs only if available
            var repositoryOwnerId = gitHub.RepositoryOwnerId();
            if (repositoryOwnerId) {
                githubData.RepositoryOwnerId = repositoryOwnerId;
            }

            var repositoryId = gitHub.RepositoryId();
            if (repositoryId) {
                githubData.RepositoryId = repositoryId;
            }

            // Return as JSON string
            return JSON.stringify(githubData);
        }

        function PolicyViewModel(parent, packageOwners, data) {
            var self = this;
            data = data || {};

            // Common properties
            this.Key = ko.observable(0);
            this.PolicyName = ko.observable();
            this.PendingPolicyName = ko.observable();
            this.Owner = ko.observable();
            this.PublisherName = ko.observable();
            
            // Provider specific properties
            _gitHubDetails.Initialize(this);
            
            this._UpdateData = function (data) {
                this.Key(data.Key || 0);
                this.PolicyName(data.PolicyName || null);
                this.IsOwnerValid(data.IsOwnerValid || null);
                this.PendingPolicyName(data.PolicyName || null);
                this.Owner(data.Owner || null);
                this.PublisherName(data.PublisherName || null);

                if (this.Owner()) {
                    var existingOwner = ko.utils.arrayFirst(
                        this.PackageOwners,
                        function (owner) {
                            return owner.toUpperCase() === data.Owner.toUpperCase()
                        });

                    if (existingOwner !== null) {
                        this.PackageOwner(existingOwner);
                    }
                } else if (this.PackageOwners.length == 1) {
                    this.PackageOwner(this.PackageOwners[0]);
                }

                // Provider specific properties
                _gitHubDetails.Update(this, data);
            };
            
            this.PackageOwners = packageOwners;
            this.packageViewModels = [];

            // Package owner selection
            this.PackageOwner = ko.observable(null);
            this.IsOwnerValid = ko.observable(null);
            this.PendingCreateOrEdit = ko.observable(false);
            this.JustCreated = ko.observable(false);
            this.JustRegenerated = ko.observable(false);

            // Computed properties
            this.FormUid = computedUid(self, "form");
            this.EditContainerUid = computedUid(self, "edit", "container", "create-container");
            this.StartEditUid = computedUid(self, "start-edit");
            this.CancelEditUid = computedUid(self, "cancel-edit");
            this.PolicyNameUid = computedUid(self, "policy-name");
            this.PackageOwnerUid = computedUid(self, "package-owner");
            this.IconUrl = ko.pureComputed(function () {
                // Use disabled icon if there's an invalid reason or if enabled days left is 0 or less
                if (!this.IsOwnerValid() || this.gitHub.EnabledDaysLeft() <= 0) {
                    return initialData.ImageUrls.DisabledTrustedPolicy;
                }
                if (!this.gitHub.IsPermamentlyEnabled()) {
                    return initialData.ImageUrls.TemporaryTrustedPolicy;
                }
                return initialData.ImageUrls.TrustedPolicy;
            }, this);
            this.IconUrlFallback = ko.pureComputed(function () {
                var url = initialData.ImageUrls.TrustedPolicyFallback;
                if (!this.IsOwnerValid() || this.gitHub.EnabledDaysLeft() <= 0) {
                    return initialData.ImageUrls.DisabledTrustedPolicyFallback;
                }
                if (!this.gitHub.IsPermamentlyEnabled()) {
                    return initialData.ImageUrls.TemporaryTrustedPolicyFallback;
                }
                return "this.src='" + url + "'; this.onerror = null;";
            }, this);


            this._UpdateData(data);

            // Methods
            this.StopPropagation = function (_, e) {
                e.stopPropagation();
                return true;
            };

            this.AttachExtensions = function () {
                // Enable form validation.
                var $form = $("#" + self.FormUid());
                $.validator.unobtrusive.parse($form);
                var $validator = $form.validate();

                // Immediately validate the PolicyName
                $validator.submitted[self.PolicyNameUid()] = null;
                _gitHubDetails.AttachExtensions(self, $validator);
            }

            this.Valid = function () {
                // Execute form validation.
                const $form = $("#" + this.FormUid());
                const formError = !$form.valid();
                
                // Check if PackageOwner is selected
                const packageOwnerError = !this.PackageOwner();
                const gitHubValid = _gitHubDetails.Valid(this);
                return !formError && !packageOwnerError && gitHubValid;
            }

            this.CancelEdit = function () {
                // Hide the form.
                var containerId = self.Key() ? self.EditContainerUid() : 'create-container';
                $("#" + containerId).collapse('hide');

                // Reset the field values.
                self.PendingPolicyName(self.PolicyName());
                _gitHubDetails.CancelEdit(self);

                // Reset PackageOwner to null for new items, or to the current Owner for existing items
                if (!self.Key()) {
                    self.PackageOwner(null);
                }

                // Reset the form.
                var formElement = $("#" + self.FormUid())[0];
                window.nuget.resetFormValidation(formElement);

                // Remove error classes from the form groups.
                $("#" + containerId + " .form-group.has-error-brand").removeClass("has-error-brand");

                // Scroll to the top of the available packages list.
                $("#" + containerId + " .available-packages .panel-body").scrollTop(0);

                // Re-attach extensions.
                self.AttachExtensions();

                // Focus the edit link so that the next tab key will continue with where it was left off prior to
                // opening the edit form. See https://github.com/NuGet/NuGetGallery/issues/8183.
                $("#" + self.StartEditUid()).focus();
            };

            this.Delete = function () {
                if (!window.nuget.confirmEvent(ConfirmDeleteMessage)) {
                    return;
                }

                // Build the request.
                var data = {
                    federatedCredentialKey: this.Key()
                };
                window.nuget.addAjaxAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.RemoveUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        parent.Error(null);
                        parent.Policies.remove(self);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(DeleteErrorMessage);
                    }
                });
            };

            this.Enable = function () {
                // Build the request.
                var data = {
                    federatedCredentialKey: this.Key()
                };
                window.nuget.addAjaxAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.EnableUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (newData) {
                        parent.Error(null);
                        self._UpdateData(newData);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(EnableErrorMessage);
                    }
                });
            };

            this.CreateOrEdit = function () {
                if (!this.Valid()) {
                    return;
                }

                if (!this.Key()) {
                    this.Create();
                } else {
                    this.Edit();
                }
            };

            this.Create = function () {
                // Set loading state immediately
                this.PendingCreateOrEdit(true);

                // Get owner and repo IDs first
                _gitHubDetails.LookupGitHubIdentifiers(self, parent.Policies(),
                    function () {
                        self.CreateAfterLookup();
                    }
                );
            };

            this.CreateAfterLookup = function () {
                // Build the request.
                var data = {
                    policyName: this.PendingPolicyName(),
                    owner: this.PackageOwner(),
                    criteria: _gitHubDetails.CreatePendingCriteria(this)
                };
                window.nuget.addAjaxAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.GenerateUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        self._UpdateData(data);
                        self.JustCreated(true);
                        parent.Policies.unshift(self);

                        var newPolicy = new PolicyViewModel(parent, packageOwners);
                        parent.NewPolicy(newPolicy);
                        newPolicy.CancelEdit();

                        $("#manage-container").collapse("show");
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(CreateErrorMessage);
                    },
                    complete: function () {
                        self.PendingCreateOrEdit(false);
                    }
                });
            };

            this.Edit = function () {
                // Build the request.
                var data = {
                    federatedCredentialKey: this.Key(),
                    criteria: _gitHubDetails.CreatePendingCriteria(this),
                    policyName: this.PendingPolicyName()
                };
                window.nuget.addAjaxAntiForgeryToken(data);

                // Send the request.
                this.PendingCreateOrEdit(true);
                parent.Error(null);
                $.ajax({
                    url: initialData.EditUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (newData) {
                        parent.Error(null);
                        self._UpdateData(newData);
                        self.CancelEdit();
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(EditErrorMessage);
                    },
                    complete: function () {
                        self.PendingCreateOrEdit(false);
                    }
                });
            };

            this.Idle = function () {
                this.JustCreated(false);
                this.JustRegenerated(false);
            };
        }

        function PolicyListViewModel(initialData) {
            var self = this;

            var policies = $.map(initialData.Policies, function (data) {
                return new PolicyViewModel(self, initialData.PackageOwners, data);
            });
            var newPolicy = new PolicyViewModel(self, initialData.PackageOwners, { PublisherName: GitHubActionsPublisherName });

            this.Policies = ko.observableArray(policies);
            this.NewPolicy = ko.observable(newPolicy);
            this.Error = ko.observable();

            this.AnyJustCreated = ko.pureComputed(function () {
                var policies = this.Policies();
                for (var i in policies) {
                    if (policies[i].JustCreated()) {
                        return true;
                    }
                }
                return false;
            }, this);

            this.Idle = function () {
                var policies = self.Policies();
                for (var i in policies) {
                    policies[i].Idle();
                }
            };
        }

        // Set up the data binding.
        var policyListViewModel = new PolicyListViewModel(initialData);
        ko.applyBindings(policyListViewModel, document.body);

        // Configure the expander headings.
        window.nuget.configureExpander(
            "create-container",
            "Add",
            null,
            "CalculatorSubtract",
            null);
        window.nuget.configureExpanderHeading("manage-container");

        // Start the idle timer for 15 minutes.
        window.nuget.executeOnInactive(policyListViewModel.Idle, 15 * 60 * 1000);
    });
})();
