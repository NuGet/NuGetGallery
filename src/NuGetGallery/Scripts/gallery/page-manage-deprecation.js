'use strict';

function ManageDeprecationViewModel(id, versionsDictionary, defaultVersion, submitUrl, packageUrl) {
    var self = this;

    this.versions = Object.keys(versionsDictionary);
    this.chosenVersions = ko.observableArray();

    this.isVulnerable = ko.observable(false);
    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    this.hasCveIds = ko.observable(false);
    this.addedCveIds = ko.observableArray();
    this.cveIds = ko.pureComputed(function () {
        if (self.hasCveIds()) {
            return self.addedCveIds();
        } else {
            return [];
        }
    }, this);
    this.addCveId = ko.observable('');
    this.addCve = function () {
        self.addedCveIds.push(self.addCveId());
        self.addCveId('');
    };

    this.hasCvss = ko.observable(false);
    this.selectedCvssRating = ko.observable(0);
    this.cvssRating = ko.pureComputed(function () {
        if (self.hasCvss()) {
            return self.selectedCvssRating();
        } else {
            return null;
        }
    }, this);

    this.hasCweIds = ko.observable(false);
    this.addedCweIds = ko.observableArray();
    this.cweIds = ko.pureComputed(function () {
        if (self.hasCweIds()) {
            return self.addedCweIds();
        } else {
            return [];
        }
    }, this);
    this.addCweId = ko.observable('');
    this.addCwe = function () {
        self.addedCweIds.push(self.addCweId());
        self.addCweId('');
    };

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
                cveIds: self.cveIds(),
                cvssRating: self.cvssRating(),
                cweIds: self.cweIds(),
                alternatePackageId: self.alternatePackageId(),
                alternatePackageVersion: self.alternatePackageVersion(),
                customMessage: self.customMessage(),
                shouldUnlist: self.shouldUnlist()
            }),
            success: function () {
                window.location.href = packageUrl;
            },
            error: function (jqXHR) {
                var newError = jqXHR.responseJSON === null ? "An unknown error occurred." : jqXHR.responseJSON.error;
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
        version.isOther = self.isOther();
        version.CVEIds = self.cveIds();
        version.CVSSRating = self.cvssRating();
        version.CWEIds = self.cweIds();
        version.AlternatePackageId = self.alternatePackageId();
        version.AlternatePackageVersion = self.alternatePackageVersion();
        version.CustomMessage = self.customMessage();
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
        self.isOther(version.isOther);

        self.hasCveIds(version.CVEIds !== null && version.CVEIds.length);
        self.addedCveIds(version.CVEIds);

        self.hasCvss(version.CVSSRating);
        self.selectedCvssRating(version.CVSSRating);

        self.hasCweIds(version.CWEIds !== null && version.CWEIds.length);
        self.addedCweIds(version.CWEIds);

        self.alternatePackageId(version.AlternatePackageId);
        if (version.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([version.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(version.AlternatePackageVersion);
        }

        self.customMessage(version.CustomMessage);
    }, this);
    if (versionsDictionary[defaultVersion]) {
        this.chosenVersions([defaultVersion]);
    }

    ko.applyBindings(this, $(".page-manage-deprecation")[0]);
}