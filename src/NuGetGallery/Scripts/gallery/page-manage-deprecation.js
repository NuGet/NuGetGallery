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

    // Existing deprecation state information per version.
    var items = Object.keys(versionsDictionary).map(function (version) {
        var versionData = versionsDictionary[version];
        return new MultiSelectDropdownItem(
            version,
            versionData.Text,
            version,
            version === defaultVersion,
            versionData.IsVulnerable || versionData.IsLegacy || versionData.IsOther);
    });

    this.dropdown = new MultiSelectDropdown(items, "Select version(s) to deprecate", "All current versions");

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
                versions: self.dropdown.chosenItems(),
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

    var saveDeprecationFormState = function (version) {
        var versionData = versionsDictionary[version];
        if (!versionData) {
            return;
        }

        versionData.IsVulnerable = self.isVulnerable();
        versionData.IsLegacy = self.isLegacy();
        versionData.IsOther = self.isOther();
        versionData.CVEIds = self.cves.export();
        versionData.CVSSRating = self.cvssRating();
        versionData.CWEIds = self.cwes.export();
        versionData.AlternatePackageId = self.alternatePackageId();
        versionData.AlternatePackageVersion = self.alternatePackageVersion();
        versionData.CustomMessage = self.customMessage();
        versionData.ShouldUnlist = self.shouldUnlist();
    };

    var loadDeprecationFormState = function (version) {
        var versionData = versionsDictionary[version];
        if (!versionData) {
            return;
        }

        self.isVulnerable(versionData.IsVulnerable);
        self.isLegacy(versionData.IsLegacy);
        self.isOther(versionData.IsOther);

        self.cves.import(versionData.CVEIds);

        self.hasCvss(versionData.CVSSRating);
        self.selectedCvssRating(versionData.CVSSRating);

        self.cwes.import(versionData.CWEIds);

        self.chosenAlternatePackageId(versionData.AlternatePackageId);
        if (versionData.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([versionData.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(versionData.AlternatePackageVersion);
        }

        self.customMessage(versionData.CustomMessage);
        self.shouldUnlist(versionData.ShouldUnlist);
    };

    // When the chosen versions are changed, remember the contents of the form in case the user navigates back to this version.
    this.dropdown.chosenItems.subscribe(function (oldVersions) {
        if (!oldVersions || oldVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't cache the contents of the form.
            return;
        }

        saveDeprecationFormState(oldVersions[0]);
    }, this, "beforeChange");

    // When the chosen versions are changed, load the existing deprecation state for this version.
    this.dropdown.chosenItems.subscribe(function (newVersions) {
        if (!newVersions || newVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't load the existing deprecation state.
            return;
        }

        loadDeprecationFormState(newVersions[0]);
    }, this);

    // Load the state for the default version.
    loadDeprecationFormState(defaultVersion);

    ko.applyBindings(this, $(".page-manage-deprecation")[0]);
}