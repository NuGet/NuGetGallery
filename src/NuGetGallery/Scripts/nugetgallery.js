// Global utility script for NuGetGallery
/// <reference path="jquery-1.6.4.js" />
(function (window, $, undefined) {
    function attachSearchBoxBehavior($input, $menu) {
        if ($input.length == 0 || $menu.length == 0) {
            // If we were given nothing, just return.
            return;
        }

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

        attachSearchBoxBehavior($('#searchBoxInput.expanding-search'), $('#menu.expanding-search'));

        attachPlugins();
    });

	// Add validator that ensures provided value is NOT equal to a specified value.
    $.validator.addMethod('notequal', function (value, element, params) {
        return value !== params;
    });

    // Add unobtrusive adapters for mandatory checkboxes and notequal values
    $.validator.unobtrusive.adapters.addBool("mandatory", "required");
    $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');

    // Attach script plugins
    function attachPlugins() {
        $('.s-toggle[data-show][data-hide]').delegate('', 'click', function (evt) {
            evt.preventDefault();
            var $hide = $($(this).data().hide);
            var $show = $($(this).data().show);
            $hide.fadeOut('fast', function () {
                $show.fadeIn('fast');
            });
        });
        $('.s-expand[data-target]').delegate('', 'click', function (evt) {
            evt.preventDefault();
            var $self = $(this);
            var data = $self.data();
            var $target = $(data.target);
            var toggletext = data.toggletext || $self.text();

            $target.slideToggle('fast', function () {
                var oldText = $self.text();
                $self.text(toggletext);
                data.toggletext = oldText;
            });
        });
        $('.s-confirm[data-confirm]').delegate('', 'click', function (evt) {
            if (!confirm($(this).data().confirm)) {
                evt.preventDefault();
            }
        });
        if(!navigator.mimeTypes["application/x-shockwave-flash"]) {
            $('.s-reqflash').remove();
        }
    }
})(window, jQuery);
