const aboutPhoto = document.querySelector('.about-owner-photo');
if (aboutPhoto) {
  aboutPhoto.addEventListener('load', () => {
    aboutPhoto.hidden = false;
    aboutPhoto.parentElement.classList.add('has-photo');
  });
  aboutPhoto.addEventListener('error', () => {
    aboutPhoto.hidden = true;
    aboutPhoto.parentElement.classList.remove('has-photo');
  });
  if (aboutPhoto.complete && aboutPhoto.naturalWidth > 0) {
    aboutPhoto.hidden = false;
    aboutPhoto.parentElement.classList.add('has-photo');
  }
}
