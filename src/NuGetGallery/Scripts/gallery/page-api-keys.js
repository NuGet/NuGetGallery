(function () {
    'use strict';

    $(function () {
        function addAntiForgeryToken(data) {
            var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
            data["__RequestVerificationToken"] = $field.val();
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

        function ApiKeyViewModel(parent, packageIds, data) {
            var self = this;

            data = data || {};

            // Initial each package ID as a view model. This view model is used to track manual checkbox checks
            // and whether the glob pattern matches the ID.
            var packageIdToViewModel = {};
            var packageViewModels = [];
            $.each(packageIds, function (i, packageId) {
                packageIdToViewModel[packageId] = new PackageViewModel(packageId);
                packageViewModels.push(packageIdToViewModel[packageId]);
            });

            // Generic API key properties.
            this._UpdateData = function (data) {
                this.Key(data.Key || 0);
                this.Type(data.Type || null);
                this.Value(data.Value || this.Value());
                this.Description(data.Description || null);
                this.Expires(data.Expires || null);
                this.HasExpired(data.HasExpired || false);
                this.IsNonScopedV1ApiKey(data.IsNonScopedV1ApiKey || false);
                this.Scopes(data.Scopes || []);
                this.Packages(data.Packages || []);
                this.GlobPattern(data.GlobPattern || "");
                $.each(packageViewModels, function (i, m) {
                    var index = $.inArray(m.Id(), data.Packages);
                    m.Selected(index !== -1);
                });
            };
            this.Key = ko.observable();
            this.Type = ko.observable();
            this.Value = ko.observable();
            this.Description = ko.observable();
            this.Expires = ko.observable();
            this.HasExpired = ko.observable();
            this.IsNonScopedV1ApiKey = ko.observable();
            this.Scopes = ko.observableArray();
            this.Packages = ko.observableArray();
            this.GlobPattern = ko.observable();
            this._UpdateData(data);

            // Properties used for API key create
            this.ExpiresIn = ko.observable();
            this.PushEnabled = ko.observable(false);
            this.PushScope = ko.observable(initialData.PackagePushScope);
            this.UnlistScope = ko.observableArray();

            // Properties used for API key create and edit
            this.PendingGlobPattern = ko.observable();
            this.PendingPackages = ko.observableArray();
            this.ScopesError = ko.observable();
            this.SubjectsError = ko.observable();
            this.PendingCreateOrEdit = ko.observable(false);
            this.JustCreated = ko.observable(false);
            this.JustRegenerated = ko.observable(false);

            // Computed properties
            this.ShortPackageList = ko.pureComputed(function () {
                return this.Packages().slice(0, 3);
            }, this);
            this.SelectPackagesEnabled = ko.pureComputed(function () {
                return this.Scopes().length > 0 ||
                    this.PushEnabled() ||
                    this.UnlistScope().length > 0;
            }, this);
            this.FormId = ko.pureComputed(function () {
                return "form-" + this.Key();
            }, this);
            this.EditContainerId = ko.pureComputed(function () {
                return "edit-" + this.Key() + "-container";
            }, this);
            this.StartEditId = ko.pureComputed(function () {
                return "start-edit-" + this.Key();
            }, this);
            this.CancelEditId = ko.pureComputed(function () {
                return "cancel-edit-" + this.Key();
            }, this);
            this.CopyId = ko.pureComputed(function () {
                return "copy-" + this.Key();
            }, this);
            this.IconUrl = ko.pureComputed(function () {
                if (this.HasExpired()) {
                    return initialData.ImageUrls.ApiKeyExpired;
                } else if (this.IsNonScopedV1ApiKey()) {
                    return initialData.ImageUrls.ApiKeyLegacy;
                } else if (this.Value()) {
                    return initialData.ImageUrls.ApiKeyNew;
                } else {
                    return initialData.ImageUrls.ApiKey;
                }
            }, this);

            this.PendingScopes = ko.pureComputed(function () {
                var scopes = [];
                if (this.PushEnabled()) {
                    scopes.push(this.PushScope());
                }
                scopes.push.apply(scopes, this.UnlistScope());
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

            // Apply the glob pattern to the package IDs
            this.PendingGlobPattern.subscribe(function (newValue) {
                var pattern = globToRegex(newValue || "");
                $.each(self.PendingPackages(), function (i, p) {
                    var matched = pattern.test(p.Id());
                    p.Matched(matched);
                });
            });

            // Initialize the pending data
            this.PendingPackages(packageViewModels);
            this.PendingGlobPattern(this.GlobPattern());

            // Apply validation to the scopes and subjects
            this.PendingScopes.subscribe(function (newValue) {
                if (newValue.length === 0 && self.Scopes().length === 0) {
                    self.ScopesError("At least one scope must be selected.");
                } else {
                    self.ScopesError(null);
                }
            });
            this.PendingSubjects.subscribe(function (newValue) {
                if (newValue.length === 0) {
                    self.SubjectsError("Either a glob pattern must be specified or at least one package ID must be selected.");
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
                var form = $("#" + self.FormId());
                $.validator.unobtrusive.parse(form);

                // Enable copy popover.
                self._GetCopyButton().popover({ trigger: 'manual' });
            }

            this.Valid = function (form) {
                // Execute form validation.
                var formError = !$(form).valid();

                // Execute scopes and subjects validation.
                this.PendingGlobPattern.valueHasMutated();
                this.UnlistScope.valueHasMutated();

                return !formError &&
                       !this.ScopesError() &&
                       !this.SubjectsError();
            }

            this.CancelEdit = function () {
                $("#" + self.EditContainerId()).collapse('hide');
            };

            this.Copy = function () {
                window.nuget.copyTextToClipboard(self.Value());
                var $copyButton = self._GetCopyButton();
                $copyButton.popover('show');
                setTimeout(function () {
                    $copyButton.popover('destroy');
                }, 1000);
            }

            this.Regenerate = function () {
                if (!window.nuget.confirmEvent("Are you sure you want to regenerate the API key?")) {
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
                        parent.Error("An error occurred while regenerating the API key. Please try again.");
                    }
                });
            };

            this.Delete = function () {
                if (!window.nuget.confirmEvent("Are you sure you want to remove the API key?")) {
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
                        parent.Error("An error occurred while deleting the API key. Please try again.");
                    }
                });
            };

            this.CreateOrEdit = function (form) {
                if (!this.Valid(form)) {
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
                    description: this.Description(),
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

                        var newApiKey = new ApiKeyViewModel(parent, packageIds);
                        parent.NewApiKey(newApiKey);
                        newApiKey.CancelEdit();
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        parent.Error("An error occurred while creating a new API key. Please try again.");
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
                        parent.Error("An error occurred while editing an API key. Please try again.");
                    },
                    complete: function () {
                        self.PendingCreateOrEdit(false);
                    }
                });
            };
        }

        function ApiKeysViewModel(initialData) {
            var self = this;

            var apiKeys = $.map(initialData.ApiKeys, function (data) {
                return new ApiKeyViewModel(self, initialData.PackageIds, data);
            });
            var newApiKey = new ApiKeyViewModel(self, initialData.PackageIds);

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
            
        }

        // Set up the data binding.
        var apiKeysViewModel = new ApiKeysViewModel(initialData);
        ko.applyBindings(apiKeysViewModel, $("#manage-container")[0]);

        // Configure the expander headings.
        window.nuget.configureExpanderHeading("manage-container");
        window.nuget.configureExpander(
            "edit-0-container",
            "CalculatorAddition",
            null,
            "CalculatorSubtract",
            null);
        window.nuget.configureExpanderHeading("example-container");
    });

})();
