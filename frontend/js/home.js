const homeServices = document.querySelector('#home-services');
PrincessApi.request('/api/services').then(services => {
  homeServices.replaceChildren();
  services.slice(0, 4).forEach((service, index) => {
    const card = document.createElement('article');
    card.className = 'card';
    card.innerHTML = `<div class="card-icon">${String(index + 1).padStart(2, '0')}</div><h3></h3><p></p><span class="price"></span>`;
    card.querySelector('h3').textContent = service.name;
    card.querySelector('p').textContent = service.description;
    card.querySelector('.price').textContent = `$${Number(service.price).toFixed(2)} · ${service.durationMinutes} min`;
    homeServices.append(card);
  });
}).catch(() => { homeServices.innerHTML = '<div class="empty-state">Services will appear here when the booking service is running.</div>'; });
