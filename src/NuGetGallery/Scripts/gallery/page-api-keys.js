(function () {
    'use strict';

    var MissingScopesErrorMessage = "At least one scope must be selected.";
    var MissingSubjectsErrorMessage = "Either a glob pattern must be specified or at least one package ID must be selected.";
    var ConfirmRegenerateMessage = "Are you sure you want to regenerate the API key?";
    var RegenerateErrorMessage = "An error occurred while regenerating the API key. Please try again.";
    var ConfirmDeleteMessage = "Are you sure you want to remove the API key?";
    var DeleteErrorMessage = "An error occurred while deleting the API key. Please try again.";
    var CreateErrorMessage = "An error occurred while creating a new API key. Please try again.";
    var EditErrorMessage = "An error occurred while editing an API key. Please try again.";
    var ConfirmRevokeMessage = "Are you sure you want to revoke the API key?";
    var RevokeErrorMessage = "An error occurred while revoking the API key. Please try again.";

    $(function () {
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

        function globToRegex(glob) {
            var specialChars = "\\^$*+?.()|{}[]";
            var regexChars = ["^"];
            for (var i = 0; i < glob.length; ++i) {
                var c = glob.charAt(i);
                switch (c) {
                    case '*':
                        regexChars.push(".*");
                        break;
                    default:
                        if (specialChars.indexOf(c) >= 0) {
                            regexChars.push("\\");
                        }
                        regexChars.push(c);
                }
            }

            regexChars.push("$");
            return new RegExp(regexChars.join(""), "i");
        }

        function PackageViewModel(id) {
            this.Id = ko.observable(id);
            this.Selected = ko.observable(false);
            this.Matched = ko.observable(false);
            this.Checked = ko.pureComputed({
                read: function () {
                    return this.Selected() || this.Matched();
                },
                write: function (value) {
                    this.Selected(value);
                },
                owner: this
            });
        }

        function ApiKeyViewModel(parent, packageOwners, data) {
            var self = this;

            data = data || {};

            this.PackageOwners = packageOwners;
            this.packageViewModels = [];

            // Generic API key properties.
            this._SetPackageSelection = function (packages) {
                $.each(self.packageViewModels, function (i, m) {
                    var index = $.inArray(m.Id(), packages);
                    m.Selected(index !== -1);
                });
                this._packages = packages;
            };
            this._UpdateData = function (data) {
                this.Key(data.Key || 0);
                this.Type(data.Type || null);
                this.Value(data.Value || this.Value());
                this.Description(data.Description || null);
                this.Expires(data.Expires || null);
                this.HasExpired(data.HasExpired || false);
                this.RevocationSource(data.RevocationSource || null);
                this.IsNonScopedApiKey(data.IsNonScopedApiKey || false);
                this.Owner(data.Owner || null);
                this.Scopes(data.Scopes || []);
                this.Packages(data.Packages || []);
                this.GlobPattern(data.GlobPattern || "");
                this._SetPackageSelection(this.Packages());

                if (this.Owner()) {
                    var existingOwner = ko.utils.arrayFirst(
                        this.PackageOwners,
                        function (owner) {
                            return owner.Owner.toUpperCase() === data.Owner.toUpperCase()
                        });

                    if (existingOwner === null) {
                        existingOwner = { "Owner": data.Owner, "PackageIds": [] };
                    }

                    this.PackageOwner(existingOwner);

                } else if (this.PackageOwners.length == 1) {
                    this.PackageOwner(this.PackageOwners[0]);
                } 
            };

            this.Key = ko.observable(0);
            this.Type = ko.observable();
            this.Value = ko.observable();
            this.Description = ko.observable();
            this.Expires = ko.observable();
            this.HasExpired = ko.observable();
            this.RevocationSource = ko.observable();
            this.IsNonScopedApiKey = ko.observable();
            this.Owner = ko.observable();
            this.Scopes = ko.observableArray();
            this.Packages = ko.observableArray();
            this.GlobPattern = ko.observable();
 
            // Properties used for the form
            this.PendingDescription = ko.observable();

            // Package owner selection
            this.PackageOwner = ko.observable();
            this.PackageOwnerName = ko.pureComputed(function () {
                return self.PackageOwner() && self.PackageOwner().Owner;
            }, this);
            this.PackageOwner.subscribe(function (newOwner) {
                if (newOwner == null) {
                    return;
                }

                // When the package owner scope is changed, update the selected action scopes to those that are allowed on behalf of the new package owner.
                var isPushNewSelected = function () {
                    return self.PushScope() === initialData.PackagePushScope;
                };
                var isPushExistingSelected = function () {
                    return self.PushScope() === initialData.PackagePushVersionScope;
                };
                var isUnlistSelected = function () {
                    return self.UnlistScopeChecked();
                };

                // If either push new or push existing is selected and that action is not allowed on behalf of the new owner,
                // swap the scope to the other push action if it is allowed or deselect it.
                if (!newOwner.CanPushNew && isPushNewSelected()) {
                    self.PushScope(newOwner.CanPushExisting ? initialData.PackagePushVersionScope : null);
                } else if (!newOwner.CanPushExisting && isPushExistingSelected()) {
                    self.PushScope(newOwner.CanPushNew ? initialData.PackagePushScope : null);
                }

                // If unlist is selected and that action is not allowed on behalf of the new owner, deselect it.
                if (!newOwner.CanUnlist && isUnlistSelected()) {
                    self.UnlistScopeChecked(false);
                }

                // If after this process, no actions are selected, select one that is allowed.
                if (!isPushNewSelected() && !isPushExistingSelected() && !isUnlistSelected()) {
                    if (newOwner.CanPushNew) {
                        self.PushScope(initialData.PackagePushScope);
                    } else if (newOwner.CanPushExisting) {
                        self.PushScope(initialData.PackagePushVersionScope);
                    } else if (newOwner.CanUnlist) {
                        self.UnlistScopeChecked(true);
                    }
                }

                // If either push new or push existing are selected, enable the textbox that determines if they are selectable.
                self.PushEnabled(isPushNewSelected() || isPushExistingSelected());
            });

            this.PushEnabled = ko.observable(false);

            this.PushAnyEnabled = ko.pureComputed(function () {
                return self.PackageOwner() && (self.PackageOwner().CanPushNew || self.PackageOwner().CanPushExisting);
            }, this);
            
            this.PushNewEnabled = ko.pureComputed(function () {
                return self.PackageOwner() && self.PackageOwner().CanPushNew;
            }, this);

            this.PushExistingEnabled = ko.pureComputed(function () {
                return self.PackageOwner() && self.PackageOwner().CanPushExisting;
            }, this);

            this.UnlistEnabled = ko.pureComputed(function () {
                return self.PackageOwner() && self.PackageOwner().CanUnlist;
            }, this);

            this.ExpiresIn = ko.observable();
            this.PushScope = ko.observable();
            this.UnlistScopeChecked = ko.observable(false);
            this.UnlistScope = ko.pureComputed(function () {
                return self.UnlistScopeChecked() ? initialData.PackageUnlistScope : null;
            }, this);
            this.PendingGlobPattern = ko.observable();
            this.PendingPackages = ko.observableArray();

            this.ScopesError = ko.observable();
            this.SubjectsError = ko.observable();
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

            this.ShortPackageList = ko.pureComputed(function () {
                return this.Packages().slice(0, 3);
            }, this);
            this.RemainingPackageList = ko.pureComputed(function () {
                return this.Packages().slice(3);
            }, this);
            this.SelectPackagesEnabled = ko.pureComputed(function () {
                return this.Scopes().length > 0 || this.PendingScopes().length > 0;
            }, this);
            this.FormId = ComputedId("form");
            this.RemainingPackagesId = ComputedId("remaining-packages");
            this.EditContainerId = ComputedId("edit", "container", "create-container");
            this.StartEditId = ComputedId("start-edit");
            this.CancelEditId = ComputedId("cancel-edit");
            this.CopyId = ComputedId("copy");
            this.DescriptionId = ComputedId("description");
            this.PackageOwnerId = ComputedId("package-owner");
            this.GlobPatternId = ComputedId("glob-pattern");
            this.ExpiresInId = ComputedId("expires-in");
            this.PackagePushId = ComputedId("package-push");
            this.PackagePushVersionId = ComputedId("package-push-version");
            this.IconUrl = ko.pureComputed(function () {
                if (this.HasExpired()) {
                    return initialData.ImageUrls.ApiKeyExpired;
                } else if (this.IsNonScopedApiKey()) {
                    return initialData.ImageUrls.ApiKeyLegacy;
                } else if (this.Value()) {
                    return initialData.ImageUrls.ApiKeyNew;
                } else {
                    return initialData.ImageUrls.ApiKey;
                }
            }, this);
            this.IconUrlFallback = ko.pureComputed(function () {
                var url;
                if (this.HasExpired()) {
                    url = initialData.ImageUrls.ApiKeyExpiredFallback;
                } else if (this.IsNonScopedApiKey()) {
                    url =  initialData.ImageUrls.ApiKeyLegacyFallback;
                } else if (this.Value()) {
                    url =  initialData.ImageUrls.ApiKeyNewFallback;
                } else {
                    url =  initialData.ImageUrls.ApiKeyFallback;
                }

                return "this.src='" + url + "'; this.onerror = null;";
            }, this);
            this.PendingScopes = ko.pureComputed(function () {
                var scopes = [];
                if (this.PushEnabled() && this.PushScope()) {
                    scopes.push(this.PushScope());
                }
                if (this.UnlistEnabled() && this.UnlistScope()) {
                    scopes.push(this.UnlistScope());
                }
                return scopes;
            }, this);
            this.PendingSubjects = ko.pureComputed(function () {
                var subjects = [];

                var pendingGlobPattern = this.PendingGlobPattern();
                if (pendingGlobPattern) {
                    subjects.push(pendingGlobPattern)
                }

                $.each(this.PendingPackages(), function (i, p) {
                    if (p.Selected() && !p.Matched()) {
                        subjects.push(p.Id());
                    }
                });

                return subjects;
            }, this);
            this.SelectedCount = ko.pureComputed(function () {
                return ko.utils.arrayFilter(this.PendingPackages(), function (item) {
                    return item.Checked();
                }).length;
            }, this);
            this.TotalCount = ko.pureComputed(function () {
                return this.PendingPackages().length;
            }, this);
            this.SelectedCountLabel = ko.pureComputed(function () {
                return ko.unwrap(this.SelectedCount).toLocaleString()
                    + ' of '
                    + ko.unwrap(this.TotalCount).toLocaleString()
                    + ' package' + (this.TotalCount === 1 ? '' : 's')
                    + ' selected';
            }, this);

            // Apply the glob pattern to the package IDs
            this.ApplyPendingGlobPattern = function (newValue) {
                var pattern = globToRegex(newValue || "");
                $.each(self.PendingPackages(), function (i, p) {
                    var matched = pattern.test(p.Id());
                    p.Matched(matched);
                });
            };
            this.PendingGlobPattern.subscribe(function (newValue) {
                self.ApplyPendingGlobPattern(newValue);
            });

            // Initialize pending packages data when owner selection changes.
            this.PackageOwner.subscribe(function (newValue) {
                // Initialize each package ID as a view model. This view model is used to track manual checkbox checks
                // and whether the glob pattern matches the ID.
                if (newValue == null) {
                    return;
                }

                var packageIdToViewModel = {};
                self.packageViewModels = [];
                $.each(newValue.PackageIds, function (i, packageId) {
                    packageIdToViewModel[packageId] = new PackageViewModel(packageId);
                    self.packageViewModels.push(packageIdToViewModel[packageId]);
                });
                self.PendingPackages(self.packageViewModels);
                self.ApplyPendingGlobPattern(self.PendingGlobPattern());

                // reset form validation error
                self.ScopesError(null);
                self.SubjectsError(null);
            });

            // Update data only after PackageOwner subscription is fully enabled.
            this._UpdateData(data);

            // Default owner to self to initialize the pending data.
            this.PendingGlobPattern(this.GlobPattern());

            // Apply validation to the scopes and subjects
            this.PendingScopes.subscribe(function (newValue) {
                if (newValue.length === 0 && self.Scopes().length === 0) {
                    self.ScopesError(MissingScopesErrorMessage);
                } else {
                    self.ScopesError(null);
                }
            });
            this.PendingSubjects.subscribe(function (newValue) {
                if (newValue.length === 0) {
                    self.SubjectsError(MissingSubjectsErrorMessage);
                } else {
                    self.SubjectsError(null);
                }
            });

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

                // Immediately validate the description and glob pattern.
                $validator.submitted[self.DescriptionId()] = null;
                $validator.submitted[self.GlobPatternId()] = null;

                // Enable copy popover.
                self._GetCopyButton().popover({ trigger: 'manual' });
            }

            this.Valid = function () {
                // Execute form validation.
                var formError = !$("#" + this.FormId()).valid();

                // Execute scopes and subjects validation.
                this.PendingGlobPattern.valueHasMutated();

                return !formError &&
                       !this.ScopesError() &&
                       !this.SubjectsError();
            }

            this.CancelEdit = function () {
                // Hide the form.
                var containerId = self.Key() ? self.EditContainerId() : 'create-container';
                $("#" + containerId).collapse('hide');

                // Reset the field values.
                self.PendingDescription(self.Description());
                self.ExpiresIn($("#" + self.ExpiresInId() + " option:last-child").val());
                self.PushEnabled(false);
                self.PushScope(null);
                self.UnlistScopeChecked(false);
                self.PendingGlobPattern(self.GlobPattern());
                this._SetPackageSelection(self._packages);

                // Reset the custom errors.
                self.ScopesError(null);
                self.SubjectsError(null);

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

            this.ShowRemainingPackages = function (_, e) {
                $(e.target).remove();
                $("#" + self.RemainingPackagesId()).collapse('show');
            };

            this.Copy = function () {
                var $copyButton = self._GetCopyButton();
                window.nuget.copyTextToClipboard(self.Value(), $copyButton);
                $copyButton.popover('show');
                setTimeout(function () {
                    $copyButton.popover('destroy');
                }, 1000);
            }

            this.Regenerate = function () {
                if (!window.nuget.confirmEvent(ConfirmRegenerateMessage)) {
                    return;
                }

                // Build the request.
                var data = {
                    credentialType: this.Type(),
                    credentialKey: this.Key()
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.RegenerateUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        self._UpdateData(data);
                        self.JustCreated(false);
                        self.JustRegenerated(true);
                        parent.ApiKeys.remove(self);
                        parent.ApiKeys.unshift(self);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(RegenerateErrorMessage);
                    }
                });
            };

            this.Delete = function () {
                if (!window.nuget.confirmEvent(ConfirmDeleteMessage)) {
                    return;
                }

                // Build the request.
                var data = {
                    credentialType: this.Type(),
                    credentialKey: this.Key()
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
                        parent.ApiKeys.remove(self);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(DeleteErrorMessage);
                    }
                });
            };

            this.Revoke = function () {
                if (!window.nuget.confirmEvent(ConfirmRevokeMessage)) {
                    return;
                }

                // Build the request.
                var data = {
                    credentialType: this.Type(),
                    credentialKey: this.Key(),
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: initialData.RevokeUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        self._UpdateData(data);
                        self.JustCreated(false);
                        self.JustRegenerated(false);
                        parent.ApiKeys.remove(self);
                        parent.ApiKeys.unshift(self);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error(RevokeErrorMessage);
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
                    description: this.PendingDescription(),
                    owner: this.PackageOwnerName(),
                    scopes: this.PendingScopes(),
                    subjects: this.PendingSubjects(),
                    expirationInDays: this.ExpiresIn()
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
                        parent.ApiKeys.unshift(self);

                        var newApiKey = new ApiKeyViewModel(parent, packageOwners);
                        parent.NewApiKey(newApiKey);
                        newApiKey.CancelEdit();

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
                    credentialType: this.Type(),
                    credentialKey: this.Key(),
                    subjects: this.PendingSubjects()
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
                this.Value(null);
                this.JustCreated(false);
                this.JustRegenerated(false);
            };
        }

        function ApiKeyListViewModel(initialData) {
            var self = this;

            var apiKeys = $.map(initialData.ApiKeys, function (data) {
                return new ApiKeyViewModel(self, initialData.PackageOwners, data);
            });
            var newApiKey = new ApiKeyViewModel(self, initialData.PackageOwners);

            this.ApiKeys = ko.observableArray(apiKeys);
            this.NewApiKey = ko.observable(newApiKey);
            this.Error = ko.observable();

            this.AnyJustCreated = ko.pureComputed(function () {
                var apiKeys = this.ApiKeys();
                for (var i in apiKeys) {
                    if (apiKeys[i].JustCreated()) {
                        return true;
                    }
                }
                return false;
            }, this);
            this.AnyJustRegenerated = ko.pureComputed(function () {
                var apiKeys = this.ApiKeys();
                for (var i in apiKeys) {
                    if (apiKeys[i].JustRegenerated()) {
                        return true;
                    }
                }
                return false;
            }, this);
            this.ExpiredDescriptions = ko.pureComputed(function () {
                var apiKeys = this.ApiKeys();
                var descriptions = [];
                for (var i in apiKeys) {
                    if (apiKeys[i].HasExpired()) {
                        if (!apiKeys[i].RevocationSource()) {
                            descriptions.push(apiKeys[i].Description());
                        }
                    }
                }
                return descriptions;
            }, this);
            this.RevocationDescriptions = ko.pureComputed(function () {
                var apiKeys = this.ApiKeys();
                var descriptions = [];
                for (var i in apiKeys) {
                    if (apiKeys[i].HasExpired()) {
                        if (apiKeys[i].RevocationSource()) {
                            descriptions.push(apiKeys[i].Description());
                        }
                    }
                }
                return descriptions;
            }, this);

            this.Idle = function () {
                var apiKeys = self.ApiKeys();
                for (var i in apiKeys) {
                    apiKeys[i].Idle();
                }
            };
        }

        // Set up the data binding.
        var apiKeyListViewModel = new ApiKeyListViewModel(initialData);
        ko.applyBindings(apiKeyListViewModel, document.body);

        // Configure the expander headings.
        window.nuget.configureExpander(
            "create-container",
            "Add",
            null,
            "CalculatorSubtract",
            null);
        window.nuget.configureExpanderHeading("manage-container");

        // Start the idle timer for 10 minutes.
        executeOnInactive(apiKeyListViewModel.Idle, 10 * 60 * 1000);
    });

})();
