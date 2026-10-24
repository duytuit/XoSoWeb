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
const form = { elements: { endDate: { value: '2026-10-24' } }, requestSubmit() {} };
page.querySelector = selector => selector === '.rhythm-filter' ? form : table;
const keyboardTargets = [element(), element()];
page.querySelectorAll = selector => selector.startsWith('a[href],button') ? keyboardTargets : [];
document = {
    body, activeElement: null,
    querySelector: () => page,
    getElementById(id) { if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id); },
    addEventListener(type, callback) { this[type] = callback; },
};
document.getElementById('rhythm-cycle-data').textContent = '[]';
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/number-rhythm.js'), 'utf8'), {
    document, localStorage: { getItem: () => null, setItem() {} },
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
get('expand').dispatch('click');
assert.equal(page.classList.contains('rhythm-expanded'), true);
assert.equal(body.classList.contains('rhythm-expanded-active'), true);
assert.equal(page.getAttribute('aria-modal'), 'true');
assert.equal(document.activeElement, get('expand'));
document.activeElement = keyboardTargets[1];
document.keydown({ key: 'Tab', preventDefault() {} });
assert.equal(document.activeElement, keyboardTargets[0]);
document.keydown({ key: 'Tab', shiftKey: true, preventDefault() {} });
assert.equal(document.activeElement, keyboardTargets[1]);
get('cycle-dialog').open = true;
document.keydown({ key: 'Escape', preventDefault() {} });
assert.equal(page.classList.contains('rhythm-expanded'), true);
get('cycle-dialog').open = false;
document.keydown({ key: 'Escape', preventDefault() {} });
assert.equal(page.classList.contains('rhythm-expanded'), false);
assert.equal(body.classList.contains('rhythm-expanded-active'), false);
assert.equal(page.getAttribute('aria-modal'), undefined);
get('expand').dispatch('click'); get('expand').dispatch('click');
assert.equal(page.classList.contains('rhythm-expanded'), false);
console.log('PASS: zoom bounds/reset, expand/restore, Escape, nested cycle dialog and keyboard focus wrapping.');
