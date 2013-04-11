
var test = function () {

    $('.dimension-checkbox').click(function () {

        var current = [];

        $('.dimension-checkbox:checked').each(function (index) {
            current[current.length] = $(this).attr('id').replace('dimension-', '');
        });

        var query = '';
        for (var i = 0; i < current.length; i += 1) {
            query += 'groupby' + '=' + current[i] + '&';
        }

        if (query.length > 0) {
            query = '?' + query.slice(0, -1);
        }

        window.location.href = location.pathname + query;
    });
}