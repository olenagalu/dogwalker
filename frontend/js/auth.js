const panels = ['login', 'register', 'forgot', 'reset'];
const statusBox = document.querySelector('#auth-status');
initializeGoogleSignIn();

async function initializeGoogleSignIn() {
  try {
    const config = await PrincessApi.request('/api/auth/google-config');
    if (!config.enabled) return;
    const waitForGoogle = () => new Promise((resolve, reject) => {
      let attempts = 0;
      const timer = setInterval(() => {
        if (window.google?.accounts?.id) { clearInterval(timer); resolve(); }
        else if (++attempts > 50) { clearInterval(timer); reject(new Error('Google sign-in could not load.')); }
      }, 100);
    });
    await waitForGoogle();
    document.querySelector('#google-placeholder').hidden = true;
    document.querySelector('#google-note').hidden = true;
    google.accounts.id.initialize({ client_id: config.clientId, callback: handleGoogleCredential });
    google.accounts.id.renderButton(document.querySelector('#google-signin'), {
      theme: 'outline', size: 'large', shape: 'pill', width: 320, text: 'continue_with'
    });
  } catch (error) {
    document.querySelector('#google-note').textContent = error.message;
  }
}

async function handleGoogleCredential(response) {
  try {
    const auth = await PrincessApi.request('/api/auth/google', {
      method: 'POST', body: JSON.stringify({ credential: response.credential })
    });
    PrincessApi.setSession(auth);
    location.href = destination(auth.user);
  } catch (error) { feedback(error.message, 'error'); }
}
function showPanel(name) {
  panels.forEach(panel => { document.querySelector(`#${panel}-panel`).hidden = panel !== name; });
  document.querySelectorAll('.tab').forEach(tab => tab.classList.toggle('active', tab.dataset.panel === name));
  statusBox.className = 'form-status'; statusBox.textContent = '';
}
document.querySelectorAll('.tab').forEach(tab => tab.addEventListener('click', () => showPanel(tab.dataset.panel)));
document.querySelector('#show-forgot').addEventListener('click', () => showPanel('forgot'));
document.querySelector('[data-back-login]').addEventListener('click', () => showPanel('login'));
function feedback(message, type) { statusBox.textContent = message; statusBox.className = `form-status ${type}`; }
function destination(user) { const requested = new URLSearchParams(location.search).get('returnTo'); return requested || (user.role === 'Owner' ? 'owner.html' : 'dashboard.html'); }

document.querySelector('#login-panel').addEventListener('submit', async event => {
  event.preventDefault(); const form = event.currentTarget;
  try { const response = await PrincessApi.request('/api/auth/login', { method:'POST', body:JSON.stringify(Object.fromEntries(new FormData(form))) }); PrincessApi.setSession(response); location.href = destination(response.user); }
  catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#register-panel').addEventListener('submit', async event => {
  event.preventDefault(); const form = event.currentTarget;
  try { const response = await PrincessApi.request('/api/auth/register', { method:'POST', body:JSON.stringify(Object.fromEntries(new FormData(form))) }); PrincessApi.setSession(response); location.href = destination(response.user); }
  catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#forgot-panel').addEventListener('submit', async event => {
  event.preventDefault();
  try {
    const response = await PrincessApi.request('/api/auth/forgot-password', { method:'POST', body:JSON.stringify(Object.fromEntries(new FormData(event.currentTarget))) });
    if (response.resetToken) { document.querySelector('#reset-token').value = response.resetToken; document.querySelector('#reset-email').value = document.querySelector('#forgot-email').value; showPanel('reset'); feedback('Development reset token received. Choose a new password.', 'success'); }
    else feedback(response.message, 'success');
  } catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#reset-panel').addEventListener('submit', async event => {
  event.preventDefault();
  try { const response = await PrincessApi.request('/api/auth/reset-password', { method:'POST', body:JSON.stringify(Object.fromEntries(new FormData(event.currentTarget))) }); showPanel('login'); feedback(response.message, 'success'); }
  catch (error) { feedback(error.message, 'error'); }
});
