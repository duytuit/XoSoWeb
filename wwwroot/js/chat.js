(() => {
    'use strict';
    const $ = id => document.getElementById(`chat-${id}`);
    const root = document.getElementById('community-chat'), panel = $('panel'), list = $('messages'), input = $('input');
    const messages = new Map(), pending = new Map();
    let person, connection, starting, opened = false, initialized = false, unread = 0, online = null, high = 0, earliest = 0, position = 0, synced = false, reconnectTimer, replayCursor = null, deferred = false, closingAnimation;
    let hidden;
    try { hidden = new Set(JSON.parse(localStorage.getItem('chat-hidden') || '[]')); } catch { hidden = new Set(); }
    const reactions = ['👍', '❤️', '😂', '🎉'];
    const time = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' });
    function error(text) { $('error').textContent = text; $('error').hidden = !text; }
    function state(value) {
        root.classList.remove('chat-connected', 'chat-connecting'); root.classList.add(`chat-${value}`);
        $('status').textContent = value === 'connected' ? `Đã kết nối · ${online ?? '…'} người online` : value === 'connecting' ? 'Đang kết nối…' : 'Mất kết nối · đang thử lại';
        $('send').disabled = value !== 'connected';
    }
    function badge() { $('badge').hidden = !unread; $('badge').textContent = unread > 99 ? '99+' : unread; $('launch').setAttribute('aria-label', `Chat cộng đồng${unread ? `, ${unread} tin chưa đọc` : ''}`); }
    function bottom() { return list.scrollHeight - list.scrollTop - list.clientHeight < 65; }
    function scrollEnd() { list.scrollTop = list.scrollHeight; $('new').hidden = true; unread = 0; badge(); }
    function button(label, action, aria) { const node = document.createElement('button'); node.type = 'button'; node.textContent = label; if (aria) node.setAttribute('aria-label', aria); node.onclick = action; return node; }
    function saveHidden() { try { localStorage.setItem('chat-hidden', JSON.stringify([...hidden])); } catch {} $('unhide').hidden = !hidden.size; }
    function render() {
        const fragment = document.createDocumentFragment();
        let previous;
        const ordered = [...messages.values()].sort((a, b) => a.id - b.id).slice(-200);
        for (const m of [...ordered, ...pending.values()]) {
            if (hidden.has(m.user.id)) continue;
            const own = m.user.id === person?.id;
            const row = document.createElement('article'); row.className = `chat-message${own ? ' mine' : ''}`; row.dataset.id = m.id || m.clientId;
            if (previous?.user.id === m.user.id && Math.abs(new Date(m.time) - new Date(previous.time)) < 180000) row.classList.add('chat-grouped');
            const avatar = document.createElement('span'); avatar.className = 'chat-avatar'; avatar.textContent = m.user.avatar;
            if (/^#[0-9a-f]{6}$/i.test(m.user.color)) avatar.style.backgroundColor = m.user.color;
            const body = document.createElement('div'); body.className = 'chat-body';
            const meta = document.createElement('div'); meta.className = 'chat-meta'; meta.textContent = `${m.user.name} · ${time.format(new Date(m.time))}`;
            const text = document.createElement('div'); text.className = 'chat-text'; text.textContent = m.text;
            body.append(meta, text);
            if (!m.id) {
                const delivery = document.createElement('div'); delivery.className = 'chat-delivery'; delivery.textContent = m.failed ? 'Gửi lỗi · ' : 'Đang gửi…';
                if (m.failed) {
                    delivery.append(button('Thử lại', () => transmit(m)));
                    delivery.append(button('Bỏ', () => { pending.delete(m.clientId); render(); }, 'Bỏ tin nhắn gửi lỗi'));
                }
                body.append(delivery);
            } else if (!m.deleted) {
                const tools = document.createElement('div'); tools.className = 'chat-tools';
                for (const emoji of reactions) {
                    const users = m.reactions?.[emoji] || {};
                    const b = button(`${emoji}${Object.keys(users).length ? ` ${Object.keys(users).length}` : ''}`, () => invoke('React', m.id, emoji), `Thả ${emoji}`);
                    b.setAttribute('aria-pressed', String(!!users[person?.id])); tools.append(b);
                }
                tools.append(button('⚑', async () => { if (await invoke('Report', m.id) !== false) error('Đã gửi báo cáo cho quản trị viên.'); }, 'Báo cáo tin nhắn'));
                if (!own) tools.append(button('Ẩn', () => { hidden.add(m.user.id); saveHidden(); render(); }, 'Ẩn người gửi trên thiết bị'));
                body.append(tools);
            }
            row.append(avatar, body); fragment.append(row); previous = m;
        }
        $('items').replaceChildren(fragment); $('empty').hidden = !!fragment.childNodes.length || !!$('items').childNodes.length;
    }
    function merge(batch, live = false) {
        if (!Array.isArray(batch)) batch = [];
        const atEnd = bottom(), top = list.scrollTop;
        let received = 0;
        for (const m of batch) {
            const fresh = !messages.has(m.id);
            // A delayed send acknowledgement must not overwrite a newer reaction/deletion event.
            if (fresh || !live) {
                if (live && !atEnd && messages.size >= 200 && fresh && m.user.id !== person?.id) deferred = true;
                else messages.set(m.id, m);
            }
            if (m.user.id === person?.id) pending.delete(m.clientId);
            high = Math.max(high, m.id); earliest = earliest ? Math.min(earliest, m.id) : m.id;
            if (fresh && live && m.user.id !== person?.id && !hidden.has(m.user.id)) received++;
        }
        // Bound memory and DOM; history pagination can replace the older window.
        const ids = [...messages.keys()].sort((a, b) => a - b);
        if (live) while (ids.length > 200) messages.delete(ids.shift());
        render();
        if (opened && atEnd) scrollEnd();
        else { list.scrollTop = top; if (received) { unread += received; badge(); $('new').hidden = false; } }
    }
    async function invoke(method, ...args) {
        try { if (!connection || connection.state !== signalR.HubConnectionState.Connected) throw new Error('Mất kết nối. Vui lòng thử lại khi kết nối phục hồi.'); return await connection.invoke(method, ...args); }
        catch (e) { error(e.message?.includes('HubException: ') ? e.message.split('HubException: ').pop() : 'Không thể thực hiện thao tác. Kiểm tra kết nối và thử lại.'); return false; }
    }
    async function sync() {
        if (!synced) {
            const batch = await connection.invoke('History', 0, 0); merge(batch); synced = true;
        } else {
            // Catch up all missed pages. Keep the cursor separate from live incoming events.
            let cursor = replayCursor ?? high;
            for (;;) {
                const batch = await connection.invoke('History', 0, cursor);
                if (!Array.isArray(batch) || !batch.length) break;
                merge(batch, true); cursor = batch[batch.length - 1].id; replayCursor = cursor;
                if (batch.length < 50) break;
            }
            // Refresh visible messages to recover reactions/deletions missed while offline.
            let before = messages.size ? Math.max(...messages.keys()) + 1 : 0;
            const oldest = messages.size ? Math.min(...messages.keys()) : 0;
            for (let page = 0; page < 4; page++) {
                const recent = await connection.invoke('History', before, 0);
                if (!Array.isArray(recent) || !recent.length) break;
                merge(recent.filter(m => messages.has(m.id)));
                before = Math.min(...recent.map(m => m.id));
                if (before <= oldest || recent.length < 50) break;
            }
            replayCursor = null;
        }
        await connection.invoke('Ping');
    }
    function retryStart() { clearTimeout(reconnectTimer); reconnectTimer = setTimeout(() => start(), 4000 + Math.random() * 2000); }
    async function loadSignalR() {
        if (window.signalR) return;
        await new Promise((resolve, reject) => { const script = document.createElement('script'); script.src = '/lib/signalr/signalr.min.js'; script.onload = resolve; script.onerror = reject; document.head.append(script); });
    }
    async function start() {
        if (starting || connection?.state === 'Connected') return;
        starting = true; state('connecting');
        try {
            await loadSignalR();
            const response = await fetch('/community-chat/identity', { method: 'POST', credentials: 'same-origin' });
            if (!response.ok) throw new Error('Không thể lấy danh tính chat. Vui lòng thử lại.');
            person = await response.json(); $('name').textContent = `${person.avatar} ${person.name}`;
            if (!connection) {
                connection = new signalR.HubConnectionBuilder().withUrl('/community-chat/hub').withAutomaticReconnect({ nextRetryDelayInMilliseconds: context => Math.min(30000, 1000 * 2 ** Math.min(context.previousRetryCount, 5)) + Math.random() * 500 }).configureLogging(signalR.LogLevel.Error).build();
                connection.on('Event', event => {
                    if (event.kind === 'presence') { online = event.online; state('connected'); }
                    else if (event.kind === 'update') { if (messages.has(event.message.id)) { const top = list.scrollTop; messages.set(event.message.id, event.message); render(); list.scrollTop = top; } }
                    else if (event.kind === 'message') merge([event.message], true);
                });
                connection.onreconnecting(() => { replayCursor ??= high; state('connecting'); });
                connection.onreconnected(async () => { state('connected'); try { await sync(); error(''); } catch (e) { error(e.message); await connection.stop(); retryStart(); } });
                connection.onclose(() => { replayCursor ??= high; state('disconnected'); retryStart(); });
            }
            await connection.start(); state('connected'); await sync(); error(''); if (opened && !position) scrollEnd();
        } catch (e) { if (connection?.state === 'Connected') await connection.stop(); state('disconnected'); error(e.message || 'Không thể tải kết nối chat. Đang thử lại…'); retryStart(); }
        finally { starting = false; }
    }
    async function transmit(m) {
        if (m.sending) return;
        m.sending = true; m.failed = false; pending.set(m.clientId, m); render(); scrollEnd();
        const confirmed = await invoke('Send', m.clientId, m.text);
        m.sending = false;
        if (confirmed !== false) { merge([confirmed], true); error(''); }
        else { m.failed = true; render(); }
    }
    function show() {
        closingAnimation?.cancel(); panel.inert = false;
        opened = true; panel.hidden = false; $('launch').setAttribute('aria-expanded', 'true'); panel.focus();
        requestAnimationFrame(() => { list.scrollTop = position || list.scrollHeight; if (bottom()) scrollEnd(); });
        if (!initialized) { initialized = true; start(); }
    }
    function hide() {
        position = list.scrollTop; opened = false; panel.inert = true;
        if (panel.animate && !matchMedia('(prefers-reduced-motion:reduce)').matches) {
            closingAnimation = panel.animate([{ opacity: 1, transform: 'translateY(0)' }, { opacity: 0, transform: 'translateY(8px)' }], { duration: 140, easing: 'ease-in' });
            closingAnimation.finished.then(() => { if (!opened) panel.hidden = true; }).catch(() => {});
        } else panel.hidden = true;
        $('launch').setAttribute('aria-expanded', 'false'); $('emojis').hidden = true; $('emoji-toggle').setAttribute('aria-expanded', 'false'); $('launch').focus();
    }
    $('launch').onclick = () => opened ? hide() : show(); $('close').onclick = hide; $('minimize').onclick = hide;
    panel.addEventListener('keydown', e => { if (e.key === 'Escape') { e.stopPropagation(); if (!$('emojis').hidden) { $('emojis').hidden = true; $('emoji-toggle').setAttribute('aria-expanded', 'false'); $('emoji-toggle').focus(); } else hide(); } });
    $('new').onclick = async () => {
        if (deferred) {
            const recent = await invoke('History', 0, 0); if (recent === false) return;
            messages.clear(); merge(recent); deferred = false; $('history').hidden = false;
        }
        scrollEnd();
    };
    list.addEventListener('scroll', () => { if (bottom() && !deferred) { $('new').hidden = true; unread = 0; badge(); } });
    $('history').onclick = async () => {
        $('history').disabled = true;
        try {
            const min = Math.min(...messages.keys());
            const batch = await invoke('History', Number.isFinite(min) ? min : earliest, 0);
            if (batch === false) return;
            if (!Array.isArray(batch) || !batch.length) { $('history').hidden = true; return; }
            const anchor = $('items').firstElementChild?.dataset.id, offset = $('items').firstElementChild?.getBoundingClientRect().top;
            for (const m of batch) messages.set(m.id, m);
            const ids = [...messages.keys()].sort((a, b) => a - b); while (ids.length > 200) messages.delete(ids.pop());
            render(); const node = [...$('items').children].find(n => n.dataset.id === anchor); if (node) list.scrollTop += node.getBoundingClientRect().top - offset;
            $('history').hidden = batch.length < 50;
        } finally { $('history').disabled = false; }
    };
    $('compose').onsubmit = e => {
        e.preventDefault(); if (!person || !input.value.trim() || $('send').disabled) return;
        if (pending.size >= 20) { error('Hãy thử gửi lại hoặc bỏ các tin gửi lỗi trước khi gửi thêm.'); return; }
        const m = { clientId: crypto.randomUUID(), user: person, text: input.value.trim(), time: new Date().toISOString() };
        input.value = ''; input.dispatchEvent(new Event('input')); transmit(m); input.focus();
    };
    input.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing && e.keyCode !== 229 && matchMedia('(pointer:fine)').matches) { e.preventDefault(); $('compose').requestSubmit(); } });
    input.addEventListener('input', () => { $('count').hidden = input.value.length < 400; $('count').textContent = `${input.value.length}/500`; });
    let selection = 0; input.addEventListener('select', () => selection = input.selectionStart); input.addEventListener('blur', () => selection = input.selectionStart);
    $('emoji-toggle').onclick = () => { $('emojis').hidden = !$('emojis').hidden; $('emoji-toggle').setAttribute('aria-expanded', String(!$('emojis').hidden)); };
    for (const emoji of ['😀','😍','😂','😎','🤔','👏','👍','❤️','🎉','🍀','🔥']) $('emojis').append(button(emoji, () => {
        const end = input.selectionEnd; if (input.value.length - (end - selection) + emoji.length > 500) return;
        input.setRangeText(emoji, selection, end, 'end'); selection = input.selectionStart; input.dispatchEvent(new Event('input')); input.focus(); $('emojis').hidden = true; $('emoji-toggle').setAttribute('aria-expanded', 'false');
    }, `Chèn ${emoji}`));
    $('rename').onclick = () => { $('name-form').hidden = !$('name-form').hidden; $('name-input').value = person?.name || ''; $('name-input').focus(); };
    $('name-form').onsubmit = async e => { e.preventDefault(); const renamed = await invoke('Rename', $('name-input').value); if (renamed !== false) { person = renamed; $('name').textContent = `${person.avatar} ${person.name}`; $('name-form').hidden = true; error(''); input.focus(); } };
    $('unhide').onclick = () => { hidden.clear(); saveHidden(); render(); }; $('unhide').hidden = !hidden.size;
    setInterval(() => { if (connection?.state === 'Connected') invoke('Ping'); }, 25000);
    window.addEventListener('online', () => { if (initialized && connection?.state === 'Disconnected') start(); });
    function viewport() {
        if (!window.visualViewport) return;
        root.style.setProperty('--chat-vh', `${visualViewport.height}px`);
        if (matchMedia('(max-width:991.98px)').matches) {
            const keyboard = window.innerHeight - visualViewport.height - visualViewport.offsetTop;
            panel.style.bottom = keyboard > 120 ? `${keyboard + 8}px` : '';
            panel.style.maxHeight = keyboard > 120 ? `${visualViewport.height - 16}px` : '';
        } else { panel.style.bottom = ''; panel.style.maxHeight = ''; }
    }
    window.visualViewport?.addEventListener('resize', viewport); viewport();
})();
