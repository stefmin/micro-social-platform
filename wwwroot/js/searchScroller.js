(function () {
    "use strict";

    window.MSP = window.MSP || {};

    function debounce(fn, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    function getLastMarker(container) {
        const markers = container.querySelectorAll(".search-page-marker");
        return markers.length ? markers[markers.length - 1] : null;
    }

    function readStateFromMarker(marker, fallbackPageSize) {
        if (!marker) {
            return { nextCursor: null, hasMore: false, pageSize: fallbackPageSize };
        }

        const nextCursorRaw = marker.dataset.nextCursor || null;
        const hasMoreRaw = marker.dataset.hasMore;
        const pageSizeRaw = marker.dataset.pageSize;

        const hasMore = (hasMoreRaw || "").toLowerCase() === "true";
        const pageSize = pageSizeRaw ? Number(pageSizeRaw) : fallbackPageSize;

        return {
            nextCursor: nextCursorRaw && nextCursorRaw.trim().length ? nextCursorRaw.trim() : null,
            hasMore,
            pageSize: Number.isFinite(pageSize) && pageSize > 0 ? pageSize : fallbackPageSize
        };
    }

    function setLoading(loadingEl, isLoading) {
        if (!loadingEl) return;
        loadingEl.style.display = isLoading ? "block" : "none";
    }

    function setEmpty(emptyEl, show) {
        if (!emptyEl) return;
        emptyEl.style.display = show ? "block" : "none";
    }

    function appendHtml(container, html) {
        const template = document.createElement("template");
        template.innerHTML = html;
        container.appendChild(template.content);
    }

    function isScrollable(el) {
        if (!el) return false;
        const style = window.getComputedStyle(el);
        const overflowY = style.overflowY;
        const canScroll = (overflowY === "auto" || overflowY === "scroll");
        return canScroll && el.scrollHeight > el.clientHeight;
    }

    window.MSP.initSearchScroller = function (opts) {
        const input = document.getElementById(opts.inputId);
        const container = document.getElementById(opts.containerId);
        const sentinel = document.getElementById(opts.sentinelId);
        const loadingEl = opts.loadingId ? document.getElementById(opts.loadingId) : null;
        const emptyEl = opts.emptyId ? document.getElementById(opts.emptyId) : null;

        if (!input || !container || !sentinel) return;

        const endpointUrl = opts.endpointUrl;
        const pageSize = (opts.pageSize && Number(opts.pageSize) > 0) ? Number(opts.pageSize) : 15;

        let currentQuery = "";
        let state = { nextCursor: null, hasMore: false, pageSize };
        let isLoading = false;

        // If the container is its own scroll area, observe relative to it; otherwise use window
        const rootEl = isScrollable(container) ? container : null;

        const observer = new IntersectionObserver(
            (entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting) {
                        loadMore();
                        break;
                    }
                }
            },
            { root: rootEl, rootMargin: "300px 0px", threshold: 0 }
        );

        async function runSearch(query, cursor) {
            if (isLoading) return;
            isLoading = true;
            setLoading(loadingEl, true);

            const isNewSearch = !cursor;

            if (isNewSearch) {
                container.innerHTML = "";
                state = { nextCursor: null, hasMore: false, pageSize };
            } else {
                // Remove existing markers so there is only one at the end
                container.querySelectorAll(".search-page-marker").forEach(m => m.remove());
            }

            try {
                const url = new URL(endpointUrl, window.location.origin);
                url.searchParams.set("query", query);
                url.searchParams.set("pageSize", String(pageSize));
                if (cursor) url.searchParams.set("cursor", cursor);

                const res = await fetch(url.toString(), {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                });

                if (!res.ok) {
                    state.hasMore = false;
                    observer.unobserve(sentinel);
                    return;
                }

                const html = await res.text();
                appendHtml(container, html);

                const marker = getLastMarker(container);
                state = readStateFromMarker(marker, pageSize);

            } catch (err) {
                // transient error; allow retry
            } finally {
                isLoading = false;
                setLoading(loadingEl, false);
            }
        }

        async function search(query) {
            const q = (query || "").trim();
            currentQuery = q;

            if (!q) {
                container.innerHTML = "";
                state = { nextCursor: null, hasMore: false, pageSize };
                setEmpty(emptyEl, true);
                observer.unobserve(sentinel);
                return;
            }

            setEmpty(emptyEl, false);
            await runSearch(q, null);

            // Start/stop observing based on HasMore
            observer.unobserve(sentinel);
            if (state.hasMore) observer.observe(sentinel);
        }

        async function loadMore() {
            if (isLoading) return;
            if (!state.hasMore) return;
            if (!state.nextCursor) return;
            if (!currentQuery) return;

            // Force re-trigger behavior by unobserving while loading
            observer.unobserve(sentinel);

            await runSearch(currentQuery, state.nextCursor);

            if (state.hasMore) observer.observe(sentinel);
        }

        const debouncedSearch = debounce(search, 300);

        input.addEventListener("input", (e) => {
            debouncedSearch(e.target.value);
        });

        // initial empty state
        setEmpty(emptyEl, true);
    };
})();
