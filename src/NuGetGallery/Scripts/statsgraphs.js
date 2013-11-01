
var drawNugetClientVersionBarChart = function () {

    var margin = { top: 20, right: 30, bottom: 80, left: 80 },
        width = 460 - margin.left - margin.right,
        height = 320 - margin.top - margin.bottom;

    var xScale = d3.scale.ordinal()
        .rangeRoundBands([0, width], .1);

    var yScale = d3.scale.linear()
        .range([height, 0]);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom');

    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left');

    var svg = d3.select('#downloads-by-nuget-version').append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom);

    svg.append('title').text('NuGet Client Usage (Last 6 Weeks)');
    svg.append('desc').text('This is a graph showing the number of downloads by each version of the NuGet client over the last six weeks.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

    var data = [];

    d3.selectAll('#downloads-by-nuget-version tbody tr').each(function () {
        var item = {
            nugetVersion: d3.select(this).select(':nth-child(1)').text(),
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, '')),
            percentage: d3.select(this).select(':nth-child(3)').text(),
        };
        data[data.length] = item;
    });

    xScale.domain(data.map(function (d) { return d.nugetVersion; }));
    yScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);

    //  the use of dx attribute on the text element is correct, however, the negative shift doesn't appear to work on Firefox
    //  the workaround employed here is to add a translation to the rotation transform

    svg.append("g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + height + ")")
        .call(xAxis)
        .selectAll("text")
        .style("text-anchor", "end")
        //.attr("dx", "-.8em")
        .attr("dy", ".15em")
        .attr("transform", function (d) {
            return "rotate(-65),translate(-10,0)"
        });

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis)
        .append("text")
        .attr("transform", "rotate(-90)")
        .attr("y", 6)
        .attr("dy", ".71em")
        .style("text-anchor", "end")
        .text("Downloads");

    svg.selectAll(".bar")
        .data(data)
        .enter()
        .append("rect")
            .attr("class", "bar")
            .attr("x", function (d) { return xScale(d.nugetVersion); })
            .attr("width", xScale.rangeBand())
            .attr("y", function (d) { return yScale(d.downloads); })
            .attr("height", function (d) { return height - yScale(d.downloads); })
        .append("title")
            .text(function (d) { return d.percentage; });
}

var drawMonthlyDownloadsLineChart = function () {

    var margin = { top: 20, right: 20, bottom: 80, left: 80 },
        width = 400 - margin.left - margin.right,
        height = 300 - margin.top - margin.bottom;

    var xScale = d3.scale.ordinal()
        .rangePoints([0, width]);

    var yScale = d3.scale.linear()
        .range([height, 0]);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom');

    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left');

    var data = [];

    d3.selectAll('#downloads-per-month tbody tr').each(function () {
        var item = {
            month: d3.select(this).select(':nth-child(1)').text(),
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
        };
        data[data.length] = item;
    });

    var line = d3.svg.line()
        .x(function (d) { return xScale(d.month); })
        .y(function (d) { return yScale(d.downloads); });

    var svg = d3.select("#downloads-per-month").append("svg")
        .attr("width", width + margin.left + margin.right)
        .attr("height", height + margin.top + margin.bottom);

    svg.append('title').text('Packages Downloaded Per Month');
    svg.append('desc').text('This is a graph showing the number of downloads from NuGet per month.');

    svg = svg.append("g").attr("transform", "translate(" + margin.left + "," + margin.top + ")");

    xScale.domain(data.map(function (d) { return d.month; }));
    yScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);

    //  the use of dx attribute on the text element is correct, however, the negative shift doesn't appear to work on Firefox
    //  the workaround employed here is to add a translation to the rotation transform

    svg.append("g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + height + ")")
        .call(xAxis)
        .selectAll("text")
        .style("text-anchor", "end")
        //.attr("dx", "-.8em")
        .attr("dy", ".15em")
        .attr("transform", function (d) {
            return "rotate(-65),translate(-10,0)"
        });

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis);

    svg.append("path")
        .datum(data)
        .attr("class", "line")
        .attr("d", line);

    var formatDownloads = d3.format(',');

    svg.selectAll('.point')
        .data(data)
        .enter()
        .append("svg:circle")
        .attr("class", "line-graph-dot")
        .attr("cx", function (d) { return xScale(d.month); })
        .attr("cy", function (d) { return yScale(d.downloads); })
        .attr("r", 5)
        .append("title")
            .text(function (d) { return formatDownloads(d.downloads); });
}

