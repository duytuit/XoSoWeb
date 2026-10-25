(() => {
    const specialOnly = document.querySelector('.soicau-page')?.dataset.target === 'special';
    document.querySelectorAll('.sc-pattern').forEach(pattern => pattern.addEventListener('toggle', () => {
        if (!pattern.open) return;
        pattern.closest('.sc-patterns')?.querySelectorAll('.sc-pattern[open]').forEach(other => {
            if (other !== pattern) other.open = false;
        });
        const history = pattern.querySelector('.sc-history-boards');
        if (!history || history.childElementCount) return;
        const dates = history.dataset.dates.split(',');
        const hits = history.dataset.hits.split(',');
        dates.forEach((date, index) => {
            const template = document.getElementById(`sc-draw-${date}`);
            if (!template) return;
            const wrapper = document.createElement('div');
            wrapper.className = 'sc-history-day';
            wrapper.append(template.content.cloneNode(true));
            [history.dataset.first, history.dataset.second].forEach(position => {
                wrapper.querySelector(`[data-digit-position="${position}"]`)?.classList.add('sc-source-digit');
            });
            if (hits[index]) wrapper.querySelectorAll('.sc-prize-number').forEach(number => {
                if (number.dataset.tail !== hits[index] || (specialOnly && number.dataset.prize !== '0')) return;
                [...number.querySelectorAll('[data-digit-position]')].slice(-2).forEach(digit => digit.classList.add('sc-hit-digit'));
            });
            history.append(wrapper);
        });
    }));
    const patterns = [...document.querySelectorAll('.sc-pattern')];
    const digits = [...document.querySelectorAll('[data-position]')];
    let selected = null;
    const status = document.getElementById('sc-position-status');
    const empty = document.getElementById('sc-empty');
    document.getElementById('sc-pair-search')?.addEventListener('click', () => {
        const input = document.getElementById('sc-pair-filter');
        input.required = true;
        input.focus();
        input.select();
    });
    function openPattern(pattern) {
        if (!pattern) return;
        clear(); filter(() => true);
        patterns.forEach(other => { other.open = other === pattern; });
        pattern.scrollIntoView({ behavior: 'smooth', block: 'start' });
        pattern.querySelector('summary')?.focus({ preventScroll: true });
    }
    document.querySelectorAll('[data-pattern-index]').forEach(link => link.addEventListener('click', event => {
        event.preventDefault();
        openPattern(document.getElementById(`sc-pattern-${link.dataset.patternIndex}`));
    }));
    function filter(predicate) {
        let count = 0;
        patterns.forEach(p => { p.hidden = !predicate(p); if (!p.hidden) count++; });
        if (empty) empty.hidden = count !== 0;
        return count;
    }
    function clear() {
        selected = null;
        digits.forEach(d => { d.setAttribute('aria-pressed', 'false'); d.classList.remove('sc-partner'); });
        if (status) status.textContent = '';
    }
    digits.forEach(d => d.addEventListener('click', () => {
        const value = d.dataset.position;
        if (selected === value) { clear(); filter(() => true); return; }
        if (selected !== null && d.classList.contains('sc-partner')) {
            openPattern(patterns.find(p => (p.dataset.first === selected && p.dataset.second === value)
                || (p.dataset.second === selected && p.dataset.first === value)));
            return;
        }
        clear(); selected = value;
        const partners = new Set();
        patterns.forEach(p => {
            if (p.dataset.first === value) partners.add(p.dataset.second);
            if (p.dataset.second === value) partners.add(p.dataset.first);
        });
        digits.forEach(digit => {
            digit.setAttribute('aria-pressed', String(digit.dataset.position === value));
            digit.classList.toggle('sc-partner', partners.has(digit.dataset.position));
        });
        const count = filter(p => p.dataset.first === value || p.dataset.second === value);
        if (status) status.textContent = `${count} cầu có vị trí ${Number(value) + 1}.`;
    }));
    document.querySelectorAll('[data-number]').forEach(link => link.addEventListener('click', () => {
        clear(); const number = link.dataset.number;
        const reverse = document.querySelector('[name=reverse]').value === 'true';
        filter(p => p.dataset.value === number || (reverse && p.dataset.reversed === number));
    }));
    document.getElementById('sc-reset')?.addEventListener('click', () => { clear(); filter(() => true); });
})();
