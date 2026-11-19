(function () {
  "use strict";
  var modalContext = null;
  var modalEl = document.getElementById('trackingModal');
  var bsModal = modalEl ? new bootstrap.Modal(modalEl) : null;

  function findRow(kpiId) {
    return window.TRACKING_DATA.find(function (r) { return r.kpiId === kpiId; });
  }

  window.openTrackingModal = function (kpiId, plan, title) {
    var row = findRow(kpiId);
    var existing = (row && row.plans && row.plans[plan]) || [];
    modalContext = {
      kpiId: kpiId,
      plan: plan,
      items: existing.map(function (it) { return Object.assign({}, it); })
    };
    document.getElementById('trackingModalTitle').textContent = KT('Target details — ', 'تفاصيل تنفيذ المستهدف — ') + title;
    renderChecklist();
    renderDetailBlocks();
    bsModal.show();
  };

  // Mirrors TrackingSaveService; the server re-checks everything, this only shapes the UI.
  function canEditEntity(entityId) {
    if (window.CAN_ASSIGN || window.CAN_EDIT_ALL) return true;
    return window.CAN_EDIT_OWN && entityId === window.CURRENT_ENTITY_ID;
  }

  function renderChecklist() {
    var wrap = document.getElementById('entityChecklist');
    var selected = new Set(modalContext.items.map(function (it) { return it.entityId; }));
    // A user who can only edit their own department sees just that department, not 25 disabled boxes.
    var ownOnly = !(window.CAN_ASSIGN || window.CAN_EDIT_ALL);
    var shown = ownOnly ? window.ENTITIES.filter(function (e) { return e.id === window.CURRENT_ENTITY_ID; }) : window.ENTITIES;
    wrap.className = ownOnly ? 'row row-cols-1 g-1 border rounded p-2 mb-3' : 'row row-cols-2 g-1 border rounded p-2 mb-3';
    wrap.innerHTML = shown.map(function (e) {
      var checked = selected.has(e.id) ? 'checked' : '';
      var disabled = canEditEntity(e.id) ? '' : 'disabled';
      return '<div class="col"><div class="form-check">' +
        '<input class="form-check-input" type="checkbox" value="' + e.id + '" id="ent-' + e.id + '" ' + checked + ' ' + disabled + '>' +
        '<label class="form-check-label small" for="ent-' + e.id + '">' + e.name + '</label>' +
        '</div></div>';
    }).join('');

    wrap.querySelectorAll('input[type=checkbox]').forEach(function (cb) {
      cb.addEventListener('change', function () {
        var entityId = parseInt(cb.value, 10);
        if (cb.checked) {
          if (!modalContext.items.some(function (it) { return it.entityId === entityId; })) {
            modalContext.items.push({ entityId: entityId, target: '', deadline: null, status: null });
          }
        } else {
          modalContext.items = modalContext.items.filter(function (it) { return it.entityId !== entityId; });
        }
        renderDetailBlocks();
      });
    });
  }

  function entityName(id) {
    var e = window.ENTITIES.find(function (x) { return x.id === id; });
    return e ? e.name : ('#' + id);
  }

  function renderDetailBlocks() {
    var wrap = document.getElementById('entityDetailBlocks');
    if (modalContext.items.length === 0) {
      wrap.innerHTML = '<div class="text-center text-muted small border border-dashed rounded p-3">' + KT('No department is assigned to this target yet. Tick one or more above.', 'لم تُسنَد أي جهة بعد لهذا المستهدف. حدد جهة واحدة أو أكثر أعلاه.') + '</div>';
      return;
    }
    wrap.innerHTML = modalContext.items.map(function (item, idx) {
      var editable = canEditEntity(item.entityId);
      if (!editable) {
        var statusLabel = item.status ? window.STATUS_LABELS[item.status] : KT('No status', 'بلا موقف');
        return '<div class="border rounded p-2 mb-2 bg-light">' +
          '<div class="fw-semibold small text-primary">' + entityName(item.entityId) + ' <span class="badge text-bg-secondary">' + KT('View only', 'عرض فقط') + '</span></div>' +
          '<div class="small text-muted">' + KT('Target', 'المستهدف') + ': ' + (item.target || '—') + ' | ' + KT('Deadline', 'الموعد') + ': ' + (item.deadline || '—') + ' | ' + KT('Status', 'الموقف') + ': ' + statusLabel + '</div>' +
          '</div>';
      }
      var options = ['', 'Done', 'InProgress', 'NotStarted'].map(function (s) {
        var label = s === '' ? '—' : window.STATUS_LABELS[s];
        var sel = (item.status || '') === s ? 'selected' : '';
        return '<option value="' + s + '" ' + sel + '>' + label + '</option>';
      }).join('');
      return '<div class="border rounded p-2 mb-2">' +
        '<div class="fw-semibold small text-primary mb-2">' + entityName(item.entityId) + '</div>' +
        '<div class="row g-2">' +
          '<div class="col-md-4"><label class="form-label small">' + KT('Target', 'المستهدف') + '</label>' +
            '<input type="text" dir="auto" class="form-control form-control-sm" data-idx="' + idx + '" data-field="target" value="' + (item.target || '').replace(/"/g, '&quot;') + '"></div>' +
          '<div class="col-md-4"><label class="form-label small">' + KT('Deadline', 'الموعد النهائي') + '</label>' +
            '<input type="date" dir="ltr" class="form-control form-control-sm" data-idx="' + idx + '" data-field="deadline" value="' + (item.deadline || '') + '"></div>' +
          '<div class="col-md-4"><label class="form-label small">' + KT('Status', 'موقف التنفيذ') + '</label>' +
            '<select class="form-select form-select-sm" data-idx="' + idx + '" data-field="status">' + options + '</select></div>' +
        '</div></div>';
    }).join('');

    wrap.querySelectorAll('[data-field]').forEach(function (el) {
      el.addEventListener('change', function () {
        var idx = parseInt(el.dataset.idx, 10);
        var value = el.value;
        modalContext.items[idx][el.dataset.field] = value === '' ? null : value;
      });
    });
  }

  window.saveTrackingModal = function () {
    var btn = document.getElementById('trackingModalSaveBtn');
    var originalHtml = btn.innerHTML;
    setLoading(btn, KT('Saving...', 'جارٍ الحفظ...'));
    postJson('/Kpi/Tracking?handler=Save', {
      kpiId: modalContext.kpiId,
      plan: modalContext.plan,
      items: modalContext.items
    }).then(function (resp) {
      if (resp.ok) {
        try { sessionStorage.setItem('kpiFlash', KT('Saved.', 'تم حفظ البيانات.')); } catch (e) { /* ignore */ }
        bsModal.hide();
        window.location.reload();
      } else {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
        showToast(KT('Could not save. Check your permissions.', 'تعذّر الحفظ. تحقق من الصلاحيات.'));
      }
    }).catch(function () {
      btn.disabled = false;
      btn.innerHTML = originalHtml;
      showToast(KT('Could not reach the server.', 'تعذّر الاتصال بالخادم.'));
    });
  };
})();
