$(function() {
    'use strict';

    $(".reserved-indicator").each(
        function() {
            var checkmarkImage = $(this);
            checkmarkImage.popover({ trigger: 'hover focus' });
            checkmarkImage.click(function() {
                checkmarkImage.popover('show');
                setTimeout(function() {
                        checkmarkImage.popover('destroy');
                    },
                    1000);
            });
        }
    );
});
