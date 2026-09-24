const bookingUser = PrincessApi.requireUser();
if (bookingUser?.role === 'Owner') window.location.replace('owner.html');
const form = document.querySelector('#booking-form');
const serviceSelect = document.querySelector('#book-service');
const dogSelect = document.querySelector('#book-dog');
const dateInput = document.querySelector('#book-date');
const endDateInput = document.querySelector('#book-end-date');
const timeSelect = document.querySelector('#book-time');
const statusBox = document.querySelector('#booking-status');
const review = document.querySelector('#booking-review');
let services = []; let dogs = []; let draft = null;
const query = new URLSearchParams(location.search);
const now = new Date(); const today = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().split('T')[0];
dateInput.min = today; dateInput.value = query.get('date') || today;
endDateInput.min = today;
endDateInput.value = query.get('endDate') || '';

if (bookingUser?.role === 'Customer') Promise.all([PrincessApi.request('/api/services'), PrincessApi.request('/api/dogs')]).then(([serviceData, dogData]) => {
  services = serviceData; dogs = dogData;
  services.forEach(service => serviceSelect.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}${service.isOvernightStay?' / night':''}`, service.id)));
  dogs.forEach(dog => dogSelect.add(new Option(`${dog.name}${dog.breed ? ` · ${dog.breed}` : ''}`, dog.id)));
  if (!dogs.length) { statusBox.innerHTML = 'Add at least one dog in your <a href="dashboard.html#dogs">profile</a> before booking.'; statusBox.className = 'form-status error'; }
  serviceSelect.value = query.get('serviceId') || '';
  if (serviceSelect.value) loadSlots(query.get('time'));
}).catch(error => feedback(error.message, 'error'));

serviceSelect.addEventListener('change', () => { toggleOvernight(); loadSlots(); });
dateInput.addEventListener('change', () => { endDateInput.min = dateInput.value; loadSlots(); });
function selectedService(){return services.find(item=>String(item.id)===serviceSelect.value);}
function toggleOvernight(){const overnight=Boolean(selectedService()?.isOvernightStay);document.querySelector('#book-end-field').hidden=!overnight;endDateInput.required=overnight;timeSelect.required=!overnight;timeSelect.disabled=overnight;if(overnight){timeSelect.innerHTML='<option value="">Overnight stay</option>';if(!endDateInput.value){const next=new Date(`${dateInput.value}T00:00:00`);next.setDate(next.getDate()+1);endDateInput.value=`${next.getFullYear()}-${String(next.getMonth()+1).padStart(2,'0')}-${String(next.getDate()).padStart(2,'0')}`;}}}
async function loadSlots(preferred) {
  if (selectedService()?.isOvernightStay) return toggleOvernight();
  timeSelect.disabled = true; timeSelect.innerHTML = '<option value="">Checking open times…</option>';
  if (!serviceSelect.value || !dateInput.value) return;
  try {
    const slots = await PrincessApi.request(`/api/availability/slots?from=${dateInput.value}&to=${dateInput.value}&serviceId=${serviceSelect.value}`);
    timeSelect.innerHTML = '<option value="">Choose an open time</option>';
    slots.forEach(slot => timeSelect.add(new Option(formatTime(slot.startTime), slot.startTime)));
    timeSelect.disabled = !slots.length;
    if (!slots.length) timeSelect.innerHTML = '<option value="">No open times on this date</option>';
    if (preferred) timeSelect.value = preferred;
  } catch (error) { feedback(error.message, 'error'); }
}

form.addEventListener('submit', event => {
  event.preventDefault(); if (!form.reportValidity()) return;
  draft = Object.fromEntries(new FormData(form));
  const service = services.find(item => String(item.id) === draft.serviceId); const dog = dogs.find(item => String(item.id) === draft.dogId);
  if(service.isOvernightStay)draft.startTime='22:00';
  const nights=service.isOvernightStay?daysBetween(draft.date,draft.endDate):1;
  const total=Number(service.price)*nights;
  const when=service.isOvernightStay?`${formatDate(draft.date)}–${formatDate(draft.endDate)}<br><small>${nights} night${nights===1?'':'s'}</small>`:`${formatDate(draft.date)} at ${formatTime(draft.startTime)}`;
  const price=service.isOvernightStay?`$${Number(service.price).toFixed(2)} × ${nights} nights = $${total.toFixed(2)}`:`$${total.toFixed(2)}`;
  document.querySelector('#review-details').innerHTML = `<div><dt>Dog</dt><dd>${escapeText(dog.name)}</dd></div><div><dt>Service</dt><dd>${escapeText(service.name)}</dd></div><div><dt>When</dt><dd>${when}</dd></div><div><dt>Total</dt><dd>${price}</dd></div><div><dt>Instructions</dt><dd>${escapeText(draft.specialInstructions || 'None')}</dd></div>`;
  form.hidden = true; review.hidden = false; review.scrollIntoView({ behavior:'smooth', block:'start' });
});
document.querySelector('#edit-booking').addEventListener('click', () => { review.hidden = true; form.hidden = false; });
document.querySelector('#confirm-booking').addEventListener('click', async event => {
  event.currentTarget.disabled = true;
  try {
    // ASP.NET date parsing accepts either an ISO date or null for an optional
    // end date. An empty string causes the entire booking request to be rejected.
    const payload={
      ...draft,
      dogId:Number(draft.dogId),
      serviceId:Number(draft.serviceId),
      endDate:draft.endDate||null
    };
    const booking = await PrincessApi.request('/api/bookings', { method:'POST', body:JSON.stringify(payload) });
    location.href = `dashboard.html?booked=${booking.id}`;
  }
  catch (error) { review.hidden = true; form.hidden = false; feedback(error.message, 'error'); loadSlots(); }
  finally { event.currentTarget.disabled = false; }
});
function feedback(message, type) { statusBox.textContent = message; statusBox.className = `form-status ${type}`; }
function formatDate(value) { return new Intl.DateTimeFormat('en-US', { weekday:'long', month:'long', day:'numeric', timeZone:'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [h,m] = value.split(':'); return new Intl.DateTimeFormat('en-US', { hour:'numeric', minute:'2-digit' }).format(new Date(2000,0,1,h,m)); }
function escapeText(value) { const node = document.createElement('span'); node.textContent = value; return node.innerHTML; }
function daysBetween(start,end){return Math.max(1,Math.round((new Date(`${end}T00:00:00Z`)-new Date(`${start}T00:00:00Z`))/86400000));}
