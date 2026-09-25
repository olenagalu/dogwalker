const ownerUser = PrincessApi.requireUser('Owner');
const ownerStatus = document.querySelector('#owner-status');
let ownerServices=[]; let ownerCustomers=[]; let ownerBookings=[]; let rules=[];
let ownerCalendarDate=new Date();
let ownerOvernightDate=new Date();
let ownerCalendarView='month';
let pendingDeclineBookingId=null;
initializeOwnerPanels();
initializeBookingDeclineDialog();
document.querySelector('#refresh-owner').addEventListener('click',loadOwner);
function initializeOwnerPanels(){
  document.querySelectorAll('[data-owner-panel]').forEach(button=>button.addEventListener('click',()=>showOwnerPanel(button.dataset.ownerPanel)));
  document.querySelectorAll('[data-open-owner-panel]').forEach(button=>button.addEventListener('click',()=>showOwnerPanel(button.dataset.openOwnerPanel)));
  const aliases={'owner-messages':'requests','owner-bookings':'schedule','owner-overnight':'overnight','owner-customers':'customers'};
  const requested=location.hash.replace('#','');
  showOwnerPanel(document.querySelector(`[data-owner-panel-name="${aliases[requested]||requested}"]`)?(aliases[requested]||requested):'overview',false);
}
function showOwnerPanel(name,updateHash=true){
  document.querySelectorAll('[data-owner-panel-name]').forEach(panel=>{panel.hidden=panel.dataset.ownerPanelName!==name;});
  document.querySelectorAll('[data-owner-panel]').forEach(button=>button.classList.toggle('active',button.dataset.ownerPanel===name));
  if(updateHash)history.replaceState(null,'',`#${name}`);
  window.scrollTo({top:0,behavior:'smooth'});
}
async function loadOwner(){try{await Promise.all([loadServices(),loadRules(),loadCustomers()]);await applyOwnerBookingSelection();await loadBookings();renderOwnerCalendar();renderOvernightCalendar();}catch(error){feedback(error.message,'error');}}
async function loadRequests(){
  const bookingList=document.querySelector('#owner-booking-request-list');
  bookingList.replaceChildren();
  const pending=ownerBookings.filter(item=>item.status==='Pending').sort((a,b)=>new Date(a.createdAt)-new Date(b.createdAt));
  document.querySelector('#owner-menu-request-count').textContent=pending.length;
  document.querySelector('#owner-overview-request-count').textContent=pending.length;
  if(!pending.length)bookingList.append(empty('No booking requests are waiting for approval.'));
  pending.forEach(item=>bookingList.append(bookingRequestCard(item)));
}
function bookingRequestCard(item){
  const card=ownerBookingCard(item,false);
  card.classList.add('booking-request-card');
  const actions=document.createElement('div');
  actions.className='request-actions';
  if(item.customerApprovalStatus!=='Approved'){
    const approvalNote=document.createElement('p');
    approvalNote.className='hint';
    approvalNote.textContent='Approve this customer’s service area before confirming the booking.';
    card.append(approvalNote);
    const approveCustomer=action('Approve customer first',()=>updateApproval(item.customerId,'Approved'));
    approveCustomer.className='button button-clay';
    actions.append(approveCustomer);
  }
  const approve=action('Approve request',()=>decideBookingRequest(item.id,'Confirmed'));
  approve.className='button button-clay';
  approve.disabled=item.customerApprovalStatus!=='Approved';
  const decline=action('Decline request',()=>openBookingDeclineDialog(item),'danger');
  actions.append(approve,decline);
  card.append(actions);
  return card;
}
async function decideBookingRequest(id,status){
  try{
    await PrincessApi.request(`/api/bookings/${id}/status`,{method:'PUT',body:JSON.stringify({status})});
    await loadBookings();
    feedback(status==='Confirmed'?'Booking approved and added to Schedule.':'Booking declined and the customer notification was processed.','success');
  }catch(error){feedback(error.message,'error');}
}
function initializeBookingDeclineDialog(){
  const dialog=document.querySelector('#booking-decline-dialog');
  const option=document.querySelector('#booking-decline-email-option');
  option.addEventListener('change',toggleBookingDeclineEmailFields);
  document.querySelector('#close-booking-decline').addEventListener('click',()=>dialog.close());
  document.querySelector('#cancel-booking-decline').addEventListener('click',()=>dialog.close());
  document.querySelector('#booking-decline-form').addEventListener('submit',submitBookingDecline);
  toggleBookingDeclineEmailFields();
}
function openBookingDeclineDialog(item){
  pendingDeclineBookingId=item.id;
  const form=document.querySelector('#booking-decline-form');
  form.reset();
  document.querySelector('#booking-custom-email-subject').value='About your Princess Dog Walker booking request';
  toggleBookingDeclineEmailFields();
  document.querySelector('#booking-decline-dialog').showModal();
}
function toggleBookingDeclineEmailFields(){
  const custom=document.querySelector('#booking-decline-email-option').value==='Custom';
  const fields=document.querySelector('#booking-custom-email-fields');
  const subject=document.querySelector('#booking-custom-email-subject');
  const message=document.querySelector('#booking-custom-email-message');
  fields.hidden=!custom;
  document.querySelector('#booking-auto-email-note').hidden=custom;
  subject.required=custom;message.required=custom;
}
async function submitBookingDecline(event){
  event.preventDefault();
  if(!event.currentTarget.reportValidity()||!pendingDeclineBookingId)return;
  const option=document.querySelector('#booking-decline-email-option').value;
  const payload={status:'Declined',declineEmailOption:option};
  if(option==='Custom'){
    payload.customEmailSubject=document.querySelector('#booking-custom-email-subject').value;
    payload.customEmailMessage=document.querySelector('#booking-custom-email-message').value;
  }
  const button=event.currentTarget.querySelector('button[type="submit"]');
  button.disabled=true;
  try{
    await PrincessApi.request(`/api/bookings/${pendingDeclineBookingId}/status`,{method:'PUT',body:JSON.stringify(payload)});
    document.querySelector('#booking-decline-dialog').close();
    pendingDeclineBookingId=null;
    await loadBookings();
    feedback(option==='Custom'?'Booking declined and Julia’s email was sent.':'Booking declined and the automatic scheduling-conflict email was sent.','success');
  }catch(error){feedback(error.message,'error');}
  finally{button.disabled=false;}
}
async function loadBookings(){
  ownerBookings=await PrincessApi.request('/api/bookings/admin');
  const confirmed=ownerBookings.filter(item=>item.status==='Confirmed').sort((a,b)=>a.date.localeCompare(b.date)||a.startTime.localeCompare(b.startTime));
  const today=formatIso(new Date());
  const future=confirmed.filter(item=>(item.endDate||item.date)>=today);
  const history=ownerBookings.filter(item=>item.status==='Completed'||item.status==='Declined').sort((a,b)=>new Date(b.createdAt)-new Date(a.createdAt));
  const completed=history.filter(item=>item.status==='Completed');
  const list=document.querySelector('#owner-booking-list');list.replaceChildren();
  if(!confirmed.length)list.append(empty('No confirmed bookings are on the schedule.'));
  confirmed.forEach(item=>list.append(ownerBookingCard(item,true)));
  const historyList=document.querySelector('#owner-booking-history');historyList.replaceChildren();
  if(!history.length)historyList.append(empty('No completed or declined bookings yet.'));
  history.forEach(item=>historyList.append(ownerBookingCard(item,false)));
  document.querySelector('#owner-overview-future-count').textContent=future.length;
  document.querySelector('#owner-overview-completed-count').textContent=completed.length;
  document.querySelector('#owner-overview-history-count').textContent=history.length;
  document.querySelector('#owner-completed-count').textContent=completed.length;
  document.querySelector('#owner-declined-count').textContent=history.filter(item=>item.status==='Declined').length;
  await loadRequests();renderOwnerCalendar();renderOvernightCalendar();
}
function ownerBookingCard(item,showStatusControl=true){const card=document.createElement('article');card.className=`appointment-card ${serviceColorClass(item.serviceId)}`;card.dataset.status=item.status;card.style.borderLeftColor='var(--booking-color)';const head=document.createElement('div');head.className='appointment-head';const copy=document.createElement('div');const title=document.createElement('h3');title.textContent=`${item.dogName} · ${item.serviceName}`;const nights=item.isOvernightStay?daysBetween(item.date,item.endDate):1;const when=item.isOvernightStay?`${formatDate(item.date)}–${formatDate(item.endDate)} · ${nights} night${nights===1?'':'s'} · Total $${Number(item.price).toFixed(2)}`:`${formatDate(item.date)} at ${formatTime(item.startTime)} · $${Number(item.price).toFixed(2)}`;const meta=document.createElement('p');meta.textContent=`${when} · ${item.customerName} · ${item.customerEmail}${item.customerPhone?` · ${item.customerPhone}`:''}\nBreed: ${item.dogBreed||'Not provided'}\nService address: ${item.customerServiceAddress||'Not provided'}`;meta.style.whiteSpace='pre-line';copy.append(title,meta);head.append(copy);if(showStatusControl){const select=document.createElement('select');select.setAttribute('aria-label',`Status for ${item.dogName}`);['Confirmed','Completed','Cancelled'].forEach(status=>{const option=new Option(status,status,status===item.status,status===item.status);select.add(option);});select.addEventListener('change',()=>changeBookingStatus(item.id,select));head.append(select);}card.append(head);const badge=document.createElement('span');badge.className=`status-badge status-${item.status.toLowerCase()}`;badge.textContent=item.status;card.append(badge);if(item.specialInstructions){const notes=document.createElement('p');notes.textContent=`Instructions: ${item.specialInstructions}`;card.append(notes);}return card;}
async function changeBookingStatus(id,select){select.disabled=true;try{await PrincessApi.request(`/api/bookings/${id}/status`,{method:'PUT',body:JSON.stringify({status:select.value})});feedback(select.value==='Completed'?'Booking completed and moved to History.':'Booking status updated.','success');await loadBookings();}catch(error){feedback(error.message,'error');await loadBookings();}finally{select.disabled=false;}}
async function loadServices(){ownerServices=await PrincessApi.request('/api/services?includeInactive=true');const select=document.querySelector('#owner-book-service');const selected=select.value;select.innerHTML='<option value="">Choose a service</option>';ownerServices.filter(service=>service.isActive).forEach(service=>select.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}${service.isOvernightStay?' / night':''}`,service.id)));select.value=selected;renderOwnerCalendar();}
const ownerBookCustomer=document.querySelector('#owner-book-customer');
const ownerBookDog=document.querySelector('#owner-book-dog');
const ownerBookService=document.querySelector('#owner-book-service');
const ownerBookDate=document.querySelector('#owner-book-date');
const ownerBookEndDate=document.querySelector('#owner-book-end-date');
const ownerBookTime=document.querySelector('#owner-book-time');
const bookingToday=new Date();
ownerBookDate.min=new Date(bookingToday.getTime()-bookingToday.getTimezoneOffset()*60000).toISOString().split('T')[0];
ownerBookDate.value=ownerBookDate.min;
ownerBookEndDate.min=ownerBookDate.min;
ownerBookCustomer.addEventListener('change',populateOwnerDogs);
ownerBookService.addEventListener('change',()=>{toggleOwnerOvernight();loadOwnerBookingTimes();});
ownerBookDate.addEventListener('change',()=>{ownerBookEndDate.min=ownerBookDate.value;loadOwnerBookingTimes();});
function populateOwnerDogs(){const customer=ownerCustomers.find(item=>item.id===ownerBookCustomer.value);ownerBookDog.innerHTML='<option value="">Choose a dog</option>';(customer?.dogs||[]).forEach(dog=>ownerBookDog.add(new Option(`${dog.name}${dog.breed?` · ${dog.breed}`:''}`,dog.id)));ownerBookDog.disabled=!customer?.dogs?.length;if(customer&&!customer.dogs.length)ownerBookDog.innerHTML='<option value="">This customer has no saved dogs</option>';}
function selectedOwnerService(){return ownerServices.find(service=>String(service.id)===ownerBookService.value);}
function toggleOwnerOvernight(){const overnight=Boolean(selectedOwnerService()?.isOvernightStay);document.querySelector('#owner-book-end-field').hidden=!overnight;ownerBookEndDate.required=overnight;ownerBookTime.required=!overnight;ownerBookTime.disabled=overnight;if(overnight){ownerBookTime.innerHTML='<option value="">Overnight stay</option>';if(!ownerBookEndDate.value){const next=new Date(`${ownerBookDate.value}T00:00:00`);next.setDate(next.getDate()+1);ownerBookEndDate.value=formatIso(next);}}}
async function loadOwnerBookingTimes(){if(selectedOwnerService()?.isOvernightStay)return toggleOwnerOvernight();ownerBookTime.disabled=true;ownerBookTime.innerHTML='<option value="">Choose a service and date</option>';if(!ownerBookService.value||!ownerBookDate.value)return;ownerBookTime.innerHTML='<option value="">Checking open times…</option>';try{const slots=await PrincessApi.request(`/api/availability/slots?from=${ownerBookDate.value}&to=${ownerBookDate.value}&serviceId=${ownerBookService.value}`);ownerBookTime.innerHTML='<option value="">Choose an open time</option>';slots.forEach(slot=>ownerBookTime.add(new Option(formatTime(slot.startTime),slot.startTime)));ownerBookTime.disabled=!slots.length;if(!slots.length)ownerBookTime.innerHTML='<option value="">No open times on this date</option>';}catch(error){ownerBookTime.innerHTML='<option value="">Could not load times</option>';feedback(error.message,'error');}}
async function applyOwnerBookingSelection(){
  const params=new URLSearchParams(location.search);
  const serviceId=params.get('serviceId');const date=params.get('date');const time=params.get('time');const endDate=params.get('endDate');
  if(!serviceId&&!date&&!time&&!endDate)return;
  showOwnerPanel('schedule',false);
  if(serviceId&&ownerServices.some(service=>String(service.id)===serviceId))ownerBookService.value=serviceId;
  if(date&&date>=ownerBookDate.min)ownerBookDate.value=date;
  ownerBookEndDate.min=ownerBookDate.value;
  if(endDate&&endDate>ownerBookDate.value)ownerBookEndDate.value=endDate;
  toggleOwnerOvernight();
  await loadOwnerBookingTimes();
  if(time&&[...ownerBookTime.options].some(option=>option.value===time))ownerBookTime.value=time;
  document.querySelector('#owner-booking-form').scrollIntoView({behavior:'smooth',block:'start'});
}
document.querySelector('#owner-booking-form').addEventListener('submit',async event=>{event.preventDefault();if(!event.currentTarget.reportValidity())return;const overnight=Boolean(selectedOwnerService()?.isOvernightStay);const data={customerId:ownerBookCustomer.value,dogId:Number(ownerBookDog.value),serviceId:Number(ownerBookService.value),date:ownerBookDate.value,startTime:overnight?'22:00':ownerBookTime.value,specialInstructions:document.querySelector('#owner-book-notes').value,endDate:overnight?ownerBookEndDate.value:null};try{await PrincessApi.request('/api/bookings/admin',{method:'POST',body:JSON.stringify(data)});document.querySelector('#owner-book-notes').value='';await loadBookings();await loadOwnerBookingTimes();feedback('Confirmed booking added for the customer.','success');}catch(error){feedback(error.message,'error');await loadOwnerBookingTimes();}});
document.querySelectorAll('[data-owner-calendar-view]').forEach(button=>button.addEventListener('click',()=>setOwnerCalendarView(button.dataset.ownerCalendarView)));
document.querySelector('#owner-calendar-prev').addEventListener('click',()=>navigateOwnerCalendar(-1));
document.querySelector('#owner-calendar-next').addEventListener('click',()=>navigateOwnerCalendar(1));
document.querySelector('#owner-overnight-prev').addEventListener('click',()=>{ownerOvernightDate=new Date(ownerOvernightDate.getFullYear(),ownerOvernightDate.getMonth()-1,1);renderOvernightCalendar();});
document.querySelector('#owner-overnight-next').addEventListener('click',()=>{ownerOvernightDate=new Date(ownerOvernightDate.getFullYear(),ownerOvernightDate.getMonth()+1,1);renderOvernightCalendar();});
function setOwnerCalendarView(view){ownerCalendarView=view;document.querySelectorAll('[data-owner-calendar-view]').forEach(button=>{const active=button.dataset.ownerCalendarView===view;button.classList.toggle('active',active);button.setAttribute('aria-pressed',String(active));});renderOwnerCalendar();}
function navigateOwnerCalendar(direction){if(ownerCalendarView==='month')ownerCalendarDate=new Date(ownerCalendarDate.getFullYear(),ownerCalendarDate.getMonth()+direction,1);else ownerCalendarDate=new Date(ownerCalendarDate.getFullYear(),ownerCalendarDate.getMonth(),ownerCalendarDate.getDate()+(7*direction));renderOwnerCalendar();}
function serviceColorClass(serviceId){const index=ownerServices.findIndex(service=>service.id===serviceId);return `booking-color-${(index<0?Number(serviceId):index)%6}`;}
function bookingWindows(item){if(!item.isOvernightStay||!item.endDate)return[{date:item.date,start:item.startTime,end:item.endTime,label:''}];const windows=[];let date=new Date(`${item.date}T00:00:00`);const end=new Date(`${item.endDate}T00:00:00`);while(date<end){windows.push({date:formatIso(date),start:'00:00',end:'23:59',label:'Overnight'});date.setDate(date.getDate()+1);}return windows;}
function activeDayBookings(){return ownerBookings.filter(item=>!item.isOvernightStay&&item.status==='Confirmed');}
function rulesForDate(date){const parsed=new Date(`${date}T00:00:00`);return rules.filter(rule=>rule.specificDate===date||(rule.dayOfWeek!==null&&rule.dayOfWeek!==undefined&&Number(rule.dayOfWeek)===parsed.getDay()));}
function renderOwnerCalendar(){
  const calendar=document.querySelector('#owner-calendar-grid');
  const legend=document.querySelector('#booking-legend');
  if(!calendar||!legend)return;
  document.querySelector('#owner-calendar-view-label').textContent=ownerCalendarView==='month'?'Month view':'Week view';
  const period=ownerCalendarView==='month'?'month':'week';
  document.querySelector('#owner-calendar-prev').setAttribute('aria-label',`Previous ${period}`);
  document.querySelector('#owner-calendar-next').setAttribute('aria-label',`Next ${period}`);
  if(ownerCalendarView==='week')renderOwnerWeek(calendar);else renderOwnerMonth(calendar);
  renderOwnerLegend(legend);
}
function renderOwnerMonth(calendar){
  const year=ownerCalendarDate.getFullYear();const month=ownerCalendarDate.getMonth();
  document.querySelector('#owner-calendar-month').textContent=new Intl.DateTimeFormat('en-US',{month:'long',year:'numeric'}).format(new Date(year,month,1));
  calendar.className='owner-month-calendar';calendar.replaceChildren();appendWeekdayLabels(calendar);
  for(let blank=0;blank<new Date(year,month,1).getDay();blank+=1){const cell=document.createElement('div');cell.className='owner-calendar-day is-empty';calendar.append(cell);}
  const active=activeDayBookings();const daysInMonth=new Date(year,month+1,0).getDate();
  for(let day=1;day<=daysInMonth;day+=1){
    const date=formatIso(new Date(year,month,day));const cell=document.createElement('div');cell.className='owner-calendar-day owner-calendar-zoom';cell.tabIndex=0;cell.setAttribute('role','button');cell.setAttribute('aria-label',`Open week of ${formatDate(date)}`);
    const number=document.createElement('span');number.className='calendar-day-number';number.textContent=day;cell.append(number);
    active.forEach(item=>bookingWindows(item).filter(window=>window.date===date).forEach(window=>cell.append(ownerCalendarEvent(item,window))));
    rulesForDate(date).forEach(rule=>{const block=document.createElement('div');block.className='owner-calendar-block';block.textContent=rule.startTime.startsWith('00:00')?'Blocked all day':`Blocked ${formatTime(rule.startTime)}–${formatTime(rule.endTime)}`;cell.append(block);});
    const open=()=>openOwnerWeek(date);cell.addEventListener('click',open);cell.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();open();}});calendar.append(cell);
  }
}
function renderOwnerWeek(calendar){
  const start=startOfWeek(ownerCalendarDate);const end=new Date(start);end.setDate(end.getDate()+6);
  document.querySelector('#owner-calendar-month').textContent=`${formatDate(formatIso(start))} – ${formatDate(formatIso(end))}`;
  calendar.className='owner-week-calendar';calendar.replaceChildren();
  const active=activeDayBookings();
  for(let offset=0;offset<7;offset+=1){
    const current=new Date(start);current.setDate(start.getDate()+offset);const date=formatIso(current);
    const column=document.createElement('article');column.className='owner-week-day';
    const heading=document.createElement('div');heading.className='owner-week-day-heading';heading.innerHTML=`<span>${new Intl.DateTimeFormat('en-US',{weekday:'short'}).format(current)}</span><strong>${new Intl.DateTimeFormat('en-US',{month:'short',day:'numeric'}).format(current)}</strong>`;column.append(heading);
    const events=active.flatMap(item=>bookingWindows(item).filter(window=>window.date===date).map(window=>({item,window}))).sort((a,b)=>a.window.start.localeCompare(b.window.start));
    if(!events.length&&!rulesForDate(date).length){const open=document.createElement('p');open.className='owner-week-open';open.textContent='No bookings or blocks';column.append(open);}
    events.forEach(({item,window})=>column.append(ownerCalendarEvent(item,window,true)));
    rulesForDate(date).forEach(rule=>{const block=document.createElement('div');block.className='owner-week-block';const allDay=rule.startTime.startsWith('00:00');block.textContent=allDay?'Unavailable all day':`${formatTime(rule.startTime)}–${formatTime(rule.endTime)} · Unavailable${rule.notes?` · ${rule.notes}`:''}`;column.append(block);});
    const blockButton=document.createElement('button');blockButton.type='button';blockButton.className='week-day-action';blockButton.textContent='Block this date/time';blockButton.addEventListener('click',()=>prepareBlockDate(date));column.append(blockButton);calendar.append(column);
  }
}
function ownerCalendarEvent(item,window,detailed=false){const event=document.createElement('div');event.className=`owner-calendar-event ${serviceColorClass(item.serviceId)}`;event.textContent=detailed?`${formatTime(window.start)}–${formatTime(window.end)} · ${item.dogName} · ${item.serviceName}`:`${formatTime(window.start)} · ${item.dogName}`;event.title=`${item.serviceName} · ${item.customerName} · ${item.status}`;return event;}
function renderOwnerLegend(legend){const visible=new Map();activeDayBookings().forEach(item=>visible.set(item.serviceId,item.serviceName));legend.replaceChildren();visible.forEach((name,id)=>{const entry=document.createElement('span');entry.className=`legend-item ${serviceColorClass(id)}`;const dot=document.createElement('span');dot.className='legend-dot';entry.append(dot,document.createTextNode(name));legend.append(entry);});const blocked=document.createElement('span');blocked.className='legend-item';const blockedDot=document.createElement('span');blockedDot.className='legend-dot blocked-dot';blocked.append(blockedDot,document.createTextNode('Unavailable'));legend.append(blocked);}
function appendWeekdayLabels(calendar){['Sun','Mon','Tue','Wed','Thu','Fri','Sat'].forEach(day=>{const label=document.createElement('div');label.className='calendar-weekday';label.textContent=day;calendar.append(label);});}
function startOfWeek(value){const date=new Date(value.getFullYear(),value.getMonth(),value.getDate());date.setDate(date.getDate()-date.getDay());return date;}
function openOwnerWeek(date){ownerCalendarDate=new Date(`${date}T00:00:00`);setOwnerCalendarView('week');}
function prepareBlockDate(date){ruleDate.value=date;document.querySelector('#rule-id').value='';document.querySelector('#rule-form-title').textContent=`Block ${formatDate(date)}`;document.querySelector('#availability-rule-form').scrollIntoView({behavior:'smooth',block:'center'});}
function renderOvernightCalendar(){
  const calendar=document.querySelector('#owner-overnight-calendar');if(!calendar)return;
  const year=ownerOvernightDate.getFullYear();const month=ownerOvernightDate.getMonth();
  document.querySelector('#owner-overnight-month').textContent=new Intl.DateTimeFormat('en-US',{month:'long',year:'numeric'}).format(new Date(year,month,1));
  calendar.replaceChildren();appendWeekdayLabels(calendar);
  for(let blank=0;blank<new Date(year,month,1).getDay();blank+=1){const cell=document.createElement('div');cell.className='owner-calendar-day is-empty';calendar.append(cell);}
  const overnight=ownerBookings.filter(item=>item.isOvernightStay&&item.endDate&&item.status==='Confirmed');const days=new Date(year,month+1,0).getDate();
  for(let day=1;day<=days;day+=1){const date=formatIso(new Date(year,month,day));const cell=document.createElement('div');cell.className='owner-calendar-day';const number=document.createElement('span');number.className='calendar-day-number';number.textContent=day;cell.append(number);overnight.filter(item=>item.date<=date&&item.endDate>date).forEach(item=>{const stay=document.createElement('div');stay.className=`owner-calendar-event overnight-event ${serviceColorClass(item.serviceId)}`;stay.textContent=`${item.dogName} · ${item.customerName}`;stay.title=`${item.serviceName} · ${formatDate(item.date)}–${formatDate(item.endDate)} · ${item.status}`;cell.append(stay);});calendar.append(cell);}
}
const ruleDate=document.querySelector('#rule-date');
const ownerToday=new Date();
const weekdayNames=['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
ruleDate.min=new Date(ownerToday.getTime()-ownerToday.getTimezoneOffset()*60000).toISOString().split('T')[0];
ruleDate.value=ruleDate.min;
async function loadRules(){rules=await PrincessApi.request('/api/availability');const list=document.querySelector('#availability-rule-list');list.replaceChildren();if(!rules.length)list.append(empty('Nothing else is blocked. Regular services are open daily from 6 AM–11 PM.'));rules.forEach(rule=>{const allDay=rule.startTime.startsWith('00:00')&&rule.endTime.startsWith('23:59');const when=allDay?'Entire day':`${formatTime(rule.startTime)}–${formatTime(rule.endTime)}`;const scope=rule.specificDate?formatDate(rule.specificDate):`Every ${weekdayNames[Number(rule.dayOfWeek)]}`;const card=mini(scope,`${when}${rule.notes?`\n${rule.notes}`:''}`);if(rule.specificDate)card.append(action('Edit',()=>editRule(rule)));card.append(action('Make available',()=>deleteRule(rule.id),'danger'));list.append(card);});renderOwnerCalendar();}
document.querySelector('#rule-block-type').addEventListener('change',toggleRuleTimes);function toggleRuleTimes(){const timed=document.querySelector('#rule-block-type').value==='hours';document.querySelector('#rule-time-fields').hidden=!timed;document.querySelector('#rule-start').required=timed;document.querySelector('#rule-end').required=timed;}
document.querySelector('#availability-rule-form').addEventListener('submit',async event=>{event.preventDefault();if(!event.currentTarget.reportValidity())return;const id=document.querySelector('#rule-id').value;const allDay=document.querySelector('#rule-block-type').value==='day';const data={dayOfWeek:null,specificDate:ruleDate.value,startTime:allDay?'00:00':document.querySelector('#rule-start').value,endTime:allDay?'23:59':document.querySelector('#rule-end').value,isAvailable:false,notes:document.querySelector('#rule-notes').value};try{await PrincessApi.request(`/api/availability${id?`/${id}`:''}`,{method:id?'PUT':'POST',body:JSON.stringify(data)});clearRule();await loadRules();feedback('Unavailable time saved. Customers will not be offered this period.','success');}catch(error){feedback(error.message,'error');}});
function editRule(rule){document.querySelector('#rule-id').value=rule.id;document.querySelector('#rule-form-title').textContent='Edit unavailable time';ruleDate.value=rule.specificDate||'';const allDay=rule.startTime.startsWith('00:00')&&rule.endTime.startsWith('23:59');document.querySelector('#rule-block-type').value=allDay?'day':'hours';document.querySelector('#rule-start').value=rule.startTime;document.querySelector('#rule-end').value=rule.endTime;document.querySelector('#rule-notes').value=rule.notes;toggleRuleTimes();document.querySelector('#availability-rule-form').scrollIntoView({behavior:'smooth'});}function clearRule(){document.querySelector('#availability-rule-form').reset();ruleDate.value=ruleDate.min;document.querySelector('#rule-id').value='';document.querySelector('#rule-form-title').textContent='Block an unavailable date';toggleRuleTimes();}document.querySelector('#clear-rule').addEventListener('click',clearRule);async function deleteRule(id){if(!confirm('Make this date and time available again?'))return;try{await PrincessApi.request(`/api/availability/${id}`,{method:'DELETE'});await loadRules();feedback('That date and time are available again.','success');}catch(error){feedback(error.message,'error');}}
async function loadCustomers(){
  ownerCustomers=await PrincessApi.request('/api/users/customers');
  document.querySelector('#owner-overview-customer-count').textContent=ownerCustomers.length;
  const list=document.querySelector('#owner-customer-list');
  const selected=ownerBookCustomer.value;
  list.replaceChildren();
  ownerBookCustomer.innerHTML='<option value="">Choose a customer</option>';
  ownerCustomers.filter(customer=>customer.approvalStatus==='Approved').forEach(customer=>ownerBookCustomer.add(new Option(`${customer.fullName} · ${customer.email}`,customer.id)));
  ownerBookCustomer.value=selected;
  populateOwnerDogs();
  if(!ownerCustomers.length)return list.append(empty('No registered customers or dogs yet.'));
  ownerCustomers.forEach(customer=>{
    const card=document.createElement('article');
    card.className='owner-customer-card';
    const title=document.createElement('h3');
    title.textContent=customer.fullName;
    const contact=document.createElement('p');
    contact.className='owner-customer-contact';
    contact.textContent=`${customer.email}${customer.phone?` · ${customer.phone}`:''}\nArea: ${customer.serviceArea||'Not provided'}\nAddress: ${customer.serviceAddress||'Not provided'}`;
    contact.style.whiteSpace='pre-line';
    const approval=document.createElement('span');
    approval.className=`status-badge status-${customer.approvalStatus.toLowerCase()}`;
    approval.textContent=customer.approvalStatus;
    if(customer.hasProfilePhoto){const photo=document.createElement('img');photo.className='customer-profile-photo';photo.alt=customer.fullName;PrincessApi.privateImageUrl(`/api/users/${customer.id}/photo`).then(url=>photo.src=url).catch(()=>photo.remove());card.append(photo);}
    const dogList=document.createElement('div');
    dogList.className='registered-dog-list';
    if(!customer.dogs.length)dogList.append(empty('No dogs saved.'));
    customer.dogs.forEach(dog=>{
      const row=document.createElement('div');
      row.className='registered-dog';
      const name=document.createElement('strong');
      name.textContent=dog.name;
      const details=document.createElement('span');
      details.textContent=`${dog.breed||'Breed not specified'}${dog.age!=null?` · Age ${dog.age}`:''}${dog.careInstructions?` · Care: ${dog.careInstructions}`:''}${dog.behavioralNotes?` · Behavior: ${dog.behavioralNotes}`:''}${dog.medicalNotes?` · Medical: ${dog.medicalNotes}`:''}`;
      row.append(name,details);
      dogList.append(row);
    });
    card.append(title,approval,contact,dogList);
    if(customer.approvalStatus!=='Approved')card.append(action('Approve service area',()=>updateApproval(customer.id,'Approved')));
    if(customer.approvalStatus!=='Declined')card.append(action('Decline & email customer',()=>updateApproval(customer.id,'Declined'),'danger'));
    list.append(card);
  });
}
async function updateApproval(id,status){if(status==='Declined'&&!confirm('Decline this account and email the customer?'))return;try{await PrincessApi.request(`/api/users/customers/${id}/approval`,{method:'PUT',body:JSON.stringify({status})});await loadCustomers();await loadBookings();feedback(status==='Approved'?'Customer service area approved. The booking request can now be confirmed.':'Customer declined and the service-area email was sent.','success');}catch(error){feedback(error.message,'error');}}

const ownerCustomerForm=document.querySelector('#owner-customer-form');
const ownerDogFields=ownerCustomerForm.querySelector('.form-grid');
const areaField=document.createElement('div');areaField.className='field';areaField.innerHTML='<label for="owner-customer-area">Neighborhood / service area</label><input id="owner-customer-area" name="serviceArea" maxlength="160" required>';
const addressField=document.createElement('div');addressField.className='field';addressField.innerHTML='<label for="owner-customer-address">Service address</label><input id="owner-customer-address" name="serviceAddress" maxlength="300" required>';
ownerCustomerForm.insertBefore(areaField,ownerDogFields);ownerCustomerForm.insertBefore(addressField,ownerDogFields);
document.querySelector('#owner-customer-form').addEventListener('submit',async event=>{
  event.preventDefault();
  const customerForm=event.currentTarget;
  if(!customerForm.reportValidity())return;
  const data=Object.fromEntries(new FormData(customerForm));
  data.dogAge=data.dogAge?Number(data.dogAge):null;
  const button=customerForm.querySelector('button[type="submit"]');
  button.disabled=true;
  try{
    const customer=await PrincessApi.request('/api/users/customers-with-dog',{method:'POST',body:JSON.stringify(data)});
    customerForm.reset();
    await loadCustomers();
    ownerBookCustomer.value=customer.id;
    populateOwnerDogs();
    feedback(`${customer.fullName} and ${customer.dogs[0].name} were added.`, 'success');
  }catch(error){feedback(error.message,'error');}
  finally{button.disabled=false;}
});

if(ownerUser)loadOwner();
function mini(titleText,bodyText){const card=document.createElement('article');card.className='mini-card';const title=document.createElement('h3');title.textContent=titleText;const body=document.createElement('p');body.textContent=bodyText;body.style.whiteSpace='pre-line';card.append(title,body);return card;}function action(text,handler,extra=''){const button=document.createElement('button');button.className=`link-button ${extra}`;button.type='button';button.textContent=text;button.addEventListener('click',handler);return button;}function empty(text){const node=document.createElement('div');node.className='empty-state';node.textContent=text;return node;}function feedback(text,type){ownerStatus.textContent=text;ownerStatus.className=`form-status ${type}`;ownerStatus.scrollIntoView({behavior:'smooth'});}function formatDate(value){return new Intl.DateTimeFormat('en-US',{month:'short',day:'numeric',year:'numeric',timeZone:'UTC'}).format(new Date(`${value}T00:00:00Z`));}function formatTime(value){const[h,m]=value.split(':');return new Intl.DateTimeFormat('en-US',{hour:'numeric',minute:'2-digit'}).format(new Date(2000,0,1,h,m));}function formatIso(value){return `${value.getFullYear()}-${String(value.getMonth()+1).padStart(2,'0')}-${String(value.getDate()).padStart(2,'0')}`;}function daysBetween(start,end){return Math.max(1,Math.round((new Date(`${end}T00:00:00Z`)-new Date(`${start}T00:00:00Z`))/86400000));}
