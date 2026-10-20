const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const code = fs.readFileSync('wwwroot/js/support.js', 'utf8');
let checks = 0;
async function scenario(response, throws = false) {
    let submit, resets = 0, calls = 0;
    const button = { disabled: false, textContent: '' };
    const message = { textContent: 'Old status', className: 'alert alert-success' };
    const form = {
        addEventListener: (_, callback) => submit = callback,
        querySelector: selector => selector.startsWith('button') ? button : { value: 'fixture-csrf' },
        reset: () => resets++
    };
    const document = { getElementById: id => id === 'supportForm' ? form : id === 'message' ? message : { value: 'Fixture value' } };
    let release;
    const pending = new Promise(resolve => release = resolve);
    vm.runInNewContext(code, { document, fetch: async (url, options) => {
        calls++;
        assert.equal(url, '/support');
        assert.equal(options.headers.RequestVerificationToken, 'fixture-csrf');
        assert.equal(JSON.parse(options.body).content, 'Fixture value');
        await pending;
        if (throws) throw new Error('Synthetic network error');
        return response;
    } });
    const first = submit.call(form, { preventDefault() {} });
    assert.equal(button.disabled, true);
    assert.equal(message.textContent, '');
    await submit.call(form, { preventDefault() {} });
    assert.equal(calls, 1);
    release(); await first;
    assert.equal(button.disabled, false);
    const success = !throws && response.ok && response.body?.success === true;
    assert.equal(resets, success ? 1 : 0);
    assert.equal(message.className, success ? 'alert alert-success' : 'alert alert-danger');
    if (!success) assert.ok(!message.textContent.includes('thành công'));
    checks++;
}
(async () => {
    for (const [ok, body] of [[true, { success: true }], [true, { success: false }], [false, { success: false, message: '<b>Failure</b>' }]])
        await scenario({ ok, body, json: async () => body });
    await scenario({ ok: true, json: async () => { throw new Error('Invalid JSON'); } });
    await scenario(null, true);
    console.log(`Completed ${checks} support UI scenarios: CSRF, duplicate submission, confirmed success, failures and retained input.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
