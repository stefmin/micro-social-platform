(function () {
    "use strict";

    async function loadMoreComments(postId) {
        const container = document.getElementById(`comments-container-${postId}`);
        if (!container) return;

        const marker = container.querySelector(".comments-page-marker");
        if (!marker) return;

        const hasMore = (marker.dataset.hasMore || "").toLowerCase() === "true";
        const nextTicks = marker.dataset.nextTicks;
        const nextId = marker.dataset.nextId;
        const pageSize = marker.dataset.pageSize || "10";

        if (!hasMore || !nextTicks || !nextId) {
            const btn = document.querySelector(`button.load-more-comments[data-post-id="${postId}"]`);
            if (btn) btn.style.display = "none";
            return;
        }

        // Remove the old marker so there is always one at the end
        marker.remove();

        const url = new URL("/Post/CommentsPage", window.location.origin);
        url.searchParams.set("postId", String(postId));
        url.searchParams.set("cursorTicks", String(nextTicks));
        url.searchParams.set("cursorId", String(nextId));
        url.searchParams.set("pageSize", String(pageSize));

        const res = await fetch(url.toString(), {
            method: "GET",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        });

        if (!res.ok) return;

        const html = await res.text();
        const template = document.createElement("template");
        template.innerHTML = html;
        container.appendChild(template.content);

        // Hide button if no more
        const newMarker = container.querySelector(".comments-page-marker");
        const newHasMore = newMarker && (newMarker.dataset.hasMore || "").toLowerCase() === "true";
        const btn = document.querySelector(`button.load-more-comments[data-post-id="${postId}"]`);
        if (btn && !newHasMore) btn.style.display = "none";
    }

    // Event delegation so it works for newly appended posts in infinite feed
    document.addEventListener("click", (e) => {
        const btn = e.target.closest("button.load-more-comments");
        if (!btn) return;

        const postId = btn.dataset.postId;
        if (!postId) return;

        loadMoreComments(postId);
    });
})();
