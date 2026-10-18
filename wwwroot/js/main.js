document.addEventListener('DOMContentLoaded', function () {
    const mobileMenuToggle = document.getElementById('mobile-menu-toggle');
    const mobileNavigation = document.getElementById('mobile-navigation');

    if (mobileMenuToggle && mobileNavigation) {
        const closeMobileMenu = () => {
            mobileNavigation.hidden = true;
            mobileMenuToggle.setAttribute('aria-expanded', 'false');
            document.body.classList.remove('mobile-menu-open');
            mobileMenuToggle.focus();
        };

        mobileMenuToggle.addEventListener('click', function () {
            mobileNavigation.hidden = false;
            mobileMenuToggle.setAttribute('aria-expanded', 'true');
            document.body.classList.add('mobile-menu-open');
            mobileNavigation.querySelector('.mobile-navigation-close').focus();
        });

        mobileNavigation.addEventListener('click', function (event) {
            if (event.target.closest('[data-close-mobile-menu]')) {
                closeMobileMenu();
            }
        });

        mobileNavigation.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                closeMobileMenu();
                return;
            }

            if (event.key !== 'Tab') {
                return;
            }

            const focusableElements = Array.from(mobileNavigation.querySelectorAll('a[href], button:not([disabled])'));
            const firstElement = focusableElements[0];
            const lastElement = focusableElements[focusableElements.length - 1];

            if (event.shiftKey && document.activeElement === firstElement) {
                event.preventDefault();
                lastElement.focus();
            } else if (!event.shiftKey && document.activeElement === lastElement) {
                event.preventDefault();
                firstElement.focus();
            }
        });

        window.matchMedia('(min-width: 992px)').addEventListener('change', function (event) {
            if (event.matches && !mobileNavigation.hidden) {
                closeMobileMenu();
            }
        });
    }

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

    const liveResults = document.getElementById('live-results');

    if (liveResults && liveResults.dataset.socketUrl) {
        const liveResultsGrid = document.getElementById('live-results-grid');
        const liveResultsStatus = document.getElementById('live-results-status');
        const connectionStatus = document.getElementById('live-connection-status');
        const liveSocketUrl = liveResults.dataset.socketUrl;
        const regionNames = { '1': 'Miền Bắc', '2': 'Miền Nam', '3': 'Miền Trung' };
        const reconnectDelay = 5000;
        let socket;
        let reconnectTimer;
        let lastMessage = '';
        let lastMessageAt = 0;
        let reconnectAttempt = 0;
        let unloading = false;
        const setConnectionStatus = text => {
            if (connectionStatus) connectionStatus.textContent = text;
        };
        const scheduleReconnect = () => {
            if (unloading) return;
            clearTimeout(reconnectTimer);
            const delay = Math.min(60000, reconnectDelay * (2 ** Math.min(reconnectAttempt++, 4)));
            reconnectTimer = window.setTimeout(connectLiveSocket, delay + Math.random() * 1000);
        };

        const escapeHtml = value => String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');

        const prizeLayouts = {
            '1': {
                labels: ['ĐB', 'nhất', 'nhì', 'ba', 'tư', 'năm', 'sáu', 'bảy'],
                counts: [1, 1, 2, 6, 4, 6, 3, 4],
                digits: [5, 5, 5, 5, 4, 4, 3, 2]
            },
            '2': {
                labels: ['G8', 'G7', 'G6', 'G5', 'G4', 'G3', 'G2', 'G1', 'ĐB'],
                counts: [1, 1, 3, 1, 5, 2, 1, 1, 1],
                digits: [2, 3, 4, 4, 5, 5, 5, 5, 6]
            },
            '3': {
                labels: ['G8', 'G7', 'G6', 'G5', 'G4', 'G3', 'G2', 'G1', 'ĐB'],
                counts: [1, 1, 3, 1, 5, 2, 1, 1, 1],
                digits: [2, 3, 4, 4, 5, 5, 5, 5, 6]
            }
        };

        const renderNumberCell = (values, expectedCount, digitCount, isDrawing) => {
            const numbers = values.slice(0, expectedCount);
            const missingCount = Math.max(expectedCount - numbers.length, 0);
            return numbers.map(value => `<span>${escapeHtml(value)}</span>`).join('') +
                Array.from({ length: missingCount }, (_, slot) =>
                    isDrawing && slot === 0
                    ? `<span class="live-number-loader" role="img" aria-label="Đang quay" title="Đang quay">${Array.from({ length: digitCount }, (_, index) =>
                        `<i aria-hidden="true"><b style="animation-delay: -${(index * .13 + Math.random()).toFixed(2)}s">${Array.from({ length: 11 }, (_, digit) => `<em>${digit % 10}</em>`).join('')}</b></i>`
                    ).join('')}</span>`
                    : isDrawing ? '' : '<span class="live-number-waiting" role="img" aria-label="Chờ quay" title="Chờ quay"><i aria-hidden="true"></i><i aria-hidden="true"></i><i aria-hidden="true"></i></span>'
                ).join('');
        };

        const renderHeadStatistics = (records, isNorthern) => {
            const columns = records.map(record => {
                const heads = Array.from({ length: 10 }, () => []);
                const tails = Array.from({ length: 10 }, () => []);
                record.values.flat().forEach(value => {
                    const number = String(value).replace(/\D/g, '');
                    if (number.length < 2) {
                        return;
                    }

                    const suffix = number.slice(-2);
                    heads[Number(suffix[0])].push(suffix);
                    tails[Number(suffix[1])].push(suffix);
                });

                return { record, heads, tails };
            });

            if (isNorthern) {
                const column = columns[0] || { heads: [], tails: [] };
                return `
                    <section class="live-statistics">
                        <div class="regional-loto-links">
                            <a href="#">Bảng loto Miền Bắc</a>
                            <span>/</span>
                            <a href="#">Lô Miền Bắc hôm nay</a>
                        </div>
                        <table class="loto-statistics-table">
                            <thead><tr><th>Đầu</th><th>Đuôi</th><th>Đầu</th><th>Đuôi</th></tr></thead>
                            <tbody>
                                ${Array.from({ length: 10 }, (_, digit) => `
                                    <tr>
                                        <th>${digit}</th>
                                        <td>${column.heads[digit]?.length ? column.heads[digit].join(',') : '-'}</td>
                                        <td>${column.tails[digit]?.length ? column.tails[digit].join(',') : '-'}</td>
                                        <th>${digit}</th>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </section>
                `;
            }

            return `
                <section class="live-statistics">
                    <div class="live-statistics-heading">
                        <h4>Thống kê đầu số</h4>
                        <span>Hai số cuối các giải đã quay</span>
                    </div>
                    <div class="lottery-table-wrap">
                        <table class="regional-loto-table live-statistics-table">
                            <thead>
                                <tr>
                                    <th>Đầu</th>
                                    ${columns.map(({ record }) => `<th>${escapeHtml(record.name)}</th>`).join('')}
                                </tr>
                            </thead>
                            <tbody>
                                ${Array.from({ length: 10 }, (_, digit) => `
                                    <tr>
                                        <th>${digit}</th>
                                        ${columns.map(({ heads }) => `<td>${heads[digit].length ? heads[digit].join(' - ') : '-'}</td>`).join('')}
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </div>
                </section>
            `;
        };

        const renderLiveTable = group => {
            const layout = prizeLayouts[group.region] || prizeLayouts['1'];
            const isNorthern = group.region === '1';
            // Northern draws start at first prize and finish with the special prize.
            const drawOrder = isNorthern ? [1, 2, 3, 4, 5, 6, 7, 0] : [0, 1, 2, 3, 4, 5, 6, 7, 8];
            const activePrizes = group.records.map(record => drawOrder.find(index =>
                (record.values[index]?.length || 0) < layout.counts[index]
            ));
            const completedPrizes = layout.labels.filter((label, index) =>
                group.records.some(record => record.values[index]?.length > 0)
            ).length;
            const totalPrizes = layout.labels.length;
            const headings = isNorthern ? '' : `<thead><tr><th class="prize-label">G</th>${group.records.map(record =>
                `<th>${escapeHtml(record.name)}</th>`).join('')}</tr></thead>`;
            const rows = layout.labels.map((label, index) => {
                const isSpecial = label === 'ĐB';
                const isRedPrize = isSpecial || (isNorthern ? label === 'bảy' : label === 'G8');
                const cells = isNorthern
                    ? `<td><div class="mb-prize-numbers number-count-${layout.counts[index]}">${renderNumberCell(group.records[0]?.values[index] || [], layout.counts[index], layout.digits[index], activePrizes[0] === index)}</div></td>`
                    : group.records.map((record, recordIndex) =>
                        `<td>${renderNumberCell(record.values[index] || [], layout.counts[index], layout.digits[index], activePrizes[recordIndex] === index)}</td>`
                    ).join('');

                return `<tr class="${isSpecial ? 'special-row' : ''} ${isRedPrize ? 'red-prize-row' : ''}"><th class="prize-label">${label}</th>${cells}</tr>`;
            }).join('');
            const northernHeader = isNorthern ? `
                <header class="region-detail-header live-region-detail-header">
                    <h2>XSMB - Kết quả Xổ số Miền Bắc trực tiếp</h2>
                    <div class="region-detail-links">
                        <a href="#">XSMB</a>
                        <span>/</span>
                        <a href="#">Đang quay</a>
                        <span>/</span>
                        <span>${escapeHtml(group.records[0].name)}</span>
                    </div>
                </header>
            ` : '';
            const liveSummary = isNorthern ? '' : `
                <div class="live-region-heading">
                    <h3>${group.name}</h3>
                    <span class="live-region-stat">${completedPrizes}/${totalPrizes} giải đã có số</span>
                </div>
            `;

            return `
                <section class="live-region" data-region="${group.name}">
                    ${northernHeader}
                    ${liveSummary}
                    <div class="lottery-table-wrap live-table-wrap">
                        <table class="regional-results-table ${isNorthern ? 'mb-results-table' : ''}">
                            ${headings}
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                    ${renderHeadStatistics(group.records, isNorthern)}
                </section>
            `;
        };

        const renderLiveResults = message => {
            if (message.startsWith('1!')) {
                message = message.slice(2).trim();
            }

            const parts = message.split('!').map(part => part.trim());
            const control = parts.shift().split('|');
            const region = control[1] || control[0] || '1';
            if (parts.length === 0) {
                return;
            }

            const records = parts.map(record => {
                const separatorIndex = record.indexOf('@');
                const header = (separatorIndex < 0 ? record : record.slice(0, separatorIndex)).split('|');
                const values = (separatorIndex < 0 ? '' : record.slice(separatorIndex + 1)).split('|').map(value => {
                    const numbers = value.trim();
                    return numbers ? numbers.split('-').filter(Boolean) : [];
                });

                return {
                    region,
                    code: header[2] || '',
                    name: header[3] || header[2] || 'Đài',
                    values
                };
            }).filter(Boolean);

            if (!records.length) {
                return;
            }

            const groupedRecords = ['1', '2', '3'].map(region => ({
                region,
                name: regionNames[region],
                records: records.filter(record => record.region === region)
            })).filter(group => group.records.length > 0);

            liveResultsGrid.innerHTML = groupedRecords.map(renderLiveTable).join('');

            liveResults.hidden = false;
            liveResultsStatus.textContent = `${records.length} đài đang cập nhật kết quả`;
        };

        const handleMessage = async event => {
            const message = typeof event.data === 'string'
                ? event.data.trim()
                : (event.data instanceof Blob ? (await event.data.text()).trim() : '');
            if (!message) {
                return;
            }

            lastMessageAt = Date.now();
            setConnectionStatus('Đang nhận dữ liệu trực tiếp.');
            if (message === lastMessage) return;
            lastMessage = message;
            renderLiveResults(message);
        };

        const connectLiveSocket = () => {
            try {
                socket = new WebSocket(liveSocketUrl);
            } catch (error) {
                console.error('Không thể khởi tạo live socket:', error);
                scheduleReconnect();
                return;
            }

            socket.addEventListener('open', () => {
                reconnectAttempt = 0;
                setConnectionStatus('Đã kết nối, đang chờ dữ liệu quay số.');
                liveResultsStatus.textContent = 'Đã kết nối, đang chờ dữ liệu quay số';
            });
            socket.addEventListener('message', handleMessage);
            socket.addEventListener('close', () => {
                clearTimeout(reconnectTimer);
                liveResultsStatus.textContent = 'Mất kết nối, đang thử kết nối lại';
                setConnectionStatus('Mất kết nối trực tiếp, đang thử lại. Bảng kết quả đã lưu vẫn có thể tra cứu.');
                scheduleReconnect();
            });
            socket.addEventListener('error', () => {
                liveResultsStatus.textContent = 'Không kết nối được máy chủ kết quả';
                socket.close();
            });
        };

        connectLiveSocket();
        const staleTimer = window.setInterval(() => {
            if (lastMessageAt && Date.now() - lastMessageAt > 120000 && socket?.readyState === WebSocket.OPEN)
                setConnectionStatus('Chưa nhận dữ liệu trực tiếp mới trong 2 phút. Nguồn có thể chậm hoặc tạm ngừng gửi.');
        }, 30000);
        window.addEventListener('pagehide', () => {
            unloading = true;
            clearInterval(staleTimer);
            clearTimeout(reconnectTimer);
            if (socket) socket.close();
        });
        window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload(); });

        // Preview live result locally. Uncomment these two lines to preview the table.
        //const previewLiveMessage = '0|3!1|32|TTH|Huế @96|507|2795-7931-0655|0606|28539-32978-83459-36360-49928-34381-31694|69178-83885|33140|69172|616814|!1|33|PY|Phú Yên@90|062|6272-4853-0247|9267|85789-93792-51450-37719-07609-92709-62869|22837-38481|50811|81505|342609|';
        //const previewLiveMessage = '0|2!1|13|DT|Đồng Tháp@11|751|9972-0566-8459|8148|81617-06900-04114-95223-48831-72956-22747|67116-91664|99985|08519|728409|!1|14|HCM|TPHCM @98|515|4687-2189-8217|2342|99561-65057-22017-51078-64544-16942-65052|97915-04692|09263|13891|061923|!1|15|CM|Cà Mau@63|335|2187-7933-7778|5563|95280-77906-24414-99378-57105-71759-39239|46687-12541|79042|15863|264432|';
        //const previewLiveMessage = '0|1!0|46|TD|Hà Nội@|70592|57489-68602|86718-84988-37198-60351-72717-29855|0529-0808-7371-8648|3457-1096-4779-9172-9648|||';
        //renderLiveResults(previewLiveMessage);
    }
});
