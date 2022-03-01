var packageDisplayGraphs = (function () {
    'use strict';

    var graphData;
    // This number is from trial and error and seeing what fit in the space
    var axisLabelCharLimit = 19;

    var packageDisplayGraphs = function (data) {
        window.graphData = data;
        $("#stats-graph-svg").remove();
        switch (data.Id) {
            case 'report-Version':
                drawDownloadsByVersionBarChart(data);
                break;
            case 'report-ClientName':
                drawDownloadsByClientNameBarChart(data);
                break;
            default:
                break;
        }
    };

    var drawDownloadsByVersionBarChart = function (rawData) {

        //  scrape data if we don't get a model
        var data = GetChartData(rawData, function (item) { return false; });

        if (data.length <= 0) {
            d3.selectAll('#report-Version .statistics-data tbody tr').each(function () {
                var item = {
                    label: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, ''),
                    downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
                };
                data[data.length] = item;
            });
        }

        // we get descending order from server. Reverse so we can cut the right versions.
        data.reverse();

        if (data.length < 1) {
            return;
        }

        //  limit the bar graph to the most recent 15 versions
        if (data.length > 15) {
            data = data.slice(data.length - 15, data.length);
        }

        //  draw graph
        var reportGraphWidth = $('#statistics-graph-id').width();

        reportGraphWidth = Math.min(reportGraphWidth, 1170);

        var margin = { top: 40, right: 10, bottom: 130, left: 45 },
            width = reportGraphWidth - margin.left - margin.right,
            height = 450 - margin.top - margin.bottom;

        var xScale = d3.scaleBand()
            .rangeRound([10, width])
            .padding(0.1);

        var yScale = d3.scaleLinear()
            .range([height, 0]);

        var xAxis = d3.axisBottom(xScale)
            .tickFormat(function (d) {
                return d.substring(0, axisLabelCharLimit) + (d.length > axisLabelCharLimit ? "..." : "");
            });

        var yAxis = d3.axisLeft(yScale)
            .tickFormat(function (d) {
                return GetShortNumberString(d);
            });

        var svg = d3.select('#statistics-graph-id')
            .append('svg')
            .attr('id', 'stats-graph-svg')
            .attr('width', width + margin.left + margin.right)
            .attr('height', height + margin.top + margin.bottom);

        svg.append('title').text('Downloads By Version');
        svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by version.');

        svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

        xScale.domain(data.map(function (d) { return d.label; }));
        yScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);

        //  the use of dx attribute on the text element is correct, however, the negative shift doesn't appear to work on Firefox
        //  the workaround employed here is to add a translation to the rotation transform

        svg.append("g")
            .attr("class", "x axis long")
            .attr("transform", "translate(0," + height + ")")
            .call(xAxis)
            .selectAll("text")
            .style("text-anchor", "end")
            //.attr("dx", "-.8em")
            .attr("dy", ".15em")
            .attr("transform", function (d) {
                return "rotate(-65),translate(-10,0)";
            });

        svg.selectAll(".bar")
            .data(data)
            .enter()
            .append("rect")
            .attr("class", "bar")
            .attr("x", function (d) { return xScale(d.label); })
            .attr("width", xScale.bandwidth())
            .attr("y", function (d) { return yScale(d.downloads); })
            .attr("height", function (d) { return height - yScale(d.downloads); })
            .append("title").text(function (d) { return d.downloads + " Downloads"; });

        //svg.append("foreignObject")
        //    .attr("x", "1.71em")
        //    .attr("y", -30)
        //    .attr("width", width - 20 + "px")
        //    .attr("height", "2em")
        //    .attr("font-weight", "bold")
        //    .append("xhtml:body")
        //    .append("p")
        //    .attr("style", "text-align:center")
        //    .text("Downloads for 15 Latest Package Versions (Last 6 weeks)");

        svg.append("g")
            .attr("class", "y axis")
            .call(yAxis)
            .append("text")
            .attr("transform", "rotate(-90)")
            .attr("y", 6)
            .attr("dy", ".71em")
            .style("text-anchor", "end")
            .text("Downloads");

        $("#statistics-graph-title-id").text("Downloads for 15 Latest Package Versions (Last 6 weeks)");
    }

    var drawDownloadsByClientNameBarChart = function (rawData) {

        //  scrape data

        var data = GetChartData(rawData, function (item) {
            return item.label === '(unknown)';
        });

        if (data.length <= 0) {
            d3.selectAll('#report-ClientName .statistics-data tbody tr').each(function () {
                var item = {
                    label: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, ''),
                    downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
                };

                //  filter out unknown
                if (item.label !== '(unknown)') {
                    data[data.length] = item;
                }
            });
        }

        data.reverse();

        if (data.length < 1) {
            return;
        }

        //  draw graph

        var reportGraphWidth = $('#statistics-graph-id').width();
        reportGraphWidth = Math.min(reportGraphWidth, 1170);

        var margin = { top: 40, right: 10, bottom: 50, left: 150 },
            width = reportGraphWidth - margin.left - margin.right,
            height = Math.max(550, data.length * 25) - margin.top - margin.bottom;

        var xScale = d3.scaleLinear()
            .range([0, width - 50]);

        var yScale = d3.scaleBand()
            .rangeRound([height, 20])
            .padding(0.1);

        var xAxis = d3.axisBottom(xScale)
            .tickFormat(function (d) {
                return GetShortNumberString(d);
            });

        var yAxis = d3.axisLeft(yScale)
            .tickFormat(function (d) {
                return d.substring(0, axisLabelCharLimit) + (d.length > axisLabelCharLimit ? "..." : "");
            });

        var svg = d3.select('#statistics-graph-id')
            .append('svg')
            .attr('id', 'stats-graph-svg')
            .attr('width', width + margin.left + margin.right)
            .attr('height', height + margin.top + margin.bottom);

        svg.append('title').text('Downloads By Client');
        svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by client.');

        svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

        xScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);
        yScale.domain(data.map(function (d) { return d.label; }));

        //  the use of dx attribute on the text element is correct, however, the negative shift doesn't appear to work on Firefox
        //  the workaround employed here is to add a translation to the rotation transform

        svg.append("g")
            .attr("class", "x axis")
            .attr("transform", "translate(0," + height + ")")
            .call(xAxis);

        svg.selectAll(".bar")
            .data(data)
            .enter()
            .append("rect")
            .attr("class", "bar")
            .attr("x", 0)
            .attr("width", function (d) { return xScale(d.downloads); })
            .attr("y", function (d) { return yScale(d.label); })
            .attr("height", yScale.bandwidth())
            .append("title").text(function (d) { return d.downloads.toLocaleString() + " Downloads"; });

        //svg.append("foreignObject")
        //    .attr("x", 0)
        //    .attr("y", -10)
        //    .attr("width", width + "px")
        //    .attr("height", "2em")
        //    .attr("font-weight", "bold")
        //    .append("xhtml:body")
        //    .append("p")
        //    .attr("style", "text-align:center")
        //    .text("Downloads by Client (Last 6 weeks)");

        svg.append("g")
            .attr("class", "y axis long")
            .call(yAxis);


        $("#statistics-graph-title-id").text("Downloads by Client (Last 6 weeks)");
    }

    var GetChartData = function (rawData, filter) {
        var data = [];

        if (rawData.Table && rawData.Table.length > 0) {
            rawData.Table.forEach(function (dataPoint) {
                var item = {
                    label: dataPoint[0].Data,
                    downloads: window.nuget.parseNumber(dataPoint[1].Data)
                };

                if (!filter(item)) {
                    data[data.length] = item;
                }
            });
        }

        return data;
    }

    var GetShortNumberString = function (number) {
        if (number == 0) {
            return "0";
        }

        var abbreviation = ["", "k", "M", "B", "T", "q", "Q", "s", "S", "o", "n"];
        var numDiv = 0;
        while (number >= 1000) {
            number = number / 1000.0;
            numDiv++;
        }

        var rounded = Math.floor(number);
        if (rounded >= 100) {
            number = number.toPrecision(3);
        } else {
            number = number.toPrecision(2);
        }

        if (numDiv >= abbreviation.Length) {
            return number + "10^" + numDiv * 3;
        }
        return number + abbreviation[numDiv];
    }

    $(window).resize(function () {
        packageDisplayGraphs(window.graphData);
    });

    return packageDisplayGraphs;
}());
