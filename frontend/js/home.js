const homeServices = document.querySelector('#home-services');
PrincessApi.request('/api/services').then(services => {
  homeServices.replaceChildren();
  services.slice(0, 4).forEach((service, index) => {
    const card = document.createElement('article');
    card.className = 'card';
    card.innerHTML = `<div class="card-icon">${String(index + 1).padStart(2, '0')}</div><h3></h3><p></p><span class="price"></span>`;
    card.querySelector('h3').textContent = service.name;
    card.querySelector('p').textContent = service.description;
    card.querySelector('.price').textContent = service.isOvernightStay
      ? `$${Number(service.price).toFixed(2)} / night`
      : `$${Number(service.price).toFixed(2)} · ${service.durationMinutes} min`;
    homeServices.append(card);
  });
}).catch(() => { homeServices.innerHTML = '<div class="empty-state">Services will appear here when the booking service is running.</div>'; });

const ownerPhoto = document.querySelector('.about-owner-photo');
if (ownerPhoto) {
  const showOwnerPhoto = () => {
    ownerPhoto.hidden = false;
    ownerPhoto.parentElement.classList.add('has-photo');
  };
  const hideOwnerPhoto = () => {
    ownerPhoto.hidden = true;
    ownerPhoto.parentElement.classList.remove('has-photo');
  };
  ownerPhoto.addEventListener('load', showOwnerPhoto);
  ownerPhoto.addEventListener('error', hideOwnerPhoto);
  if (ownerPhoto.complete) ownerPhoto.naturalWidth > 0 ? showOwnerPhoto() : hideOwnerPhoto();
}

PrincessApi.request('/api/site-content/owner-profile').then(profile => {
  document.querySelector('#home-owner-section-label').textContent = profile.sectionLabel;
  document.querySelector('#home-owner-section-title').textContent = profile.sectionTitle;
  document.querySelector('#home-owner-greeting').textContent = profile.greeting;
  document.querySelector('#home-owner-headline').textContent = profile.headline;
  const biography = document.querySelector('#home-owner-biography');
  biography.replaceChildren();
  profile.biography.split(/\n\s*\n/).filter(Boolean).forEach(text => {
    const paragraph = document.createElement('p');
    paragraph.textContent = text;
    biography.append(paragraph);
  });
  document.querySelector('#home-owner-phone').textContent = profile.phone;
  document.querySelector('#home-owner-phone-link').href = `tel:${profile.phone.replace(/[^+\d]/g, '')}`;
  document.querySelector('#home-owner-email').textContent = profile.email;
  document.querySelector('#home-owner-email-link').href = `mailto:${profile.email}`;
  document.querySelector('#home-owner-instagram').textContent = profile.instagramLabel;
  document.querySelector('#home-owner-instagram-link').href = profile.instagramUrl;
  document.querySelector('#home-owner-area').textContent = profile.serviceArea;
  document.querySelector('#home-owner-map-link').href = profile.mapUrl;
  document.querySelector('#home-owner-contact-button').textContent = profile.contactButtonText;
}).catch(() => {});
