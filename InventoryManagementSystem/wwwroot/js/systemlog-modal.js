// systemlog-modal.js — loaded by Views/SystemLog/Index.cshtml
// Safe from Razor: all complex JS, template literals, and regex live here.
// Supports INFINITE recursive loop expansion for nested deletion audit events across ALL formats.

// ─── Helpers ─────────────────────────────────────────────────────────────────

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

var _DELETION_ACTIONS = ['audit log deleted', 'audit logs bulk deleted', 'audit logs cleared', 'audit logs purged'];

function _isDeletion(action) {
    var al = (action || '').toLowerCase();
    return _DELETION_ACTIONS.some(function (a) { return al.indexOf(a) >= 0; });
}

function _statusBadge(s) {
    var map = { Success: 'bg-success text-dark', Warning: 'bg-warning text-dark', Failed: 'bg-danger text-white', Error: 'bg-danger text-white', Critical: 'bg-danger text-white' };
    return 'badge ' + (map[s] || 'bg-secondary text-white');
}
function _moduleBadge(m) {
    var map = { Auth: 'bg-primary', Authentication: 'bg-primary', Security: 'bg-danger', Stock: 'bg-warning text-dark', Sales: 'bg-success text-dark' };
    return 'badge ' + (map[m] || 'bg-secondary');
}
function _actionIcon(a) {
    var al = (a || '').toLowerCase();
    if (al.indexOf('delete') >= 0 || al.indexOf('clear') >= 0 || al.indexOf('purge') >= 0) return 'bi-trash-fill text-danger';
    if (al.indexOf('login') >= 0) return 'bi-box-arrow-in-right text-success';
    if (al.indexOf('logout') >= 0) return 'bi-box-arrow-right text-muted';
    if (al.indexOf('stock in') >= 0 || al.indexOf('stockin') >= 0) return 'bi-box-arrow-in-down text-success';
    if (al.indexOf('stock out') >= 0 || al.indexOf('stockout') >= 0) return 'bi-box-arrow-up text-warning';
    if (al.indexOf('create') >= 0 || al.indexOf('add') >= 0) return 'bi-plus-circle-fill text-success';
    if (al.indexOf('update') >= 0 || al.indexOf('edit') >= 0) return 'bi-pencil-square text-primary';
    return 'bi-info-circle text-info';
}

// ─── Unique ID counter for expandable nested sections ────────────────────────
var _nestedIdCounter = 0;
function _nextId() { return 'sl-nested-' + (++_nestedIdCounter); }

// ─── Render a NESTED inline section (recursive) ──────────────────────────────
function _renderNestedSection(logsArr, depth, isLegacy) {
    var indent = Math.min(depth * 12, 48);
    var label = isLegacy
        ? 'Reconstructed from audit snapshot'
        : 'Audit trail of logs erased in this deletion event';
    var cards = logsArr.map(function (log, i) { return _renderLogCard(log, i, depth + 1); }).join('');
    return '<div style="margin-top:10px;padding:12px;border-left:3px solid rgba(255,193,7,0.5);background:rgba(255,193,7,0.04);border-radius:0 8px 8px 0;margin-left:' + indent + 'px;">'
        + '<div style="display:flex;align-items:center;gap:8px;margin-bottom:12px;">'
        +   '<i class="bi bi-clock-history" style="color:#ffc107;font-size:1.1rem;"></i>'
        +   '<div>'
        +     '<div style="font-weight:700;color:#ffc107;font-size:0.9rem;">Deleted Log History &mdash; ' + logsArr.length + ' Record' + (logsArr.length !== 1 ? 's' : '') + ' <span style="font-weight:400;color:#6c757d;font-size:0.75rem;">(Depth ' + depth + ')</span></div>'
        +     '<div style="font-size:0.75rem;color:#6c757d;">' + label + '</div>'
        +   '</div>'
        + '</div>'
        + cards
        + '</div>';
}

// ─── Toggle a nested section visibility ──────────────────────────────────────
function _toggleNested(btnId, sectionId) {
    var btn = document.getElementById(btnId);
    var section = document.getElementById(sectionId);
    if (!section || !btn) return;
    var isHidden = section.style.display === 'none' || section.style.display === '';
    section.style.display = isHidden ? 'block' : 'none';
    btn.innerHTML = isHidden
        ? '<i class="bi bi-chevron-up me-1"></i>Hide Nested History'
        : '<i class="bi bi-diagram-3 me-1"></i>View Deleted Log History <span style="background:#ffc107;color:#000;padding:1px 5px;border-radius:3px;font-size:0.7rem;font-weight:700;">LOOP</span>';
}

