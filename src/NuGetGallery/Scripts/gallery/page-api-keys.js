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

        function ApiKeyViewModel(data, packageIds) {
            var self = this;

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
            this.PushScope = ko.observable(packagePushScope);
            this.UnlistScope = ko.observableArray();

            // Properties used for API key create and edit
            this.PendingGlobPattern = ko.observable();
            this.PendingPackages = ko.observableArray(packageViewModels);

            // Computed properties
            this.ShortPackageList = ko.computed(function () {
                return this.Packages().slice(0, 3);
            }, this);
            this.SelectPackagesEnabled = ko.pureComputed(function () {
                return this.PushEnabled() || this.UnlistScope().length > 0;
            }, this);
            this.SubjectsValidationId = ko.pureComputed(function () {
                return "subjects-" + this.Key() + "-validation-message";
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
                return ko.utils.arrayFilter(self.PendingPackages(), function (item) {
                    return item.Checked();
                }).length;
            });

            // Apply the glob pattern to the package IDs
            this.PendingGlobPattern.subscribe(function (newValue) {
                var pattern = globToRegex(newValue);
                $.each(self.PendingPackages(), function (i, p) {
                    var matched = pattern.test(p.Id());
                    p.Matched(matched);
                });
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

            this.Submit = function (form) {
                console.log($(form).valid());
                console.log(this.PendingScopes());
                console.log(this.PendingSubjects());
            };
        }

        // Set up the data binding.
        var apiKeys = $.map(initialApiKeys, function (x) {
            return new ApiKeyViewModel(x, packageIds);
        });
        var newApiKey = new ApiKeyViewModel({ Key: 0, Type: null }, packageIds);

        var model = {
            ApiKeys: ko.observableArray(apiKeys),
            NewApiKey: newApiKey
        };

        ko.applyBindings(model, $("#manage-container")[0]);

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
