'use strict';

// Shared model between the CVE view and the CWE view
function ManageDeprecationSecurityDetailListViewModel(title, label, placeholder) {
    var self = this;

    this.title = ko.observable(title);
    this.label = ko.observable(label);
    this.placeholder = ko.observable(placeholder);

    // Whether or not the checkbox for this section is checked.
    this.hasIds = ko.observable(false);

    // The IDs that the user has added to this form.
    this.addedIds = ko.observableArray();
    // The IDs to submit with the form.
    this.ids = ko.pureComputed(function () {
        if (self.hasIds()) {
            return self.addedIds();
        } else {
            // If the checkbox for this section is not selected, do not return the added IDs.
            return [];
        }
    }, this);

    // The ID that has been typed into the textbox but not yet submitted.
    this.addId = ko.observable('');
    this.add = function () {
        self.addedIds.push(self.addId());
        self.addId('');
    };

    this.remove = function (id) {
        self.addedIds.remove(id);
    };

    // Import the existing version deprecation state into this model.
    this.import = function (ids) {
        var hasIds = ids && ids.length;
        self.hasIds(hasIds);
        if (hasIds) {
            self.addedIds(ids);
        } else {
            self.addedIds.removeAll();
        }
    };

    // Export this model into an array of IDs.
    this.export = function () {
        // Copy the array. 
        // Otherwise, the value returned by this function will change based on the UI.
        return self.ids().slice(0);
    };
}

