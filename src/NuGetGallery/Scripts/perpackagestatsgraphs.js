
var graphData;

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

    //}
    //if ($('#report-Version').length) {
    //    //if (Modernizr.svg) {
    //        drawDownloadsByVersionBarChart(data);
    //    //}
    //}
    //if ($('#report-ClientName').length) {
    //    //if (Modernizr.svg) {
    //        drawDownloadsByClientNameBarChart(data);
    //    //}
    //}
    //if ($('#report-Operation').length) {
    //    //if (Modernizr.svg) {
    //        drawDownloadsByOperation(data);
    //    //}
    //}
}

var SemVer = function (versionString) {
    var n = versionString.split('-');
    var v = n[0].split('.');

    this.preRelease = n[1];

    if (v[0] !== undefined) {
        this.major = Number(v[0]);
    }
    if (v[1] !== undefined) {
        this.minor = Number(v[1]);
    }
    if (v[2] !== undefined) {
        this.patch = Number(v[2]);
    }

    this.toString = function () {
        var s = '';
        if (this.major !== undefined && !isNaN(this.major)) {
            s += this.major.toString();
        }
        if (this.minor !== undefined && !isNaN(this.minor)) {
            s += '.';
            s += this.minor.toString();
        }
        if (this.patch !== undefined && !isNaN(this.patch)) {
            s += '.';
            s += this.patch.toString();
        }
        if (this.preRelease !== undefined) {
            s += '-';
            s += this.preRelease;
        }
        return s;
    }

    this.compareTo = function (other) {
        if (this.major === other.major && this.minor === other.minor && this.patch === other.patch && this.preRelease === other.preRelease) {
            return 0;
        }
        if (this.major < other.major) {
            return -1;
        }
        if (this.major === other.major) {
            if (this.minor < other.minor) {
                return -1;
            }
            if (this.minor === other.minor) {
                if (this.patch < other.patch) {
                    return -1;
                }
                if (this.patch === other.patch) {
                    if (this.preRelease === undefined && other.preRelease !== undefined) {
                        return 1;
                    }
                    if (this.preRelease !== undefined && other.preRelease === undefined) {
                        return -1;
                    }
                    if (this.preRelease < other.preRelease) {
                        return -1;
                    }
                }
            }
        }
        return 1;
    }
}

var sortByVersion = function (a, b) {
    return (new SemVer(a.version)).compareTo(new SemVer(b.version));
}

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

    //  limit the bar graph to the most recent 15 versions
    if (data.length > 15) {
        data = data.slice(data.length - 15, data.length);
    }

    //  draw graph
    var reportGraphWidth = $('#statistics-graph-id').width();

    reportGraphWidth = Math.min(reportGraphWidth, 1170);

    var margin = { top: 40, right: 30, bottom: 130, left: 45 },
        width = reportGraphWidth - margin.left - margin.right,
        height = 450 - margin.top - margin.bottom;

    var xScale = d3.scale.ordinal()
        .rangeRoundBands([0, width], .1);

    var yScale = d3.scale.linear()
        .range([height, 0]);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom');

    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left')
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
        .attr("class", "x axis")
        .attr("transform", "translate(0," + height + ")")
        .call(xAxis)
        .selectAll("text")
        .style("text-anchor", "end")
        //.attr("dx", "-.8em")
        .attr("dy", ".15em")
        .attr("transform", function (d) {
            return "rotate(-65),translate(-10,0)";
        });

    svg.append("text")
        .style("text-anchor", "middle")
        .attr("x", (width - margin.right) / 2.0)
        .attr("y", -10)
        .attr("font-weight", "bold")
        .text("Downloads for 15 Latest Package Versions (Last 6 weeks)");

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
            .attr("x", function (d) { return xScale(d.label); })
            .attr("width", xScale.rangeBand())
            .attr("y", function (d) { return yScale(d.downloads); })
        .attr("height", function (d) { return height - yScale(d.downloads); });
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

    //  draw graph

    var reportGraphWidth = $('#statistics-graph-id').width();
    reportGraphWidth = Math.min(reportGraphWidth, 1170);

    var margin = { top: 40, right: 30, bottom: 100, left: 250 },
        width = reportGraphWidth - margin.left - margin.right,
        height = Math.max(550, data.length * 25) - margin.top - margin.bottom;

    var xScale = d3.scale.linear()
        .range([0, width - 50]);
    var yScale = d3.scale.ordinal()
        .rangeRoundBands([height, 20], .1);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom')
        .tickFormat(function (d) {
            return GetShortNumberString(d);
        });

    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left');

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

    svg.append("text")
        .style("text-anchor", "middle")
        .attr("x", (width - margin.right) / 2.0)
        .attr("y", -10)
        .attr("font-weight", "bold")
        .text("Downloads by Client (Last 6 weeks)");

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis);

    svg.selectAll(".bar")
        .data(data)
        .enter()
        .append("rect")
        .attr("class", "bar")
        .attr("x", 0)
        .attr("width", function (d) { return xScale(d.downloads); })
        .attr("y", function (d) { return yScale(d.label); })
        .attr("height", yScale.rangeBand());

    svg.selectAll(".bartext")
        .data(data)
        .enter()
        .append("text")
        .attr("class", "bartext")
        .attr("text-anchor", "end")
        .attr("fill", "black")
        .attr("font-size", "11px")
        .attr("x", function (d, i) {
            return xScale(d.downloads) + 40;
        })
        .attr("y", function (d, i) {
            return yScale(d.label) + yScale.rangeBand() - 4;
        })
        .text(function (d) {
            return d.downloads.toLocaleString();
        });
}

var GetChartData = function (rawData, filter) {
    var data = [];

    if (rawData.Table && rawData.Table.length > 0) {
        rawData.Table.forEach(function (dataPoint) {
            var item = {
                label: dataPoint[0].Data,
                downloads: parseInt(dataPoint[1].Data.replace(",", ""))
            };

            if (!filter(item)) {
                data[data.length] = item;
            }
        });
    }

    return data;
}

var GetShortNumberString = function (number) {
    var abbreviation = ["", "k", "M", "B", "T", "q", "Q", "s", "S", "o", "n"];
    var numDiv = 0;
    while (number >= 1000) {
        number = Math.floor(number / 1000);
        numDiv++;
    }

    if (numDiv >= abbreviation.Length) {
        return number + "10^" + numDiv*3;
    }
    return number + abbreviation[numDiv];
}


$(window).resize(function () {
    packageDisplayGraphs(graphData);
});