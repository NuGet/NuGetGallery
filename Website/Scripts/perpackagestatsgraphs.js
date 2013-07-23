
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
    
    data.sort(sortByVersion);

    //  limit the bar graph to the most recent 15 versions
    if (data.length > 15) {
        data = data.slice(data.length - 15, data.length);
    }

    //  draw graph

    var reportTableWidth = $('#report-Version').width();

    var reportGraphWidth = 960 - reportTableWidth;

    reportGraphWidth = Math.min(reportGraphWidth, 590);

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
        data[data.length] = item;
    });

    data.reverse();

    //  draw graph

    var margin = { top: 20, right: 30, bottom: 220, left: 100 },
        width = 420 - margin.left - margin.right,
        height = 550 - margin.top - margin.bottom;

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

    svg.append('title').text('Downloads By Client');
    svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by client.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

    xScale.domain(data.map(function (d) { return d.clientName; }));
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
            .attr("x", function (d) { return xScale(d.clientName); })
            .attr("width", xScale.rangeBand())
            .attr("y", function (d) { return yScale(d.downloads); })
            .attr("height", function (d) { return height - yScale(d.downloads); });
}

var drawDownloadsByOperation = function () {

    //  scrape data

    var data = [];

    d3.selectAll('#report-Operation .statistics-data tbody tr').each(function () {
        var item = {
            operation: d3.select(this).select(':nth-child(1)').text().replace(/(^\s*)|(\s*$)/g, ''),
            downloads: +(d3.select(this).select(':nth-child(2)').text().replace(/[^0-9]+/g, ''))
        };

        //  filter out unknown so we just compare Install, Restore etc.
        if (item.operation !== 'unknown') {
            data[data.length] = item;
        }
    });

    //  reversing the data moves the high columns to the right (away from the y-axis label "Downloads")
    data.reverse();

    //  draw graph

    var margin = { top: 20, right: 30, bottom: 220, left: 100 },
        width = 420 - margin.left - margin.right,
        height = 550 - margin.top - margin.bottom;

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

    svg.append('title').text('Downloads By Operation');
    svg.append('desc').text('This is a graph showing the number of downloads of this Package broken out by Operation.');

    svg = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

    xScale.domain(data.map(function (d) { return d.operation; }));
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
            .attr("x", function (d) { return xScale(d.operation); })
            .attr("width", xScale.rangeBand())
            .attr("y", function (d) { return yScale(d.downloads); })
            .attr("height", function (d) { return height - yScale(d.downloads); });
}
