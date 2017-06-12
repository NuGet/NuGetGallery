$(function () {
    'use strict';

    function parseNumber(unparsedValue) {
        unparsedValue = ('' + unparsedValue).replace(/,/g, '');
        var parsedValue = parseInt(unparsedValue);
        return parsedValue;
    }

    function commaThousands(value) {
        var output = value.toString();
        for (var i = output.length - 3; i > 0; i -= 3)
        {
            output = output.slice(0, i) + ',' + output.slice(i);
        }

        return output;
    }

    function updateStat(observable, unparsedValue) {
        var parsedValue = parseNumber(unparsedValue);
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

    function updateStats() {
        $.get('/stats/totals')
            .done(function (data) {
                updateStat(stats.packageDownloads, data['Downloads']);
                updateStat(stats.packageVersions, data['TotalPackages']);
                updateStat(stats.uniquePackages, data['UniquePackages']);
            })
            .error(function () {
                // Fail silently.
            });
    }

    ko.bindingHandlers.animateNumber = {
        init: function (element, valueAccessor) {
            var value = ko.unwrap(valueAccessor());
            $(element).text(value);
        },
        update: function (element, valueAccessor) {
            var oldValue = parseNumber($(element).text());
            var newValue = ko.unwrap(valueAccessor());

            $({ value: oldValue }).animate({ value: newValue }, {
                duration: 250,
                easing: 'swing',
                step: function () {
                    $(element).text(commaThousands(Math.floor(this.value)));
                },
                done: function () {
                    $(element).text(commaThousands(newValue));
                }
            });
        }
    };

    if (!window.nuget.supportsSvg()) {
        $('.circuit-board').hide();
    }

    ko.applyBindings(stats);
    updateStats();
});
