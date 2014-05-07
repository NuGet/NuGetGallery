function getStats(currData) {
    currData = currData || {};

    $.get(window.app.root + 'stats/totals', function (data) {
        var section = $('section.aggstats');
        section.show();
        update(data, currData, 'UniquePackages');
        update(data, currData, 'Downloads');
        update(data, currData, 'TotalPackages');
    }).error(function () {
        // Don't show the stats error anymore.  Just fail silently.
        // var section = $('section.aggstatserr');
        // section.show();
    });

    setTimeout(function () { getStats(currData); }, 30000);
}

function update(data, currData, key) {
    var currentValue = currData[key] || '';
    var value = data[key].toString();
    var self = $('#' + key);
    
    if (currentValue != value) {
        currData[key] = value;
        var length = value.length;
        var currLength = currentValue.length;
        var items = self.children('span');

        if (currLength > length) {
            items.slice(0, currLength - length).remove();
            items = items.slice(currLength - length);
        }

        if (currLength) {
            // Do not animate the first time around.
            $.each(value.split('').reverse(), function (i, e) {
                var c = (i <= currLength) ? currentValue.charAt(currLength - i - 1) : '';
                if (c != e) {
                    var el = $(items[length - i - 1]);
                    animateEl(el, e);
                }
            });
        }
        if (currLength < length) {
            var i;
            for (i = currLength; i < length; i++) {
                self.prepend('<span>' + value.charAt(length - i - 1) + '</span>');
            }
            items = self.children('span');
        }
    }
}

function animateEl(el, v) {
    v = v || '';
    var parent = el.parent();
    el.stop(true, true).animate({ top: 0.3 * parseInt(parent.height()) }, 350, 'linear', function () {
        $(this).html(v).css({ top: -0.8 * parseInt(parent.height()) }).animate({ top: 0 }, 350, 'linear');
    });
}

$(document).ready(function () {
    var elem = document.getElementsByClassName("aggstats");
    if (elem != null && elem.length > 0) {
        getStats();
    }

});
