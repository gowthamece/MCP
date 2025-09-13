// Bootstrap Modal Helper Functions
window.bootstrapModal = {
    show: function (modalId) {
        // Wait for Bootstrap to be loaded
        if (typeof bootstrap !== 'undefined') {
            const modalElement = document.getElementById(modalId);
            if (modalElement) {
                const modal = new bootstrap.Modal(modalElement);
                modal.show();
            }
        } else {
            // Retry after a short delay if Bootstrap isn't loaded yet
            setTimeout(() => {
                if (typeof bootstrap !== 'undefined') {
                    const modalElement = document.getElementById(modalId);
                    if (modalElement) {
                        const modal = new bootstrap.Modal(modalElement);
                        modal.show();
                    }
                } else {
                    console.error('Bootstrap is not loaded yet');
                }
            }, 100);
        }
    },
    hide: function (modalId) {
        if (typeof bootstrap !== 'undefined') {
            const modalElement = document.getElementById(modalId);
            if (modalElement) {
                const modal = bootstrap.Modal.getInstance(modalElement);
                if (modal) {
                    modal.hide();
                }
            }
        }
    }
};
