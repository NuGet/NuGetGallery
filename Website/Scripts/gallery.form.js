(function (d) { d.each(["backgroundColor", "borderBottomColor", "borderLeftColor", "borderRightColor", "borderTopColor", "color", "outlineColor"], function (f, e) { d.fx.step[e] = function (g) { if (!g.colorInit) { g.start = c(g.elem, e); g.end = b(g.end); g.colorInit = true } g.elem.style[e] = "rgb(" + [Math.max(Math.min(parseInt((g.pos * (g.end[0] - g.start[0])) + g.start[0]), 255), 0), Math.max(Math.min(parseInt((g.pos * (g.end[1] - g.start[1])) + g.start[1]), 255), 0), Math.max(Math.min(parseInt((g.pos * (g.end[2] - g.start[2])) + g.start[2]), 255), 0)].join(",") + ")" } }); function b(f) { var e; if (f && f.constructor == Array && f.length == 3) { return f } if (e = /rgb\(\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*\)/.exec(f)) { return [parseInt(e[1]), parseInt(e[2]), parseInt(e[3])] } if (e = /rgb\(\s*([0-9]+(?:\.[0-9]+)?)\%\s*,\s*([0-9]+(?:\.[0-9]+)?)\%\s*,\s*([0-9]+(?:\.[0-9]+)?)\%\s*\)/.exec(f)) { return [parseFloat(e[1]) * 2.55, parseFloat(e[2]) * 2.55, parseFloat(e[3]) * 2.55] } if (e = /#([a-fA-F0-9]{2})([a-fA-F0-9]{2})([a-fA-F0-9]{2})/.exec(f)) { return [parseInt(e[1], 16), parseInt(e[2], 16), parseInt(e[3], 16)] } if (e = /#([a-fA-F0-9])([a-fA-F0-9])([a-fA-F0-9])/.exec(f)) { return [parseInt(e[1] + e[1], 16), parseInt(e[2] + e[2], 16), parseInt(e[3] + e[3], 16)] } if (e = /rgba\(0, 0, 0, 0\)/.exec(f)) { return a.transparent } return a[d.trim(f).toLowerCase()] } function c(g, e) { var f; do { f = d.curCSS(g, e); if (f != "" && f != "transparent" || d.nodeName(g, "body")) { break } e = "backgroundColor" } while (g = g.parentNode); return b(f) } var a = { aqua: [0, 255, 255], azure: [240, 255, 255], beige: [245, 245, 220], black: [0, 0, 0], blue: [0, 0, 255], brown: [165, 42, 42], cyan: [0, 255, 255], darkblue: [0, 0, 139], darkcyan: [0, 139, 139], darkgrey: [169, 169, 169], darkgreen: [0, 100, 0], darkkhaki: [189, 183, 107], darkmagenta: [139, 0, 139], darkolivegreen: [85, 107, 47], darkorange: [255, 140, 0], darkorchid: [153, 50, 204], darkred: [139, 0, 0], darksalmon: [233, 150, 122], darkviolet: [148, 0, 211], fuchsia: [255, 0, 255], gold: [255, 215, 0], green: [0, 128, 0], indigo: [75, 0, 130], khaki: [240, 230, 140], lightblue: [173, 216, 230], lightcyan: [224, 255, 255], lightgreen: [144, 238, 144], lightgrey: [211, 211, 211], lightpink: [255, 182, 193], lightyellow: [255, 255, 224], lime: [0, 255, 0], magenta: [255, 0, 255], maroon: [128, 0, 0], navy: [0, 0, 128], olive: [128, 128, 0], orange: [255, 165, 0], pink: [255, 192, 203], purple: [128, 0, 128], violet: [128, 0, 128], red: [255, 0, 0], silver: [192, 192, 192], white: [255, 255, 255], yellow: [255, 255, 0], transparent: [255, 255, 255]} })(jQuery);

/**
* hoverIntent r5 // 2007.03.27 // jQuery 1.1.2+
* <http://cherne.net/brian/resources/jquery.hoverIntent.html>
*
* @param  f  onMouseOver function || An object with configuration options
* @param  g  onMouseOut function  || Nothing (use configuration options object)
* @author    Brian Cherne <brian@cherne.net>
*/
(function ($) { $.fn.hoverIntent = function (f, g) { var cfg = { sensitivity: 7, interval: 100, timeout: 0 }; cfg = $.extend(cfg, g ? { over: f, out: g} : f); var cX, cY, pX, pY; var track = function (ev) { cX = ev.pageX; cY = ev.pageY; }; var compare = function (ev, ob) { ob.hoverIntent_t = clearTimeout(ob.hoverIntent_t); if ((Math.abs(pX - cX) + Math.abs(pY - cY)) < cfg.sensitivity) { $(ob).unbind("mousemove", track); ob.hoverIntent_s = 1; return cfg.over.apply(ob, [ev]); } else { pX = cX; pY = cY; ob.hoverIntent_t = setTimeout(function () { compare(ev, ob); }, cfg.interval); } }; var delay = function (ev, ob) { ob.hoverIntent_t = clearTimeout(ob.hoverIntent_t); ob.hoverIntent_s = 0; return cfg.out.apply(ob, [ev]); }; var handleHover = function (e) { var p = (e.type == "mouseover" ? e.fromElement : e.toElement) || e.relatedTarget; while (p && p != this) { try { p = p.parentNode; } catch (e) { p = this; } } if (p == this) { return false; } var ev = jQuery.extend({}, e); var ob = this; if (ob.hoverIntent_t) { ob.hoverIntent_t = clearTimeout(ob.hoverIntent_t); } if (e.type == "mouseover") { pX = ev.pageX; pY = ev.pageY; $(ob).bind("mousemove", track); if (ob.hoverIntent_s != 1) { ob.hoverIntent_t = setTimeout(function () { compare(ev, ob); }, cfg.interval); } } else { $(ob).unbind("mousemove", track); if (ob.hoverIntent_s == 1) { ob.hoverIntent_t = setTimeout(function () { delay(ev, ob); }, cfg.timeout); } } }; return this.mouseover(handleHover).mouseout(handleHover); }; })(jQuery);

function compose(a, b) {
    return function () { a(); b(); }
}

function setupValidation() {
    var highlighter = function () {
        $("#form div.invalid").removeClass();
        $("#form .input-validation-error").closest("div").addClass("invalid");
    };
    $("#form").submit(highlighter).find("input,textarea").blur(highlighter);
}

$(function () {
    setupValidation();

    var $formElements = $("div").children("input,select,textarea");
    $formElements.focus(function () {
        var $parent = $(this).parent().addClass("selected");
        $parent.stop().animate({ backgroundColor: formFieldFocusColor }, 200);
    });
    $formElements.blur(function () {
        var $parent = $(this).parent().removeClass("selected");
        $parent.stop().animate({ backgroundColor: 'transparent' }, 200);
    });

    var formWidth = $("#form").width();
    $("#form>div").each(function () {
        var $input = $(this).find("input,select,textarea");
        if (!$input.length) return;
        var inputWidth = $input.width();
        var asideWidth = formWidth - inputWidth - 100;
        $(this).find("aside").width(asideWidth);
    });

    try {
        $("input:focus").focus();
    }
    catch (ex) {
        jQuery.extend(jQuery.expr[':'], { focus: function (element) { return element == document.activeElement; } });
        $("input:focus").focus();
    }
});
