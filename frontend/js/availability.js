const serviceSelect = document.querySelector('#availability-service');
const fromInput = document.querySelector('#availability-from');
const toInput = document.querySelector('#availability-to');
const slotList = document.querySelector('#slot-list');
const today = new Date();
const localToday = new Date(today.getTime() - today.getTimezoneOffset() * 60000).toISOString().split('T')[0];
fromInput.min = localToday; fromInput.value = localToday;
const nextWeek = new Date(today); nextWeek.setDate(nextWeek.getDate() + 7);
toInput.min = localToday; toInput.value = new Date(nextWeek.getTime() - nextWeek.getTimezoneOffset() * 60000).toISOString().split('T')[0];

PrincessApi.request('/api/services').then(services => services.forEach(service => serviceSelect.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}`, service.id))));
document.querySelector('#availability-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  slotList.innerHTML = '<div class="empty-state">Checking the calendar…</div>';
  try {
    const slots = await PrincessApi.request(`/api/availability/slots?from=${fromInput.value}&to=${toInput.value}&serviceId=${serviceSelect.value}`);
    slotList.replaceChildren();
    if (!slots.length) return slotList.append(empty('No open times in this range. Try another service or week.'));
    slots.forEach(slot => {
      const link = document.createElement('a'); link.className = 'slot';
      link.href = `book.html?serviceId=${serviceSelect.value}&date=${slot.date}&time=${slot.startTime}`;
      link.textContent = `${formatDate(slot.date)} · ${formatTime(slot.startTime)}`;
      slotList.append(link);
    });
  } catch (error) { slotList.replaceChildren(empty(error.message)); }
});
function empty(text) { const node = document.createElement('div'); node.className = 'empty-state'; node.textContent = text; return node; }
function formatDate(value) { return new Intl.DateTimeFormat('en-US', { weekday:'short', month:'short', day:'numeric', timeZone:'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [h,m] = value.split(':'); return new Intl.DateTimeFormat('en-US', { hour:'numeric', minute:'2-digit' }).format(new Date(2000,0,1,h,m)); }
