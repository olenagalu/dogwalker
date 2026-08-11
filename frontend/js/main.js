const currentUser = typeof PrincessApi !== 'undefined' ? PrincessApi.user() : null;
const currentPage = window.location.pathname.split('/').pop() || 'index.html';
const nav = document.querySelector('.nav-links');

if (nav) {
  const accountLink = currentUser
    ? `<a href="${currentUser.role === 'Owner' ? 'owner.html' : 'dashboard.html'}">Dashboard</a><button class="nav-signout" type="button">Sign out</button>`
    : '<a href="auth.html">Sign in / Sign up</a>';
  nav.innerHTML = `
    <a href="index.html">Home</a><a href="services.html">Services</a>
    <a href="availability.html">Availability</a><a href="about.html">About</a>
    <a href="contact.html">Contact</a>${accountLink}<a class="button" href="book.html">Book a service</a>`;
  nav.querySelector(`a[href="${currentPage}"]`)?.setAttribute('aria-current', 'page');
  nav.querySelector('.nav-signout')?.addEventListener('click', () => PrincessApi.signOut());
}

document.querySelectorAll('.brand').forEach(brand => {
  brand.innerHTML = '<span class="brand-mark">PDW</span>Princess Dog Walker';
  brand.setAttribute('href', 'index.html');
});

const toggle = document.querySelector('.nav-toggle');
toggle?.addEventListener('click', () => {
  const isOpen = nav.classList.toggle('open');
  toggle.setAttribute('aria-expanded', String(isOpen));
});

nav?.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
  nav.classList.remove('open');
  toggle?.setAttribute('aria-expanded', 'false');
}));

document.querySelectorAll('[data-year]').forEach(element => { element.textContent = new Date().getFullYear(); });

const decorLayer = document.createElement('div');
decorLayer.className = 'decor-layer';
decorLayer.setAttribute('aria-hidden', 'true');
[['sparkle', '✦'], ['heart', '♥'], ['bow', ''], ['sparkle', '✧'], ['heart', '♥'], ['sparkle', '✦']]
  .forEach(([className, symbol]) => {
    const decoration = document.createElement('span');
    decoration.className = `decor ${className}`;
    decoration.textContent = symbol;
    decorLayer.append(decoration);
  });
document.body.prepend(decorLayer);
