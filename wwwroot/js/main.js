document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.lottery-grid-row').forEach(row => {
        row.addEventListener('mouseenter', () => {
            row.style.backgroundColor = 'rgba(0, 25, 118, 0.05)';
        });
        row.addEventListener('mouseleave', () => {
            row.style.backgroundColor = '';
        });
    });
});