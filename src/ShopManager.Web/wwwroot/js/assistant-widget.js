(() => {
    const widget = document.getElementById('assistant-widget');
    if (!widget) return;

    const bubble = widget.querySelector('.assistant-bubble');
    const panel = widget.querySelector('.assistant-panel');
    const close = widget.querySelector('.assistant-close');
    const form = widget.querySelector('.assistant-form');
    const question = form.elements.question;
    const submit = form.querySelector('button');
    const messages = widget.querySelector('.assistant-messages');
    const positionKey = 'shopmanager-assistant-position';
    const margin = 12;
    let drag;
    let ignoreClick = false;
    let customPosition = false;
    let previousTurn;

    function placePanel() {
        if (panel.hidden) return;
        const button = bubble.getBoundingClientRect();
        const width = panel.offsetWidth;
        const height = panel.offsetHeight;
        const above = button.top - height - 10;
        const top = above >= margin ? above : button.bottom + 10;
        panel.style.left = Math.max(margin, Math.min(innerWidth - width - margin, button.right - width)) + 'px';
        panel.style.top = Math.max(margin, Math.min(innerHeight - height - margin, top)) + 'px';
    }

    function moveBubble(left, top) {
        bubble.style.right = 'auto';
        bubble.style.bottom = 'auto';
        bubble.style.left = Math.max(margin, Math.min(innerWidth - bubble.offsetWidth - margin, left)) + 'px';
        bubble.style.top = Math.max(margin, Math.min(innerHeight - bubble.offsetHeight - margin, top)) + 'px';
        customPosition = true;
        placePanel();
    }

    function savePosition() {
        try {
            const { left, top } = bubble.getBoundingClientRect();
            localStorage.setItem(positionKey, JSON.stringify({ left, top }));
        } catch { /* Position storage is optional. */ }
    }

    try {
        const saved = JSON.parse(localStorage.getItem(positionKey));
        if (Number.isFinite(saved?.left) && Number.isFinite(saved?.top)) moveBubble(saved.left, saved.top);
    } catch { /* Start in the default corner. */ }

    function setOpen(open) {
        panel.hidden = !open;
        bubble.setAttribute('aria-expanded', String(open));
        if (open) {
            placePanel();
            question.focus({ preventScroll: true });
        } else bubble.focus({ preventScroll: true });
    }

    bubble.addEventListener('pointerdown', event => {
        if (!event.isPrimary || event.button !== 0) return;
        ignoreClick = false;
        const rect = bubble.getBoundingClientRect();
        drag = { id: event.pointerId, x: event.clientX, y: event.clientY, left: rect.left, top: rect.top, moved: false };
        bubble.setPointerCapture(event.pointerId);
    });
    bubble.addEventListener('pointermove', event => {
        if (drag?.id !== event.pointerId) return;
        const dx = event.clientX - drag.x;
        const dy = event.clientY - drag.y;
        if (!drag.moved && Math.hypot(dx, dy) < 6) return;
        drag.moved = true;
        moveBubble(drag.left + dx, drag.top + dy);
    });
    function finishDrag(event) {
        if (drag?.id !== event.pointerId) return;
        ignoreClick = drag.moved;
        if (drag.moved) savePosition();
        drag = undefined;
    }
    bubble.addEventListener('pointerup', finishDrag);
    bubble.addEventListener('pointercancel', finishDrag);
    bubble.addEventListener('lostpointercapture', finishDrag);
    bubble.addEventListener('click', event => {
        if (ignoreClick && event.detail !== 0) {
            ignoreClick = false;
            return;
        }
        setOpen(panel.hidden);
    });
    bubble.addEventListener('keydown', event => {
        const deltas = { ArrowLeft: [-20, 0], ArrowRight: [20, 0], ArrowUp: [0, -20], ArrowDown: [0, 20] };
        const delta = deltas[event.key];
        if (!delta) return;
        event.preventDefault();
        const rect = bubble.getBoundingClientRect();
        moveBubble(rect.left + delta[0], rect.top + delta[1]);
        savePosition();
    });
    close.addEventListener('click', () => setOpen(false));
    widget.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !panel.hidden) setOpen(false);
    });
    window.addEventListener('resize', () => {
        if (customPosition) {
            const { left, top } = bubble.getBoundingClientRect();
            moveBubble(left, top);
        } else placePanel();
    });

    function addMessage(text, style = '') {
        const message = document.createElement('p');
        message.className = 'assistant-message' + style;
        message.textContent = text;
        messages.append(message);
        messages.scrollTop = messages.scrollHeight;
        return message;
    }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        const text = question.value.trim();
        if (!text || submit.disabled) return;

        const payload = new URLSearchParams(new FormData(form));
        payload.set('question', text);
        if (previousTurn) {
            payload.set('previousQuestion', previousTurn.question);
            payload.set('previousAnswer', previousTurn.answer);
        }
        addMessage(text, ' assistant-message-user');
        const reply = addMessage('Đang trả lời…');
        question.value = '';
        submit.disabled = true;
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: payload, headers: { Accept: 'application/json' }
            });
            if (response.redirected) {
                reply.textContent = 'Phiên đăng nhập đã hết. Tải lại trang và đăng nhập để tiếp tục.';
            } else if (response.status === 429) {
                reply.textContent = 'Trợ lý đang bận. Hãy gửi lại sau.';
            } else {
                const result = await response.json();
                reply.textContent = response.ok ? result.answer : result.error;
                if (response.ok) previousTurn = { question: text, answer: result.answer.slice(0, 2000) };
            }
        } catch {
            reply.textContent = 'Chưa kết nối được với trợ lý. Hãy thử lại.';
        } finally {
            submit.disabled = false;
            messages.scrollTop = messages.scrollHeight;
        }
    });
})();
