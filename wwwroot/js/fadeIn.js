document.addEventListener("DOMContentLoaded", function () {
    const locationText = document.getElementById("location_text");
    const helpfulLinksText = document.getElementById("helpful_links_text");
    const headings = document.querySelectorAll(".heading-links");

    // Initialize hidden state
    locationText.classList.add("hidden");
    helpfulLinksText.classList.add("hidden");

    let isLocationRevealed = false;
    let isLinksRevealed = false;

    // Prevent multiple transitions from happening simultaneously
    let isLocationTransitioning = false;
    let isLinksTransitioning = false;

    // Fade in function - smoother appearance
    function fadeInElement(element) {
        // Don't do anything if we're in the middle of a transition
        if (element === locationText && isLocationTransitioning) return;
        if (element === helpfulLinksText && isLinksTransitioning) return;

        // Set transition flag
        if (element === locationText) isLocationTransitioning = true;
        if (element === helpfulLinksText) isLinksTransitioning = true;

        // Remove hidden class first
        element.classList.remove("hidden");
        element.classList.remove("fade-out");

        // Force reflow to ensure transitions work properly
        void element.offsetWidth;

        // Add fade-in class to trigger transition
        element.classList.add("fade-in");

        // Reset transition flag when done
        element.addEventListener('transitionend', function resetFlag(e) {
            if (e.propertyName === 'opacity') {
                if (element === locationText) isLocationTransitioning = false;
                if (element === helpfulLinksText) isLinksTransitioning = false;
                element.removeEventListener('transitionend', resetFlag);
            }
        });
    }

    // Fade out function - maintain position while fading
    function fadeOutElement(element) {
        // Don't do anything if we're in the middle of a transition
        if (element === locationText && isLocationTransitioning) return;
        if (element === helpfulLinksText && isLinksTransitioning) return;

        // Set transition flag
        if (element === locationText) isLocationTransitioning = true;
        if (element === helpfulLinksText) isLinksTransitioning = true;

        // Get the current height of the element and store it
        const height = element.offsetHeight;

        // Remove fade-in class and add fade-out
        element.classList.remove("fade-in");
        element.classList.add("fade-out");

        // After the opacity transition completes, hide the element
        element.addEventListener('transitionend', function completeHide(e) {
            if (e.propertyName === 'opacity') {
                element.classList.remove("fade-out");
                element.classList.add("hidden");

                // Reset transition flag
                if (element === locationText) isLocationTransitioning = false;
                if (element === helpfulLinksText) isLinksTransitioning = false;

                element.removeEventListener('transitionend', completeHide);
            }
        });
    }

    // Click handlers with toggle behavior
    headings[0].addEventListener("click", () => {
        if (!isLocationRevealed) {
            fadeInElement(locationText);
            headings[0].classList.add("no-underline");
            isLocationRevealed = true;
        } else {
            fadeOutElement(locationText);
            headings[0].classList.remove("no-underline");
            isLocationRevealed = false;
        }
    });

    headings[1].addEventListener("click", () => {
        if (!isLinksRevealed) {
            fadeInElement(helpfulLinksText);
            headings[1].classList.add("no-underline");
            isLinksRevealed = true;
        } else {
            fadeOutElement(helpfulLinksText);
            headings[1].classList.remove("no-underline");
            isLinksRevealed = false;
        }
    });

    // Scroll handler to reveal content on scroll
    function handleScroll() {
        if (window.scrollY > 25) {
            if (!isLocationRevealed) {
                fadeInElement(locationText);
                headings[0].classList.add("no-underline");
                isLocationRevealed = true;
            }

            if (!isLinksRevealed) {
                fadeInElement(helpfulLinksText);
                headings[1].classList.add("no-underline");
                isLinksRevealed = true;
            }

            if (isLocationRevealed && isLinksRevealed) {
                window.removeEventListener("scroll", handleScroll);
            }
        }
    }

    window.addEventListener("scroll", handleScroll);
});