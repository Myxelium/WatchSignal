(function (root) {
    "use strict";

    function pad(value) {
        return String(value).padStart(2, "0");
    }

    function formatClock(date) {
        var hours = date.getHours();
        var suffix = hours >= 12 ? "PM" : "AM";
        hours = hours % 12;
        if (hours === 0) {
            hours = 12;
        }

        return hours + ":" + String(date.getMinutes()).padStart(2, "0") + " " + suffix;
    }

    function formatLongDate(date, locale) {
        return date.toLocaleDateString(locale, {
            weekday: "long",
            month: "long",
            day: "numeric",
            year: "numeric"
        });
    }

    function formatWhen(data, now, locale) {
        if (data.airAt) {
            var at = new Date(data.airAt);
            if (Number.isNaN(at.getTime())) {
                return null;
            }

            var time = formatClock(at);
            var startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
            var startThat = new Date(at.getFullYear(), at.getMonth(), at.getDate());
            var dayDiff = Math.round((startThat.getTime() - startToday.getTime()) / 86400000);
            if (dayDiff === 0) {
                return "today at " + time;
            }

            if (dayDiff === 1) {
                return "tomorrow at " + time;
            }

            return formatLongDate(at, locale) + " at " + time;
        }

        if (!data.airDate) {
            return null;
        }

        var parts = String(data.airDate).split("-");
        if (parts.length !== 3) {
            return null;
        }

        var year = Number(parts[0]);
        var month = Number(parts[1]);
        var day = Number(parts[2]);
        if (!year || !month || !day) {
            return null;
        }

        return formatLongDate(new Date(year, month - 1, day), locale);
    }

    function valueOf(data, camel, pascal) {
        if (!data) {
            return undefined;
        }

        if (data[camel] != null && data[camel] !== "") {
            return data[camel];
        }

        return data[pascal];
    }

    function textValue(value) {
        return value == null ? "" : String(value);
    }

    function releaseFields(data, now, locale) {
        var when = formatWhen(data, now || new Date(), locale) || "";
        var dateOnly = "";
        var time = "";
        if (data.airAt || data.AirAt) {
            var at = new Date(data.airAt || data.AirAt);
            if (!Number.isNaN(at.getTime())) {
                dateOnly = formatLongDate(at, locale);
                time = formatClock(at);
            }
        } else if (when) {
            dateOnly = when;
        }

        var season = valueOf(data, "seasonNumber", "SeasonNumber");
        var episode = valueOf(data, "episodeNumber", "EpisodeNumber");
        var kind = textValue(valueOf(data, "kind", "Kind"));
        var code = "";
        if (season != null && episode != null) {
            code = "S" + pad(season) + "E" + pad(episode);
        }

        return {
            "release date": when,
            date: dateOnly,
            time: time,
            season: season == null ? "" : String(season),
            episode: episode == null ? "" : String(episode),
            code: code,
            title: textValue(valueOf(data, "episodeTitle", "EpisodeTitle")),
            network: textValue(valueOf(data, "network", "Network")),
            kind: kind,
            source: textValue(valueOf(data, "source", "Source"))
        };
    }

    function applyTemplate(template, fields) {
        var output = "";
        var stack = [true];
        var index = 0;
        while (index < template.length) {
            var open = template.indexOf("{", index);
            if (open < 0) {
                if (stack[stack.length - 1]) {
                    output += template.slice(index);
                }
                break;
            }

            if (stack[stack.length - 1]) {
                output += template.slice(index, open);
            }

            var close = template.indexOf("}", open + 1);
            if (close < 0) {
                if (stack[stack.length - 1]) {
                    output += template.slice(open);
                }
                break;
            }

            var tag = template.slice(open + 1, close).trim().toLowerCase();
            index = close + 1;
            if (tag.charAt(0) === "/") {
                if (stack.length > 1) {
                    stack.pop();
                }
                continue;
            }

            if (tag.charAt(0) === "?") {
                var expression = tag.slice(1).trim();
                var equals = expression.indexOf("=");
                var key = (equals === -1 ? expression : expression.slice(0, equals)).trim();
                var expected = equals === -1 ? null : expression.slice(equals + 1).trim();
                var field = fields[key] || "";
                var matches = expected === null ? field.length > 0 : field === expected;
                stack.push(stack[stack.length - 1] && matches);
                continue;
            }

            if (stack[stack.length - 1]) {
                output += fields[tag] || "";
            }
        }

        return output.replace(/[ \t]{2,}/g, " ").replace(/\s+([.,])/g, "$1").trim();
    }

    function formatRelease(data, now, locale, template) {
        if (!data || valueOf(data, "found", "Found") === false || valueOf(data, "found", "Found") == null) {
            return null;
        }

        var normalized = {
            found: true,
            kind: valueOf(data, "kind", "Kind"),
            seasonNumber: valueOf(data, "seasonNumber", "SeasonNumber"),
            episodeNumber: valueOf(data, "episodeNumber", "EpisodeNumber"),
            episodeTitle: valueOf(data, "episodeTitle", "EpisodeTitle"),
            airAt: valueOf(data, "airAt", "AirAt"),
            airDate: valueOf(data, "airDate", "AirDate"),
            network: valueOf(data, "network", "Network")
        };
        var fields = releaseFields(normalized, now, locale);
        if (!fields["release date"]) {
            return null;
        }

        if (template && String(template).trim()) {
            var custom = applyTemplate(String(template), fields);
            return custom || null;
        }

        var network = fields.network ? " on " + fields.network : "";
        if (normalized.kind === "season" && normalized.seasonNumber) {
            return "Season " + normalized.seasonNumber + " premieres " + fields["release date"] + network + ".";
        }

        var label = normalized.kind === "special" ? "Next special" : "Next episode";
        var bits = [];
        if (normalized.kind !== "special" && fields.code) {
            bits.push(fields.code);
        }

        if (fields.title) {
            bits.push("\u201c" + fields.title + "\u201d");
        }

        var lead = bits.length ? label + " " + bits.join(" ") : label;
        return lead + " airs " + fields["release date"] + network + ".";
    }

        function boot() {
        var shown = null;
        var requestSerial = 0;
        var timer = 0;
        var fetchedId = null;
        var pendingId = null;

        function ensureStyle() {
            if (document.getElementById("watchsignal-style")) {
                return;
            }

            var style = document.createElement("style");
            style.id = "watchsignal-style";
            style.textContent = ".watchsignal-release{display:block;margin:0 0 0.85em;font-size:1.05em;font-weight:600;line-height:1.4;color:inherit;}";
            document.head.appendChild(style);
        }

        function detailsItemId() {
            var raw = (window.location.hash || "").replace(/^#!?/, "");
            var queryIndex = raw.indexOf("?");
            var path = queryIndex === -1 ? raw : raw.slice(0, queryIndex);
            if (path !== "/details" && path !== "details") {
                return null;
            }

            return new URLSearchParams(queryIndex === -1 ? "" : raw.slice(queryIndex + 1)).get("id");
        }

        function findPage() {
            return document.getElementById("itemDetailPage") || document.querySelector(".itemDetailPage");
        }

        function place(page, element) {
            var content = page.querySelector(".detailSectionContent");
            var overview = content ? content.querySelector(".overview") : page.querySelector(".overview");
            if (overview && overview.parentNode) {
                if (element.parentNode !== overview.parentNode || element.nextSibling !== overview) {
                    overview.parentNode.insertBefore(element, overview);
                }
                return;
            }

            if (content) {
                if (element.parentNode !== content) {
                    content.insertBefore(element, content.firstChild);
                }
                return;
            }

            var ribbon = page.querySelector(".detailRibbon");
            if (ribbon && ribbon.parentNode) {
                if (element.parentNode !== ribbon.parentNode || element.previousSibling !== ribbon) {
                    ribbon.parentNode.insertBefore(element, ribbon.nextSibling);
                }
                return;
            }

            if (element.parentNode !== page) {
                page.insertBefore(element, page.firstChild);
            }
        }

        function paint() {
            var id = detailsItemId();
            var page = findPage();
            if (!id || !page || !shown || shown.id !== id || !shown.text) {
                var stale = page && page.querySelector(".watchsignal-release");
                if (stale && (!id || stale.dataset.itemId !== id)) {
                    stale.remove();
                }
                return;
            }

            ensureStyle();
            var element = page.querySelector(".watchsignal-release") || document.createElement("div");
            element.className = "watchsignal-release";
            element.dataset.itemId = id;
            if (element.textContent !== shown.text) {
                element.textContent = shown.text;
            }
            element.title = shown.source ? "From " + shown.source : "";
            place(page, element);
        }

        function tokenOf(api) {
            if (!api) {
                return null;
            }

            return typeof api.accessToken === "function" ? api.accessToken() : api.accessToken || null;
        }

        function refresh() {
            var id = detailsItemId();
            var page = findPage();
            if (!id) {
                fetchedId = null;
                pendingId = null;
                shown = null;
                paint();
                return;
            }

            if (!page || fetchedId === id || pendingId === id) {
                if (page) {
                    paint();
                }
                return;
            }

            var api = window.ApiClient;
            var token = tokenOf(api);
            if (!api || typeof api.getUrl !== "function" || !token) {
                window.setTimeout(refresh, 400);
                return;
            }

            pendingId = id;
            var serial = ++requestSerial;
            fetch(api.getUrl("WatchSignal/Series/" + encodeURIComponent(id) + "/Upcoming"), {
                headers: { "X-Emby-Token": token },
                credentials: "same-origin"
            }).then(function (response) {
                if (!response.ok) {
                    return null;
                }
                return response.json();
            }).then(function (data) {
                if (serial !== requestSerial || detailsItemId() !== id) {
                    return;
                }

                pendingId = null;
                fetchedId = id;
                var template = data && (data.lineFormat || data.LineFormat);
                var text = formatRelease(data, new Date(), undefined, template);
                var source = data && (data.source || data.Source);
                shown = { id: id, text: text || "", source: source || "" };
                paint();
            }).catch(function () {
                if (serial !== requestSerial) {
                    return;
                }
                pendingId = null;
                fetchedId = id;
                shown = { id: id, text: "", source: "" };
                paint();
            });
        }

        function schedule(force) {
            if (force) {
                fetchedId = null;
                pendingId = null;
            }

            window.clearTimeout(timer);
            timer = window.setTimeout(refresh, 60);
        }

        document.addEventListener("viewshow", function () {
            schedule(true);
        });
        window.addEventListener("hashchange", function () {
            schedule(true);
        });
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", function () {
                schedule(false);
            });
        } else {
            schedule(false);
        }

        var paintTimer = 0;
        var observer = new MutationObserver(function () {
            var id = detailsItemId();
            var page = id && findPage();
            if (!page) {
                return;
            }

            window.clearTimeout(paintTimer);
            paintTimer = window.setTimeout(function () {
                if (fetchedId !== id && pendingId !== id) {
                    schedule(false);
                    return;
                }

                var overview = page.querySelector(".detailSectionContent .overview") || page.querySelector(".overview");
                var element = page.querySelector(".watchsignal-release");
                if (!element || (overview && element.nextSibling !== overview)) {
                    paint();
                }
            }, 40);
        });
        var observeRoot = document.body || document.documentElement;
        if (observeRoot) {
            observer.observe(observeRoot, { childList: true, subtree: true });
        }
    }

    root.WatchSignalFormat = { formatRelease: formatRelease, applyTemplate: applyTemplate };
    if (typeof document !== "undefined") {
        boot();
    }
})(typeof globalThis !== "undefined" ? globalThis : this);
