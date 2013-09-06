// Global utility script for NuGetGallery
/// <reference path="jquery-1.6.4.js" />
(function (window, $, undefined) {
    function attachSearchBoxBehavior($input, $menu) {
        // Remember the previous state in order to perform smooth animation transforms
        var prevstate = false;
        function popit(assumeFocused) {
            return function () {
                // Calculate the new state
                var state;
                if ($input.val().length > 0 && ($input.is(":focus") || assumeFocused)) {
                    state = true;
                } else {
                    state = false;
                }

                // If there's a change
                if (state != prevstate) {
                    // Record it and stop all current animations to avoid glitching
                    prevstate = state;
                    $input.stop();
                    $menu.stop();

                    // Start new ones to transition to the new state
                    if (state) {
                        $menu.animate({ opacity: 0 }, {
                            duration: 200, queue: true, complete: function () {
                                $menu.css({ position: 'absolute', top: -10000, left: -10000 });
                                $input.animate({ width: '920px' }, { duration: 200, queue: true });
                            }
                        });
                    } else {
                        $input.animate({ width: '160px' }, {
                            duration: 200, queue: true, complete: function () {
                                $menu.css({ position: 'static', top: 'auto', left: 'auto' });
                                $menu.animate({ opacity: 1 }, { duration: 200, queue: true });
                                prevstate = state;
                            }
                        });
                    }
                }
            }
        }

        // Bind handlers
        $input.delegate('', 'keyup', popit(false));
        $input.delegate('', 'blur', popit(false));
        $input.delegate('', 'focus', popit(true));
    }

    function checkServiceStatus() {
        $.get(app.root + 'api/v2/service-alert?cachebust=' + new Date().getTime())
            .done(function (data) {
                if (typeof data === 'string' && data.length > 0) {
                    $('#service-alert').html(data).slideDown('fast');
                }
                else {
                    $('#service-alert').slideUp('fast').html();
                }
            }) // If this fails, just silently show no status.
    }

    $(function () {
        // Export an object with global config data
        var app = $(document.documentElement).data();
        window.app = app;

        if (!app.root) {
            app.root = '';
        }

        // Get the service status
        checkServiceStatus();

        attachSearchBoxBehavior($('#searchBoxInput'), $('#menu'));
    });

	// Add validator that ensures provided value is NOT equal to a specified value.
    $.validator.addMethod('notequal', function (value, element, params) {
        return value !== params;
    });

    // Add unobtrusive adapters for mandatory checkboxes and notequal values
    $.validator.unobtrusive.adapters.addBool("mandatory", "required");
    $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');
})(window, jQuery);
