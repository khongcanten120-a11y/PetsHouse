(function () {
    "use strict";

    const root = document.documentElement;
    const themeButton = document.getElementById("themeBtn");
    const savedTheme = window.localStorage.getItem("petshouse-theme");

    if (savedTheme === "dark") {
        root.dataset.theme = "dark";
    }

    if (themeButton) {
        themeButton.addEventListener("click", function () {
            root.dataset.theme = root.dataset.theme === "dark" ? "light" : "dark";
            window.localStorage.setItem("petshouse-theme", root.dataset.theme);
        });
    }

    const mobileMenu = document.getElementById("mobileMenu");
    const topNav = document.getElementById("topNav");

    if (mobileMenu && topNav) {
        mobileMenu.addEventListener("click", function () {
            const isOpen = topNav.classList.toggle("open");
            mobileMenu.setAttribute("aria-expanded", String(isOpen));
        });
    }

    const sidebarToggle = document.getElementById("sidebarToggle");
    const adminShell = document.getElementById("adminShell");

    if (sidebarToggle && adminShell) {
        sidebarToggle.addEventListener("click", function () {
            const mobile = window.matchMedia("(max-width: 680px)").matches;
            const className = mobile ? "sidebar-open" : "sidebar-collapsed";
            const isOpen = adminShell.classList.toggle(className);
            sidebarToggle.setAttribute("aria-expanded", String(isOpen));
        });
    }

    document.querySelectorAll("[data-submit-on-change]").forEach(function (field) {
        field.addEventListener("change", function () {
            field.form?.requestSubmit();
        });
    });

    const bookingForm = document.querySelector("[data-quick-booking]");

    if (bookingForm) {
        bookingForm.addEventListener("submit", function (event) {
            const dateInput = document.getElementById("quickDate");
            const timeInput = document.getElementById("quickTime");
            const combinedInput = document.getElementById("quickStartsAt");

            if (!dateInput || !timeInput || !combinedInput) {
                event.preventDefault();
                return;
            }

            if (!dateInput.value || !timeInput.value) {
                event.preventDefault();
                bookingForm.reportValidity();
                return;
            }

            const startsAt = `${dateInput.value}T${timeInput.value}`;
            const selectedDate = new Date(startsAt);

            if (Number.isNaN(selectedDate.getTime()) || selectedDate <= new Date()) {
                event.preventDefault();
                dateInput.setCustomValidity("Vui lòng chọn thời gian trong tương lai.");
                dateInput.reportValidity();
                return;
            }

            dateInput.setCustomValidity("");
            combinedInput.value = startsAt;
        });

        const quickDate = document.getElementById("quickDate");

        quickDate?.addEventListener("change", function () {
            quickDate.setCustomValidity("");
        });
    }
})();
