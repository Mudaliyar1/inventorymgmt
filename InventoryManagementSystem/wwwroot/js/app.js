/* ============================================================
   SIMS App JS – Sidebar, Dropdowns, Toasts, Utilities (Dark Only)
   ============================================================ */
(function () {
  'use strict';

  const byId = id => document.getElementById(id);
  const qsa  = sel => document.querySelectorAll(sel);

  /* ── Sidebar ──────────────────────────────────────── */
  function initSidebar() {
    const sidebar = byId('sidebar');
    const main    = byId('main');
    const overlay = byId('sb-overlay');
    const colBtn  = byId('sb-collapse-btn');
    const mobBtn  = byId('sb-mobile-btn');
    if (!sidebar) return;

    // Restore collapsed state on desktop
    if (window.innerWidth >= 1024 && localStorage.getItem('sims-sb-collapsed') === 'true') {
      sidebar.classList.add('collapsed');
      if (main) main.classList.add('sidebar-collapsed');
    }

    // Desktop collapse
    if (colBtn) {
      colBtn.addEventListener('click', () => {
        const c = sidebar.classList.toggle('collapsed');
        if (main) main.classList.toggle('sidebar-collapsed', c);
        localStorage.setItem('sims-sb-collapsed', c);
      });
    }

    // Mobile open
    if (mobBtn) {
      mobBtn.addEventListener('click', () => openMobile());
    }

    // Overlay close
    if (overlay) {
      overlay.addEventListener('click', closeMobile);
    }

    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeMobile(); });
  }

  function openMobile() {
    const sidebar = byId('sidebar');
    const overlay = byId('sb-overlay');
    if (sidebar) sidebar.classList.add('sb-mobile-open');
    if (overlay) overlay.classList.add('show');
    document.body.style.overflow = 'hidden';
  }

  function closeMobile() {
    const sidebar = byId('sidebar');
    const overlay = byId('sb-overlay');
    if (sidebar) sidebar.classList.remove('sb-mobile-open');
    if (overlay) overlay.classList.remove('show');
    document.body.style.overflow = '';
  }

  /* ── Dropdowns (Custom Single-Click Event Delegation) ─ */
  function initDropdowns() {
    document.addEventListener('click', function (e) {
      const toggleBtn = e.target.closest('[data-dd-toggle="dropdown"]');

      if (toggleBtn) {
        e.preventDefault();
        e.stopPropagation();

        const parent = toggleBtn.closest('.dropdown') || toggleBtn.parentElement;
        if (!parent) return;

        const menu = parent.querySelector('.dropdown-menu');
        if (!menu) return;

        const isCurrentlyOpen = menu.classList.contains('show');

        // Close all other open dropdown menus
        document.querySelectorAll('.dropdown-menu.show').forEach(m => {
          if (m !== menu) m.classList.remove('show');
        });
        document.querySelectorAll('[data-dd-toggle="dropdown"]').forEach(b => {
          if (b !== toggleBtn) b.setAttribute('aria-expanded', 'false');
        });

        // Toggle target dropdown menu cleanly
        if (isCurrentlyOpen) {
          menu.classList.remove('show');
          toggleBtn.setAttribute('aria-expanded', 'false');
        } else {
          menu.classList.add('show');
          toggleBtn.setAttribute('aria-expanded', 'true');
        }
      } else if (!e.target.closest('.dropdown-menu')) {
        // Dismiss dropdowns when clicking anywhere outside
        document.querySelectorAll('.dropdown-menu.show').forEach(m => m.classList.remove('show'));
        document.querySelectorAll('[data-dd-toggle="dropdown"]').forEach(b => b.setAttribute('aria-expanded', 'false'));
      }
    });
  }

  /* ── Tooltips ──────────────────────────────────────── */
  function initTooltips() {
    if (typeof bootstrap === 'undefined') return;
    qsa('[data-bs-toggle="tooltip"]').forEach(el => {
      try {
        new bootstrap.Tooltip(el, { placement: 'right', trigger: 'hover' });
      } catch (err) {}
    });
  }

  /* ── Date ──────────────────────────────────────────── */
  function renderDate() {
    const el = byId('topbar-date');
    if (!el) return;
    el.textContent = new Date().toLocaleDateString('en-IN', { timeZone: 'Asia/Kolkata', weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' }) + ' (IST)';
  }

  /* ── Notifications ─────────────────────────────────── */
  function loadNotifications() {
    const list  = byId('notif-list');
    const badge = byId('notif-badge');
    if (!list) return;

    fetch('/api/notifications')
      .then(r => r.ok ? r.json() : [])
      .then(data => {
        if (!data || !data.length) {
          list.innerHTML = '<div class="empty-s" style="padding:24px"><i class="bi bi-bell-slash empty-s-icon"></i><div class="empty-s-title">No notifications</div></div>';
          if (badge) badge.style.display = 'none';
          return;
        }
        if (badge) badge.style.display = 'block';
        list.innerHTML = data.slice(0, 6).map(n => {
          const cls = n.type === 'Danger' ? 'ic-red' : n.type === 'Warning' ? 'ic-amber' : 'ic-blue';
          return `<div class="dd-item" style="align-items:flex-start;gap:12px;padding:12px 14px;border-bottom:1px solid var(--border)">
            <div class="sc-icon ${cls}" style="width:32px;height:32px;font-size:14px;flex-shrink:0"><i class="bi bi-bell-fill"></i></div>
            <div><div style="font-size:13px;font-weight:600;color:var(--text-primary)">${n.title}</div>
            <div style="font-size:12px;color:var(--text-muted);margin-top:2px">${n.message}</div></div>
          </div>`;
        }).join('');
      })
      .catch(() => {
        if (list) list.innerHTML = '<div class="empty-s" style="padding:24px"><i class="bi bi-bell-slash empty-s-icon"></i><div class="empty-s-title">No notifications</div></div>';
      });
  }

  /* ── Toast ─────────────────────────────────────────── */
  const ICONS  = { success:'bi-check-circle-fill', danger:'bi-x-circle-fill', warning:'bi-exclamation-triangle-fill', info:'bi-info-circle-fill' };
  const TITLES = { success:'Success', danger:'Error', warning:'Warning', info:'Info' };

  function showToast(message, type = 'info') {
    const stack = byId('toast-stack');
    if (!stack) return;
    const el = document.createElement('div');
    el.className = `toast-item toast-${type}`;
    el.innerHTML = `
      <div class="toast-icon-w"><i class="bi ${ICONS[type]||ICONS.info}"></i></div>
      <div class="toast-body">
        <div class="toast-title">${TITLES[type]||'Notice'}</div>
        <div class="toast-msg">${message}</div>
      </div>
      <button class="toast-close" onclick="this.closest('.toast-item').remove()"><i class="bi bi-x"></i></button>`;
    stack.appendChild(el);
    setTimeout(() => { el.classList.add('hiding'); setTimeout(() => el.remove(), 250); }, 4200);
  }

  /* ── Table search ──────────────────────────────────── */
  function initTableSearch() {
    qsa('[data-tbl-search]').forEach(input => {
      const tbl = byId(input.getAttribute('data-tbl-search'));
      if (!tbl) return;
      input.addEventListener('input', function () {
        const q = this.value.toLowerCase();
        tbl.querySelectorAll('tbody tr').forEach(row => {
          row.style.display = !q || row.textContent.toLowerCase().includes(q) ? '' : 'none';
        });
      });
    });
  }

  /* ── Confirm delete ────────────────────────────────── */
  function initConfirm() {
    qsa('[data-confirm]').forEach(btn => {
      btn.addEventListener('click', function (e) {
        if (!confirm(this.getAttribute('data-confirm') || 'Are you sure?')) e.preventDefault();
      });
    });
  }

  /* ── Image preview ─────────────────────────────────── */
  function initImgPreview() {
    qsa('[data-preview]').forEach(input => {
      input.addEventListener('change', function () {
        const img = byId(this.getAttribute('data-preview'));
        if (!img || !this.files[0]) return;
        const reader = new FileReader();
        reader.onload = e => { img.src = e.target.result; };
        reader.readAsDataURL(this.files[0]);
      });
    });
  }

  /* ── Init ──────────────────────────────────────────── */
  document.addEventListener('DOMContentLoaded', () => {
    initSidebar();
    initDropdowns();
    initTooltips();
    renderDate();
    loadNotifications();
    initTableSearch();
    initConfirm();
    initImgPreview();

    // Flash message from server
    const msg  = document.body.dataset.flashMsg;
    const type = document.body.dataset.flashType;
    if (msg) showToast(msg, type || 'info');
  });

  window.showToast = showToast;
})();
