// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

window.SponsorshipValidation = (function () {
    'use strict';

    // Maximum number of sponsorship links allowed (should match server-side GalleryConstants.MaxSponsorshipLinksPerPackage)
    var MAX_SPONSORSHIP_LINKS = 10;

    /**
     * Validates sponsorship URL input (basic client-side checks only)
     * @param {string} url - The URL to validate
     * @param {number} currentCount - Current number of sponsorship links
     * @returns {object} - { isValid: boolean, errorMessage: string }
     */
    function validateSponsorshipUrl(url, currentCount) {
        var result = {
            isValid: false,
            errorMessage: ''
        };

        // Check maximum links limit
        if (currentCount >= MAX_SPONSORSHIP_LINKS) {
            result.errorMessage = 'You can add a maximum of ' + MAX_SPONSORSHIP_LINKS + ' sponsorship links.';
            return result;
        }

        // Validate that URL is not empty
        if (!url || url.trim() === '') {
            result.errorMessage = 'Please enter a URL.';
            return result;
        }

        // Server handles all URL format and domain validation
        result.isValid = true;
        return result;
    }

    /**
     * Sets up alert clearing behavior for sponsorship URL inputs
     * @param {Observable} urlObservable - Knockout observable for URL input
     * @param {Observable} messageObservable - Knockout observable for error messages
     * @param {Observable} successObservable - Knockout observable for success messages
     */
    function setupAlertClearing(urlObservable, messageObservable, successObservable) {
        urlObservable.subscribe(function(newValue) {
            if (!newValue || newValue.trim() === '') {
                messageObservable('');
                successObservable('');
            }
        });
    }

    // Public API
    return {
        validateSponsorshipUrl: validateSponsorshipUrl,
        setupAlertClearing: setupAlertClearing,
        MAX_SPONSORSHIP_LINKS: MAX_SPONSORSHIP_LINKS
    };
})();
