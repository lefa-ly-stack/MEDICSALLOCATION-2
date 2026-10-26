(() => {
    const backgroundVideos = document.querySelectorAll(".home-background-video, .auth-background-video");
    if (backgroundVideos.length > 0) {
        const motionPreference = window.matchMedia("(prefers-reduced-motion: reduce)");
        const updateVideoPlayback = () => {
            backgroundVideos.forEach(video => {
                if (motionPreference.matches) video.pause();
                else if (video.paused) video.play().catch(() => {});
            });
        };
        updateVideoPlayback();
        motionPreference.addEventListener("change", updateVideoPlayback);
    }

    const themes = ["cobalt", "lavender", "taupe", "purple-pink", "gold-charcoal"];
    const picker = document.getElementById("themePicker");
    if (!picker) return;

    const savedTheme = localStorage.getItem("medicsallocation-theme");
    const currentTheme = themes.includes(savedTheme) ? savedTheme : "cobalt";
    document.documentElement.dataset.theme = currentTheme;
    picker.value = currentTheme;

    picker.addEventListener("change", () => {
        if (!themes.includes(picker.value)) return;
        document.documentElement.dataset.theme = picker.value;
        localStorage.setItem("medicsallocation-theme", picker.value);
    });
})();
