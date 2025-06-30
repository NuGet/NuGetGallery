(function () {
    'use strict';

    var ConfirmDeleteMessage = "Are you sure you want to remove the Trusted Publisher?";
    var DeleteErrorMessage = "An error occurred while deleting the Trusted Publisher. Please try again.";
    var CreateErrorMessage = "An error occurred while creating a new Trusted Publisher. Please try again.";
    var EditErrorMessage = "An error occurred while editing an Trusted Publisher. Please try again.";

    $(function () {

        // Toggle collapsible example sections
        $(document).on('click', '.collapsible-example-toggle', function () {
            var $this = $(this);
            var $content = $this.closest('.collapsible-example').find('.collapsible-example-content');
            var expanded = $this.attr('aria-expanded') === 'true';

            // Toggle the visibility
            if (expanded) {
                $content.slideUp('fast');
                $this.attr('aria-expanded', 'false');
                $this.find('.ms-Icon').removeClass('ms-Icon--ChevronUp').addClass('ms-Icon--ChevronDown');
            } else {
                $content.slideDown('fast');
                $this.attr('aria-expanded', 'true');
                $this.find('.ms-Icon').removeClass('ms-Icon--ChevronDown').addClass('ms-Icon--ChevronUp');
            }
        });

        // Copy button functionality
        $(document).on('click', '.copy-button', function () {
            var $this = $(this);
            var $code = $this.closest('.collapsible-example').find('.example-code');
            var text = $code.text();
            window.nuget.copyTextToClipboard(text, $this);
        });

        function addAntiForgeryToken(data) {
            var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
            data["__RequestVerificationToken"] = $field.val();
        }

        function executeOnInactive(onTimeout, timeoutInMs) {
            var t;
            window.onload = resetTimer;
            document.onmousemove = resetTimer;
            document.onkeypress = resetTimer;

            function resetTimer() {
                clearTimeout(t);
                t = setTimeout(onTimeout, timeoutInMs)
            }
        }

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
                return id;
            }, self);
        }

        var _gitHubDetails = {};
        _gitHubDetails.Initialize = function (self) {
            // Create a gitHub object to hold all GitHub-related properties
            self.gitHub = {
                RepositoryOwner: ko.observable(),
                PendingRepositoryOwner: ko.observable(),
                RepositoryOwnerUid: computedUid(self, "github-repository-owner"),

                Repository: ko.observable(),
                PendingRepository: ko.observable(),
                RepositoryUid: computedUid(self, "github-repository"),

                RepositoryId: ko.observable(),
                PendingRepositoryId: ko.observable(),
                RepositoryIdUid: computedUid(self, "github-repository-id"),

                WorkflowFile: ko.observable(),
                PendingWorkflowFile: ko.observable(),
                WorkflowFileUid: computedUid(self, "github-workflow-file"),

                Environment: ko.observable(),
                PendingEnvironment: ko.observable(),
                EnvironmentUid: computedUid(self, "github-environment")
            };
        }

        _gitHubDetails.Update = function (self, data) {
            const details = data.PublisherName !== "GitHub" ? {} : data.PublisherDetails || {};
            const gitHub = self.gitHub;
            gitHub.RepositoryOwner(details.RepositoryOwner || '');
            gitHub.PendingRepositoryOwner(details.RepositoryOwner || '');

            gitHub.Repository(details.Repository || '');
            gitHub.PendingRepository(details.Repository || '');

            gitHub.RepositoryId(details.RepositoryId || '');
            gitHub.PendingRepositoryId(details.RepositoryId || '');

            gitHub.WorkflowFile(details.WorkflowFile || '');
            gitHub.PendingWorkflowFile(details.WorkflowFile || '');

            gitHub.Environment(details.Environment || '');
            gitHub.PendingEnvironment(details.Environment || '');
        }

        _gitHubDetails.CancelEdit = function (self) {
            const gitHub = self.gitHub;
            gitHub.PendingRepositoryOwner(gitHub.RepositoryOwner());
            gitHub.PendingRepository(gitHub.Repository());
            gitHub.PendingRepositoryId(gitHub.RepositoryId());
            gitHub.PendingWorkflowFile(gitHub.WorkflowFile());
            gitHub.PendingEnvironment(gitHub.Environment());
        }

        _gitHubDetails.AttachExtensions = function (self, validator) {
            // Validate required fields only
            const gitHub = self.gitHub;
            validator.submitted[gitHub.RepositoryOwnerUid()] = null;
            validator.submitted[gitHub.RepositoryUid()] = null;
            validator.submitted[gitHub.RepositoryIdUid()] = null;
            validator.submitted[gitHub.WorkflowFileUid()] = null;
        }

        _gitHubDetails.Valid = function (self) {
            const gitHub = self.gitHub;
            const owner = gitHub.PendingRepositoryOwner();
            const repository = gitHub.PendingRepository();
            const repositoryId = gitHub.PendingRepositoryId();
            const workflowFile = gitHub.PendingWorkflowFile();

            return owner && repository && workflowFile && repositoryId;
        }

        _gitHubDetails.CreatePendingCriteria = function (self) {
            const gitHub = self.gitHub;
            var githubData = {
                Name: 'GitHub',
                RepositoryOwner: gitHub.PendingRepositoryOwner() || '',
                Repository: gitHub.PendingRepository() || '',
                RepositoryId: gitHub.PendingRepositoryId() || '',
                WorkflowFile: gitHub.PendingWorkflowFile() || ''
            };

            // Only include environment if it has a value
            var environment = gitHub.PendingEnvironment();
            if (environment && environment.trim()) {
                githubData.Environment = environment;
            }

            // Return as JSON string
            return JSON.stringify(githubData);
        }

        function TrustedPublisherViewModel(parent, packageOwners, data) {
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
                return initialData.ImageUrls.TrustedPublisher;
            }, this);
            this.IconUrlFallback = ko.pureComputed(function () {
                const url =  initialData.ImageUrls.TrustedPublisherFallback;
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
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.RemoveUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        parent.Error(null);
                        parent.TrustedPublishers.remove(self);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(DeleteErrorMessage);
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
                var data = {
                    policyName: this.PendingPolicyName(),
                    owner: this.PackageOwner(),
                    criteria: _gitHubDetails.CreatePendingCriteria(this)
                };
                // Build the request.
                addAntiForgeryToken(data);

                // Send the request.
                this.PendingCreateOrEdit(true);
                $.ajax({
                    url: initialData.GenerateUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        self._UpdateData(data);
                        self.JustCreated(true);
                        parent.TrustedPublishers.unshift(self);

                        var newTrustedPublisher = new TrustedPublisherViewModel(parent, packageOwners);
                        parent.NewTrustedPublisher(newTrustedPublisher);
                        newTrustedPublisher.CancelEdit();

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
                    policyName: this.PendingPolicyName(),
                    owner: this.PackageOwner(),
                    criteria: _gitHubDetails.CreatePendingCriteria(this)
                };
                addAntiForgeryToken(data);

                // Send the request.
                this.PendingCreateOrEdit(true);
                parent.Error(null);
                $.ajax({
                    url: initialData.EditUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        self._UpdateData(data);
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

        function TrustedPublisherListViewModel(initialData) {
            var self = this;

            var trustedPublishers = $.map(initialData.TrustedPublishers, function (data) {
                return new TrustedPublisherViewModel(self, initialData.PackageOwners, data);
            });
            var newTrustedPublisher = new TrustedPublisherViewModel(self, initialData.PackageOwners, {PublisherName: 'GitHub'});

            this.TrustedPublishers = ko.observableArray(trustedPublishers);
            this.NewTrustedPublisher = ko.observable(newTrustedPublisher);
            this.Error = ko.observable();

            this.AnyJustCreated = ko.pureComputed(function () {
                var trustedPublishers = this.TrustedPublishers();
                for (var i in trustedPublishers) {
                    if (trustedPublishers[i].JustCreated()) {
                        return true;
                    }
                }
                return false;
            }, this);

            this.Idle = function () {
                var trustedPublishers = self.TrustedPublishers();
                for (var i in trustedPublishers) {
                    trustedPublishers[i].Idle();
                }
            };
        }

        // Set up the data binding.
        var trustedPublisherListViewModel = new TrustedPublisherListViewModel(initialData);
        ko.applyBindings(trustedPublisherListViewModel, document.body);

        // Configure the expander headings.
        window.nuget.configureExpander(
            "create-container",
            "Add",
            null,
            "CalculatorSubtract",
            null);
        window.nuget.configureExpanderHeading("manage-container");

        // Start the idle timer for 15 minutes.
        executeOnInactive(trustedPublisherListViewModel.Idle, 15 * 60 * 1000);
    });
})();