// ─── Render one log detail card (recursive-aware) ────────────────────────────
function _renderLogCard(log, idx, depth) {
    depth = depth || 1;
    var icon = _actionIcon(log.Action);
    var modBadge = _moduleBadge(log.Module);
    var stBadge = _statusBadge(log.Status);

    var isCardDeletion = _isDeletion(log.Action)
        || !!(log.PreviousData)
        || (log.Details && (
            log.Details.indexOf('DELETED LOG') >= 0 ||
            log.Details.indexOf('\u2022 Log') >= 0 ||
            log.Details.indexOf('NESTED DELETED') >= 0 ||
            log.Details.indexOf('SNAPSHOT') >= 0
        ));

    // Suppress raw details dump for deletion events
    var detailsBlock = '';
    if (log.Details && !isCardDeletion) {
        detailsBlock = '<div style="grid-column:1/-1;margin-top:4px;">'
            + '<span style="color:#6c757d;font-size:0.8rem;">Details</span>'
            + '<div style="margin-top:4px;background:rgba(0,0,0,0.3);border-radius:4px;padding:8px;font-size:0.79rem;color:#dee2e6;white-space:pre-wrap;">'
            + escapeHtml(log.Details) + '</div></div>';
    }

    // Nested loop button — parse PreviousData or Details for nested logs
    var nestedBlock = '';
    if (isCardDeletion) {
        var nestedBtnId = _nextId() + '-btn';
        var nestedSecId = _nextId() + '-sec';
        var nestedHtml = '';

        // 1. Try PreviousData (JSON string)
        if (log.PreviousData) {
            try {
                var nestedParsed = JSON.parse(log.PreviousData);
                var nestedArr = Array.isArray(nestedParsed) ? nestedParsed : [nestedParsed];
                nestedHtml = _renderNestedSection(nestedArr, depth + 1, false);
            } catch (e) {}
        }

        // 2. Try JSON in Details
        if (!nestedHtml && log.Details) {
            var jsonArr = _extractJsonFromDetails(log.Details);
            if (jsonArr && jsonArr.length > 0) {
                nestedHtml = _renderNestedSection(jsonArr, depth + 1, true);
            }
        }

        // 3. Try legacy text parse in Details
        if (!nestedHtml && log.Details) {
            var legacyNested = _parseLegacyDetails(log.Details);
            if (legacyNested.length > 0) {
                nestedHtml = _renderNestedSection(legacyNested, depth + 1, true);
            }
        }

        if (nestedHtml) {
            nestedBlock = '<div style="grid-column:1/-1;margin-top:8px;">'
                + '<button id="' + nestedBtnId + '" '
                + 'onclick="_toggleNested(\'' + nestedBtnId + '\',\'' + nestedSecId + '\')" '
                + 'style="background:rgba(255,193,7,0.15);border:1px solid rgba(255,193,7,0.4);color:#ffc107;border-radius:6px;padding:5px 12px;font-size:0.8rem;cursor:pointer;display:flex;align-items:center;gap:6px;">'
                + '<i class="bi bi-diagram-3 me-1"></i>'
                + 'View Deleted Log History '
                + '<span style="background:#ffc107;color:#000;padding:1px 5px;border-radius:3px;font-size:0.7rem;font-weight:700;">LOOP</span>'
                + '</button>'
                + '<div id="' + nestedSecId + '" style="display:none;">' + nestedHtml + '</div>'
                + '</div>';
        }
    }

    var depthColor = depth === 1 ? 'rgba(255,255,255,0.1)' : ('rgba(255,193,7,' + Math.max(0.05, 0.15 - depth * 0.03) + ')');

    return '<div style="border:1px solid ' + depthColor + ';border-radius:8px;background:rgba(255,255,255,0.03);padding:14px;margin-bottom:12px;">'
        + '<div style="display:flex;justify-content:space-between;align-items:flex-start;flex-wrap:wrap;gap:6px;margin-bottom:10px;">'
        +   '<div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">'
        +     '<span style="background:#dc3545;color:#fff;font-size:0.72rem;padding:2px 7px;border-radius:4px;font-weight:700;">#' + (idx + 1) + '</span>'
        +     (isCardDeletion ? '<span style="background:rgba(255,193,7,0.2);color:#ffc107;font-size:0.68rem;padding:1px 5px;border-radius:3px;border:1px solid rgba(255,193,7,0.3);">LOOP</span>' : '')
        +     '<i class="bi ' + icon + '" style="font-size:1rem;"></i>'
        +     '<span style="font-weight:600;color:#f8f9fa;font-size:0.95rem;">' + escapeHtml(log.Action || '-') + '</span>'
        +     '<span class="' + modBadge + '" style="font-size:0.72rem;">' + escapeHtml(log.Module || '-') + '</span>'
        +   '</div>'
        +   '<div style="display:flex;gap:5px;flex-wrap:wrap;">'
        +     '<span class="' + stBadge + '" style="font-size:0.72rem;">' + escapeHtml(log.Status || '-') + '</span>'
        +     '<span class="badge bg-dark text-secondary" style="font-size:0.72rem;">' + escapeHtml(log.LogLevel || '-') + '</span>'
        +   '</div>'
        + '</div>'
        + '<div style="display:grid;grid-template-columns:1fr 1fr;gap:6px 16px;font-size:0.82rem;">'
        +   '<div><span style="color:#6c757d;">Log ID</span><br><span style="font-family:monospace;color:#0d6efd;font-size:0.78rem;">' + escapeHtml(log.Id || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Timestamp</span><br><span style="color:#f8f9fa;">' + escapeHtml(log.TimeIstString || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Employee</span><br><span style="color:#f8f9fa;font-weight:600;">' + escapeHtml(log.EmployeeName || log.ExecutedBy || '-') + '</span> <span style="color:#6c757d;font-size:0.76rem;">@' + escapeHtml(log.Username || '-') + ' &middot; ' + escapeHtml(log.UserRole || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Target</span><br><span style="color:#f8f9fa;">' + escapeHtml(log.Target || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">IP Address</span><br><span style="font-family:monospace;color:#0dcaf0;">' + escapeHtml(log.IpAddress || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Browser / OS</span><br><span style="color:#f8f9fa;">' + escapeHtml(log.Browser || '-') + ' &middot; ' + escapeHtml(log.OperatingSystem || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Request URL</span><br><span style="font-family:monospace;color:#6c757d;font-size:0.76rem;">' + escapeHtml(log.RequestUrl || '-') + '</span></div>'
        +   '<div><span style="color:#6c757d;">Device</span><br><span style="color:#6c757d;">' + escapeHtml(log.DeviceType || '-') + '</span></div>'
        +   detailsBlock
        +   nestedBlock
        + '</div>'
        + '</div>';
}

// ─── Build the top-level deleted section wrapper ──────────────────────────────
function _buildDeletedSection(logsArr, isLegacy) {
    var label = isLegacy ? 'Reconstructed from audit snapshot' : 'Complete audit trail of every log erased in this deletion event';
    var cards = logsArr.map(function (log, i) { return _renderLogCard(log, i, 1); }).join('');
    return '<div style="border:1px solid rgba(255,193,7,0.35);border-radius:8px;background:rgba(255,193,7,0.04);padding:16px;margin-top:4px;">'
        + '<div style="display:flex;align-items:center;gap:10px;margin-bottom:14px;">'
        +   '<i class="bi bi-clock-history" style="color:#ffc107;font-size:1.3rem;"></i>'
        +   '<div>'
        +     '<div style="font-weight:700;color:#ffc107;">Deleted Log History &mdash; ' + logsArr.length + ' Record' + (logsArr.length !== 1 ? 's' : '') + '</div>'
        +     '<div style="font-size:0.8rem;color:#6c757d;">' + label + ' &bull; Click <span style="color:#ffc107;">LOOP</span> badges to expand nested deletion chains</div>'
        +   '</div>'
        + '</div>'
        + cards
        + '</div>';
}

// ─── Legacy plain-text snapshot parser (Handles both Multi-bullet & Single-delete breakdown) ─
function _parseLegacyDetails(detailsText) {
    if (!detailsText) return [];
    var lines = detailsText.split('\n');
    var entries = [];
    var cur = null;
    var bulletChar = '\u2022';

    // Check if it's Single-Log Breakdown format ([DELETED LOG RECORD BREAKDOWN])
    if (detailsText.indexOf('[DELETED LOG RECORD BREAKDOWN]') >= 0 || detailsText.indexOf('\u2022 Log ID:') >= 0) {
        var singleLog = {
            Id: '', TimeIstString: '', ExecutedBy: '', EmployeeName: '', Username: '', UserRole: '',
            Module: '', Action: '', Target: '', Details: '', PreviousData: '',
            IpAddress: '', Browser: '', OperatingSystem: '', DeviceType: '', RequestUrl: '',
            Status: 'Warning', LogLevel: 'Warning'
        };

        for (var k = 0; k < lines.length; k++) {
            var ln = lines[k].trim();
            if (ln.indexOf(bulletChar + ' Log ID:') === 0 || ln.indexOf('• Log ID:') === 0) {
                singleLog.Id = ln.replace(/^•\s*Log ID:\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' Action:') === 0 || ln.indexOf('• Action:') === 0) {
                singleLog.Action = ln.replace(/^•\s*Action:\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' Module:') === 0 || ln.indexOf('• Module:') === 0) {
                singleLog.Module = ln.replace(/^•\s*Module:\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' Executed By:') === 0 || ln.indexOf('• Executed By:') === 0) {
                var ebStr = ln.replace(/^•\s*Executed By:\s*/, '').trim();
                var pStart = ebStr.indexOf('(');
                var pEnd = ebStr.indexOf(')');
                singleLog.ExecutedBy = pStart >= 0 ? ebStr.slice(0, pStart).trim() : ebStr;
                singleLog.EmployeeName = singleLog.ExecutedBy;
                if (pStart >= 0 && pEnd > pStart) {
                    var inParen = ebStr.slice(pStart + 1, pEnd);
                    var at = inParen.indexOf('@');
                    var dash = inParen.lastIndexOf(' - ');
                    singleLog.Username = at >= 0 ? inParen.slice(at + 1, dash >= 0 ? dash : undefined).trim() : inParen;
                    singleLog.UserRole = dash >= 0 ? inParen.slice(dash + 3).trim() : '';
                }
            } else if (ln.indexOf(bulletChar + ' Original Timestamp (IST):') === 0 || ln.indexOf('• Original Timestamp (IST):') === 0) {
                singleLog.TimeIstString = ln.replace(/^•\s*Original Timestamp \(IST\):\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' Description / Target:') === 0 || ln.indexOf('• Description / Target:') === 0) {
                singleLog.Target = ln.replace(/^•\s*Description \/ Target:\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' Original Details:') === 0 || ln.indexOf('• Original Details:') === 0) {
                singleLog.Details = ln.replace(/^•\s*Original Details:\s*/, '').trim();
            } else if (ln.indexOf(bulletChar + ' IP & Device:') === 0 || ln.indexOf('• IP & Device:') === 0) {
                var ipDev = ln.replace(/^•\s*IP & Device:\s*/, '').trim();
                var p1 = ipDev.indexOf('(');
                var p2 = ipDev.indexOf(')');
                singleLog.IpAddress = p1 >= 0 ? ipDev.slice(0, p1).trim() : ipDev;
                if (p1 >= 0 && p2 > p1) {
                    var sys = ipDev.slice(p1 + 1, p2);
                    var slash = sys.indexOf(' / ');
                    singleLog.Browser = slash >= 0 ? sys.slice(0, slash).trim() : sys;
                    singleLog.OperatingSystem = slash >= 0 ? sys.slice(slash + 3).trim() : '';
                }
            } else if (ln.indexOf(bulletChar + ' Status & Severity:') === 0 || ln.indexOf('• Status & Severity:') === 0) {
                var ss = ln.replace(/^•\s*Status & Severity:\s*/, '').trim();
                var sl = ss.split('/');
                singleLog.Status = sl[0] ? sl[0].trim() : 'Warning';
                singleLog.LogLevel = sl[1] ? sl[1].trim() : 'Warning';
            }
        }
        if (singleLog.Id || singleLog.Action) {
            entries.push(singleLog);
            return entries;
        }
    }

    // Otherwise, parse Multi-bullet format ("• Log #<id> | ...")
    for (var i = 0; i < lines.length; i++) {
        var trimmed = lines[i].trim();
        if (trimmed.indexOf(bulletChar + ' Log #') === 0 || trimmed.indexOf('• Log #') === 0) {
            if (cur) entries.push(cur);
            var rest = trimmed.replace(/^•\s*Log #/, '');
            var idEnd = rest.indexOf(' ');
            var logId = idEnd >= 0 ? rest.slice(0, idEnd) : rest;
            rest = idEnd >= 0 ? rest.slice(idEnd + 1) : '';
            var parts = rest.split(' | ');
            var time = parts[0] ? parts[0].trim() : '';
            var execBy = '', uname = '', urole = '';
            if (parts[1]) {
                var eb = parts[1].replace('Executed By:', '').trim();
                var parenStart = eb.indexOf('(');
                var parenEnd = eb.indexOf(')');
                execBy = parenStart >= 0 ? eb.slice(0, parenStart).trim() : eb;
                if (parenStart >= 0 && parenEnd > parenStart) {
                    var inner = eb.slice(parenStart + 1, parenEnd);
                    var atIdx = inner.indexOf('@');
                    var dashIdx = inner.lastIndexOf(' - ');
                    uname = atIdx >= 0 ? inner.slice(atIdx + 1, dashIdx >= 0 ? dashIdx : undefined).trim() : inner;
                    urole = dashIdx >= 0 ? inner.slice(dashIdx + 3).trim() : '';
                }
            }
            var modStr = parts[2] ? parts[2].replace('Module:', '').trim() : '';
            var actStr = parts[3] ? parts[3].replace('Action:', '').trim() : '';
            cur = {
                Id: logId, TimeIstString: time, ExecutedBy: execBy, EmployeeName: execBy,
                Username: uname, UserRole: urole, Module: modStr, Action: actStr,
                Target: '', Details: '', PreviousData: '',
                IpAddress: '', Browser: '', OperatingSystem: '', DeviceType: '', RequestUrl: '',
                Status: actStr.toLowerCase().indexOf('delete') >= 0 ? 'Warning' : 'Success',
                LogLevel: 'Information'
            };
        } else if (cur && trimmed.indexOf('Target:') === 0) {
            cur.Target = trimmed.replace('Target:', '').trim();
        } else if (cur && trimmed.indexOf('Original Details:') === 0) {
            cur.Details = trimmed.replace('Original Details:', '').trim();
        }
    }
    if (cur) entries.push(cur);
    return entries;
}

// Try to extract a JSON array/object embedded in the details field
function _extractJsonFromDetails(detailsText) {
    if (!detailsText) return null;
    var lower = detailsText.toLowerCase();

    // Look for JSON start after common headers or anywhere in text
    var jsonStart = detailsText.indexOf('--- NESTED DELETED LOGS HISTORY LOOP');
    if (jsonStart < 0) jsonStart = detailsText.indexOf('--- DELETED LOGS');
    if (jsonStart < 0) jsonStart = 0;

    var startIdx = -1;
    for (var i = jsonStart; i < detailsText.length; i++) {
        if (detailsText[i] === '[' || detailsText[i] === '{') {
            startIdx = i;
            break;
        }
    }
    if (startIdx < 0) return null;

    var candidate = detailsText.slice(startIdx).trim();
    try {
        var parsed = JSON.parse(candidate);
        return Array.isArray(parsed) ? parsed : [parsed];
    } catch (e) {
        var lastBracket = Math.max(candidate.lastIndexOf(']'), candidate.lastIndexOf('}'));
        if (lastBracket > 0) {
            try {
                var trimmed = candidate.slice(0, lastBracket + 1);
                var parsed2 = JSON.parse(trimmed);
                return Array.isArray(parsed2) ? parsed2 : [parsed2];
            } catch (e2) {}
        }
    }
    return null;
}


// ─── Main modal opener ────────────────────────────────────────────────────────
async function openLogModal(id) {
    _nestedIdCounter = 0; // reset IDs each modal open

    var modalEl = document.getElementById('logDetailsModal');
    if (!modalEl) return;
    var modal = bootstrap.Modal.getInstance(modalEl);
    if (!modal) modal = new bootstrap.Modal(modalEl);
    modal.show();

    var body = document.getElementById('log-modal-body');
    body.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>';

    try {
        var resp = await fetch('/SystemLog/Details?id=' + encodeURIComponent(id));
        var data = await resp.json();

        if (!data.success) {
            body.innerHTML = '<div class="alert alert-danger">' + escapeHtml(data.message || 'Record not found.') + '</div>';
            return;
        }

        // Detect deletion events by action name OR by content markers in details/previousData
        var isDeletionByAction = _isDeletion(data.action);
        var isDeletionByContent = !!(data.previousData)
            || !!(data.details && (
                data.details.indexOf('[BULK DELETED LOGS ARCHIVE SNAPSHOT') >= 0
                || data.details.indexOf('[DELETED LOG RECORD BREAKDOWN]') >= 0
                || data.details.indexOf('--- NESTED DELETED LOGS HISTORY LOOP') >= 0
                || data.details.indexOf('--- Deleted Log') >= 0
                || data.details.indexOf('\u2022 Log') >= 0
                || data.details.indexOf('Original Details:') >= 0
            ));
        var isDeletionEvent = isDeletionByAction || isDeletionByContent;

        // Build deleted-logs history section
        var deletedLogsHtml = '';

        // 1. NEW format: previousData JSON array
        if (data.previousData) {
            try {
                var parsed = JSON.parse(data.previousData);
                var arr = Array.isArray(parsed) ? parsed : [parsed];
                deletedLogsHtml = _buildDeletedSection(arr, false);
            } catch (e) { /* fall through */ }
        }

        // 2. LEGACY JSON-embed format: "--- NESTED DELETED LOGS HISTORY LOOP ---\n[{...}]"
        if (!deletedLogsHtml && isDeletionEvent && data.details) {
            var jsonArr = _extractJsonFromDetails(data.details);
            if (jsonArr && jsonArr.length > 0) {
                deletedLogsHtml = _buildDeletedSection(jsonArr, true);
            }
        }

        // 3. LEGACY text format: "• Log #<id> | ..." or "[DELETED LOG RECORD BREAKDOWN]"
        if (!deletedLogsHtml && isDeletionEvent && data.details) {
            var legacyEntries = _parseLegacyDetails(data.details);
            if (legacyEntries.length > 0) {
                deletedLogsHtml = _buildDeletedSection(legacyEntries, true);
            }
        }

        // Never show raw details text for deletion events — it's always a snapshot dump
        var showRawDetails = !isDeletionEvent && data.details;
        var detailsHtml = showRawDetails
            ? '<div style="margin-top:8px;color:#adb5bd;font-size:0.83rem;white-space:pre-wrap;">' + escapeHtml(data.details) + '</div>'
            : '';

        var deletedSection = deletedLogsHtml
            ? '<div class="col-md-12">' + deletedLogsHtml + '</div>'
            : '';

        body.innerHTML =
            '<div class="row g-3">'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">Log ID</div>'
            +   '<div class="font-monospace text-primary" style="font-size:0.85rem;">' + escapeHtml(data.id) + '</div>'
            + '</div></div>'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">Timestamp (IST)</div>'
            +   '<div class="font-monospace text-light">' + escapeHtml(data.timeIst) + '</div>'
            + '</div></div>'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">User / Employee</div>'
            +   '<div class="fw-bold text-light">' + escapeHtml(data.employeeName) + '</div>'
            +   '<div class="small text-muted">@' + escapeHtml(data.username) + ' &middot; <span class="badge bg-dark text-muted">' + escapeHtml(data.userRole) + '</span></div>'
            + '</div></div>'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">Module &amp; Action</div>'
            +   '<div class="fw-bold text-light"><i class="bi ' + _actionIcon(data.action) + ' me-1"></i>' + escapeHtml(data.module) + ' &mdash; ' + escapeHtml(data.action) + '</div>'
            +   '<div class="mt-1"><span class="' + _statusBadge(data.status) + ' me-1" style="font-size:0.75rem;">' + escapeHtml(data.status) + '</span>'
            +   '<span class="badge bg-dark text-muted" style="font-size:0.75rem;">' + escapeHtml(data.logLevel) + '</span></div>'
            + '</div></div>'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">IP Address</div>'
            +   '<div class="font-monospace text-info">' + (escapeHtml(data.ipAddress) || '&mdash;') + '</div>'
            + '</div></div>'
            + '<div class="col-md-6"><div class="card-d p-3">'
            +   '<div class="small text-muted">Browser &amp; OS</div>'
            +   '<div class="text-light">' + escapeHtml(data.browser) + ' <span class="text-muted">&middot; ' + escapeHtml(data.operatingSystem) + '</span></div>'
            + '</div></div>'
            + '<div class="col-md-12"><div class="card-d p-3">'
            +   '<div class="small text-muted mb-1">Description / Target</div>'
            +   '<div class="text-light fw-semibold">' + escapeHtml(data.target) + '</div>'
            +   detailsHtml
            + '</div></div>'
            + deletedSection
            + '</div>';

    } catch (e) {
        console.error(e);
        body.innerHTML = '<div class="alert alert-danger">An error occurred while loading audit details.</div>';
    }
}
