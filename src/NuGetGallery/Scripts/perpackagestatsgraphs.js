
var packageDisplayGraphs = function () {

    if ($('#report-Version').length) {
        if (Modernizr.svg) {
            drawDownloadsByVersionBarChart();
        }
    }
    if ($('#report-ClientName').length) {
        if (Modernizr.svg) {
            drawDownloadsByClientNameBarChart();
        }
    }
    if ($('#report-Operation').length) {
        if (Modernizr.svg) {
            drawDownloadsByOperation();
        }
    }
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

var sortByTopDownloads = function (a, b) {
    if (a.downloads > b.downloads) return -1;
    if (a.downloads < b.downloads) return 1;
    return 0;
}

var drawDownloadsByVersionBarChart = function () {

    //  scrape data

    var data = [];

    d3.selectAll('#report-Version .statistics-data tbody tr').each(function () {
        var item = {
            version: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, ''),
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
        };
        data[data.length] = item;
    });

    //  limit the bar graph to the top 15 downloaded packages
    if (data.length > 15) {
        data.sort(sortByTopDownloads);
        data = data.slice(data.length - 15, data.length);
    }

    data.sort(sortByVersion);

    //  draw graph

    var reportGraphWidth = $('#report-Version').width();

    reportGraphWidth = Math.min(reportGraphWidth, 960);

    var margin = { top: 20, right: 30, bottom: 160, left: 80 },
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
        .orient('left');

    var svg = d3.select('#statistics-graph-id')
        .append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom);

    svg.append('title').text('Downloads By Version');
    svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by version.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

    xScale.domain(data.map(function (d) { return d.version; }));
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
        .text("Top Downloads by Package Version (Last 6 weeks)");

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
            .attr("x", function (d) { return xScale(d.version); })
            .attr("width", xScale.rangeBand())
            .attr("y", function (d) { return yScale(d.downloads); })
            .attr("height", function (d) { return height - yScale(d.downloads); });
}

var drawDownloadsByClientNameBarChart = function () {

    //  scrape data

    var data = [];

    d3.selectAll('#report-ClientName .statistics-data tbody tr').each(function () {
        var item = {
            clientName: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, ''),
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
        };
        
        //  filter out unknown
        if (item.clientName !== '(unknown)') {
            data[data.length] = item;
        }
    });

    data.reverse();

    //  draw graph

    var reportGraphWidth = $('#report-ClientName').width();
    reportGraphWidth = Math.min(reportGraphWidth, 960);

    var margin = { top: 20, right: 30, bottom: 100, left: 250 },
        width = reportGraphWidth - margin.left - margin.right,
        height = Math.max(550, data.length * 25) - margin.top - margin.bottom;

    var xScale = d3.scale.linear()
        .range([0, width - 50]);
    var yScale = d3.scale.ordinal()
        .rangeRoundBands([height, 20], .1);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom');
    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left');

    var svg = d3.select('#statistics-graph-id')
        .append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom);

    svg.append('title').text('Downloads By Client');
    svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by client.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');
    
    xScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);
    yScale.domain(data.map(function (d) { return d.clientName; }));

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
            .attr("y", function (d) { return yScale(d.clientName); })
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
                return yScale(d.clientName) + yScale.rangeBand() - 4;
            })
            .text(function (d) {
                return d.downloads.toLocaleString();
            });
}

var drawDownloadsByOperation = function () {

    //  scrape data

    var data = [];

    d3.selectAll('#report-Operation .statistics-data tbody tr').each(function () {
        var item = {
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, '')),
            operation: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, '')
        };

        //  filter out unknown so we just compare Install, Restore etc.
        if (item.operation !== 'unknown') {
            data[data.length] = item;
        }
    });

    //  reversing the data moves the high columns to the right (away from the y-axis label "Downloads")
    data.reverse();

    //  draw graph
    var reportGraphWidth = $('#report-Operation').width();

    reportGraphWidth = Math.min(reportGraphWidth, 960);

    var margin = { top: 20, right: 30, bottom: 100, left: 250 },
        width = reportGraphWidth - margin.left - margin.right,
        height = Math.max(550, data.length * 25) - margin.top - margin.bottom;

    var xScale = d3.scale.linear()
        .range([0, width-50]);
    var yScale = d3.scale.ordinal()
        .rangeRoundBands([height, 20], .1);

    var xAxis = d3.svg.axis()
        .scale(xScale)
        .orient('bottom');
    var yAxis = d3.svg.axis()
        .scale(yScale)
        .orient('left');

    var svg = d3.select('#statistics-graph-id')
        .append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom);

    svg.append('title').text('Downloads By Operation');
    svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by Operation.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

    xScale.domain([0, d3.max(data, function (d) { return d.downloads; })]);
    yScale.domain(data.map(function (d) { return d.operation; }));

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
        .text("Downloads by Operation (Last 6 weeks)");

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis);

    svg.selectAll(".bar")
        .data(data)
        .enter()
        .append("rect")
        .attr("class", "bar")
        .attr("x", 0)
        .attr("width", function(d) { return xScale(d.downloads); })
        .attr("y", function(d) { return yScale(d.operation); })
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
                return yScale(d.operation) + 25;
            })
            .text(function (d) {
                return d.downloads.toLocaleString();
            });
}
