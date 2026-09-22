const PrincessApi = (() => {
  const isLocal = ['localhost', '127.0.0.1'].includes(window.location.hostname);
  const baseUrl = window.PRINCESS_API_BASE || (isLocal ? 'http://localhost:5095' : window.location.origin);
  const tokenKey = 'princessDogWalkerToken';
  const userKey = 'princessDogWalkerUser';

  async function request(path, options = {}) {
    const token = sessionStorage.getItem(tokenKey);
    const isFormData = options.body instanceof FormData;
    const headers = { ...(options.body && !isFormData ? { 'Content-Type': 'application/json' } : {}), ...options.headers };
    if (token) headers.Authorization = `Bearer ${token}`;
    const response = await fetch(`${baseUrl}${path}`, { ...options, headers });
    const body = response.status === 204 ? null : await response.json().catch(() => ({}));
    if (!response.ok) {
      const message = body?.message || (body?.errors && Object.values(body.errors).flat().join(' ')) || body?.title || 'The request could not be completed.';
      const error = new Error(message);
      error.status = response.status;
      throw error;
    }
    return body;
  }

  async function privateImageUrl(path) {
    const token = sessionStorage.getItem(tokenKey);
    const response = await fetch(`${baseUrl}${path}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    if (!response.ok) throw new Error('The private photo could not be loaded.');
    return URL.createObjectURL(await response.blob());
  }

  async function uploadReadyImage(file) {
    const maximumBytes = 2 * 1024 * 1024;
    if (!file || file.size <= maximumBytes) return file;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type))
      throw new Error('Use a JPEG, PNG, or WebP photo.');
    try {
      const bitmap = await createImageBitmap(file);
      const scale = Math.min(1, 1600 / Math.max(bitmap.width, bitmap.height));
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(bitmap.width * scale));
      canvas.height = Math.max(1, Math.round(bitmap.height * scale));
      canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      bitmap.close?.();
      for (const quality of [.86, .74, .62]) {
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
        if (blob && blob.size <= maximumBytes)
          return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type:'image/jpeg' });
      }
    } catch { /* Show the clear size message below instead of browser internals. */ }
    throw new Error('This photo is too large to prepare. Please choose a smaller photo.');
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
      const page = window.location.pathname.split('/').pop() || 'dashboard.html';
      const returnTo = encodeURIComponent(`${page}${window.location.search}`);
      window.location.href = `auth.html?returnTo=${returnTo}`;
      return null;
    }
    return current;
  }

  return { request, privateImageUrl, uploadReadyImage, setSession, user, signOut, requireUser };
})();
