document.addEventListener("DOMContentLoaded", function () {
    const locationText = document.getElementById("location_text");
    const helpfulLinksText = document.getElementById("helpful_links_text");
    const headings = document.querySelectorAll(".heading-links");

    // Improved height calculation with animation frame
    function prepareElementForFadeIn(element) {
        if (element.classList.contains("hidden")) {
            // Force layout calculation
            element.style.display = 'block';
            element.style.visibility = 'hidden';
            const targetHeight = element.scrollHeight;

            // Reset to initial state
            element.style.display = '';
            element.style.visibility = 'visible';
            element.style.height = '0';
            element.style.opacity = '0';

            // Trigger height transition
            requestAnimationFrame(() => {
                element.style.height = `${targetHeight}px`;
                element.style.opacity = '1';
            });
        }
    }

    // Initialize hidden state
    locationText.classList.add("hidden");
    helpfulLinksText.classList.add("hidden");

    let isLocationRevealed = false;
    let isLinksRevealed = false;

    // Scroll handler
    function handleScroll() {
        if (window.scrollY > 25) {
            if (!isLocationRevealed) {
                prepareElementForFadeIn(locationText);
                locationText.classList.replace("hidden", "fade-in");
                headings[0].classList.add("no-underline"); // Add this line
                isLocationRevealed = true;
            }

            if (!isLinksRevealed) {
                prepareElementForFadeIn(helpfulLinksText);
                helpfulLinksText.classList.replace("hidden", "fade-in");
                headings[1].classList.add("no-underline"); // Add this line
                isLinksRevealed = true;
            }

            if (isLocationRevealed && isLinksRevealed) {
                window.removeEventListener("scroll", handleScroll);
            }
        }
    }

    window.addEventListener("scroll", handleScroll);

    // Click handlers
    headings[0].addEventListener("click", () => {
        if (!isLocationRevealed) {
            prepareElementForFadeIn(locationText);
            locationText.classList.replace("hidden", "fade-in");
            headings[0].classList.add("no-underline"); // Add this line
            isLocationRevealed = true;
        }
    });

    headings[1].addEventListener("click", () => {
        if (!isLinksRevealed) {
            prepareElementForFadeIn(helpfulLinksText);
            helpfulLinksText.classList.replace("hidden", "fade-in");
            headings[1].classList.add("no-underline"); // Add this line
            isLinksRevealed = true;
        }
    });
});