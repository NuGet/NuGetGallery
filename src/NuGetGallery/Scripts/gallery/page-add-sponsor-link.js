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

            // Check if maximum number of sponsorship links (10) has been reached
            var currentLinkCount = $('.sponsorship-link-item').length;
            if (currentLinkCount >= 10) {
                self.message('You can add a maximum of 10 sponsorship links.');
                return false;
            }

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

            // Sponsorship domain validation function
            function isValidSponsorshipUrl(string) {
                try {
                    // Use URL constructor to validate basic URL structure
                    var urlObj = new URL(string);
                    
                    // Must be http or https protocol
                    if (urlObj.protocol !== 'http:' && urlObj.protocol !== 'https:') {
                        return false;
                    }
                    
                    var hostname = urlObj.hostname.toLowerCase();
                    
                    // Check for trusted sponsorship domains
                    var trustedDomains = [
                        'github.com',           // GitHub Sponsors
                        'www.github.com',
                        'patreon.com',          // Patreon
                        'www.patreon.com',
                        'opencollective.com',   // Open Collective
                        'www.opencollective.com',
                        'ko-fi.com',            // Ko-fi
                        'www.ko-fi.com',
                        'tidelift.com',         // Tidelift
                        'www.tidelift.com',
                        'liberapay.com',        // Liberapay
                        'www.liberapay.com'
                    ];
                    return trustedDomains.indexOf(hostname) !== -1;
                } catch (e) {
                    // URL constructor throws for invalid URLs
                    return false;
                }
            }
            
            if (!isValidSponsorshipUrl(url)) {
                self.message('Please enter a valid URL from a supported sponsorship platform.');
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

    // Clear alerts when textbox is emptied - set up before applying bindings
    sponsorshipViewModel.newSponsorshipUrl.subscribe(function(newValue) {
        // Clear alerts when textbox becomes empty
        if (!newValue || newValue.trim() === '') {
            console.log('Clearing alerts because textbox is empty'); // Debug line
            sponsorshipViewModel.message('');
            sponsorshipViewModel.successMessage('');
        }
    });

    // Apply knockout bindings to sponsorship section
    var sponsorshipContainer = document.querySelector('.page-manage-sponsorship-links');
    if (sponsorshipContainer) {
        ko.applyBindings(sponsorshipViewModel, sponsorshipContainer);
    } else {
        // Fallback - try to apply to document
        ko.applyBindings(sponsorshipViewModel);
    }

    // Handle remove sponsorship link clicks for dynamically added content
    $(document).on('click', '.remove-sponsorship-link a', function (e) {
        e.preventDefault();
        var url = $(this).data('url');
        if (confirm('Are you sure you want to remove this sponsorship link?')) {
            sponsorshipViewModel.removeSponsorshipLink(url);
        }
    });
});
