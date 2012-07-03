function getStats() {
    $.get('/Stats', function(data) {
        var section = $('section.aggstats');
        section.show();
        update(data, 'UniquePackages');
        update(data, 'Downloads');
        update(data, 'TotalPackages');
    })
    setTimeout(getStats, 30000);
}

function update(data, key) {
    var value = data[key].toString();
    var self = $('#' + key);
    var currentValue = $.trim(self.text().replace(/\s/g, ''));
    if (currentValue != value) {
        var length = value.length;
        var currLength = currentValue.length;
        var items = self.children('span');

        if (currLength > length) {
            while (items.length > length) {
                items.first().remove();
                items = self.children('span');
            }
        }
        else if (currLength < length) {
            var i;
            for (i = currLength; i < length; i++) {
                self.prepend('<span>' + value.charAt(length - i - 1) + '</span>');
            }
            items = self.children('span');
            currentValue = $.trim(self.text().replace(/\s/g, ''));
            currLength = currentValue.length;
        }

        $.each(value.split('').reverse(), function (i, e) {
            var c = (i < currLength) ? currentValue.charAt(currLength - i - 1) : '';
            if (c != e) {
                var el = $(items[length - i - 1]);
                animateEl(el, e);
            }
        });
    }

    function animateEl(el, v) {
        v = v || '';
        var parent = el.parent();
        el.animate({ top: 0.3 * parseInt(parent.height()) }, 350, 'linear', function () {
            $(this).html(v).css({ top: -0.8 * parseInt(parent.height()) }).animate({ top: 0 }, 350, 'linear')
        });
    }
}
