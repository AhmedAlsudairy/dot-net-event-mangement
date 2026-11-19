function postJson(url, body) {
  var tokenMeta = document.querySelector('meta[name="csrf-token"]');
  return fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': tokenMeta ? tokenMeta.content : ''
    },
    body: JSON.stringify(body)
  });
}

// Call from a submit button's onclick (fires before the native form submit
// navigates away, so no need to ever re-enable it). Shows a spinner + label
// and disables the button so slow requests (Excel import, login) give
// immediate feedback instead of looking unresponsive to a second click.
function setLoading(btn, label) {
  if (!btn || btn.disabled) return;
  btn.disabled = true;
  btn.dataset.originalHtml = btn.innerHTML;
  btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> ' + (label || (window.KT ? KT('Working...', 'جارٍ المعالجة...') : '...'));
}

function showToast(message) {
  var el = document.getElementById('toast');
  var body = document.getElementById('toastBody');
  if (!el || !body) return;
  body.textContent = message;
  var toast = bootstrap.Toast.getOrCreateInstance(el, { delay: 2500 });
  toast.show();
}

// Auto-show a toast for any TempData-driven message rendered into #flashMessage.
document.addEventListener('DOMContentLoaded', function () {
  var flash = document.getElementById('flashMessage');
  if (flash && flash.textContent.trim()) showToast(flash.textContent.trim());
  try {
    var pending = sessionStorage.getItem('kpiFlash');
    if (pending) { sessionStorage.removeItem('kpiFlash'); showToast(pending); }
  } catch (e) { /* storage unavailable: skip the toast */ }
});
