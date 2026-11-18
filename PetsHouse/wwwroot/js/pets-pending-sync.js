// Khi Admin hoặc Nhân viên xử lý một lịch, các tài khoản còn lại
// sẽ tự cập nhật hàng chờ sau tối đa khoảng 12 giây nếu tab đang mở.
(() => {
    "use strict";

    const queue = document.querySelector("[data-pets-pending]");
    if (!queue) return;

    const url = queue.dataset.refreshUrl;
    if (!url) return;

    const firstIds = queue.dataset.ids || "";
    const firstCount = Number(queue.dataset.count || "0");
    let checking = false;

    async function checkQueue() {
        if (document.hidden || checking) return;

        // Không làm gián đoạn người đang chọn bác sĩ hoặc nhập bộ lọc.
        const activeTag = document.activeElement?.tagName;
        if (["INPUT", "SELECT", "TEXTAREA"].includes(activeTag)) return;

        checking = true;

        try {
            const response = await fetch(url, {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                headers: { "Accept": "application/json" }
            });

            if (!response.ok) return;

            const latest = await response.json();
            const newIds = Array.isArray(latest.ids) ? latest.ids.join(",") : "";
            const newCount = Number(latest.count || 0);

            if (newCount !== firstCount || newIds !== firstIds) {
                window.location.reload();
            }
        } catch {
            // Lỗi mạng tạm thời không được cắt ngang thao tác của người dùng.
        } finally {
            checking = false;
        }
    }

    window.setInterval(checkQueue, 12000);
    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) void checkQueue();
    });
})();
