const serviceSelect = document.querySelector('#availability-service');
const viewSelect = document.querySelector('#availability-view');
const dateInput = document.querySelector('#availability-date');
const calendarGrid = document.querySelector('#calendar-grid');
const periodHeading = document.querySelector('#calendar-period');
const schedulePanel = document.querySelector('#day-schedule');
const scheduleDate = document.querySelector('#schedule-date');
const scheduleService = document.querySelector('#schedule-service');
const timelineList = document.querySelector('#timeline-list');
const today = new Date();
const localToday = formatIso(new Date(today.getFullYear(), today.getMonth(), today.getDate()));
let services = [];
let calendarDate = parseDate(localToday);
let selectedDate = null;

dateInput.min = localToday;
dateInput.value = localToday;

PrincessApi.request('/api/services').then(items => {
  services = items;
  items.forEach(service => serviceSelect.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}`, service.id)));
});

document.querySelector('#availability-form').addEventListener('submit', event => {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  calendarDate = parseDate(dateInput.value);
  selectedDate = null;
  renderCalendar();
});

document.querySelector('#public-calendar-prev').addEventListener('click', () => navigateCalendar(-1));
document.querySelector('#public-calendar-next').addEventListener('click', () => navigateCalendar(1));

function navigateCalendar(direction) {
  if (!serviceSelect.value) return;
  const view = viewSelect.value;
  if (view === 'day') calendarDate.setDate(calendarDate.getDate() + direction);
  if (view === 'week') calendarDate.setDate(calendarDate.getDate() + (7 * direction));
  if (view === 'month') calendarDate = new Date(calendarDate.getFullYear(), calendarDate.getMonth() + direction, 1);
  if (view === 'year') calendarDate = new Date(calendarDate.getFullYear() + direction, calendarDate.getMonth(), 1);
  dateInput.value = formatIso(calendarDate) < localToday ? localToday : formatIso(calendarDate);
  selectedDate = null;
  renderCalendar();
}

async function renderCalendar() {
  const service = selectedService();
  if (!service) return;
  schedulePanel.hidden = true;
  calendarGrid.innerHTML = '<div class="empty-state">Building your calendar…</div>';
  const view = viewSelect.value;
  const range = getRange(view, formatIso(calendarDate));
  periodHeading.textContent = range.label;

  let openDates = new Set();
  if (!service.isOvernightStay && view !== 'year') {
    try {
      const slots = await PrincessApi.request(`/api/availability/slots?from=${range.from}&to=${range.to}&serviceId=${service.id}`);
      openDates = new Set(slots.map(slot => slot.date));
    } catch (error) {
      calendarGrid.replaceChildren(empty(error.message));
      return;
    }
  }

  calendarGrid.replaceChildren();
  if (view === 'day') renderDayGrid(range.from, openDates);
  if (view === 'week') renderWeekGrid(range.from, openDates);
  if (view === 'month') calendarGrid.append(renderMonthGrid(calendarDate.getFullYear(), calendarDate.getMonth(), openDates));
  if (view === 'year') renderYearGrid(calendarDate.getFullYear());
}

function renderDayGrid(date, openDates) {
  const grid = document.createElement('div');
  grid.className = 'public-day-view';
  grid.append(dateButton(date, openDates));
  calendarGrid.append(grid);
}

function renderWeekGrid(from, openDates) {
  const grid = calendarShell('public-week-calendar');
  const start = parseDate(from);
  for (let offset = 0; offset < 7; offset += 1) {
    const date = new Date(start);
    date.setDate(start.getDate() + offset);
    grid.append(dateButton(formatIso(date), openDates));
  }
  calendarGrid.append(grid);
}

function renderMonthGrid(year, month, openDates, compact = false) {
  const grid = calendarShell(compact ? 'public-month-calendar mini' : 'public-month-calendar');
  const firstDay = new Date(year, month, 1).getDay();
  for (let blank = 0; blank < firstDay; blank += 1) grid.append(calendarBlank());
  const days = new Date(year, month + 1, 0).getDate();
  for (let day = 1; day <= days; day += 1) grid.append(dateButton(formatIso(new Date(year, month, day)), openDates, compact));
  return grid;
}

function renderYearGrid(year) {
  const yearGrid = document.createElement('div');
  yearGrid.className = 'public-year-calendar';
  for (let month = 0; month < 12; month += 1) {
    const section = document.createElement('section');
    section.className = 'public-year-month';
    const heading = document.createElement('h3');
    heading.textContent = new Intl.DateTimeFormat('en-US', { month: 'long' }).format(new Date(year, month, 1));
    section.append(heading, renderMonthGrid(year, month, new Set(), true));
    yearGrid.append(section);
  }
  calendarGrid.append(yearGrid);
}

function calendarShell(className) {
  const grid = document.createElement('div');
  grid.className = className;
  ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].forEach(day => {
    const label = document.createElement('div');
    label.className = 'calendar-weekday';
    label.textContent = day;
    grid.append(label);
  });
  return grid;
}

function calendarBlank() {
  const blank = document.createElement('div');
  blank.className = 'public-calendar-day is-empty';
  blank.setAttribute('aria-hidden', 'true');
  return blank;
}

function dateButton(date, openDates, compact = false) {
  const value = parseDate(date);
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'public-calendar-day';
  button.dataset.date = date;
  const isPast = date < localToday;
  button.disabled = isPast;
  if (date === localToday) button.classList.add('is-today');
  if (date === selectedDate) button.classList.add('is-selected');
  if (openDates.has(date)) button.classList.add('has-openings');

  const number = document.createElement('strong');
  number.textContent = value.getDate();
  const status = document.createElement('span');
  const service = selectedService();
  status.textContent = compact ? '' : isPast ? 'Past' : service?.isOvernightStay ? 'Choose date' : openDates.has(date) ? 'Open times' : 'View day';
  button.append(number, status);
  button.setAttribute('aria-label', `${formatLongDate(date)}${openDates.has(date) ? ', open times available' : ''}`);
  button.addEventListener('click', () => selectDate(date));
  return button;
}

async function selectDate(date) {
  selectedDate = date;
  dateInput.value = date;
  document.querySelectorAll('.public-calendar-day[data-date]').forEach(button => {
    button.classList.toggle('is-selected', button.dataset.date === date);
  });
  schedulePanel.hidden = false;
  scheduleDate.textContent = formatLongDate(date);
  timelineList.innerHTML = '<div class="empty-state">Checking this day…</div>';
  const service = selectedService();
  scheduleService.textContent = `${service.name} · ${service.durationMinutes} minutes`;

  if (service.isOvernightStay) {
    timelineList.replaceChildren();
    const card = document.createElement('div');
    card.className = 'overnight-date-choice';
    const copy = document.createElement('p');
    copy.textContent = 'Use this as your check-in date, then choose a checkout date on the booking form.';
    const link = document.createElement('a');
    link.className = 'button button-clay';
    link.href = `book.html?serviceId=${service.id}&date=${date}`;
    link.textContent = 'Choose overnight dates';
    card.append(copy, link);
    timelineList.append(card);
    schedulePanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    return;
  }

  try {
    const segments = await PrincessApi.request(`/api/availability/day?date=${date}&serviceId=${service.id}`);
    renderTimeline(segments, date, service);
    schedulePanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    timelineList.replaceChildren(empty(error.message));
  }
}

function renderTimeline(segments, date, service) {
  timelineList.replaceChildren();
  const groups = [
    { label: 'Overnight', from: 0, to: 5 },
    { label: 'Morning', from: 5, to: 12 },
    { label: 'Afternoon', from: 12, to: 17 },
    { label: 'Evening', from: 17, to: 24 }
  ];
  groups.forEach(group => {
    const section = document.createElement('section');
    section.className = 'schedule-period';
    const heading = document.createElement('h3');
    heading.textContent = group.label;
    const grid = document.createElement('div');
    grid.className = 'timeline-grid';
    segments.filter(segment => hourOf(segment.startTime) >= group.from && hourOf(segment.startTime) < group.to)
      .forEach(segment => grid.append(scheduleSlot(segment, date, service)));
    section.append(heading, grid);
    timelineList.append(section);
  });
}

function scheduleSlot(segment, date, service) {
  const bookable = segment.isBookable;
  const node = document.createElement(bookable ? 'a' : 'div');
  const statusClass = segment.status.toLowerCase();
  node.className = `timeline-slot ${statusClass}${bookable ? ' is-bookable' : ''}`;
  const time = document.createElement('strong');
  time.textContent = formatTime(segment.startTime);
  const status = document.createElement('span');
  status.textContent = bookable ? 'Available' : segment.status === 'Available' ? `Doesn’t fit ${service.durationMinutes} min` : segment.status;
  node.append(time, status);
  if (bookable) {
    node.href = `book.html?serviceId=${service.id}&date=${date}&time=${segment.startTime}`;
    node.setAttribute('aria-label', `Book ${service.name} on ${formatLongDate(date)} at ${formatTime(segment.startTime)}`);
  }
  return node;
}

function getRange(view, value) {
  const selected = parseDate(value);
  let from = new Date(selected);
  let to = new Date(selected);
  if (view === 'week') {
    from.setDate(selected.getDate() - selected.getDay());
    to = new Date(from);
    to.setDate(from.getDate() + 6);
  }
  if (view === 'month') {
    from = new Date(selected.getFullYear(), selected.getMonth(), 1);
    to = new Date(selected.getFullYear(), selected.getMonth() + 1, 0);
  }
  if (view === 'year') {
    from = new Date(selected.getFullYear(), 0, 1);
    to = new Date(selected.getFullYear(), 11, 31);
  }
  const label = view === 'day' ? formatLongDate(formatIso(from))
    : view === 'month' ? new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(from)
      : view === 'year' ? String(from.getFullYear())
        : `${formatShortDate(formatIso(from))} – ${formatShortDate(formatIso(to))}`;
  return { from: formatIso(from), to: formatIso(to), label };
}

function selectedService() { return services.find(item => String(item.id) === serviceSelect.value); }
function empty(text) { const node = document.createElement('div'); node.className = 'empty-state'; node.textContent = text; return node; }
function parseDate(value) { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day); }
function formatIso(value) { const year = value.getFullYear(); const month = String(value.getMonth() + 1).padStart(2, '0'); const day = String(value.getDate()).padStart(2, '0'); return `${year}-${month}-${day}`; }
function formatLongDate(value) { return new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatShortDate(value) { return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [hours, minutes] = value.split(':'); return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(2000, 0, 1, hours, minutes)); }
function hourOf(value) { return Number(value.split(':')[0]); }
