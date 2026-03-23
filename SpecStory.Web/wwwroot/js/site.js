// SpecStory Web - Client-side JavaScript

(function () {
    'use strict';

    // Auto-dismiss alerts after 5 seconds
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            bsAlert.close();
        }, 5000);
    });

    // Highlight active nav link
    var currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navbar-nav .nav-link').forEach(function (link) {
        var href = link.getAttribute('href');
        if (href && currentPath.startsWith(href.toLowerCase()) && href !== '/') {
            link.classList.add('active');
        } else if (href === '/' && currentPath === '/') {
            link.classList.add('active');
        }
    });

    // Copy session ID to clipboard
    document.querySelectorAll('[data-copy]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var text = this.getAttribute('data-copy');
            navigator.clipboard.writeText(text).then(function () {
                btn.innerHTML = '<i class="bi bi-check"></i> Copied!';
                setTimeout(function () {
                    btn.innerHTML = '<i class="bi bi-clipboard"></i> Copy';
                }, 2000);
            });
        });
    });

    // Confirm dangerous actions
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(this.getAttribute('data-confirm'))) {
                e.preventDefault();
            }
        });
    });

    // Auto-refresh dashboard stats (optional, every 30s)
    if (document.querySelector('[data-auto-refresh]')) {
        setInterval(function () {
            fetch('/api/sessions?pageSize=1')
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    var el = document.querySelector('[data-stat="total-sessions"]');
                    if (el) el.textContent = data.total;
                })
                .catch(function () { /* silent */ });
        }, 30000);
    }

    // Keyboard shortcuts
    document.addEventListener('keydown', function (e) {
        // Press 's' to focus search (when not in input)
        if (e.key === 's' && !e.ctrlKey && !e.metaKey &&
            document.activeElement.tagName !== 'INPUT' &&
            document.activeElement.tagName !== 'TEXTAREA') {
            var searchInput = document.querySelector('input[name="search"]');
            if (searchInput) {
                e.preventDefault();
                searchInput.focus();
            }
        }
    });
})();