function ManageDeprecationViewModel(id, versionsDictionary, defaultVersion, submitUrl, packageUrl, getAlternatePackageVersions) {
    var self = this;

    this.dropdownSelector = '.deprecation-section .dropdown';
    this.dropdownBtnSelector = self.dropdownSelector + ' .dropdown-btn';
    this.dropdownContentSelector = self.dropdownSelector + ' .dropdown-content';
    this.getFocusableDropdownContentElements = function () {
        return $(self.dropdownContentSelector).find(':focusable:not(:has(:focusable))');
    };

    this.dropdownOpen = ko.observable(false);
    this.toggleDropdown = function () {
        self.dropdownOpen(!self.dropdownOpen());
    };

    this.isAncestor = function (element, ancestorSelector) {
        var $target = $(element);
        // '.closest' returns the list of ancestors between this element and the selector.
        // If the selector is not an ancestor of the element, it returns an empty list.
        return $target.closest(ancestorSelector).length;
    };

    this.isElementInsideDropdown = function (element) {
        return self.isAncestor(element, self.dropdownSelector);
    };

    // If the user clicks outside of the dropdown, close it.
    $(document).click(function (event) {
        if (!self.isElementInsideDropdown(event.target)) {
            self.dropdownOpen(false);
        }
    });

    // If an element outside of the dropdown gains focus, close it.
    $(document).focusin(function (event) {
        if (!self.isElementInsideDropdown(event.target)) {
            self.dropdownOpen(false);
        }
    });

    this.escapeKeyCode = 27;
    $(document).keydown(function (event) {
        var target = event.target;
        if (self.isElementInsideDropdown(target)) {
            // If we press escape while focus is inside the dropdown, close it
            if (event.which === self.escapeKeyCode) { // Escape key
                self.dropdownOpen(false);
                event.preventDefault();
                $(self.dropdownBtnSelector).focus();
            }
        }
    });

    // A filter to be applied to the versions.
    this.versionFilter = ko.observable('');

    // Existing deprecation state information per version.
    this.versions = Object.keys(versionsDictionary).map(function (version) {
        // Whether or not a version's checkbox is selected.
        var checked = ko.observable(false);

        // Whether or not the version should appear in the UI (it is not filtered out).
        var visible = ko.pureComputed(function () {
            return version.startsWith(self.versionFilter());
        });

        var versionData = versionsDictionary[version];
        return {
            version: version,
            text: versionData.Text,
            deprecated: versionData.IsVulnerable || versionData.IsLegacy || versionData.IsOther,
            checked: checked,
            visible: visible,
            selected: ko.pureComputed(function () {
                // If a version is checked but not visible in the UI, it is not selected.
                return checked() && visible();
            })
        };
    });

    // The versions selected in the UI.
    this.chosenVersions = ko.pureComputed(function () {
        return ko.utils
            .arrayFilter(
                self.versions,
                function (version) { return version.selected(); })
            .map(function (version) { return version.version; });
    }, this);

    this.hasNoVersionsSelected = ko.pureComputed(function () {
        return !self.chosenVersions().length;
    }, this);

    // A string to display to the user describing how many versions are selected out of how many.
    this.chosenVersionsString = ko.pureComputed(function () {
        var chosenVersions = self.chosenVersions();
        if (chosenVersions.length === 0) {
            return "Select version(s) to deprecate";
        }

        if (chosenVersions.length === self.versions.length) {
            "All current versions";
        }

        return chosenVersions.join(', ');
    }, this);

    // Whether or not the select all checkbox for the versions is selected.
    this.versionSelectAllChecked = ko.pureComputed(function () {
        return !ko.utils
            .arrayFirst(
                self.versions,
                function (version) {
                    // If a version is visible in the UI and is not checked, select all must not be checked.
                    return version.visible() && !version.checked();
                });
    }, this);

    // Toggles whether or not all versions are selected.
    // If the checkbox is not selected, it selects all versions visible in the UI.
    // If the checkbox is already selected, it deselects all versions visible in the UI.
    this.toggleVersionSelectAll = function () {
        var checked = !self.versionSelectAllChecked();
        ko.utils.arrayForEach(
            self.versions,
            function (version) {
                if (version.visible()) {
                    version.checked(checked);
                }
            });

        return true;
    };

    this.isVulnerable = ko.observable(false);
    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    // The model for the CVEs view.
    this.cves = new ManageDeprecationSecurityDetailListViewModel(
        "CVE ID(s)",
        "You can provide a list of CVEs applicable to the vulnerability.",
        "Add CVE by ID e.g. CVE-2014-999999, CVE-2015-888888");

    // Whether or not the checkbox for the CVSS section is checked.
    this.hasCvss = ko.observable(false);

    // The CVSS rating entered by the user.
    this.selectedCvssRating = ko.observable(0);

    // A string describing the severity of the CVSS rating entered by the user.
    var invalidCvssRatingString = 'Invalid CVSS rating!';
    this.cvssRatingLabel = ko.pureComputed(function () {
        var rating = self.selectedCvssRating();
        if (!rating) {
            return '';
        }

        var ratingFloat = parseFloat(rating);
        if (isNaN(ratingFloat) || ratingFloat < 0 || ratingFloat > 10) {
            return invalidCvssRatingString;
        }

        if (ratingFloat < 4) {
            return 'Low';
        }

        if (ratingFloat < 7) {
            return 'Medium';
        }

        if (ratingFloat < 9) {
            return 'High';
        }

        return 'Critical';
    }, this);
    this.cvssRatingIsInvalid = ko.pureComputed(function () {
        return self.cvssRatingLabel() === invalidCvssRatingString;
    }, this);

    // The CVSS rating to submit with the form.
    this.cvssRating = ko.pureComputed(function () {
        if (self.hasCvss()) {
            return self.selectedCvssRating();
        } else {
            // If the CVSS section is unchecked, don't submit the CVSS rating with the form.
            return null;
        }
    }, this);

    // The model for the CWEs view
    this.cwes = new ManageDeprecationSecurityDetailListViewModel(
        "CWE(s)",
        "You can add one or more CWEs applicable to the vulnerability.",
        "Add CWE by ID or title");

    // The ID entered into the alternate package ID textbox.
    this.chosenAlternatePackageId = ko.observable('');

    // The version chosen by the alternate package version dropdown.
    this.chosenAlternatePackageVersion = ko.observable();

    // The cached list of versions associated with the currently entered alternate package ID.
    this.alternatePackageVersionsCached = ko.observableArray();

    // The list of options in the alternate package version dropdown.
    this.alternatePackageVersions = ko.pureComputed(function () {
        // Include an "Any Version" option in case users want to select the package registration.
        return [strings_AnyVersion].concat(self.alternatePackageVersionsCached());
    }, this);

    // Whether or not the versions of the currently entered alternate package ID have been loaded.
    this.hasAlternatePackageVersions = ko.pureComputed(function () {
        return self.alternatePackageVersionsCached().length > 0;
    }, this);

    // The error to show with the currently entered alternate package ID.
    // E.g. the package does not exist or cannot be chosen as an alternate.
    this.chosenAlternatePackageIdError = ko.observable();

    // When a new alternate package ID is entered, load the list of versions from the server.
    this.chosenAlternatePackageId.subscribe(function (id) {
        if (!id) {
            // If the user hasn't input an ID, don't query the server.
            self.chosenAlternatePackageIdError(null);
            return;
        }

        $.ajax({
            url: getAlternatePackageVersions,
            dataType: 'json',
            type: 'GET',
            data: {
                id: id
            },

            statusCode: {
                200: function (data) {
                    if (self.alternatePackageId() === id) {
                        self.alternatePackageVersionsCached(data);
                        self.chosenAlternatePackageIdError(null);
                    }
                },

                404: function () {
                    if (self.alternatePackageId() === id) {
                        self.alternatePackageVersionsCached.removeAll();
                        self.chosenAlternatePackageIdError("Could not find alternate package '" + id + "'.");
                    }
                }
            },

            error: function () {
                if (self.alternatePackageId() === id) {
                    self.alternatePackageVersionsCached.removeAll();
                    self.chosenAlternatePackageIdError("An unknown occurred when searching for alternate package '" + id + "'.");
                }
            }
        });
    }, this);

    // The alternate package ID to submit with the form.
    this.alternatePackageId = ko.pureComputed(function () {
        if (self.isLegacy()) {
            return self.chosenAlternatePackageId();
        } else {
            // If the legacy checkbox is not selected, this section of the form is hidden.
            // Don't submit the chosen alternate package ID with the form.
            return null;
        }
    }, this);
    this.alternatePackageVersion = ko.pureComputed(function () {
        if (self.alternatePackageId()) {
            var version = self.chosenAlternatePackageVersion();
            // If the chosen version is the "Any Version" string, don't submit it with the form.
            if (version !== strings_AnyVersion) {
                return version;
            }
        }

        // If there is no alternate package ID to submit with the form, don't submit the chosen alternate package version.
        return null;
    }, this);

    // The custom message to submit with the form.
    this.customMessage = ko.observable('');

    // Whether or not the packages should be unlisted.
    this.shouldUnlist = ko.observable(true);

    this.submitError = ko.observable();
    this.submit = function () {
        self.submitError(null);
        $.ajax({
            url: submitUrl,
            dataType: 'json',
            type: 'POST',
            data: window.nuget.addAjaxAntiForgeryToken({
                id: id,
                versions: self.chosenVersions(),
                isVulnerable: self.isVulnerable(),
                isLegacy: self.isLegacy(),
                isOther: self.isOther(),
                cveIds: self.cves.export(),
                cvssRating: self.cvssRating(),
                cweIds: self.cwes.export(),
                alternatePackageId: self.alternatePackageId(),
                alternatePackageVersion: self.alternatePackageVersion(),
                customMessage: self.customMessage(),
                shouldUnlist: self.shouldUnlist()
            }),
            success: function () {
                window.location.href = packageUrl;
            },
            error: function (jqXHR) {
                var newError = jqXHR && jqXHR.responseJSON ? jqXHR.responseJSON.error : "An unknown error occurred when submitting the form.";
                self.submitError(newError);
            }
        });
    };

    // When the chosen versions are changed, remember the contents of the form in case the user navigates back to this version.
    this.chosenVersions.subscribe(function (oldVersions) {
        if (!oldVersions || oldVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't cache the contents of the form.
            return;
        }

        var version = versionsDictionary[oldVersions[0]];
        if (!version) {
            return;
        }

        version.IsVulnerable = self.isVulnerable();
        version.IsLegacy = self.isLegacy();
        version.IsOther = self.isOther();
        version.CVEIds = self.cves.export();
        version.CVSSRating = self.cvssRating();
        version.CWEIds = self.cwes.export();
        version.AlternatePackageId = self.alternatePackageId();
        version.AlternatePackageVersion = self.alternatePackageVersion();
        version.CustomMessage = self.customMessage();
        version.ShouldUnlist = self.shouldUnlist();
    }, this, "beforeChange");

    // When the chosen versions are changed, load the existing deprecation state for this version.
    this.chosenVersions.subscribe(function (newVersions) {
        if (!newVersions || newVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't load the existing deprecation state.
            return;
        }

        var version = versionsDictionary[newVersions[0]];
        if (!version) {
            return;
        }

        self.isVulnerable(version.IsVulnerable);
        self.isLegacy(version.IsLegacy);
        self.isOther(version.IsOther);

        self.cves.import(version.CVEIds);

        self.hasCvss(version.CVSSRating);
        self.selectedCvssRating(version.CVSSRating);

        self.cwes.import(version.CWEIds);

        self.chosenAlternatePackageId(version.AlternatePackageId);
        if (version.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([version.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(version.AlternatePackageVersion);
        }

        self.customMessage(version.CustomMessage);
        self.shouldUnlist(version.ShouldUnlist);
    }, this);

    // Select the default version in the form.
    if (versionsDictionary[defaultVersion]) {
        for (var index in self.versions) {
            var version = self.versions[index];
            if (version.version === defaultVersion) {
                version.checked(true);
            } else {
                version.checked(false);
            }
        }
    }

    ko.applyBindings(this, $(".page-manage-deprecation")[0]);

    self.getFocusableDropdownContentElements().attr('tabindex', '-1');
}