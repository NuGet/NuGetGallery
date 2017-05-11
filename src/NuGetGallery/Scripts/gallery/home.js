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
                }
            });
        }
    };

    ko.applyBindings(stats);

    function updateStats() {
        $.get('/stats/totals')
            .done(function (data) {
                stats.packageDownloads(parseInt(data['Downloads']));
                stats.packageVersions(parseInt(data['TotalPackages']));
                stats.uniquePackages(parseInt(data['UniquePackages']));
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
