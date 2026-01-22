(function () {
    "use strict";

    window.MSP = window.MSP || {};

    function getLastMarker(container) {
        const markers = container.querySelectorAll(".groups-page-marker");
        return markers.length ? markers[markers.length - 1] : null;
    }

    function readStateFromMarker(marker, fallbackPageSize) {
        if (!marker) {
            return { nextTicks: null, nextId: null, hasMore: false, pageSize: fallbackPageSize, search: "" };
        }

        const nextTicksRaw = marker.dataset.nextTicks;
        const nextIdRaw = marker.dataset.nextId;
        const hasMoreRaw = marker.dataset.hasMore;
        const pageSizeRaw = marker.dataset.pageSize;
        const searchRaw = marker.dataset.search;

        const nextTicks = nextTicksRaw && nextTicksRaw.trim().length ? nextTicksRaw.trim() : null;
        const nextId = nextIdRaw && nextIdRaw.trim().length ? Number(nextIdRaw) : null;
        const hasMore = (hasMoreRaw || "").toLowerCase() === "true";
        const pageSize = pageSizeRaw ? Number(pageSizeRaw) : fallbackPageSize;

        return {
            nextTicks,
            nextId: Number.isFinite(nextId) ? nextId : null,
            hasMore,
            pageSize: Number.isFinite(pageSize) && pageSize > 0 ? pageSize : fallbackPageSize,
            search: searchRaw || ""
        };
    }

    function setLoading(loadingEl, isLoading) {
        if (!loadingEl) return;
        loadingEl.style.display = isLoading ? "block" : "none";
    }

    function appendHtml(container, html) {
        const template = document.createElement("template");
        template.innerHTML = html;
        container.appendChild(template.content);
    }

    window.MSP.initGroupsScroller = function (opts) {
        const form = opts.formId ? document.getElementById(opts.formId) : null;
        const input = opts.inputId ? document.getElementById(opts.inputId) : null;

        const container = document.getElementById(opts.containerId);
        const sentinel = document.getElementById(opts.sentinelId);
        const loadingEl = opts.loadingId ? document.getElementById(opts.loadingId) : null;

        if (!container || !sentinel) return;

        const endpointUrl = opts.endpointUrl;
        const fallbackPageSize = (opts.pageSize && Number(opts.pageSize) > 0) ? Number(opts.pageSize) : 15;

        let marker = getLastMarker(container);
        let state = readStateFromMarker(marker, fallbackPageSize);

        let isLoading = false;

        const observer = new IntersectionObserver(
            (entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting) {
                        loadNextPage();
                        break;
                    }
                }
            },
            { root: null, rootMargin: "400px 0px", threshold: 0 }
        );

        async function fetchFirstPage(searchTerm) {
            if (isLoading) return;

            isLoading = true;
            setLoading(loadingEl, true);

            observer.unobserve(sentinel);
            container.innerHTML = "";

            try {
                const url = new URL(endpointUrl, window.location.origin);
                url.searchParams.set("pageSize", String(fallbackPageSize));
                if (searchTerm && searchTerm.trim().length) {
                    url.searchParams.set("search", searchTerm.trim());
                }

                const res = await fetch(url.toString(), {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                });

                if (!res.ok) {
                    state = { nextTicks: null, nextId: null, hasMore: false, pageSize: fallbackPageSize, search: searchTerm || "" };
                    return;
                }

                const html = await res.text();
                appendHtml(container, html);

                marker = getLastMarker(container);
                state = readStateFromMarker(marker, fallbackPageSize);

            } finally {
                isLoading = false;
                setLoading(loadingEl, false);

                if (state.hasMore) observer.observe(sentinel);
            }
        }

        async function loadNextPage() {
            if (isLoading) return;
            if (!state.hasMore) return;
            if (!state.nextTicks || state.nextId == null) return;

            isLoading = true;
            setLoading(loadingEl, true);

            // Force retrigger
            observer.unobserve(sentinel);

            // Remove markers so only one is at end
            container.querySelectorAll(".groups-page-marker").forEach(m => m.remove());

            try {
                const url = new URL(endpointUrl, window.location.origin);
                url.searchParams.set("pageSize", String(state.pageSize || fallbackPageSize));
                url.searchParams.set("cursorTicks", state.nextTicks); // keep as STRING
                url.searchParams.set("cursorId", String(state.nextId));
                if (state.search) url.searchParams.set("search", state.search);

                const res = await fetch(url.toString(), {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                });

                if (!res.ok) {
                    state.hasMore = false;
                    return;
                }

                const html = await res.text();
                appendHtml(container, html);

                marker = getLastMarker(container);
                state = readStateFromMarker(marker, fallbackPageSize);

            } finally {
                isLoading = false;
                setLoading(loadingEl, false);

                if (state.hasMore) observer.observe(sentinel);
            }
        }

        // Initial observer based on server-rendered marker
        if (state.hasMore) observer.observe(sentinel);

        // AJAX search submit (makes it behave like Search page)
        if (form) {
            form.addEventListener("submit", function (e) {
                e.preventDefault();
                const term = input ? input.value : "";
                fetchFirstPage(term);
            });
        }
    };
})();
