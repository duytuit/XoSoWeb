// Unit checks for the view controls with an isolated DOM stub; not a browser rendering test.
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const nodes = new Map();
let document;
function element() {
    const classes = new Set(), attributes = new Map(), events = new Map();
    return {
        hidden: false, checked: false, disabled: false, open: false, textContent: '',
        classList: { contains: key => classes.has(key), toggle(key, force) {
            const enabled = force === undefined ? !classes.has(key) : force;
            enabled ? classes.add(key) : classes.delete(key); return enabled;
        } },
        style: { values: new Map(), setProperty(key, value) { this.values.set(key, value); } },
        setAttribute: (key, value) => attributes.set(key, value),
        getAttribute: key => attributes.get(key), removeAttribute: key => attributes.delete(key),
        addEventListener: (type, callback) => events.set(type, callback),
        dispatch(type, event = {}) { events.get(type)?.(event); },
        focus() { document.activeElement = this; },
        getClientRects: () => [{}], closest: () => null,
        querySelectorAll: () => [],
    };
}
const page = element(), body = element(), table = element();
table.clientWidth = 1920;
const numberCells = Array.from({ length: 100 }, element);
const dateCell = { getBoundingClientRect: () => ({ width: 104 }) };
const form = { elements: { endDate: { value: '2026-10-24' } }, requestSubmit() {} };
page.querySelector = selector => selector === '.rhythm-filter' ? form : selector === '.rhythm-number-row > th' ? dateCell : table;
const keyboardTargets = [element(), element()];
page.querySelectorAll = selector => selector.startsWith('a[href],button') ? keyboardTargets : selector === '.rhythm-number-row [data-column]' ? numberCells : [];
document = {
    body, activeElement: null,
    querySelector: () => page,
    getElementById(id) { if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id); },
    addEventListener(type, callback) { this[type] = callback; },
};
document.getElementById('rhythm-cycle-data').textContent = '[]';
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/number-rhythm.js'), 'utf8'), {
    document, URLSearchParams, FormData: class { *[Symbol.iterator]() { yield ["days", "30"]; yield ["q", "12,34"]; yield ["target", "special"]; } }, localStorage: { getItem: () => null, setItem() {} },
});
const get = id => document.getElementById('rhythm-' + id);
assert.equal(get('zoom-value').textContent, '100%');
for (let i = 0; i < 10; i++) get('zoom-in').dispatch('click');
assert.equal(get('zoom-value').textContent, '175%');
assert.equal(get('zoom-in').disabled, true);
assert.equal(page.style.values.get('--rhythm-zoom'), '1.75');
for (let i = 0; i < 10; i++) get('zoom-out').dispatch('click');
assert.equal(get('zoom-value').textContent, '75%');
assert.equal(get('zoom-out').disabled, true);
get('zoom-reset').dispatch('click');
assert.equal(get('zoom-value').textContent, '100%');
get('fit-width').dispatch('click');
assert.equal(get('fit-width').getAttribute('aria-pressed'), 'true');
assert.equal(page.style.values.get('--rhythm-cell-width'), '18px');
get('zoom-in').dispatch('click');
assert.equal(get('fit-width').getAttribute('aria-pressed'), 'false');
assert.equal(page.style.values.get('--rhythm-cell-width'), '26px');
get('fit-width').dispatch('click');
assert.equal(get('zoom-value').textContent, '100%');
table.clientWidth = 390;
get('fit-width').dispatch('click'); get('fit-width').dispatch('click');
assert.equal(page.style.values.get('--rhythm-cell-width'), '16px');
get('expand').dataset = { viewPath: '/thong-ke/nhip-so-mien-bac/phong-to' };
get('expand').dispatch('click');
assert.equal(get('expand').href, '/thong-ke/nhip-so-mien-bac/phong-to?days=30&q=12%2C34&target=special');
get('expand').dataset.viewPath = '/thong-ke/nhip-so-mien-bac';
let navigations = 0;
get('expand').click = () => { navigations++; get('expand').dispatch('click'); };
page.classList.toggle('rhythm-expanded', true);
get('cycle-dialog').open = true;
document.keydown({ key: 'Escape', preventDefault() {} });
assert.equal(navigations, 0);
get('cycle-dialog').open = false;
document.keydown({ key: 'Escape', preventDefault() {} });
assert.equal(navigations, 1);
assert.equal(get('expand').href, '/thong-ke/nhip-so-mien-bac?days=30&q=12%2C34&target=special');
console.log('PASS: zoom bounds/reset, fit width, separate-view navigation preserves filters, Escape respects cycle dialog.');
