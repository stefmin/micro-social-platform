(function () {
  "use strict";

  window.MSP = window.MSP || {};

  function getMarker(container, markerClass) {
    const markers = container.querySelectorAll("." + markerClass);
    return markers.length > 0 ? markers[markers.length - 1] : null;
  }

  function readStateFromMarker(marker) {
    if (!marker) {
      return { nextTicks: null, nextId: null, hasMore: false, pageSize: 15 };
    }

    const nextTicksRaw = marker.dataset.nextTicks;
    const nextIdRaw = marker.dataset.nextId;
    const hasMoreRaw = marker.dataset.hasMore;
    const pageSizeRaw = marker.dataset.pageSize;

    const nextTicks = nextTicksRaw ? Number(nextTicksRaw) : null;
    const nextId = nextIdRaw ? Number(nextIdRaw) : null;
    const hasMore = (hasMoreRaw || "").toLowerCase() === "true";
    const pageSize = pageSizeRaw ? Number(pageSizeRaw) : 15;

    return {
      nextTicks: Number.isFinite(nextTicks) ? nextTicks : null,
      nextId: Number.isFinite(nextId) ? nextId : null,
      hasMore,
      pageSize: Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 15,
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

  window.MSP.initInfiniteScroller = function (opts) {
    const container = document.getElementById(opts.containerId);
    const sentinel = document.getElementById(opts.sentinelId);
    const loadingEl = opts.loadingId
      ? document.getElementById(opts.loadingId)
      : null;
    const markerClass = opts.markerClass || "feed-page-marker";

    if (!container || !sentinel) return;

    let marker = getMarker(container, markerClass);
    let state = readStateFromMarker(marker);

    const endpointUrl = opts.endpointUrl;
    const pageSize = opts.pageSize || state.pageSize || 15;

    let isLoading = false;

    function isSentinelVisible() {
      const rect = sentinel.getBoundingClientRect();
      const viewportHeight =
        window.innerHeight || document.documentElement.clientHeight;

      return rect.top <= viewportHeight + 600;
    }

    function canLoadMore() {
      return state.hasMore && state.nextTicks && state.nextId;
    }

    async function loadNextPage() {
      if (isLoading) return;
      if (!canLoadMore()) return;

      const prevTicks = state.nextTicks;
      const prevId = state.nextId;

      isLoading = true;
      setLoading(loadingEl, true);

      // Remove all old markers so the container stays clean before appending new content.
      container.querySelectorAll("." + markerClass).forEach((m) => m.remove());

      try {
        const url = new URL(endpointUrl, window.location.origin);
        url.searchParams.set("pageSize", String(pageSize));
        url.searchParams.set("cursorTicks", String(state.nextTicks));
        url.searchParams.set("cursorId", String(state.nextId));

        const res = await fetch(url.toString(), {
          method: "GET",
          headers: { "X-Requested-With": "XMLHttpRequest" },
          credentials: "same-origin",
        });

        if (!res.ok) {
          // Stop trying if the server rejects.
          state.hasMore = false;
          observer.disconnect();
          return;
        }

        const html = await res.text();
        appendHtml(container, html);

        // Read the new marker added by the partial
        marker = getMarker(container, markerClass);
        state = readStateFromMarker(marker);

        // Safety check: if cursor didn't change, stop to prevent infinite loop
        if (state.nextTicks === prevTicks && state.nextId === prevId) {
          state.hasMore = false;
        }

        if (!state.hasMore) {
          observer.disconnect();
        }
      } catch (err) {
        // On transient errors, keep hasMore as-is so user can scroll again to retry.
        // You can add UI messaging here if you want.
      } finally {
        isLoading = false;
        setLoading(loadingEl, false);

        // If sentinel is still visible after loading and we can load more, continue
        if (canLoadMore() && isSentinelVisible()) {
          loadNextPage();
        }
      }
    }

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            loadNextPage();
            break;
          }
        }
      },
      {
        root: null,
        // Start loading a bit before the user hits the bottom
        rootMargin: "600px 0px",
        threshold: 0,
      }
    );

    // If the first page already has no more results, don't observe.
    if (state.hasMore) {
      observer.observe(sentinel);
    }
  };
})();
