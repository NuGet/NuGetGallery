$(function () {
    'use strict';

    for (var i = 0; i < listItemCount; i++) {
        var id = "reserved-indicator-" + i;
        configureCheckmarkImagePopover(id);
    }

    function configureCheckmarkImagePopover(id) {
        var checkmarkImage = $('#' + id);
        if (checkmarkImage.length == 1) {   // i.e. checkmark exists
            checkmarkImage.popover({ trigger: 'hover focus' });
            checkmarkImage.click(function() {
                checkmarkImage.popover('show');
                setTimeout(function() {
                        checkmarkImage.popover('destroy');
                    },
                    1000);
            });
        }
    }
});
