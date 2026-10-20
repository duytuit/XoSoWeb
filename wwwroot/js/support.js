const supportForm = document.getElementById('supportForm');
supportForm.addEventListener('submit', async function (event) {
    event.preventDefault();
    const submitButton = this.querySelector('button[type="submit"]');
    if (submitButton.disabled) return;
    const message = document.getElementById('message');
    message.textContent = '';
    message.className = '';
    submitButton.disabled = true;
    submitButton.textContent = 'Đang gửi…';
    const data = {
        fullName: document.getElementById('fullName').value,
        email: document.getElementById('email').value,
        subject: document.getElementById('subject').value,
        content: document.getElementById('content').value
    };
    try {
        const response = await fetch('/support', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': this.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(data)
        });
        const result = await response.json();
        if (response.ok && result?.success === true) {
            message.className = 'alert alert-success';
            message.textContent = 'Yêu cầu hỗ trợ đã được gửi thành công.';
            this.reset();
        } else {
            message.className = 'alert alert-danger';
            message.textContent = typeof result?.message === 'string' ? result.message : 'Không thể gửi yêu cầu hỗ trợ. Vui lòng thử lại sau.';
        }
    } catch {
        message.className = 'alert alert-danger';
        message.textContent = 'Chưa xác nhận được việc gửi yêu cầu. Vui lòng thử lại sau.';
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = 'Gửi yêu cầu hỗ trợ';
    }
});
