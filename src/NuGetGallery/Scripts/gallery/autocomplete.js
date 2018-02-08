$(function () {
    'use strict';
    var _autocompleteTimeout = 0;
    var _autocompleteDelay = 100; //ms
    var _resultsCache = {};
    var _maxResults = 9;

    function hookAutocomplete(maxResults) {
        _resultsCache.results = ko.observable();
        $(document).keyup(function (e) {
            if (e.keyCode === 27) {
                removeOldAutocompleteResults();
                e.stopPropagation();
            }
        });

        var searchBox = $("#search");
        searchBox.on("keyup", function (e) {
            clearTimeout(_autocompleteTimeout);
            if (e.keyCode === 27 || $(this).val().length < 1) {
                removeOldAutocompleteResults();
                e.stopPropagation();;
                return;
            }

            if ((e.keyCode >= 46 && e.keyCode <= 90)        //delete, 0-9, a-z
                || (e.keyCode >= 96 && e.keyCode <= 111)    //numpad
                || (e.keyCode >= 186)                       //punctuation
                || (e.keyCode == 8))                        //backspace
            {
                _autocompleteTimeout = setTimeout(doSearch.bind(this, maxResults), _autocompleteDelay);
            }
        });
    }

    function removeOldAutocompleteResults() {
        var oldBox = $("#autocomplete-results");
        oldBox.remove();
        $("#autocomplete-results-container").hide();
    }

    function doSearch(maxResults) {

        var currInput = $("#search").val();
        if (currInput.length < 1) {
            return;
        }

        var requestUrl = "https://api-v2v3search-0.nuget.org/autocomplete?q=" + currInput + "&take=" + maxResults;
        $.ajax(requestUrl, {
            success: function (data, status) {
                _resultsCache.results(data);

                var container = $("#autocomplete-results");
                if (container.length < 1) {
                    container = document.createElement("div");
                    $(container).attr("id", "autocomplete-results");
                    $(container).attr("data-bind", "template: { name: 'autocomplete-results-template', data: results }");
                    $("#autocomplete-results-container").append(container);

                    ko.applyBindings(_resultsCache, container);
                }

                for (var i = 0; i < data.data.length; i++) {
                    var id = data.data[i];
                    _resultsCache[safeId(id)] = ko.observable(id);
                    setupAuxData(id);
                }
            }
        });

        $("#autocomplete-results-container").show();
    }

    function setupAuxData(id) {
        var requestUrl = "https://api-v2v3search-0.nuget.org/query?q=packageid:";
        var searchData = _resultsCache[safeId(id)];
        appendAuxData(id);

        if (typeof searchData() == "string") {
            $.ajax(requestUrl + id, {
                success: function (someId, data, status) {
                    _resultsCache[safeId(someId)](data);
                }.bind(this, id)
            });
        }
    }

    function appendAuxData(id) {
        var testNotExist = $("#autocomplete-results-row-" + jquerySafeId(id)).length < 1;

        if (testNotExist) {
            var container = document.createElement("div");
            $(container).attr("id", "autocomplete-results-row-" + id);
            $(container).attr("data-bind", "template: { name: 'autocomplete-results-row', data: " + safeId(id) + " }");
            var parent = $("#autocomplete-container-" + jquerySafeId(id));
            parent.append(container);

            ko.applyBindings(_resultsCache, container);
        }
    }

    function jquerySafeId(id) {
        return id.replace(/\./g, "\\.");
    }

    function safeId(id) {
        return id.replace(/(\.|-)/g, "");
    }

    hookAutocomplete(_maxResults);
});