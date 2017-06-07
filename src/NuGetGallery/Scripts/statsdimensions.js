var renderGraph = function (baseUrl, query, clickedId) {
    var renderGraphHandler = function (data) {
        // Render the graph using the data table
        packageDisplayGraphs(data);

        data.Table.sort(function (a, b) {
            return parseInt(b[1].Data.replace(",", "")) - parseInt(a[1].Data.replace(",", ""));
        });

        // Populate the data table
        ko.applyBindings({ report: data });

        // Add the click handler to the checkboxes
        groupbyNavigation(baseUrl);
        // Set the focus to the checkbox that initiated this request
        if (clickedId) {
            $('#' + clickedId).focus();
        }
    }

    $.ajax({
        url: baseUrl + query,
        type: 'GET',
        dataType: 'json',
        success: renderGraphHandler,
        error: function () {
            renderGraphHandler(null);

            $('#statistics-retry').click(function () {
                renderGraph(baseUrl, query);
            });
        }
    });
}

var groupbyNavigation = function (baseUrl) {
    $('.dimension-checkbox').click(function (event) {
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

        renderGraph(baseUrl, query, clickedId);
    });
}