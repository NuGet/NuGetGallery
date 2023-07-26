$(function() {
    'use strict';

    $(".reserved-indicator").each(window.nuget.setPopovers);
    $(".package-warning--vulnerable").each(window.nuget.setPopovers);
    $(".package-warning--deprecated").each(window.nuget.setPopovers);

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






    // Define the breakpoint for small screens
    const smallScreenBreakpoint = 992; // You can adjust this value as needed

    // Function to create the toggle button
    function createToggleButton() {
        // Check if the screen width is small
        //if (window.innerWidth <= smallScreenBreakpoint && !document.getElementById('advancedSearchToggleButton')) {
            // Create the toggle button element dynamically
            const toggleButton = document.createElement('div');
            toggleButton.className = 'toggle-button';
            toggleButton.id = 'advancedSearchToggleButton'
            toggleButton.textContent = 'Toggle Filters';
            const theButton = document.createElement('button');
            theButton.innerHTML = 'Click Me';
            toggleButton.appendChild(theButton);

            // Get the container element that wraps the filters content
            const filtersContainer = document.getElementById('advancedSearchPanel');

            //filtersContainer.appendChild(toggleButton);

            // Insert the toggle button as the first child of the container
            filtersContainer.insertBefore(toggleButton, filtersContainer.firstChild);

            // Add the event listener to the toggle button
            toggleButton.addEventListener('click', toggleFilters);
        //}
        if (window.innerWidth > smallScreenBreakpoint) {
            toggleButton.style.display = 'none';
        }
    }

    // Call the function on page load
    window.addEventListener('load', createToggleButton);

    function toggleCollapsibleFiltersButton() {
        if (window.innerWidth > smallScreenBreakpoint) {
            toggleButton.style.display = 'none';
        }
        else {
            toggleButton.style.display = 'block';
        }
    }
    
    function toggleFilters() {
        const filtersContent = document.getElementById('advancedSearchPanel');

        // Check if the screen width is small
        //if (window.innerWidth <= smallScreenBreakpoint) {
        //    filtersContent.style.display = (filtersContent.style.display === 'none') ? 'block' : 'none';
        //}
    }

    // Call the function on window resize to handle changes in screen size
    window.addEventListener('resize', toggleCollapsibleFiltersButton);










    function toggleCollapsible() {
        var dataTab = document.getElementById(this.getAttribute('tab') + 'tab');
        var expandButton = document.getElementById(this.getAttribute('tab') + 'button');
        const tfmCheckboxes = dataTab.querySelectorAll('[parent=' + this.getAttribute('tab') + ']');

        this.classList.toggle('active');
        expandButton.classList.toggle('ms-Icon--ChevronDown');
        expandButton.classList.toggle('ms-Icon--ChevronUp');

        if (this.classList.contains('active')) {
            this.setAttribute("aria-expanded", "true");

            dataTab.style.display = 'block';
            dataTab.style.maxHeight = dataTab.scrollHeight + "px";
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

        var frameworks = document.getElementById('frameworks');
        frameworks.name = "";
        allFrameworks.forEach(function (framework) {
            if (framework.checked) {
                frameworks.name = "frameworks";
            }
        });

        var tfms = document.getElementById('tfms');
        tfms.name = "";
        allTfms.forEach(function (tfm) {
            if (tfm.checked) {
                tfm.name = "tfms";
            }
        });

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
});
