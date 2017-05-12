$(function () {
    var stats = {
        packageDownloads: ko.observable(0),
        packageVersions: ko.observable(0),
        uniquePackages: ko.observable(0)
    };

    ko.bindingHandlers.animateNumber = {
        init: function (element, valueAccessor) {
            var value = ko.unwrap(valueAccessor());
            $(element).text(value);
        },
        update: function (element, valueAccessor) {
            var oldValue = parseInt($(element).text());
            var newValue = ko.unwrap(valueAccessor());

            $({ value: oldValue }).animate({ value: newValue }, {
                duration: 250,
                easing: 'swing',
                step: function () {
                    $(element).text(Math.floor(this.value));
                },
                done: function () {
                    $(element).text(newValue);
                }
            });
        }
    };

    ko.applyBindings(stats);

    function updateStat(observable, unparsedValue) {
        var parsedValue = parseInt(unparsedValue);
        if (!isNaN(parsedValue)) {
            observable(parsedValue);
        }
    }

    function updateStats() {
        $.get('/stats/totals')
            .done(function (data) {
                updateStat(stats.packageDownloads, data['Downloads']);
                updateStat(stats.packageVersions, data['TotalPackages']);
                updateStat(stats.uniquePackages, data['UniquePackages']);
            })
            .error(function () {
                // Fail silently.
            })
            .always(function () {
                setTimeout(updateStats, 30000);
            });
    }

    updateStats();
});
