const serviceSelect = document.querySelector('#availability-service');
const viewSelect = document.querySelector('#availability-view');
const dateInput = document.querySelector('#availability-date');
const slotList = document.querySelector('#slot-list');
const periodHeading = document.querySelector('#calendar-period');
const today = new Date();
let services = [];
const localToday = new Date(today.getTime() - today.getTimezoneOffset() * 60000).toISOString().split('T')[0];
dateInput.min = localToday; dateInput.value = localToday;

PrincessApi.request('/api/services').then(items => { services = items; items.forEach(service => serviceSelect.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}`, service.id))); });
document.querySelector('#availability-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  const service = services.find(item => String(item.id) === serviceSelect.value);
  if (service?.isOvernightStay) {
    periodHeading.textContent = 'Overnight stay'; slotList.replaceChildren();
    const link = document.createElement('a'); link.className = 'button button-clay';
    link.href = `book.html?serviceId=${service.id}&date=${dateInput.value}`; link.textContent = 'Choose overnight dates';
    slotList.append(link); return;
  }
  if (viewSelect.value === 'year') return renderYear();
  const range = getRange(viewSelect.value, dateInput.value);
  slotList.innerHTML = '<div class="empty-state">Checking the calendar…</div>';
  try {
    const slots = await PrincessApi.request(`/api/availability/slots?from=${range.from}&to=${range.to}&serviceId=${serviceSelect.value}`);
    slotList.replaceChildren();
    periodHeading.textContent = range.label;
    if (!slots.length) return slotList.append(empty('No bookable times in this period. Please choose another date.'));
    const byDate = new Map();
    slots.forEach(slot => {
      if (!byDate.has(slot.date)) byDate.set(slot.date, []);
      byDate.get(slot.date).push(slot);
    });
    byDate.forEach((daySlots, date) => slotList.append(renderDay(date, daySlots)));
  } catch (error) { slotList.replaceChildren(empty(error.message)); }
});

function getRange(view, value) {
  const selected = parseDate(value);
  let from = new Date(selected); let to = new Date(selected);
  if (view === 'week') {
    const mondayOffset = (selected.getDay() + 6) % 7;
    from.setDate(selected.getDate() - mondayOffset);
    to = new Date(from); to.setDate(from.getDate() + 6);
  }
  if (view === 'month') {
    from = new Date(selected.getFullYear(), selected.getMonth(), 1);
    to = new Date(selected.getFullYear(), selected.getMonth() + 1, 0);
  }
  const label = view === 'day'
    ? formatLongDate(formatIso(from))
    : `${formatLongDate(formatIso(from))} – ${formatLongDate(formatIso(to))}`;
  return { from: formatIso(from), to: formatIso(to), label };
}

function renderDay(date, slots) {
  const section = document.createElement('section'); section.className = 'calendar-day';
  const heading = document.createElement('h3'); heading.textContent = formatLongDate(date);
  const grid = document.createElement('div'); grid.className = 'slot-grid';
  slots.forEach(slot => {
    const link = document.createElement('a'); link.className = 'slot';
    link.href = `book.html?serviceId=${serviceSelect.value}&date=${slot.date}&time=${slot.startTime}`;
    link.textContent = formatTime(slot.startTime);
    link.setAttribute('aria-label', `Book ${formatLongDate(slot.date)} at ${formatTime(slot.startTime)}`);
    grid.append(link);
  });
  section.append(heading, grid); return section;
}

function renderYear() {
  const year = parseDate(dateInput.value).getFullYear();
  periodHeading.textContent = String(year); slotList.replaceChildren();
  const grid = document.createElement('div'); grid.className = 'year-grid';
  for (let month = 0; month < 12; month += 1) {
    const first = new Date(year, month, 1); const last = new Date(year, month + 1, 0);
    const button = document.createElement('button'); button.type = 'button'; button.className = 'calendar-month';
    button.innerHTML = `<strong>${new Intl.DateTimeFormat('en-US', { month:'long' }).format(first)}</strong><span>View bookable times</span>`;
    button.disabled = formatIso(last) < localToday;
    button.addEventListener('click', () => {
      viewSelect.value = 'month'; dateInput.value = formatIso(first) < localToday ? localToday : formatIso(first);
      document.querySelector('#availability-form').requestSubmit();
    });
    grid.append(button);
  }
  slotList.append(grid);
}

function empty(text) { const node = document.createElement('div'); node.className = 'empty-state'; node.textContent = text; return node; }
function parseDate(value) { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day); }
function formatIso(value) { const year = value.getFullYear(); const month = String(value.getMonth() + 1).padStart(2, '0'); const day = String(value.getDate()).padStart(2, '0'); return `${year}-${month}-${day}`; }
function formatLongDate(value) { return new Intl.DateTimeFormat('en-US', { weekday:'long', month:'long', day:'numeric', year:'numeric', timeZone:'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [h,m] = value.split(':'); return new Intl.DateTimeFormat('en-US', { hour:'numeric', minute:'2-digit' }).format(new Date(2000,0,1,h,m)); }
