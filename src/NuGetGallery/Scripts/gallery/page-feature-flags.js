'use strict';

function FeatureViewModel(name, existed, wasEnabled) {
    var self = this;

    this.name = ko.observable(name);
    this.isEnabled = ko.observable(wasEnabled);
    this.existed = existed;
    this.wasEnabled = wasEnabled;

    this.warningMessage = ko.pureComputed(function () {
        if (!self.existed) {
            return 'You are creating a feature named ' + self.name() + ' and it ' + (self.isEnabled() ? 'will' : 'will not') + ' be enabled.';
        }

        if (self.isEnabled() && !self.wasEnabled) {
            return 'You are enabling a feature named ' + self.name() + ' that was disabled before.';
        }

        if (!self.isEnabled() && self.wasEnabled) {
            return 'You are disabling a feature named ' + self.name() + ' that was enabled before.';
        }

        return null;
    }, this);

    this.isChanged = ko.pureComputed(function () {
        return self.warningMessage() !== null;
    }, this);
}

function FlightViewModel(name, existed, all, siteAdmins, accounts, domains) {
    var self = this;

    this.name = ko.observable(name);
    this.all = ko.observable(all);
    this.siteAdmins = ko.observable(siteAdmins);
    this.accounts = ko.observableArray(accounts.slice(0));
    this.domains = ko.observableArray(domains.slice(0));

    this.existed = existed;
    this.wasAll = all;
    this.wasSiteAdmins = siteAdmins;
    this.wasAccounts = accounts;
    this.wasDomains = domains;

    this.warningMessage = ko.pureComputed(function () {
        if (!self.existed) {
            return 'You are creating a flight named ' + self.name() + ' and it ' + (self.all() ? 'will' : 'will not') + ' be enabled for all users.';
        }

        if (self.all() && !self.wasAll) {
            return 'You are enabling a flight named ' + self.name() + ' for all users that was disabled before.';
        }

        if (!self.all() && self.wasAll) {
            return 'You are disabling a feature named ' + self.name() + ' for all users that was enabled before.';
        }

        return null;
    }, this);

    this.isChanged = ko.pureComputed(function () {
        return self.warningMessage() !== null;
    }, this);
}

function FeatureFlagsViewModel(jsonString) {
    var self = this;
    var object = JSON.parse(jsonString);

    this.features = ko.observableArray();
    for (var featureName in object.Features) {
        var feature = new FeatureViewModel(true, object.Features[featureName] === "Enabled");
        this.features.push(feature);
    }


}