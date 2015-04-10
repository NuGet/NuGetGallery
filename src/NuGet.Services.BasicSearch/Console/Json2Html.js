var json2html = function (obj) {

    var urlregexp = /^http:|^https:/;

    var is_array = function (value) {
        return Object.prototype.toString.apply(value) === '[object Array]';
    }

    var displayValue = function (value) {
        if (value === null) {
            return 'null';
        } else {
            if (is_array(value)) {
                return displayArray(value);
            } else if (typeof value === 'object') {
                return displayObject(value);
            } else {
                switch (typeof value) {
                    case 'string':
                        if (urlregexp.test(value)) {
                            return '<a class="json2html" href="' + value + '">"' + value + '"</a>';
                        }
                        else {
                            return '<span class="json-prop-value-string">"' + value + '"</span>';
                        }
                    case 'number': return value.toString();
                    case 'boolean': return value.toString();
                    default: throw 'unrecognized value type';
                }
            }
        }
    }

    var displayObject = function (obj) {
        var html = '{';
        html += '<ul class="json-ul">';
        for (var prop in obj) {
            html += '<li class="json-li"><span class="json-prop-name">"' + prop + '"</span> : ';
            html += displayValue(obj[prop]);
            html += ',</li>';
        }
        html = html.slice(0, html.length - 6);
        html += '</li></ul>';
        html += '}';
        return html;
    }

    var displayArray = function (array) {
        if (array.length === 0) {
            return '[]';
        }
        var html = '[';
        html += '<ul class="json-ul">';
        for (var i = 0; i < array.length; i += 1) {
            html += '<li class="json-li">';
            html += displayValue(array[i]);
            html += ',</li>';
        }
        html = html.slice(0, html.length - 1);
        html += '</li></ul>';
        html += ']';
        return html;
    }

    return displayValue(obj);
}