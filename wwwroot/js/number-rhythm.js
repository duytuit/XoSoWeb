(() => {
    const page = document.querySelector('.rhythm-page');
    if (!page) return;
    const zoomOut = document.getElementById('rhythm-zoom-out');
    const zoomIn = document.getElementById('rhythm-zoom-in');
    const zoomReset = document.getElementById('rhythm-zoom-reset');
    const zoomValue = document.getElementById('rhythm-zoom-value');
    const expandButton = document.getElementById('rhythm-expand');
    let zoom = 100;
    function applyZoom(value) {
        zoom = Math.max(75, Math.min(175, value));
        page.style.setProperty('--rhythm-zoom', String(zoom / 100));
        zoomValue.textContent = `${zoom}%`;
        zoomOut.disabled = zoom === 75;
        zoomIn.disabled = zoom === 175;
    }
    zoomOut.addEventListener('click', () => applyZoom(zoom - 25));
    zoomIn.addEventListener('click', () => applyZoom(zoom + 25));
    zoomReset.addEventListener('click', () => applyZoom(100));
    function setExpanded(expanded) {
        page.classList.toggle('rhythm-expanded', expanded);
        document.body.classList.toggle('rhythm-expanded-active', expanded);
        expandButton.setAttribute('aria-pressed', String(expanded));
        expandButton.textContent = expanded ? '✕ Thu gọn bảng' : '⛶ Phóng to bảng';
        if (expanded) {
            page.setAttribute('role', 'dialog');
            page.setAttribute('aria-modal', 'true');
            page.setAttribute('aria-label', 'Nhịp số miền Bắc — xem bảng phóng to');
        } else {
            page.removeAttribute('role'); page.removeAttribute('aria-modal'); page.removeAttribute('aria-label');
        }
        expandButton.focus({ preventScroll: true });
    }
    expandButton.addEventListener('click', () => setExpanded(!page.classList.contains('rhythm-expanded')));
    document.addEventListener('keydown', event => {
        if (!page.classList.contains('rhythm-expanded') || document.getElementById('rhythm-cycle-dialog').open) return;
        if (event.key === 'Escape') { event.preventDefault(); setExpanded(false); return; }
        if (event.key !== 'Tab') return;
        const focusable = [...page.querySelectorAll('a[href],button:not(:disabled),input:not(:disabled),select:not(:disabled),summary,[tabindex="0"]')]
            .filter(element => element.getClientRects().length && !element.closest('[hidden]'));
        const first = focusable[0], last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    });
    applyZoom(100);

    const key = 'xosoweb.number-rhythm.marks.v1';
    const marks = new Set();
    const status = document.getElementById('rhythm-mark-status');
    try {
        const stored = JSON.parse(localStorage.getItem(key) || '[]');
        if (Array.isArray(stored)) stored.forEach(n => { if (typeof n === 'string' && /^\d{2}$/.test(n)) marks.add(n); });
    } catch { status.textContent = 'Không đọc được dấu đã lưu; vẫn có thể đánh dấu trong phiên này.'; }
    const cells = [...page.querySelectorAll('[data-column]')];
    const columns = [...page.querySelectorAll('[data-cycle]')].map(button => button.dataset.cycle);
    const onlyMarked = document.getElementById('rhythm-only-marked');
    function render() {
        cells.forEach(cell => {
            const marked = marks.has(cell.dataset.column);
            cell.classList.toggle('is-marked', marked);
            cell.hidden = onlyMarked.checked && !marked;
        });
        page.querySelectorAll('[data-mark]').forEach(button => button.setAttribute('aria-pressed', String(marks.has(button.dataset.mark))));
        document.getElementById('rhythm-mark-count').textContent = marks.size;
        document.getElementById('rhythm-no-marks').hidden = !onlyMarked.checked || columns.some(n => marks.has(n));
        page.querySelector('.rhythm-table-wrap').hidden = onlyMarked.checked && !columns.some(n => marks.has(n));
    }
    function persist() {
        try { localStorage.setItem(key, JSON.stringify([...marks].sort())); status.textContent = ''; }
        catch { status.textContent = 'Trình duyệt không cho lưu dấu; dấu chỉ dùng trong phiên này.'; }
        render();
    }
    function toggle(number) { marks.has(number) ? marks.delete(number) : marks.add(number); persist(); }
    page.querySelectorAll('[data-mark]').forEach(button => button.addEventListener('click', () => toggle(button.dataset.mark)));
    page.querySelectorAll('tbody [data-column]').forEach(cell => cell.addEventListener('dblclick', () => toggle(cell.dataset.column)));
    onlyMarked.addEventListener('change', render);
    document.getElementById('rhythm-clear-marks').addEventListener('click', () => { marks.clear(); persist(); });
    render();

    const form = page.querySelector('.rhythm-filter');
    page.querySelectorAll('[data-shift]').forEach(button => button.addEventListener('click', () => {
        const input = form.elements.endDate;
        const date = new Date(`${input.value}T00:00:00Z`);
        if (Number.isNaN(date.getTime())) return;
        date.setUTCDate(date.getUTCDate() + Number(button.dataset.shift));
        input.value = date.toISOString().slice(0, 10);
        form.requestSubmit();
    }));

    const data = JSON.parse(document.getElementById('rhythm-cycle-data').textContent);
    const dialog = document.getElementById('rhythm-cycle-dialog');
    page.querySelectorAll('[data-cycle]').forEach(button => button.addEventListener('click', () => {
        const column = data.find(item => item.number === button.dataset.cycle);
        if (!column) return;
        document.getElementById('rhythm-cycle-title').textContent = `Chu kỳ cặp ${column.number}`;
        const gap = column.exact ? `${column.gap}` : `ít nhất ${column.gap}`;
        document.getElementById('rhythm-cycle-summary').textContent = `${column.total} lần về trong ${column.hitDays} ngày. Lần về gần nhất trong khoảng: ${column.lastSeen || 'chưa ghi nhận'}. Chưa về ${gap} ngày liên tiếp tính từ ngày cuối. Các chu kỳ bên dưới chỉ tính trong khoảng đã chọn.`;
        const body = document.getElementById('rhythm-cycle-body');
        body.replaceChildren();
        column.cycles.forEach(cycle => {
            const row = document.createElement('tr');
            [cycle.from, cycle.to, `${cycle.days} ngày`].forEach(value => {
                const cell = document.createElement('td'); cell.textContent = value; row.append(cell);
            });
            body.append(row);
        });
        document.getElementById('rhythm-cycle-empty').hidden = column.cycles.length > 0;
        dialog.showModal();
    }));
})();
