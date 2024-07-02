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
        var validatorErrorClass = 'help-block';
        $.validator.setDefaults({
            highlight: function (element) {
                $(element).closest('.form-group').addClass('has-error-brand');
            },
            unhighlight: function (element) {
                $(element).closest('.form-group').removeClass('has-error-brand');
            },
            errorElement: 'span',
            errorClass: validatorErrorClass,
            errorPlacement: function (error, element) {
                if (element.parent('.input-group').length) {
                    error.insertAfter(element.parent());
                } else {
                    error.insertAfter(element);
                }
            },
            showErrors: function (errorMap, errorList) {
                this.defaultShowErrors();

                var i;
                for (i = 0; this.errorList[i]; i++) {
                    fixAccessibilityIssuesWithAriaDescribedBy(this.errorList[i].element, validatorErrorClass);
                }

                for (i = 0; this.successList[i]; i++) {
                    fixAccessibilityIssuesWithAriaDescribedBy(this.successList[i], validatorErrorClass);
                }
            }
        });
    }

    function fixAccessibilityIssuesWithAriaDescribedBy(element, validatorErrorClass) {
        var describedBy = element.getAttribute("aria-describedby");
        if (!describedBy) {
            return;
        }

        var uniqueIds = [];
        var ids = describedBy
            .split(" ")
            .filter(function (describedById) {
                if (!describedById) {
                    return false;
                }

                // The default showErrors adds an aria-describedby attribute to every field that it validates, even if it finds no issues.
                // This is a problem, because the aria-describedby attribute will then link to an empty element.
                // If the element linked to by the aria-describedby attribute is empty, remove the aria-describedby.
                var describedByElement = $("#" + describedById);
                return describedByElement && describedByElement.text();
            })
            .map(function (describedById) {
                // The default showErrors puts the error inside a container.
                // This causes Narrator to read the error as being part of a group, even though it is the only error.
                // JQuery Validator only ever shows a single error for each form input so it is always possible for us to simply unwrap the error.
                var describedByElement = $("#" + describedById);
                var parent = describedByElement.parent();
                if (parent.hasClass(validatorErrorClass)) {
                    parent.text(describedByElement.text());
                    describedByElement.remove();
                    return parent.attr('id');
                } else {
                    return describedById;
                }
            })
            .filter(function (describedById) {
                // Remove any duplicate IDs.
                var isUnique = $.inArray(describedById, uniqueIds) === -1;
                if (isUnique) {
                    uniqueIds.push(describedById);
                }

                return isUnique;
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
        var showId = '#show-' + prefix;
        var show = $(showId);
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

        // If the URI fragment (hash) matches the expander ID, automatically expand the section.
        if (document.location.hash === showId) {
            hidden.collapse('show');
        }
    };

    nuget.configureExpanderHeading = function (prefix) {
        window.nuget.configureExpander(prefix, "ChevronRight", null, "ChevronDown", null);
    };

    nuget.configureFileInputButton = function (id) {
        // File input buttons should respond to keyboard events.
        $("#" + id).on("keypress", function (e) {
            var code = e.keyCode || e.which;
            var isInteract = (code === 13 /*enter*/ || code === 32 /*space*/) && !e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey;
            if (isInteract) {
                $(this).click();
            }
        });
    };

    nuget.canElementBeFocused = function (element) {
        element = $(element);
        if (!element.is(':visible')) {
            return false;
        }

        // Elements with tabindex set to a value besides -1 are focusable.
        var tabIndex = element.attr('tabindex');
        if (!!tabIndex && tabIndex >= 0) {
            return true;
        }

        // See https://developer.mozilla.org/en-US/docs/Web/Guide/HTML/Content_categories#Interactive_content
        var alwaysInteractiveElements = ['a', 'button', 'details', 'embed', 'iframe', 'keygen', 'label', 'select', 'textarea'];
        var i;
        for (i = 0; i < alwaysInteractiveElements.length; i++) {
            if (element.is(alwaysInteractiveElements[i])) {
                return true;
            }
        }

        return element.is("audio") && !!element.attr("controls") ||
            element.is("img") && !!element.attr("usemap") ||
            element.is("input") && element.attr("type") !== "hidden" ||
            element.is("menu") && element.attr("type") !== "toolbar" ||
            element.is("object") && !!element.attr("usemap") ||
            element.is("video") && !!element.attr("controls");
    };

    nuget.canElementBeTabbedTo = function (element) {
        return window.nuget.canElementBeFocused(element) && $(element).attr('tabindex') !== "-1";
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

    nuget.isAiAvailable = function () {
        return typeof window.appInsights === 'object';
    };

    nuget.isInstrumentationAvailable = function () {
        return typeof window.NuGetInstrumentation === 'object';
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
        if (data instanceof FormData) {
            data.append($tokenKey, $field.val());
        }
        else {
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
    };

    nuget.isAncestor = function (element, ancestorSelector) {
        var $target = $(element);
        // '.closest' returns the list of ancestors between this element and the selector.
        // If the selector is not an ancestor of the element, it returns an empty list.
        return !!$target.closest(ancestorSelector).length;
    };

    nuget.configureDropdown = function (dropdownSelector, dropdownHeaderSelector, setDropdownOpen, openWhenFocused) {
        var isElementInsideDropdown = function (element) {
            return window.nuget.isAncestor(element, dropdownSelector);
        };

        // If the user clicks outside the dropdown, close it
        $(document).click(function (event) {
            if (!isElementInsideDropdown(event.target)) {
                setDropdownOpen(false);
            }
        });

        $(document).focusin(function (event) {
            var isInsideDropdown = isElementInsideDropdown(event.target);
            if (isInsideDropdown && openWhenFocused) {
                // If an element inside the dropdown gains focus, open the dropdown if configured to
                setDropdownOpen(true);
            } else if (!isInsideDropdown) {
                // If an element outside of the dropdown gains focus, close it
                setDropdownOpen(false);
            }
        });

        $(document).keydown(function (event) {
            var target = event.target;
            if (isElementInsideDropdown(target)) {
                // If we press escape while focus is inside the dropdown, close it
                if (event.which === 27) { // Escape key
                    setDropdownOpen(false);
                    event.preventDefault();
                    $(dropdownHeaderSelector).focus();
                }
            }
        });
    };

    nuget.sendAnalyticsEvent = function (category, action, label, eventValue, options) {
        if (window.nuget.isGaAvailable()) {
            ga('send', 'event', category, action, label, eventValue, options);
        }
    };

    nuget.sendMetric = function (name, value, properties) {
        if (window.nuget.isInstrumentationAvailable()) {
            window.NuGetInstrumentation.trackMetric({
                name: name,
                average: value,
                sampleCount: 1,
                min: value,
                max: value
            }, properties);
        } else if (window.nuget.isAiAvailable()) {
            window.appInsights.trackMetric(name, value, 1, value, value, properties);
        }
    };

    nuget.setPopovers = function () {
        setPopoversInternal(this, rightWithVerticalFallback);
    }

    function rightWithVerticalFallback(popoverElement, ownerElement) {
        // Both numbers below are in CSS pixels.
        const MinSpaceOnRight = 150;
        const MinSpaceOnTop = 100;

        const ownerBoundingBox = ownerElement.getBoundingClientRect();
        const spaceOnRight = window.innerWidth - ownerBoundingBox.right;
        if (spaceOnRight > MinSpaceOnRight) {
            return 'right';
        }
        const spaceOnTop = ownerBoundingBox.top;
        if (spaceOnTop > MinSpaceOnTop) {
            return 'top';
        }

        return 'bottom';
    }

    function setPopoversInternal(element, placement) {
        var popoverElement = $(element);
        var popoverElementDom = element;
        var originalLabel = popoverElementDom.ariaLabel;
        var popoverHideTimeMS = 2000;
        var popoverFadeTimeMS = 200;

        var popoverOptions = { trigger: 'hover', container: 'body' };
        if (placement) {
            popoverOptions.placement = placement;
        }

        popoverElement.popover(popoverOptions);
        popoverElement.click(popoverShowAndHide);
        popoverElement.focus(popoverShowAndHide);
        popoverElement.keyup(function (event) {
            // normalize keycode for browser compatibility
            var code = event.which || event.keyCode || event.charCode;

            // This is the keycode for the 'Esc' key
            if (code === 27) {
                popoverElement.popover('hide');
            }
        });

        function popoverShowAndHide() {
            popoverElement.popover('show');

            // Windows Narrator does not announce popovers' content. See: https://github.com/twbs/bootstrap/issues/18618
            // We can force Narrator to announce the popover's content by "flashing" the element's ARIA label.
            popoverElementDom.ariaLabel = "";

            setTimeout(function () {
                popoverElement.popover('hide');

                // We need to restore the element's original ARIA label.
                // Wait 0.15 seconds for the popover to fade away first.
                // Otherwise, the screen reader will re-announce the popover's content.
                setTimeout(function () {
                    popoverElementDom.ariaLabel = originalLabel;
                }, popoverFadeTimeMS);
            }, popoverHideTimeMS);
        }
    };

    window.nuget = nuget;

    jQuery.extend(jQuery.expr.pseudos, {
        focusable: window.nuget.canElementBeFocused,
        tabbable: window.nuget.canElementBeTabbedTo
    });

    initializeJQueryValidator();

    // Add listener to the theme selector
    var themeSelector = document.getElementById("select-option-theme");
    if (themeSelector != null) {
        themeSelector.addEventListener("change", () => {
            if (themeSelector.value === "system") {
                localStorage.setItem("theme", "system");
                document.body.setAttribute('data-theme', defaultTheme);
                document.getElementById("user-prefered-theme").textContent = "System";
            }
            else {
                localStorage.setItem("theme", themeSelector.value);
                document.body.setAttribute('data-theme', themeSelector.value);
                document.getElementById("user-prefered-theme").textContent = themeSelector.value == "light" ? "Light" : "Dark";
            }
            window.nuget.sendMetric("ThemeChanged", 1, { "ThemeChanged": themeSelector.value });
        })

        // Set the theme selector to the user's preferred theme
        var theme = localStorage.getItem("theme")
        themeSelector.value = theme;
        document.getElementById("user-prefered-theme").textContent = theme.charAt(0).toUpperCase() + theme.slice(1);
    }

    $(function () {
        // Enable the POST links. These are links that perform a POST via a form instead of traditional navigation.
        $(".post-link").on('click', function () {
            $("#" + $(this).data().formId).submit();
            return false;
        });

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
        $('*[data-confirm]').on('click', '', function (e) {
            window.nuget.confirmEvent($(this).data().confirm, e);
        });

        // Select the first input that has an error.
        $('.has-error-brand')
            .find('input,textarea,select')
            .filter(':visible:first')
            .trigger('focus');

        // Handle Application Insights tracking event on specific links.
        var emitClickEvent = function (e) {
            if (!window.nuget.isAiAvailable()) {
                return;
            }

            var href = $(this).attr('href');
            var category = $(this).data().track;

            var trackValue = $(this).data().trackValue;
            if (typeof trackValue === 'undefined') {
                trackValue = 1;
            }

            if (href && category) {
                window.nuget.sendMetric('BrowserClick', trackValue, {
                    href: href,
                    category: category
                });
            }
        };
        $.each($('a[data-track]'), function () {
            $(this).on('mouseup', function (e) {
                if (e.which === 2) { // Middle-mouse click
                    emitClickEvent.call(this, e);
                }
            });
            $(this).on('click', function (e) {
                emitClickEvent.call(this, e);
            });
        });

        // Don't close the dropdown on click events inside of the dropdown.
        $(document).on('click', '.dropdown-menu', function (e) {
            e.stopPropagation();
        });

        $(document).on('keydown', function (e) {
            var code = e.keyCode || e.which;
            var isValidInputCharacter =
                (code >= 48 && code <= 57           // numbers 0-9
                    || code >= 64 && code <= 90     // letters a-z
                    || code >= 96 && code <= 111    // numpad
                    || code >= 186 && code <= 192   // ; = , - . / `
                    || code >= 219 && code <= 222)  // [\ ] '
                && !e.altKey && !e.ctrlKey && !e.metaKey;

            if (isValidInputCharacter && document.activeElement === document.body) {
                var searchbox = $("#search");
                searchbox.focus();
                var currInput = searchbox.val();
                searchbox.val("");
                searchbox.val(currInput);
            }
        });

        $("#skipToContent").on('click', function () {
            // Focus on the first element that can be tabbed to inside the "skippedToContent" element.
            var skippedToContent = $("#skippedToContent");
            var firstChildThatCanBeTabbedTo = skippedToContent.find(':tabbable').first();
            if (firstChildThatCanBeTabbedTo !== null) {
                firstChildThatCanBeTabbedTo.focus();
            } else {
                // Focus on the "skippedToContent" element itself if we can't find an element on the page we can tab to.
                // It's better to lose tab focus than to have the focus stay on the "Skip to Content" link. 
                skippedToContent.focus();
            }
        });

        window.WcpConsent && WcpConsent.init("en-US", "cookie-banner", function (err, _siteConsent) {
            if (err !== undefined) {
                console.log("Error initializing WcpConsent: ", err);
            } else {
                window.nuget.wcpSiteConsent = _siteConsent;  // wcpSiteConsent is used to get the current consent
            }
        });

        if (window.nuget.wcpSiteConsent && window.nuget.wcpSiteConsent.isConsentRequired) {
            $("#footer-privacy-policy-link").after(" - <a class='button' href = 'javascript: window.nuget.wcpSiteConsent.manageConsent()' > Manage Cookies</a >")
        }
    });
}());
