const PrincessApi = (() => {
  const isLocal = ['localhost', '127.0.0.1'].includes(window.location.hostname);
  const baseUrl = window.PRINCESS_API_BASE || (isLocal ? 'http://localhost:5095' : window.location.origin);
  const tokenKey = 'princessDogWalkerToken';
  const userKey = 'princessDogWalkerUser';

  async function request(path, options = {}) {
    const token = sessionStorage.getItem(tokenKey);
    const headers = { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers };
    if (token) headers.Authorization = `Bearer ${token}`;
    const response = await fetch(`${baseUrl}${path}`, { ...options, headers });
    const body = response.status === 204 ? null : await response.json().catch(() => ({}));
    if (!response.ok) {
      const message = body?.message || body?.title || (body?.errors && Object.values(body.errors).flat().join(' ')) || 'The request could not be completed.';
      const error = new Error(message);
      error.status = response.status;
      throw error;
    }
    return body;
  }

  function setSession(response) {
    sessionStorage.setItem(tokenKey, response.token);
    sessionStorage.setItem(userKey, JSON.stringify(response.user));
  }

  function user() {
    try { return JSON.parse(sessionStorage.getItem(userKey)); } catch { return null; }
  }

  function signOut() {
    sessionStorage.removeItem(tokenKey);
    sessionStorage.removeItem(userKey);
    window.location.href = 'index.html';
  }

  function requireUser(role) {
    const current = user();
    if (!current || (role && current.role !== role)) {
      const returnTo = encodeURIComponent(window.location.pathname.split('/').pop() || 'dashboard.html');
      window.location.href = `auth.html?returnTo=${returnTo}`;
      return null;
    }
    return current;
  }

  return { request, setSession, user, signOut, requireUser };
})();
