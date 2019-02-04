'use strict';

function ManageDeprecationSecurityDetailListViewModel(title, label) {
    var self = this;

    this.title = ko.observable(title);
    this.label = ko.observable(label);

    this.hasIds = ko.observable(false);
    this.addedIds = ko.observableArray();
    this.ids = ko.pureComputed(function () {
        if (self.hasIds()) {
            return self.addedIds();
        } else {
            return [];
        }
    }, this);
    this.addId = ko.observable('');
    this.add = function () {
        self.addedIds.push(self.addId());
        self.addId('');
    };

    this.remove = function (id) {
        self.addedIds.remove(id);
    };

    this.import = function (ids) {
        var hasIds = ids !== null && ids.length;
        self.hasIds(hasIds);
        if (hasIds) {
            self.addedIds(ids);
        } else {
            self.addedIds.removeAll();
        }
    };

    this.export = function () {
        // Copy the array. 
        // Otherwise, the value returned by this function will change based on the UI.
        return self.ids().slice(0);
    };
}

function ManageDeprecationViewModel(id, versionsDictionary, defaultVersion, submitUrl, packageUrl) {
    var self = this;

    this.versions = Object.keys(versionsDictionary);
    this.chosenVersions = ko.observableArray();

    this.isVulnerable = ko.observable(false);
    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    this.cves = new ManageDeprecationSecurityDetailListViewModel(
        "CVE ID(s)",
        "You can provide a list of CVEs.");

    this.hasCvss = ko.observable(false);
    this.selectedCvssRating = ko.observable(0);
    this.cvssRating = ko.pureComputed(function () {
        if (self.hasCvss()) {
            return self.selectedCvssRating();
        } else {
            return null;
        }
    }, this);

    this.cwes = new ManageDeprecationSecurityDetailListViewModel(
        "CWE(s)",
        "You can add one or more CWE(s) applicable to the vulnerability.");

    this.alternatePackageId = ko.observable('');
    this.alternatePackageVersionsCached = ko.observableArray();
    this.alternatePackageVersions = ko.pureComputed(function () {
        return [strings_AnyVersion].concat(self.alternatePackageVersionsCached());
    }, this);
    this.hasAlternatePackageVersions = ko.pureComputed(function () {
        return self.alternatePackageVersionsCached().length > 0;
    }, this);
    this.chosenAlternatePackageVersion = ko.observable();
    this.alternatePackageVersion = ko.pureComputed(function () {
        if (!self.hasAlternatePackageVersions() || self.chosenAlternatePackageVersion() === strings_AnyVersion) {
            return null;
        } else {
            return self.chosenAlternatePackageVersion();
        }
    }, this);

    this.customMessage = ko.observable('');
    this.shouldUnlist = ko.observable(true);

    this.error = ko.observable();

    this.submit = function () {
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
                var newError = jqXHR && jqXHR.responseJSON ? jqXHR.responseJSON.error : "An unknown error occurred.";
                self.error(newError);
            }
        });
    };

    this.chosenVersions.subscribe(function (oldVersions) {
        if (!oldVersions || oldVersions.length !== 1) {
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
    this.chosenVersions.subscribe(function (newVersions) {
        self.alternatePackageVersionsCached([]);

        if (!newVersions || newVersions.length !== 1) {
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

        self.alternatePackageId(version.AlternatePackageId);
        if (version.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([version.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(version.AlternatePackageVersion);
        }

        self.customMessage(version.CustomMessage);
        self.shouldUnlist(version.ShouldUnlist);
    }, this);
    if (versionsDictionary[defaultVersion]) {
        this.chosenVersions([defaultVersion]);
    }

    ko.applyBindings(this, $(".page-manage-deprecation")[0]);
}