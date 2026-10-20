const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const base = 'http://127.0.0.1:5189';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
(async () => {
    const browser = await chromium.launch({ headless: true });
    const a = await browser.newContext(), b = await browser.newContext();
    const p = await a.newPage(), q = await b.newPage();
    const offlineText = 'Tin trong lúc mất mạng ' + Date.now();
    const errors = []; for (const page of [p, q]) page.on('pageerror', e => errors.push(e.message));
    let chatRequests = 0; p.on('request', r => { if (r.url().includes('/community-chat/')) chatRequests++; });
    await p.goto(base + '/fixture'); await q.goto(base + '/fixture'); assert.equal(chatRequests, 0, 'lazy initialization');
    async function open(page) { await page.locator('#chat-launch').click(); await page.locator('#chat-status').filter({ hasText: 'Đã kết nối' }).waitFor(); }
    await open(p); await open(q);
    await p.waitForFunction(() => document.querySelector('#chat-status').textContent.includes('2 người'));
    const name = await p.locator('#chat-name').textContent();
    async function send(page, text) { await page.locator('#chat-input').fill(text); await page.locator('#chat-send').click(); }
    const text = `Hello realtime ${Date.now()}`;
    await send(p, text); await q.getByText(text, { exact: true }).waitFor();
    const row = q.locator('.chat-message').filter({ hasText: text });
    await row.getByRole('button', { name: 'Thả 👍', exact: true }).click();
    const ownRow = p.locator('.chat-message').filter({ hasText: text });
    await ownRow.getByRole('button', { name: 'Thả 👍', exact: true }).filter({ hasText: '1' }).waitFor();
    await row.getByRole('button', { name: 'Thả 👍', exact: true }).click();
    await p.waitForFunction(t => [...document.querySelectorAll('.chat-message')].find(n => n.textContent.includes(t)).querySelector('.chat-tools button').textContent === '👍', text);
    await row.getByRole('button', { name: 'Báo cáo tin nhắn', exact: true }).click();
    await q.locator('#chat-error').filter({ hasText: 'Đã gửi báo cáo' }).waitFor();
    await row.getByRole('button', { name: 'Ẩn người gửi trên thiết bị', exact: true }).click();
    assert.equal(await q.getByText(text, { exact: true }).count(), 0);
    await q.locator('#chat-unhide').click(); await q.getByText(text, { exact: true }).waitFor();
    // Same cookie in another tab is one online identity.
    const tab = await a.newPage(); await tab.goto(base + '/fixture'); await open(tab);
    await p.waitForFunction(() => document.querySelector('#chat-status').textContent.includes('2 người'));
    await tab.close();
    await p.reload(); await open(p); assert.equal(await p.locator('#chat-name').textContent(), name, 'guest survives reload');
    await p.locator('#chat-rename').click(); await p.locator('#chat-name-input').fill('Bạn May Mắn'); await p.locator('#chat-name-form button').click();
    await p.locator('#chat-name').filter({ hasText: 'Bạn May Mắn' }).waitFor();
    await p.reload(); await open(p); assert.match(await p.locator('#chat-name').textContent(), /Bạn May Mắn/);
    // IME Enter is ignored; Shift+Enter preserves a newline; emoji inserts at the caret.
    await p.locator('#chat-input').fill('IME draft');
    await p.locator('#chat-input').dispatchEvent('keydown', { key: 'Enter', isComposing: true, keyCode: 229 });
    assert.equal(await p.locator('#chat-input').inputValue(), 'IME draft');
    await p.locator('#chat-input').press('End'); await p.locator('#chat-input').press('Shift+Enter');
    assert.equal(await p.locator('#chat-input').inputValue(), 'IME draft\n');
    await p.locator('#chat-input').fill('AB'); await p.locator('#chat-input').evaluate(n => n.setSelectionRange(1, 1));
    await p.locator('#chat-emoji-toggle').click(); await p.getByRole('button', { name: 'Chèn 🍀', exact: true }).click();
    assert.equal(await p.locator('#chat-input').inputValue(), 'A🍀B');
    await send(p, '<img src=x onerror=alert(1)>'); await p.locator('#chat-error').filter({ hasText: 'không chứa HTML' }).waitFor();
    assert.equal(await q.locator('#chat-items img').count(), 0);
    await p.locator('#chat-minimize').click(); await send(q, 'Tin khi thu nhỏ'); await p.locator('#chat-badge').waitFor();
    await open(p); await p.keyboard.press('Escape'); assert.equal(await p.locator('#chat-launch').evaluate(n => n === document.activeElement), true);
    await open(p);
    // Real transport outage, message arrives while offline, then resume without duplicates.
    await a.setOffline(true); await sleep(1800); await send(q, offlineText);
    await a.setOffline(false); await p.locator('#chat-status').filter({ hasText: 'Đã kết nối' }).waitFor({ timeout: 45000 });
    await p.getByText(offlineText, { exact: true }).waitFor({ timeout: 45000 });
    assert.equal(await p.getByText(offlineText, { exact: true }).count(), 1);
    await a.setOffline(true); await sleep(1800);
    await q.request.post(base + '/fixture/seed/65');
    await a.setOffline(false);
    await p.waitForFunction(() => document.querySelectorAll('.chat-text').length >= 65, null, { timeout: 45000 });
    // Independent raw SignalR client checks server authority and idempotency.
    const serverChecks = await q.evaluate(async () => {
        const conn = new signalR.HubConnectionBuilder().withUrl('/community-chat/hub').build(); await conn.start();
        const results = {};
        async function rejects(method, ...args) { try { await conn.invoke(method, ...args); return false; } catch { return true; } }
        results.admin = await rejects('Delete', 1); results.ban = await rejects('Ban', 'g-fake');
        results.name = await rejects('Rename', 'Admin quản trị'); results.emoji = await rejects('React', 1, '💣');
        results.length = await rejects('Send', crypto.randomUUID(), 'x'.repeat(501));
        await new Promise(r => setTimeout(r, 10500));
        const id = crypto.randomUUID(); const first = await conn.invoke('Send', id, 'Idempotent retry'); const second = await conn.invoke('Send', id, 'Idempotent retry'); results.dedup = first.id === second.id;
        let limited = false; for (let n = 0; n < 9; n++) if (await rejects('Send', crypto.randomUUID(), 'Rate test ' + n)) limited = true;
        results.rate = limited;
        const history = await conn.invoke('History', 0, 0); results.history = Array.isArray(history) && history.length <= 50;
        results.payload = await rejects('Send', crypto.randomUUID(), 'z'.repeat(6000));
        await conn.stop(); return results;
    });
    for (const [name, result] of Object.entries(serverChecks)) assert.equal(result, true, name);
    const crossOrigin = await q.request.post(base + '/community-chat/identity', { headers: { Origin: 'https://evil.invalid' } }); assert.equal(crossOrigin.status(), 403);
    const forged = await browser.newContext(); await forged.addCookies([{ name: 'chat-guest', value: 'g-fake-admin', url: base }]);
    const forgedResult = await forged.request.post(base + '/community-chat/identity'); const forgedPerson = await forgedResult.json(); assert.notEqual(forgedPerson.id, 'g-fake-admin');
    await forged.close();
    // Bound rendered messages, retain scroll position, and page older history.
    await q.request.post(base + '/fixture/seed/160');
    await p.waitForFunction(() => document.querySelectorAll('.chat-message').length >= 190);
    assert.ok(await p.locator('.chat-message').count() <= 201); // 200 committed + the failed local draft
    await p.locator('#chat-messages').evaluate(n => n.scrollTop = 100);
    const scroll = await p.locator('#chat-messages').evaluate(n => n.scrollTop);
    await q.request.post(base + '/fixture/seed/1'); await p.locator('#chat-new').waitFor();
    assert.equal(await p.locator('#chat-messages').evaluate(n => n.scrollTop), scroll);
    await p.locator('#chat-new').click(); await p.locator('#chat-history').click();
    assert.ok(await p.locator('.chat-message').count() > 50);
    await p.reload(); await open(p); assert.ok(await p.locator('.chat-message').count() <= 50);
    const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    const m = await mobile.newPage(); await m.goto(base + '/fixture'); await open(m);
    const box = await m.locator('#chat-panel').boundingBox(); assert.ok(box.x >= 0 && box.x + box.width <= 390 && box.y >= 0 && box.y + box.height < 780);
    assert.equal(await m.locator('#chat-panel').evaluate(n => getComputedStyle(n).borderRadius), '16px');
    await m.emulateMedia({ reducedMotion: 'reduce' });
    assert.equal(await m.locator('#chat-panel').evaluate(n => getComputedStyle(n).animationName), 'none');
    await m.locator('#chat-emoji-toggle').click(); await m.getByRole('button', { name: 'Chèn 🍀', exact: true }).click(); assert.equal(await m.locator('#chat-input').inputValue(), '🍀');
    await m.screenshot({ path: 'tests/ChatIntegration/mobile.png' }); await p.screenshot({ path: 'tests/ChatIntegration/desktop.png' });
    assert.deepEqual(errors, []); await browser.close();
    console.log('PASS: two sessions, live messages/reactions, multi-tab presence, persisted identity/rename, IME/newline/caret insertion, HTML rejection, unread/focus, offline recovery over 50 messages, idempotent retry, rate limit, admin denial, forged token, cross-origin denial, DOM cap, scroll anchor, paginated history, mobile bounds/radius, emoji.');
})().catch(e => { console.error(e); process.exit(1); });
