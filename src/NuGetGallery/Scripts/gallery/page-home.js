$(function () {
    'use strict';

    function updateStat(observable, unparsedValue) {
        var parsedValue = parseInt(unparsedValue);
        if (!isNaN(parsedValue)) {
            observable(parsedValue);
        }
    }

    var stats = {
        packageDownloads: ko.observable(0),
        packageVersions: ko.observable(0),
        uniquePackages: ko.observable(0)
    };

    stats.label = ko.computed(function () {
        return 'NuGet.org has ' +
            stats.packageDownloads() + ' package download' + (stats.packageDownloads() != 1 ? 's' : '') + ', ' +
            stats.packageVersions() + ' package version' + (stats.packageVersions() != 1 ? 's' : '') + ', and ' +
            stats.uniquePackages() + ' unique package' + (stats.uniquePackages() != 1 ? 's' : '') + '.';
    });

    function showModal() {
        $(document).on('ready', function (e) {
            $("#popUpModal").modal({
                show: true,
                focus: true
            });
        })
    };

    function updateStats() {
        $.get('/stats/totals')
            .done(function (data) {
                updateStat(stats.packageDownloads, data['Downloads']);
                updateStat(stats.packageVersions, data['TotalPackages']);
                updateStat(stats.uniquePackages, data['UniquePackages']);
            })
            .fail(function () {
                // Fail silently.
            });
    }

    ko.bindingHandlers.animateNumber = {
        init: function (element, valueAccessor) {
            var value = ko.unwrap(valueAccessor());
            $(element).data('value', value);
            $(element).text(value.toLocaleString());
        },
        update: function (element, valueAccessor) {
            var oldValue = $(element).data('value');
            var newValue = ko.unwrap(valueAccessor());
            $(element).data('value', newValue);

            $({ value: oldValue }).animate({ value: newValue }, {
                duration: 250,
                easing: 'swing',
                step: function () {
                    $(element).text(Math.floor(this.value).toLocaleString());
                },
                done: function () {
                    $(element).text(newValue.toLocaleString());
                }
            });
        }
    };

    if (!window.nuget.supportsSvg()) {
        $('.circuit-board').hide();
    }

    ko.applyBindings(stats);
    updateStats();
    if (window.showModal) {
        showModal();
    }
});
