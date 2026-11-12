(() => {
    const catalog = JSON.parse(document.getElementById('catalog-data').textContent);
    const products = new Map(catalog.map(p => [p.id, p]));
    const form = document.getElementById('checkout-form');
    const cartKey = form.dataset.cartKey;
    const guest = form.dataset.guest === 'true';
    const selectItems = form.dataset.selectItems === 'true';
    const guestKey = 'shopmanager-cart:guest';
    const requestId = document.getElementById('RequestId').value;
    const cart = new Map();
    const initial = JSON.parse(document.getElementById('initial-cart').textContent);
    const saved = key => JSON.parse(sessionStorage.getItem(key))?.lines ?? [];
    const restore = lines => {
        for (const line of lines) {
            const product = products.get(line.productId);
            if (!product || !Number.isSafeInteger(line.quantity) || line.quantity < 1) continue;
            cart.set(line.productId, {
                quantity: Math.min(product.stock, 1000000, line.quantity),
                expectedPrice: product.price,
                selected: line.selected !== false
            });
        }
    };
    if (initial.length) {
        if (cartKey && selectItems) {
            try {
                restore(saved(cartKey));
                for (const line of cart.values()) line.selected = false;
            } catch { /* Storage may be unavailable. */ }
        }
        restore(initial.map(line => ({ ...line, selected: true })));
    }
    else if (cartKey) {
        try {
            restore(saved(cartKey));
            if (!guest) {
                for (const line of saved(guestKey)) {
                    const existing = cart.get(line.productId);
                    if (existing) existing.quantity = Math.min(products.get(line.productId).stock, 1000000, existing.quantity + line.quantity);
                    else restore([line]);
                }
                sessionStorage.removeItem(guestKey);
            }
        } catch { /* Storage may be unavailable. */ }
    }
    const money = value => new Intl.NumberFormat('vi-VN').format(value) + ' ₫';
    const selectedLines = () => [...cart].filter(([, line]) => !selectItems || line.selected);
    const total = () => selectedLines().reduce((sum, [id, line]) => sum + products.get(id).price * line.quantity, 0);
    const voucher = document.getElementById('VoucherCode');
    function updateTotal() {
        const subtotal = total();
        const discount = voucher && voucher.value.trim().toUpperCase() === voucher.dataset.code
            ? Math.min(Number(voucher.dataset.limit), Math.floor(subtotal * Number(voucher.dataset.percent) / 100))
            : 0;
        if (voucher) document.getElementById('voucher-discount').textContent = money(discount);
        document.getElementById('cart-total').textContent = money(subtotal - discount);
    }
    voucher?.addEventListener('input', updateTotal);
    const cash = document.getElementById('CashReceived');
    const error = document.getElementById('cart-error');
    const input = (name, value) => {
        const el = document.createElement('input'); el.type = 'hidden'; el.name = name; el.value = value; return el;
    };
    const text = (tag, value, cls) => {
        const el = document.createElement(tag); el.textContent = value; if (cls) el.className = cls; return el;
    };
    function render() {
        const root = document.getElementById('cart-items'); root.replaceChildren();
        const fields = document.getElementById('cart-inputs'); fields.replaceChildren();
        if (!cart.size) {
            const empty = text('div', '', 'empty-state compact');
            empty.append(text('span', '▱'), text('h3', 'Giỏ hàng đang trống'), text('p', 'Thêm sản phẩm từ trang mua hàng để bắt đầu.'));
            root.append(empty);
        }
        let index = 0;
        for (const [id, line] of cart) {
            const p = products.get(id), row = text('div', '', 'cart-line');
            const info = text('div', '', 'cart-line-info'); info.append(text('strong', p.name), text('small', money(p.price)));
            const controls = text('div', '', 'quantity-controls');
            const minus = text('button', '−'), plus = text('button', '+'), remove = text('button', '×', 'remove-item');
            minus.type = plus.type = remove.type = 'button';
            minus.setAttribute('aria-label', 'Giảm số lượng ' + p.name); plus.setAttribute('aria-label', 'Tăng số lượng ' + p.name);
            remove.setAttribute('aria-label', 'Xóa ' + p.name);
            minus.onclick = () => { if (--line.quantity <= 0) cart.delete(id); render(); };
            plus.onclick = () => { if (line.quantity < p.stock) { line.quantity++; render(); } else error.textContent = 'Số lượng đã bằng tồn kho hiện tại.'; };
            remove.onclick = () => { cart.delete(id); render(); };
            controls.append(minus, text('span', line.quantity), plus);
            if (selectItems) {
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = line.selected;
                checkbox.setAttribute('aria-label', 'Chọn mua ' + p.name);
                checkbox.onchange = () => {
                    line.selected = checkbox.checked;
                    render();
                    root.querySelectorAll('input[type="checkbox"]')[Array.from(cart.keys()).indexOf(id)]?.focus();
                };
                row.append(checkbox);
            }
            row.append(info, controls, text('b', money(p.price * line.quantity)), remove); root.append(row);
            if (!selectItems || line.selected) { fields.append(input(`Lines[${index}].ProductId`, id), input(`Lines[${index}].Quantity`, line.quantity), input(`Lines[${index}].ExpectedPrice`, line.expectedPrice)); index++; }
        }
        const itemCount = [...cart.values()].reduce((sum, line) => sum + line.quantity, 0);
        document.getElementById('cart-count').textContent = itemCount;
        const cartLinkCount = document.getElementById('cart-link-count');
        if (cartLinkCount) cartLinkCount.textContent = itemCount;
        const headerCount = document.getElementById('shop-cart-count');
        if (headerCount) {
            headerCount.textContent = itemCount;
            headerCount.hidden = itemCount === 0;
        }
        updateTotal();
        const checkout = document.getElementById('checkout-button');
        if (checkout) checkout.disabled = selectedLines().length === 0;
        if (cartKey) {
            try {
                const lines = [...cart].map(([productId, line]) => ({ productId, ...line }));
                sessionStorage.setItem(cartKey, JSON.stringify({ requestId, lines }));
            } catch { /* Storage may be unavailable. */ }
        }
        error.textContent = ''; updateChange();
    }
    function updateChange() {
        if (!cash) return;
        const change = Number(cash.value || 0) - total();
        document.getElementById('cash-change').textContent = change >= 0 ? money(change) : 'Thiếu ' + money(-change);
    }
    const notice = document.getElementById('shop-cart-notice');
    let noticeTimer;
    function announce(message) {
        if (!notice) return;
        notice.hidden = false;
        notice.textContent = message;
        clearTimeout(noticeTimer);
        noticeTimer = setTimeout(() => { notice.hidden = true; }, 3000);
    }
    document.querySelectorAll('button.catalog-card, [data-add-product]').forEach(button => button.onclick = () => {
        const id = Number(button.dataset.addProduct ?? button.dataset.id), p = products.get(id), line = cart.get(id);
        if (line && line.quantity >= p.stock) {
            const message = p.name + ' không đủ tồn kho.';
            error.textContent = message;
            announce(message);
            return;
        }
        cart.set(id, { quantity: (line?.quantity || 0) + 1, expectedPrice: p.price, selected: true }); render();
        announce('Đã thêm ' + p.name + ' vào giỏ hàng.');
    });
    const normalize = value => value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    const search = document.getElementById('product-search');
    const category = document.getElementById('category-filter');
    function filterProducts() {
        const query = normalize(search.value);
        let count = 0;
        document.querySelectorAll('.catalog-card').forEach(card => {
            const entry = card.closest('.catalog-entry');
            entry.hidden = !normalize(card.dataset.search).includes(query)
                || Boolean(category?.value && category.value !== card.dataset.category);
            if (!entry.hidden) count++;
        });
        document.getElementById('product-count').textContent = count + ' sản phẩm';
        document.getElementById('no-products').hidden = count > 0;
    }
    search.addEventListener('input', filterProducts);
    category?.addEventListener('change', filterProducts);
    document.getElementById('clear-cart').onclick = () => { cart.clear(); render(); };
    if (cash) {
        document.getElementById('exact-cash').onclick = () => { cash.value = total(); updateChange(); };
        cash.addEventListener('input', updateChange);
    }
    form.addEventListener('submit', event => {
        if (guest) { event.preventDefault(); return; }
        if (!selectedLines().length || (cash && Number(cash.value) < total())) {
            event.preventDefault();
            error.textContent = selectedLines().length ? 'Tiền khách đưa phải đủ.' : 'Chọn ít nhất một sản phẩm để đặt hàng.';
            return;
        }
        document.getElementById('checkout-button').disabled = true;
        document.getElementById('checkout-button').textContent = 'Đang lưu đơn…';
    });
    render();
})();
