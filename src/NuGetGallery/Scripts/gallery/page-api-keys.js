(function () {
    'use strict';

    $.validator.addMethod("requiredscopes", function (value, element) {
        var model = ko.dataFor(element);
        return model.PendingScopes().length > 0;
    }, "At least one scope must be selected.");

    $.validator.addMethod("requiredsubjects", function (value, element) {
        var model = ko.dataFor(element);
        return model.PendingSubjects().length > 0;
    }, "Either a glob pattern must be specified or at least one package ID must be selected.");

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

        function ApiKeyViewModel(parent, data, packageIds) {
            var self = this;
            this._parent = parent;

            // Initial each package ID as a view model. This view model is used to track manual checkbox checks
            // and whether the glob pattern matches the ID.
            var packageIdToViewModel = {};
            var packageViewModels = [];
            $.each(packageIds, function (i, packageId) {
                packageIdToViewModel[packageId] = new PackageViewModel(packageId);
                packageViewModels.push(packageIdToViewModel[packageId]);
            });
            $.each(data.Packages, function (i, packageId) {
                if (packageId in packages) {
                    packageIdToViewModel[packageId].Selected(true);
                }
            });

            // Generic API key properties.
            this.Key = ko.observable(data.Key);
            this.Type = ko.observable(data.Type);
            this.Value = ko.observable(data.Value);
            this.Description = ko.observable(data.Description);
            this.Expires = ko.observable(data.Expires);
            this.HasExpired = ko.observable(data.HasExpired);
            this.IsNonScopedV1ApiKey = ko.observable(data.IsNonScopedV1ApiKey);
            this.Scopes = ko.observableArray(data.Scopes);
            this.Packages = ko.observableArray(data.Packages);
            this.GlobPattern = ko.observable(data.GlobPattern);

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

            // Computed properties
            this.ShortPackageList = ko.computed(function () {
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
                var pattern = globToRegex(newValue);
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

            this.EnableValidation = function () {
                var form = $("#" + this.FormId());
                $.validator.unobtrusive.parse(form);
            }

            this.Valid = function (form) {
                // Execute form validation.
                var formError = !$(form).valid();

                // Execute scopes and subjects validation.
                this.PendingGlobPattern(this.PendingGlobPattern() || "");
                this.UnlistScope(this.UnlistScope() || []);

                return !formError &&
                       !this.ScopesError() &&
                       !this.SubjectsError();
            }

            this.Submit = function (form) {
                if (!this.Valid(form)) {
                    return;
                }

                // Build the request.
                var data, url;
                if (!this.Key()) {
                    // Generate a new key.
                    data = {
                        description: this.Description(),
                        scopes: this.PendingScopes(),
                        subjects: this.PendingSubjects(),
                        expirationInDays: this.ExpiresIn()
                    };
                    url = initialData.GenerateUrl;
                } else {
                    // Edit the existing key.
                    data = {
                        credentialType: this.Type(),
                        credentialKey: this.Key(),
                        subjects: this.PendingSubjects()
                    };
                    url = initialData.EditUrl;
                }
                addAntiForgeryToken(data);

                $.ajax({
                    url: url,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        console.log(arguments);
                    }
                })
            };
        }

        // Set up the data binding.
        var viewModel = {};
        var apiKeys = $.map(initialData.ApiKeys, function (x) {
            return new ApiKeyViewModel(viewModel, x, initialData.PackageIds);
        });
        var newApiKey = new ApiKeyViewModel(viewModel, { Key: 0, Type: null }, initialData.PackageIds);

        viewModel.ApiKeys = ko.observableArray(apiKeys);
        viewModel.NewApiKey = ko.observable(newApiKey);

        ko.applyBindings(viewModel, $("#manage-container")[0]);

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
