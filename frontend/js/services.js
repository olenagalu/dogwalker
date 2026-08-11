const serviceList = document.querySelector('#service-list');
PrincessApi.request('/api/services').then(services => {
  serviceList.replaceChildren();
  services.forEach((service, index) => {
    const row = document.createElement('article');
    row.className = 'service-row';
    row.innerHTML = `<span class="service-number">${String(index + 1).padStart(2, '0')}</span><div><h3></h3><p></p></div><span class="service-price"></span>`;
    row.querySelector('h3').textContent = service.name;
    row.querySelector('p').textContent = `${service.description} · ${service.durationMinutes} minutes`;
    row.querySelector('.service-price').textContent = `$${Number(service.price).toFixed(2)}`;
    serviceList.append(row);
  });
}).catch(error => { serviceList.innerHTML = `<div class="empty-state">${error.message}</div>`; });
