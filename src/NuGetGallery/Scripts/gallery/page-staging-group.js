// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

(function () {
    'use strict';

    function StagingGroupPackageViewModel(packageItem, searchQuery) {
        const self = this;

        this.SearchText = `${packageItem.Id} ${packageItem.Version}`.toLowerCase();
        this.Visible = ko.pureComputed(function () {
            const query = searchQuery().trim().toLowerCase();
            return query.length === 0 || self.SearchText.indexOf(query) !== -1;
        });
    }

    function StagingGroupViewModel(data) {
        const self = this;

        this.SearchQuery = ko.observable('');
        this.Packages = data.Packages.map(function (packageItem) {
            return new StagingGroupPackageViewModel(packageItem, self.SearchQuery);
        });
        this.VisiblePackageCount = ko.pureComputed(function () {
            return self.Packages.filter(function (packageItem) {
                return packageItem.Visible();
            }).length;
        });
        this.VisiblePackageSummary = ko.pureComputed(function () {
            const visibleCount = self.VisiblePackageCount();
            const totalCount = self.Packages.length;
            const totalText = `${totalCount} staged package${totalCount === 1 ? '' : 's'}`;
            if (self.SearchQuery().trim().length === 0) {
                return totalText;
            }

            return `Showing ${visibleCount} of ${totalText}`;
        });
        this.Search = function () {
            return false;
        };
    }

    const page = document.getElementById('staging-group');
    if (page) {
        ko.applyBindings(new StagingGroupViewModel(window.stagingGroupData), page);
    }
})();
