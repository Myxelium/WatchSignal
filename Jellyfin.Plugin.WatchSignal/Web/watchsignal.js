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

    function formatRelease(data, now, locale) {
        if (!data || !data.found) {
            return null;
        }

        var when = formatWhen(data, now || new Date(), locale);
        if (!when) {
            return null;
        }

        var network = data.network ? " on " + data.network : "";
        if (data.kind === "season" && data.seasonNumber) {
            return "Season " + data.seasonNumber + " premieres " + when + network + ".";
        }

        var label = data.kind === "special" ? "Next special" : "Next episode";
        var bits = [];
        if (data.kind !== "special" && data.seasonNumber != null && data.episodeNumber != null) {
            bits.push("S" + pad(data.seasonNumber) + "E" + pad(data.episodeNumber));
        }

        if (data.episodeTitle) {
            bits.push("\u201c" + data.episodeTitle + "\u201d");
        }

        var lead = bits.length ? label + " " + bits.join(" ") : label;
        return lead + " airs " + when + network + ".";
    }

    function boot() {
        var shown = null;
        var requestSerial = 0;
        var timer = 0;

        function ensureStyle() {
            if (document.getElementById("watchsignal-style")) {
                return;
            }

            var style = document.createElement("style");
            style.id = "watchsignal-style";
            style.textContent = ".watchsignal-release{margin:0.35em 0 0.8em;font-size:1.02em;font-weight:600;line-height:1.4;}";
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
            var anchor = page.querySelector(".itemMiscInfo-primary") || page.querySelector(".itemName");
            var parent = anchor ? anchor.parentNode : (page.querySelector(".detailPagePrimaryContent") || page);
            if (!parent) {
                return;
            }

            if (anchor) {
                if (element.previousSibling !== anchor) {
                    parent.insertBefore(element, anchor.nextSibling);
                }
            } else if (element.parentNode !== parent) {
                parent.insertBefore(element, parent.firstChild);
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
            if (!id || !page) {
                shown = null;
                paint();
                return;
            }

            if (shown && shown.id !== id) {
                shown = null;
                paint();
            }

            var api = window.ApiClient;
            var token = tokenOf(api);
            if (!api || typeof api.getUrl !== "function" || !token) {
                window.setTimeout(refresh, 400);
                return;
            }

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

                var text = formatRelease(data, new Date());
                shown = text ? { id: id, text: text, source: data && data.source } : null;
                paint();
            }).catch(function () {
                if (serial !== requestSerial) {
                    return;
                }
                shown = null;
                paint();
            });
        }

        function schedule() {
            window.clearTimeout(timer);
            timer = window.setTimeout(refresh, 60);
        }

        document.addEventListener("viewshow", schedule);
        window.addEventListener("hashchange", schedule);
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", schedule);
        } else {
            schedule();
        }

        var paintTimer = 0;
        var observer = new MutationObserver(function () {
            if (!detailsItemId()) {
                return;
            }

            window.clearTimeout(paintTimer);
            paintTimer = window.setTimeout(paint, 40);
        });
        var observeRoot = document.body || document.documentElement;
        if (observeRoot) {
            observer.observe(observeRoot, { childList: true, subtree: true });
        }
    }

    root.WatchSignalFormat = { formatRelease: formatRelease };
    if (typeof document !== "undefined") {
        boot();
    }
})(typeof globalThis !== "undefined" ? globalThis : this);
