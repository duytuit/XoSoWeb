document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.lottery-grid-row').forEach(row => {
        row.addEventListener('mouseenter', () => {
            row.style.backgroundColor = 'rgba(0, 25, 118, 0.05)';
        });
        row.addEventListener('mouseleave', () => {
            row.style.backgroundColor = '';
        });
    });

    const topbar = document.getElementById('topbar');
    let lastScrollY = window.scrollY;

    if (topbar) {
        window.addEventListener('scroll', function () {
            const currentScrollY = window.scrollY;

            if (currentScrollY > lastScrollY && currentScrollY > topbar.offsetHeight) {
                topbar.classList.add('is-hidden');
            } else if (currentScrollY < lastScrollY) {
                topbar.classList.remove('is-hidden');
            }

            lastScrollY = Math.max(currentScrollY, 0);
        }, { passive: true });
    }
});