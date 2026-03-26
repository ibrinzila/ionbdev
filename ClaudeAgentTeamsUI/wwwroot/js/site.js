// Claude Agent Teams UI - SignalR + Real-time Features

(function () {
    'use strict';

    // SignalR Connection
    let connection = null;

    function initSignalR() {
        if (typeof signalR === 'undefined') return;

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/team')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .build();

        connection.on('TeamUpdated', function (teamId) {
            console.log('Team updated:', teamId);
            // Refresh if we're on the dashboard or team page
            if (window.location.pathname === '/' || window.location.pathname.includes('/Teams/')) {
                showToast('Team updated', 'info');
            }
        });

        connection.on('TaskUpdated', function (task) {
            console.log('Task updated:', task);
            showToast('Task "' + (task.title || 'Unknown') + '" updated', 'info');
        });

        connection.on('TaskMoved', function (task) {
            console.log('Task moved:', task);
            // Could dynamically move the card, but for simplicity, show a toast
            showToast('Task moved to ' + task.status, 'info');
        });

        connection.on('MessageReceived', function (msg) {
            console.log('Message received:', msg);
            updateNotificationCount();
            showToast('New message from ' + msg.fromMember, 'primary');
        });

        connection.on('NotificationReceived', function (notif) {
            console.log('Notification:', notif);
            updateNotificationCount();
            showToast(notif.title, 'warning');
        });

        connection.on('ProvisioningProgress', function (state) {
            console.log('Provisioning progress:', state);
        });

        connection.start()
            .then(function () {
                console.log('SignalR connected');
                // Join dashboard group
                connection.invoke('JoinDashboard');
            })
            .catch(function (err) {
                console.warn('SignalR connection failed:', err);
            });
    }

    // Notification badge updates
    function updateNotificationCount() {
        fetch('/api/NotificationsApi/unread-count')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var badge = document.getElementById('notif-count');
                if (badge) {
                    if (data.count > 0) {
                        badge.textContent = data.count;
                        badge.style.display = '';
                    } else {
                        badge.style.display = 'none';
                    }
                }
            })
            .catch(function () { });
    }

    // Toast notifications
    function showToast(message, type) {
        var container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1090';
            document.body.appendChild(container);
        }

        var toast = document.createElement('div');
        toast.className = 'toast show border-' + (type || 'info');
        toast.setAttribute('role', 'alert');
        toast.innerHTML =
            '<div class="toast-body d-flex align-items-center gap-2">' +
            '<span>' + message + '</span>' +
            '<button type="button" class="btn-close btn-close-white ms-auto" onclick="this.closest(\'.toast\').remove()"></button>' +
            '</div>';
        container.appendChild(toast);

        setTimeout(function () {
            if (toast.parentNode) toast.remove();
        }, 5000);
    }

    // Health display
    function updateHealthDisplay() {
        var el = document.getElementById('health-display');
        if (el) {
            var now = new Date();
            el.textContent = now.toLocaleTimeString();
        }
    }

    // Initialize
    document.addEventListener('DOMContentLoaded', function () {
        initSignalR();
        updateNotificationCount();
        updateHealthDisplay();
        setInterval(updateHealthDisplay, 1000);
        setInterval(updateNotificationCount, 30000);
    });
})();
