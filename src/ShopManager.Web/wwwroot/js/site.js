(() => {
    const completed = document.querySelector('[data-completed-cart]');
    if (!completed) return;
    try {
        const saved = JSON.parse(sessionStorage.getItem(completed.dataset.cartKey));
        if (saved?.requestId !== completed.dataset.completedCart) return;
        const purchased = new Set(completed.dataset.purchasedProducts.split(',').map(Number));
        saved.lines = saved.lines.filter(line => !purchased.has(line.productId));
        sessionStorage.setItem(completed.dataset.cartKey, JSON.stringify(saved));
    } catch { /* Storage may be unavailable. */ }
})();
(() => {
    document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    }));
    document.getElementById('print-invoice')?.addEventListener('click', () => window.print());
})();
