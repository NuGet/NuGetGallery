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

        var _gitHubDetails = {};
        _gitHubDetails.Initialize = function (self) {
            self.GitHub_RepositoryOwner = ko.observable();
            self.GitHub_Repository = ko.observable();
            self.GitHub_RepositoryId = ko.observable();
            self.GitHub_WorkflowFile = ko.observable();
            self.GitHub_Environment = ko.observable();
            self.GitHub_Branch = ko.observable();
            self.GitHub_Tag = ko.observable();
        }

        _gitHubDetails.Update = function (self, data) {
            // Incoming data property names much match JsonProperty names in in GitHubPublisherDetailsViewModel.cs
            const details = data.PublisherName !== "GitHub" ? {} : data.PublisherDetails || {};
            self.GitHub_RepositoryOwner(details.repository_owner || '');
            self.GitHub_Repository(details.repository || '');
            self.GitHub_RepositoryId(details.repository_id || 0);
            self.GitHub_WorkflowFile(details.workflow || '');
            self.GitHub_Environment(details.environment || '');
            self.GitHub_Branch(details.branch || '');
            self.GitHub_Tag(details.tag || '');
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

                    if (existingOwner === null) {
                        existingOwner = { "Owner": data.Owner };
                    }

                    this.PackageOwner(existingOwner);

                } else if (this.PackageOwners.length == 1) {
                    this.PackageOwner(this.PackageOwners[0]);
                }

                // Provider specific properties
                _gitHubDetails.Update(this, data);
            };
            
            this.PackageOwners = packageOwners;
            this.packageViewModels = [];

            // Package owner selection
            this.PackageOwner = ko.observable(false);
            this.PendingCreateOrEdit = ko.observable(false);
            this.JustCreated = ko.observable(false);
            this.JustRegenerated = ko.observable(false);

            // Computed properties
            function ComputedId(prefix, suffix, defaultId ) {
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

            this.FormId = ComputedId("form");
            this.EditContainerId = ComputedId("edit", "container", "create-container");
            this.StartEditId = ComputedId("start-edit");
            this.CancelEditId = ComputedId("cancel-edit");
            this.CopyId = ComputedId("copy");
            this.PolicyNameId = ComputedId("policy-name");
            this.PackageOwnerId = ComputedId("package-owner");
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

            this._GetCopyButton = function () {
                return $("#" + self.CopyId());
            }

            this.AttachExtensions = function () {
                // Enable form validation.
                var $form = $("#" + self.FormId());
                $.validator.unobtrusive.parse($form);
                var $validator = $form.validate();

                // Immediately validate the PolicyName
                $validator.submitted[self.PolicyNameId()] = null;
            }

            this.Valid = function () {
                // Execute form validation.
                const formError = !$("#" + this.FormId()).valid();
                return !formError;
            }

            this.CancelEdit = function () {
                // Hide the form.
                var containerId = self.Key() ? self.EditContainerId() : 'create-container';
                $("#" + containerId).collapse('hide');

                // Reset the field values.
                self.PendingPolicyName(self.PolicyName());

                // Reset the form.
                var formElement = $("#" + self.FormId())[0];
                window.nuget.resetFormValidation(formElement);

                // Remove error classes from the form groups.
                $("#" + containerId + " .form-group.has-error-brand").removeClass("has-error-brand");

                // Scroll to the top of the available packages list.
                $("#" + containerId + " .available-packages .panel-body").scrollTop(0);

                // Re-attach extensions.
                self.AttachExtensions();

                // Focus the edit link so that the next tab key will continue with where it was left off prior to
                // opening the edit form. See https://github.com/NuGet/NuGetGallery/issues/8183.
                $("#" + self.StartEditId()).focus();
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
                // Build the request.
                var data = {
                    policyName: this.PendingPolicyName(),
                    owner: this.PackageOwner()
                };
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
                    federatedCredentialKey: this.Key()
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
            var newTrustedPublisher = new TrustedPublisherViewModel(self, initialData.PackageOwners);

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
