var renderGraph = (function () {
    'use strict';

    var renderGraph = function (baseUrl, query, clickedId) {
        var renderGraphHandler = function (rawData) {
            var data = JSON.parse(JSON.stringify(rawData));

            $("#loading-placeholder").hide();
            if (data != null) {
                // Populate the data table
                data['reportSize'] = data.Table != null ? data.Table.length : 0;

                data['ShownRows'] = function (allRows) {
                    var shownRows = [];
                    var index = 0;
                    while (shownRows.length < Math.min(6, allRows.length)) {
                        shownRows.push(allRows[index]);
                        var currRowSpan = shownRows[index].reduce(function (currMax, nextObj) {
                            return Math.max(currMax, nextObj != null ? nextObj.Rowspan : 0);
                        }, 0);
                        for (var i = 0; i < currRowSpan - 1; i++) {
                            index++;
                            shownRows.push(allRows[index]);
                        }

                        index++;
                    }
                    return shownRows;
                }(data.Table != null ? data.Table : []);

                data['HiddenRows'] = data.Table != null ? data.Table.slice(data['ShownRows'].length) : [];

                data['SetupHiddenRows'] = setupHiddenRows.bind(this, data['HiddenRows']);
            }

            $("#report").remove();

            var reportContainerElement = document.createElement("div");
            $(reportContainerElement).attr("id", "report");
            $(reportContainerElement).attr("data-bind", "template: { name: 'report-template', data: report }");
            $("#report-container").append(reportContainerElement);

            ko.applyBindings({ report: data }, reportContainerElement);
            // Render the graph using the data table
            packageDisplayGraphs(rawData);

            window.nuget.configureExpander(
                "hidden-rows",
                "CalculatorAddition",
                "Show less",
                "CalculatorSubtract",
                "Show more");

            // Add the click handler to the checkboxes
            groupbyNavigation(baseUrl);

            // Set the focus to the checkbox that initiated this request
            if (clickedId) {
                $('#' + clickedId).focus();
            }
        };


        $.ajax({
            url: baseUrl + query,
            type: 'GET',
            dataType: 'json',
            success: renderGraphHandler,
            error: function () {
                renderGraphHandler(null);
                $("#loading-placeholder").hide();

                $('#statistics-retry').click(function () {
                    renderGraph(baseUrl, query);
                });
            }
        });
    };

    var groupbyNavigation = function (baseUrl) {
        $('.dimension-checkbox').click(function (event) {
            var container = $("#stats-data-display").parent();
            $("#stats-data-display").remove();
            $("#loading-placeholder").show();
            var clickedId = event.target.id;

            var query = '';
            $('.dimension-checkbox').each(function (index, element) {
                if (element.checked) {
                    if (query) {
                        query += '&';
                    } else {
                        query = '?';
                    }
                    query += 'groupby=' + element.value;
                }
            });

            history.replaceState({}, "", query);
            renderGraph(baseUrl, query, clickedId);
        });
    }

    var setupHiddenRows = function (data) {
        var container = $("#hidden-rows");
        // no-op if we've already appended the hidden rows
        if (container.children().length > 0) {
            return;
        }

        var tableContainer = container.parent();
        container.remove();

        var trArr = [];
        for (var i = 0; i < data.length; i++) {
            var tempTr = $(document.createElement("tr"));
            var tdArr = [];
            for (var j = 0; j < data[i].length; j++) {
                var tempTd = $(document.createElement("td"));
                var item = data[i][j];
                if (item != null) {
                    tempTd.attr("class", item.IsNumeric ? "statistics-number" : "");
                    tempTd.attr("rowspan", item.Rowspan > 0 ? item.Rowspan : "");
                    var content = null;
                    if (item.Uri != null) {
                        content = $(document.createElement("a"));
                        content.attr("href", item.Uri);
                        content.text(item.Data);
                    } else {
                        var textValue = item.IsNumeric ? parseInt(item.Data).toLocaleString() : item.Data;
                        content = $(document.createElement("span"));
                        content.attr("aria-label", textValue);
                        content.text(textValue);
                    }

                    tempTd.append(content);
                    tdArr.push(tempTd);
                }
            }
            tempTr.append(tdArr);
            trArr.push(tempTr);
        }

        container.append(trArr);
        tableContainer.append(container);

        // When we remove the 'hidden-rows' element from the container above, we apparently kill all the event handlers on it. So reattach here.
        window.nuget.configureExpander(
            "hidden-rows",
            "CalculatorAddition",
            "Show less",
            "CalculatorSubtract",
            "Show more");
    }

    return renderGraph;
}());
