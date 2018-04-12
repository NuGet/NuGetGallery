// Initialize window.nuget (common logic usable across all pages).
(function () {
    'use strict';

    var nuget = {};

    function detectIE() {
        var ua = window.navigator.userAgent;
        var msie = ua.indexOf('MSIE ');
        if (msie > 0) {
            // IE 10 or older => return version number
            return parseInt(ua.substring(msie + 5, ua.indexOf('.', msie)), 10);
        }

        var trident = ua.indexOf('Trident/');
        if (trident > 0) {
            // IE 11 => return version number
            var rv = ua.indexOf('rv:');
            return parseInt(ua.substring(rv + 3, ua.indexOf('.', rv)), 10);
        }

        // other browser or edge
        return false;
    }

    function initializeJQueryValidator() {
        // Add validator that ensures provided value is NOT equal to a specified value.
        $.validator.addMethod('notequal', function (value, element, params) {
            return value !== params;
        });

        // Add unobtrusive adapters for mandatory checkboxes and notequal values
        $.validator.unobtrusive.adapters.addBool("mandatory", "required");
        $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');

        // Source: https://stackoverflow.com/questions/18754020/bootstrap-3-with-jquery-validation-plugin
        // Set the JQuery validation plugin's defaults to use classes recognized by Bootstrap.
        $.validator.setDefaults({
            highlight: function (element) {
                $(element).closest('.form-group').addClass('has-error');
            },
            unhighlight: function (element) {
                $(element).closest('.form-group').removeClass('has-error');
            },
            errorElement: 'span',
            errorClass: 'help-block',
            errorPlacement: function (error, element) {
                if (element.parent('.input-group').length) {
                    error.insertAfter(element.parent());
                } else {
                    error.insertAfter(element);
                }
            },
            showErrors: function (errorMap, errorList) {
                this.defaultShowErrors();

                // By default, showErrors adds an aria-describedby attribute to every field that it validates, even if it finds no issues.
                // This is a problem, because the aria-describedby attribute will then link to an empty element.
                // This code removes the aria-describedby if the describing element is missing or empty.
                var i;
                for (i = 0; this.errorList[i]; i++) {
                    removeInvalidAriaDescribedBy(this.errorList[i].element);
                }

                for (i = 0; this.successList[i]; i++) {
                    removeInvalidAriaDescribedBy(this.successList[i]);
                }
            }
        });
    }

    function removeInvalidAriaDescribedBy(element) {
        var describedBy = element.getAttribute("aria-describedby");
        if (!describedBy) {
            return;
        }

        var ids = describedBy.split(" ")
            .filter(function (describedById) {
                if (!describedById) {
                    return false;
                }

                var describedByElement = $("#" + describedById);
                return describedByElement && describedByElement.text();
            });

        if (ids.length) {
            element.setAttribute("aria-describedby", ids.join(" "));
        } else {
            element.removeAttribute("aria-describedby");
        }
    }

    nuget.parseNumber = function (unparsedValue) {
        unparsedValue = ('' + unparsedValue).replace(/,/g, '');
        var parsedValue = parseInt(unparsedValue);
        return parsedValue;
    };

    // source: http://stackoverflow.com/questions/400212/how-do-i-copy-to-the-clipboard-in-javascript
    // enhancement with special case for IEs, otherwise the temp textarea will be visible
    nuget.copyTextToClipboard = function (text, elementToFocus) {
        if (detectIE()) {
            try {
                window.clipboardData.setData('Text', text);
                console.log('Copying text command via IE-setData');
            } catch (err) {
                console.log('Oops, unable to copy via IE-setData');
            }
        }
        else {

            var textArea = document.createElement("textarea");

            //
            //  This styling is an extra step which is likely not required. 
            //
            // Why is it here? To ensure:
            // 1. the element is able to have focus and selection.
            // 2. if element was to flash render it has minimal visual impact.
            // 3. less flakyness with selection and copying which might occur if
            //    the textarea element is not visible.
            //
            // The likelihood is the element won't even render, not even a flash,
            // so some of these are just precautions. 
            // 
            // However in IE the element
            // is visible whilst the popup box asking the user for permission for
            // the web page to copy to the clipboard. To prevent this, we are using 
            // the detectIE workaround.

            // Place in top-left corner of screen regardless of scroll position.
            textArea.style.position = 'fixed';
            textArea.style.top = 0;
            textArea.style.left = 0;

            // Ensure it has a small width and height. Setting to 1px / 1em
            // doesn't work as this gives a negative w/h on some browsers.
            textArea.style.width = '2em';
            textArea.style.height = '2em';

            // We don't need padding, reducing the size if it does flash render.
            textArea.style.padding = 0;

            // Clean up any borders.
            textArea.style.border = 'none';
            textArea.style.outline = 'none';
            textArea.style.boxShadow = 'none';

            // Avoid flash of white box if rendered for any reason.
            textArea.style.background = 'transparent';


            textArea.value = text;

            document.body.appendChild(textArea);

            textArea.select();

            try {
                var successful = document.execCommand('copy');
                var msg = successful ? 'successful' : 'unsuccessful';
                console.log('Copying text command was ' + msg);
            } catch (err) {
                console.log('Oops, unable to copy');
            }

            document.body.removeChild(textArea);

            // Focus the element provided so that tab order is not reset to the beginning of the page.
            if (elementToFocus) {
                elementToFocus.focus();
            }
        }
    };

    nuget.configureExpander = function (prefix, lessIcon, lessMessage, moreIcon, moreMessage) {
        var hidden = $('#' + prefix);
        var show = $('#show-' + prefix);
        var showIcon = $('#show-' + prefix + ' i');
        var showText = $('#show-' + prefix + ' span');
        hidden.on('hide.bs.collapse', function (e) {
            showIcon.removeClass('ms-Icon--' + moreIcon);
            showIcon.addClass('ms-Icon--' + lessIcon);
            if (moreMessage !== null) {
                showText.text(moreMessage);
            }
            e.stopPropagation();
        });
        hidden.on('show.bs.collapse', function (e) {
            showIcon.removeClass('ms-Icon--' + lessIcon);
            showIcon.addClass('ms-Icon--' + moreIcon);
            if (lessMessage !== null) {
                showText.text(lessMessage);
            }
            e.stopPropagation();
        });
        show.on('click', function (e) {
            e.preventDefault();
        });
    };

    nuget.configureExpanderHeading = function (prefix) {
        window.nuget.configureExpander(prefix, "ChevronRight", null, "ChevronDown", null);
    };

    // Source: https://stackoverflow.com/a/27568129/52749
    // Detects whether SVG is supported in the browser.
    nuget.supportsSvg = function () {
        return !!(document.createElementNS && document.createElementNS('http://www.w3.org/2000/svg', 'svg').createSVGRect);
    };

    // Source: https://developers.google.com/analytics/devguides/collection/analyticsjs/sending-hits
    nuget.createFunctionWithTimeout = function (callback, opt_timeout) {
        var called = false;
        function fn() {
            if (!called) {
                called = true;
                callback();
            }
        }
        setTimeout(fn, opt_timeout || 1000);
        return fn;
    };

    nuget.confirmEvent = function (message, e) {
        if (!confirm(message)) {
            if (e) {
                e.stopPropagation();
                e.preventDefault();
            }            
            return false;
        }

        return true;
    };

    nuget.commaJoin = function (items) {
        if (!items) {
            return '';
        }

        var allButLast = items.slice(0, -1).join(', ');
        var last = items.slice(-1)[0];
        return [allButLast, last].join(items.length < 2 ? '' : items.length === 2 ? ' and ' : ', and ');
    };

    nuget.resetFormValidation = function (formElement) {
        var validator = $(formElement).validate();
        $(formElement).find("*[name][data-val='true']").each(function () {
            validator.successList.push(this);
        });
        validator.showErrors();
        validator.resetForm();
        validator.reset();
    };

    nuget.isGaAvailable = function () {
        return typeof ga === 'function';
    };

    nuget.getDateFormats = function (input) {
        var datetime = moment.utc(input);

        if (!datetime.isValid()) {
            return null;
        }

        var title = datetime.utc().format();

        // Determine if the duration is less than 11 months, which is moment.js's threshold to switch to
        // years display.
        var duration = moment.duration(moment().diff(datetime)).abs();
        var text;
        if (duration.as('M') <= 10) {
            text = datetime.fromNow();
        } else {
            text = null;
        }

        return {
            title: title,
            text: text
        };
    };

    nuget.getFileName = function (fullPath) {
        return fullPath.split(/(\\|\/)/g).pop();
    };

    // Shared function for adding an anti-forgery token defined by ViewHelpers.AjaxAntiForgeryToken to an ajax request
    nuget.addAjaxAntiForgeryToken = function (data) {
        var $tokenKey = "__RequestVerificationToken";
        var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
        if (data instanceof FormData)
        {
            data.append($tokenKey, $field.val())
        }
        else
        {
            data["__RequestVerificationToken"] = $field.val();
        }
        return data;
    };

    // Implementation of C#'s string.Format for use in Javascript
    nuget.formatString = function (stringToFormat) {
        var i = arguments.length - 1;

        while (i--) {
            stringToFormat = stringToFormat.replace(new RegExp('\\{' + i + '\\}', 'gm'), arguments[i + 1]);
        }

        return stringToFormat;
    }

    window.nuget = nuget;

    initializeJQueryValidator();

    $(function () {
        // Use moment.js to format attributes with the "datetime" attribute to "X time ago".
        $.each($('*[data-datetime]'), function () {
            var $el = $(this);
            var formats = window.nuget.getDateFormats($el.data().datetime);
            if (!formats) {
                return;
            }

            if (!$el.attr('title')) {
                $el.attr('title', formats.title);
            }

            if (formats.text) {
                $el.text(formats.text);
            }
        });

        // Handle confirm pop-ups.
        $('*[data-confirm]').delegate('', 'click', function (e) {
            window.nuget.confirmEvent($(this).data().confirm, e);
        });

        // Select the first input that has an error.
        $('.has-error')
            .find('input,textarea,select')
            .filter(':visible:first')
            .focus();

        // Handle Google analytics tracking event on specific links.
        $.each($('a[data-track]'), function () {
            $(this).click(function (e) {
                var href = $(this).attr('href');
                var category = $(this).data().track;
                if (window.nuget.isGaAvailable() && href && category) {
                    if (e.altKey || e.ctrlKey || e.metaKey) {
                        ga('send', 'event', category, 'click', href);
                    } else {
                        e.preventDefault();
                        ga('send', 'event', category, 'click', href, {
                            'transport': 'beacon',
                            'hitCallback': window.nuget.createFunctionWithTimeout(function () {
                                document.location = href;
                            })
                        });
                    }
                }
            });
        });

        // Show elements that require ClickOnce
        (function () {
            var userAgent = window.navigator.userAgent.toUpperCase();
            var hasNativeDotNet = userAgent.indexOf('.NET CLR 3.5') >= 0;
            if (hasNativeDotNet) {
                $('.no-clickonce').removeClass('no-clickonce');
            }
        })();

        // Don't close the dropdown on click events inside of the dropdown.
        $(document).on('click', '.dropdown-menu', function (e) {
            e.stopPropagation();
        });

        $(document).on('keydown', function (e) {
            var code = (e.keyCode || e.which);
            var isValidInputCharacter =
                ((code >= 48 && code <= 57)           // numbers 0-9
                    || (code >= 64 && code <= 90)     // letters a-z
                    || (code >= 96 && code <= 111)    // numpad
                    || (code >= 186 && code <= 192)   // ; = , - . / `
                    || (code >= 219 && code <= 222))  // [\ ] '
                && !e.altKey && !e.ctrlKey && !e.metaKey;

            if (isValidInputCharacter && document.activeElement == document.body) {
                var searchbox = $("#search");
                searchbox.focus();
                var currInput = searchbox.val();
                searchbox.val("");
                searchbox.val(currInput);
            }
        });
    });
}());

