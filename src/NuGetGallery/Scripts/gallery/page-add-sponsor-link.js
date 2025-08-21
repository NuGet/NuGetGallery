$(function () {
    'use strict';

    // Set up sponsorship links section
    var sponsorshipViewModel = {
        message: ko.observable(''),
        successMessage: ko.observable(''),
        newSponsorshipUrl: ko.observable(''),
        confirmation: ko.observable(false),

        addSponsorshipLink: function (data, event) {
            var self = this;
            
            // Prevent form submission
            if (event && event.preventDefault) {
                event.preventDefault();
            }
            
            var url = self.newSponsorshipUrl().trim();

            // Clear previous error messages
            self.message('');

            // Validate URL input
            if (!url) {
                self.message('Please enter a valid URL.');
                return false;
            }

            // Ensure URL has protocol first
            if (!url.startsWith('http://') && !url.startsWith('https://')) {
                url = 'https://' + url;
                self.newSponsorshipUrl(url);
            }

            // Custom URL validation function
            function isValidUrl(string) {
                try {
                    // Use URL constructor to validate
                    var urlObj = new URL(string);
                    
                    // Must be http or https protocol
                    if (urlObj.protocol !== 'http:' && urlObj.protocol !== 'https:') {
                        return false;
                    }
                    
                    // Must have a valid hostname (not empty, not just whitespace)
                    if (!urlObj.hostname || urlObj.hostname.trim() === '') {
                        return false;
                    }
                    
                    // Hostname should not contain invalid characters for domain names
                    // Basic check for valid domain characters (letters, numbers, dots, hyphens)
                    var hostnamePattern = /^[a-zA-Z0-9.-]+$/;
                    if (!hostnamePattern.test(urlObj.hostname)) {
                        return false;
                    }
                    
                    // Should have at least one dot in hostname (unless localhost)
                    if (urlObj.hostname !== 'localhost' && !urlObj.hostname.includes('.')) {
                        return false;
                    }
                    
                    return true;
                } catch (e) {
                    // URL constructor throws for invalid URLs
                    return false;
                }
            }
            
            if (!isValidUrl(url)) {
                self.message('Please enter a valid URL.');
                return false;
            }

            // TODO: Implement actual server-side submission
            // For now, just add to UI
            try {
                var linkHtml = '<div class="sponsorship-link-item">' +
                    '<div class="row">' +
                    '<div class="col-xs-8 col-sm-9 col-md-9 sponsorship-link-url"><a href="' + url + '" target="_blank">' + url + '</a></div>' +
                    '<div class="col-xs-4 col-sm-3 col-md-3 text-right remove-sponsorship-link">' +
                    '<a role="button" class="icon-link sponsorship-remove-link" href="#" data-url="' + url + '" aria-label="Remove sponsorship link ' + url + '">' +
                    '<i class="ms-Icon ms-Icon--Cancel" aria-hidden="true"></i>' +
                    ' <span>Remove</span>' +
                    '</a>' +
                    '</div>' +
                    '</div>' +
                    '</div>';

                // Hide "No sponsorship links" message if it exists
                $('#sponsorship-links-container p').hide();
                $('#sponsorship-links-container').append(linkHtml);

                // Clear the form and error message only after successful addition
                self.newSponsorshipUrl('');
                self.message(''); // Clear any error messages
                self.successMessage('URL added successfully.');
                
                // Clear success message after 3 seconds
                setTimeout(function() {
                    self.successMessage('');
                }, 3000);
            } catch (error) {
                console.error('Error adding sponsorship link:', error);
                self.message('An error occurred while managing sponsorship links.');
            }

            return false; // Prevent form submission
        },

        removeSponsorshipLink: function (url) {
            var self = this;
            
            self.message('Removing sponsorship link...');

            // TODO: Implement actual server-side removal
            // For now, just remove from UI
            setTimeout(function () {
                $('[data-url="' + url + '"]').closest('.sponsorship-link-item').remove();

                // Show "No sponsorship links" message if no links remain
                if ($('.sponsorship-link-item').length === 0) {
                    $('#sponsorship-links-container').html('<p>No sponsorship links have been added.</p>');
                }

                self.message('');
            }, 500);
        }
    };

    // Apply knockout bindings to sponsorship section
    var sponsorshipContainer = document.querySelector('.page-manage-sponsorship-links');
    if (sponsorshipContainer) {
        ko.applyBindings(sponsorshipViewModel, sponsorshipContainer);
    } else {
        // Fallback - try to apply to document
        ko.applyBindings(sponsorshipViewModel);
    }

    // Add real-time validation for URL input
    $(document).on('input', '#newSponsorshipUrl', function() {
        var urlInput = this;
        var url = urlInput.value.trim();
        
        // Clear validation errors when user starts typing and input becomes valid
        if (url) {
            // Add protocol if missing for validation
            var urlToValidate = url;
            if (!url.startsWith('http://') && !url.startsWith('https://')) {
                urlToValidate = 'https://' + url;
            }
            
            // Use same validation function as in addSponsorshipLink
            function isValidUrl(string) {
                try {
                    var urlObj = new URL(string);
                    if (urlObj.protocol !== 'http:' && urlObj.protocol !== 'https:') {
                        return false;
                    }
                    if (!urlObj.hostname || urlObj.hostname.trim() === '') {
                        return false;
                    }
                    var hostnamePattern = /^[a-zA-Z0-9.-]+$/;
                    if (!hostnamePattern.test(urlObj.hostname)) {
                        return false;
                    }
                    if (urlObj.hostname !== 'localhost' && !urlObj.hostname.includes('.')) {
                        return false;
                    }
                    return true;
                } catch (e) {
                    return false;
                }
            }
            
            if (isValidUrl(urlToValidate)) {
                sponsorshipViewModel.message('');
            } else {
                // Show validation error for invalid URLs as user types
                sponsorshipViewModel.message('Please enter a valid URL.');
            }
        } else {
            // Clear errors when field is empty (don't show required error until submit)
            sponsorshipViewModel.message('');
        }
    });

    // Handle remove sponsorship link clicks for dynamically added content
    $(document).on('click', '.remove-sponsorship-link a', function (e) {
        e.preventDefault();
        var url = $(this).data('url');
        if (confirm('Are you sure you want to remove this sponsorship link?')) {
            sponsorshipViewModel.removeSponsorshipLink(url);
        }
    });
});
