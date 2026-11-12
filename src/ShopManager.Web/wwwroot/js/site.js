(() => {
    const completed = document.querySelector('[data-completed-cart]');
    if (!completed) return;
    try {
        const saved = JSON.parse(sessionStorage.getItem(completed.dataset.cartKey));
        if (saved?.requestId === completed.dataset.completedCart)
            sessionStorage.removeItem(completed.dataset.cartKey);
    } catch { /* Storage may be unavailable. */ }
})();
(() => {
    document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    }));
    document.getElementById('print-invoice')?.addEventListener('click', () => window.print());
})();
