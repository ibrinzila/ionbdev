/**
 * ClawTeam Live Board — SSE-powered real-time dashboard.
 */
function initBoard(teamName) {
    const evtSource = new EventSource(`/Board/Events/${teamName}`);

    evtSource.onmessage = function (event) {
        try {
            const data = JSON.parse(event.data);
            if (data.error) {
                console.error('Board error:', data.error);
                return;
            }
            renderBoard(data);
        } catch (e) {
            console.error('Parse error:', e);
        }
    };

    evtSource.onerror = function () {
        console.warn('SSE connection lost, will retry...');
    };
}

function renderBoard(data) {
    // Update stats
    const ts = data.taskSummary || {};
    setText('stat-total', ts.total || 0);
    setText('stat-pending', ts.pending || 0);
    setText('stat-progress', ts.inProgress || 0);
    setText('stat-completed', ts.completed || 0);
    setText('stat-blocked', ts.blocked || 0);

    // Update kanban columns
    const tasks = data.tasks || {};
    renderTasks('tasks-pending', 'count-pending', tasks.pending || []);
    renderTasks('tasks-progress', 'count-progress', tasks.in_progress || tasks.inProgress || []);
    renderTasks('tasks-completed', 'count-completed', tasks.completed || []);
    renderTasks('tasks-blocked', 'count-blocked', tasks.blocked || []);

    // Update members
    renderMembers(data.members || [], data.team?.leadAgentId);

    // Update messages
    renderMessages(data.messages || []);
}

function renderTasks(containerId, countId, tasks) {
    const container = document.getElementById(containerId);
    const countEl = document.getElementById(countId);
    if (!container) return;

    if (countEl) countEl.textContent = tasks.length;

    container.innerHTML = tasks.map(t => `
        <div class="task-card">
            <div class="task-card-subject">${esc(t.subject)}</div>
            <div class="task-card-meta">
                <span class="task-priority priority-${t.priority || 'medium'}">${t.priority || 'medium'}</span>
                ${t.owner ? `<span>&middot; ${esc(t.owner)}</span>` : ''}
            </div>
        </div>
    `).join('');
}

function renderMembers(members, leadAgentId) {
    const container = document.getElementById('members-list');
    if (!container) return;

    container.innerHTML = members.map(m => {
        const initials = (m.name || '?').substring(0, 2).toUpperCase();
        const isLeader = m.agentId === leadAgentId;
        const inboxBadge = m.inboxCount > 0
            ? `<span class="inbox-badge">${m.inboxCount}</span>` : '';

        return `
            <div class="member-badge">
                <div class="member-avatar">${initials}</div>
                <div>
                    <div class="member-name">
                        ${esc(m.name)}
                        ${isLeader ? '<span style="color:var(--yellow)">(leader)</span>' : ''}
                    </div>
                    <div class="member-type">${esc(m.agentType || '')}</div>
                </div>
                ${inboxBadge}
            </div>
        `;
    }).join('');
}

function renderMessages(messages) {
    const container = document.getElementById('messages-list');
    if (!container) return;

    const recent = messages.slice(-30);
    if (recent.length === 0) {
        container.innerHTML = '<div class="empty-state" style="padding:1rem;"><p>No messages yet.</p></div>';
        return;
    }

    container.innerHTML = recent.map(msg => `
        <div class="message-item">
            <span class="message-from">${esc(msg.from || msg.fromLabel || '?')}</span>
            <span class="message-content">${esc(msg.content || '')}</span>
            <span class="message-type">${esc(msg.type || 'chat')}</span>
            <span class="message-time">${formatTime(msg.timestamp)}</span>
        </div>
    `).join('');
}

function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
}

function esc(str) {
    const div = document.createElement('div');
    div.textContent = str || '';
    return div.innerHTML;
}

function formatTime(ts) {
    if (!ts) return '';
    try {
        const d = new Date(ts);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch {
        return ts;
    }
}
