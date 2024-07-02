$(function() {
    'use strict';

    const storage = window['localStorage'];
    const focusResultsColumnKey = 'focus_results_column';

    if (storage && storage.getItem(focusResultsColumnKey)) {
        storage.removeItem(focusResultsColumnKey);
        document.getElementById('results-column').focus({ preventScroll: true });
    }

    const searchForm = document.forms.search;
    const allFrameworks = document.querySelectorAll('.framework');
    const allTfms = document.querySelectorAll('.tfm');

    // Checkbox logic for Framework and Tfm filters
    for (const framework of allFrameworks) {
        framework.addEventListener('click', clickFrameworkCheckbox);
    }

    for (const tfm of allTfms) {
        tfm.addEventListener('click', clickTfmCheckbox);
    }

    function clickFrameworkCheckbox() {
        this.indeterminate = false;

        const tfms = document.querySelectorAll('[parent=' + this.id + ']');
        tfms.forEach((tfm) => {
            tfm.checked = false;
        });
    }

    function clickTfmCheckbox() {
        const framework = document.getElementById(this.getAttribute('parent'));
        const tfms = document.querySelectorAll('[parent=' + this.getAttribute('parent') + ']');

        const checkedCount = Array.from(tfms).reduce((accumulator, tfm) => accumulator + (tfm.checked ? 1 : 0), 0);

        framework.checked = false;
        framework.indeterminate = checkedCount !== 0;
    }

    // Accordion/collapsible logic
    const collapsibles = document.querySelectorAll('.collapsible');

    for (const collapsible of collapsibles) {
        collapsible.addEventListener('click', toggleCollapsible);
    }

    var resized = false;
    var initialScreenSize = window.innerWidth;
    const chevronIcon = document.getElementById('advancedSearchToggleChevron');

    const advancedSearchToggleButton = document.getElementById('advancedSearchToggleButton');

    if (advancedSearchToggleButton) {
        advancedSearchToggleButton.addEventListener('click', toggleAdvancedSearchPanel);
    }

    window.addEventListener('resize', () => {
        resized = true;
        toggleAdvancedSearchPanel();
    });

    /* For narrow screens only */
    function toggleAdvancedSearchPanel() {

        const filtersContent = document.getElementById('advancedSearchPanel');

        if (filtersContent) {
            var computedStyle = window.getComputedStyle(filtersContent);

            if (window.innerWidth <= 992 && !resized) {
                filtersContent.style.display = (computedStyle.display === 'none') ? 'block' : 'none';
                chevronIcon.classList.toggle('ms-Icon--ChevronDown');
                chevronIcon.classList.toggle('ms-Icon--ChevronUp');
            }
            else if (window.innerWidth <= 992 && initialScreenSize > 992 && resized) {
                filtersContent.style.display = 'none';
                chevronIcon.classList.add('ms-Icon--ChevronDown');
                chevronIcon.classList.remove('ms-Icon--ChevronUp');

            }
            else if (window.innerWidth > 992) {
                filtersContent.style.display = 'block';
            }
        }

        initialScreenSize = window.innerWidth;
        resized = false;
    }

    function toggleCollapsible() {
        var dataTab = document.getElementById(this.getAttribute('tab') + 'tab');
        var expandButton = document.getElementById(this.getAttribute('tab') + 'button');
        const tfmCheckboxes = dataTab.querySelectorAll('[parent=' + this.getAttribute('tab') + ']');

        this.classList.toggle('active');
        expandButton.classList.toggle('ms-Icon--ChevronRight');
        expandButton.classList.toggle('ms-Icon--ChevronDown');

        if (this.classList.contains('active')) {
            this.setAttribute("aria-expanded", "true");

            dataTab.style.display = 'block';
            dataTab.style.maxHeight = dataTab.scrollHeight + "px";
            dataTab.style.width = '100%';
        }
        else {
            this.setAttribute("aria-expanded", "false");

            dataTab.style.display = 'none';
            dataTab.style.maxHeight = 0;
        }
    }

    // Update query params before submitting the form
    function submitSearchForm() {
        constructFilterParameter(searchForm.frameworks, allFrameworks);
        constructFilterParameter(searchForm.tfms, allTfms);

        if (storage) {
            storage.setItem(focusResultsColumnKey, true);
        }

        // Remove empty or default attributes from the URL
        var frameworkFiltersSelected = false;

        var frameworks = document.getElementById('frameworks');
        frameworks.name = "";
        allFrameworks.forEach(function (framework) {
            if (framework.checked) {
                frameworks.name = "frameworks";
                frameworkFiltersSelected = true;
            }
        });

        var tfms = document.getElementById('tfms');
        tfms.name = "";
        allTfms.forEach(function (tfm) {
            if (tfm.checked) {
                tfms.name = "tfms";
                frameworkFiltersSelected = true;
            }
        });

        if (!frameworkFiltersSelected) {
            if (searchForm.includeComputedFrameworks.value == true) {
                searchForm.includeComputedFrameworks.name = "";
            }

            var frameworkFilterModeAll = document.getElementById('all-selector');
            if (frameworkFilterModeAll.checked) {
                frameworkFilterModeAll.name = "";
            }
        }

        var packageTypes = document.getElementById('packagetype');
        var allPackages = packageTypes.querySelector('input[value=""]');
        if (allPackages.checked) {
            allPackages.name = "";
        }

        searchForm.submit();
    }

    // Update the query string with the selected frameworks and tfms
    function constructFilterParameter(searchField, checkboxList) {
        searchField.value = "";

        checkboxList.forEach((framework) => {
            if (framework.checked) {
                searchField.value += framework.id + ",";
            }
        });

        // trim trailing commas
        searchField.value = searchField.value.replace(/,+$/, '');
    }

    // Initialize state for Framework and Tfm checkboxes
    // NOTE: We first click on all selected Framework checkboxes and then on the selected Tfm checkboxes, which
    // allows us to correctly handle cases where a Framework AND one of its child Tfms is present in the query
    function initializeFrameworkAndTfmCheckboxes() {
        var inputFrameworkFilters = searchForm.frameworks.value.split(',').map(f => f.trim()).filter(f => f);
        var inputTfmFilters = searchForm.tfms.value.split(',').map(f => f.trim()).filter(f => f);
        searchForm.frameworks.value = "";
        searchForm.tfms.value = "";

        inputFrameworkFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());
        inputTfmFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());

        // expand TFM section if a TFM from that generation has been selected
        allFrameworks.forEach((checkbox) => {
            if (checkbox.indeterminate) {
                document.querySelector('[tab=' + checkbox.id + ']').click();
            }
        });
    }

    // The /profiles pages use this js file too, but some code needs to be applied only to the search page
    if (searchForm) {
        searchForm.sortby.addEventListener('change', (e) => {
            searchForm.sortby.value = e.target.value;
            submitSearchForm();
        });
        searchForm.addEventListener('submit', submitSearchForm);
        initializeFrameworkAndTfmCheckboxes();
    }

    $(".reserved-indicator").each(window.nuget.setPopovers);
    $(".package-warning--vulnerable").each(window.nuget.setPopovers);
    $(".package-warning--deprecated").each(window.nuget.setPopovers);
    //for tooltip hover and focus
    $('.tooltip-target').each(function () {
        $(this).on('mouseenter focusin', function () {
            $(this).find('.tooltip-wrapper').addClass('show');
        });
        $(this).on('mouseleave focusout', function () {
            $(this).find('.tooltip-wrapper').removeClass('show');
        });
    });

    // for using arrow keys in Framwork filter mode checkbox tree 
    $('.tfmTab li input').each(function () {
        $(this).on('keydown', function (e) {
            switch (e.key) {
                case "ArrowDown":
                    if ($(this).parent().next().length > 0) {
                        $(this).parent().next().find('.tfm').focus();
                    }
                    break;
                case "ArrowUp":
                    if ($(this).parent().prev().length > 0) {
                        $(this).parent().prev().find('.tfm').focus();
                    }
                    break;
            }
        });
    });
});
