const teamGrid = document.querySelector('#team-grid');
const teamOwner = PrincessApi.user()?.role === 'Owner';
let teamMembers = [];
let teamEditor = null;

loadTeam();

async function loadTeam() {
  try {
    teamMembers = await PrincessApi.request('/api/team');
    renderTeam();
  } catch {
    teamGrid.textContent = 'Team profiles are unavailable right now.';
  }
}

function renderTeam() {
  teamGrid.replaceChildren();
  if (teamOwner) renderOwnerTeamToolbar();
  if (!teamMembers.length) {
    const blank = document.createElement('div');
    blank.className = 'empty-state';
    blank.textContent = teamOwner ? 'No team members yet. Use “Add team member” to publish one.' : 'Team profiles are coming soon.';
    teamGrid.append(blank);
    return;
  }
  teamMembers.forEach(item => teamGrid.append(teamCard(item)));
}

function renderOwnerTeamToolbar() {
  const toolbar = document.createElement('div');
  toolbar.className = 'owner-inline-controls owner-team-toolbar';
  const add = document.createElement('button');
  add.type = 'button'; add.className = 'owner-inline-edit'; add.textContent = 'Add team member';
  add.addEventListener('click', () => openTeamEditor());
  const dashboard = document.createElement('a');
  dashboard.className = 'owner-inline-edit'; dashboard.href = 'owner.html'; dashboard.textContent = 'Owner dashboard';
  toolbar.append(add, dashboard);
  teamGrid.append(toolbar);
}

function teamCard(item) {
  const card = document.createElement('article');
  card.className = 'team-card';
  if (item.hasPhoto) {
    const image = document.createElement('img'); image.src = `api/team/${item.id}/photo`; image.alt = item.name; card.append(image);
  } else {
    const placeholder = document.createElement('div'); placeholder.className = 'team-photo-placeholder'; placeholder.textContent = item.name.charAt(0); card.append(placeholder);
  }
  const title = document.createElement('h2'); title.textContent = item.name;
  const role = document.createElement('p'); role.className = 'eyebrow'; role.textContent = item.role;
  const bio = document.createElement('p'); bio.textContent = item.bio;
  card.append(title, role, bio);
  if (teamOwner) {
    const controls = document.createElement('div'); controls.className = 'owner-card-controls';
    controls.append(ownerTeamButton('Edit text or photo', () => openTeamEditor(item)), ownerTeamButton('Remove', () => removeTeamMember(item), true));
    card.append(controls);
  }
  return card;
}

function ownerTeamButton(text, handler, danger = false) {
  const button = document.createElement('button');
  button.type = 'button'; button.className = `owner-inline-edit${danger ? ' danger' : ''}`; button.textContent = text;
  button.addEventListener('click', handler); return button;
}

function ensureTeamEditor() {
  if (teamEditor) return;
  teamEditor = document.createElement('dialog');
  teamEditor.className = 'owner-edit-dialog';
  teamEditor.innerHTML = `<form class="panel-form owner-inline-form" id="inline-team-form">
    <input type="hidden" id="inline-team-id">
    <div class="owner-dialog-heading"><div><span class="eyebrow">Visible to customers</span><h2 id="inline-team-title">Add team member</h2></div><button class="owner-dialog-close" type="button" aria-label="Close">×</button></div>
    <div class="field"><label for="inline-team-name">Name</label><input id="inline-team-name" maxlength="120" required></div>
    <div class="field"><label for="inline-team-role">Role</label><input id="inline-team-role" maxlength="120"></div>
    <div class="field"><label for="inline-team-bio">Description</label><textarea id="inline-team-bio" maxlength="1500"></textarea></div>
    <div class="field"><label for="inline-team-photo">New photo (optional)</label><input type="file" id="inline-team-photo" accept="image/jpeg,image/png,image/webp"><span class="hint">Leave empty to keep the current photo.</span></div>
    <div class="owner-dialog-actions"><button class="button button-clay" type="submit">Save team member</button><span class="form-status" role="status"></span></div>
  </form>`;
  document.body.append(teamEditor);
  teamEditor.querySelector('.owner-dialog-close').addEventListener('click', () => teamEditor.close());
  teamEditor.querySelector('form').addEventListener('submit', saveTeamMember);
}

function openTeamEditor(item = null) {
  ensureTeamEditor();
  teamEditor.querySelector('#inline-team-id').value = item?.id ?? '';
  teamEditor.querySelector('#inline-team-title').textContent = item ? `Edit ${item.name}` : 'Add team member';
  teamEditor.querySelector('#inline-team-name').value = item?.name ?? '';
  teamEditor.querySelector('#inline-team-role').value = item?.role ?? '';
  teamEditor.querySelector('#inline-team-bio').value = item?.bio ?? '';
  teamEditor.querySelector('#inline-team-photo').value = '';
  const status = teamEditor.querySelector('.form-status'); status.textContent = ''; status.className = 'form-status';
  teamEditor.showModal();
}

async function saveTeamMember(event) {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  const id = teamEditor.querySelector('#inline-team-id').value;
  const data = new FormData();
  data.append('name', teamEditor.querySelector('#inline-team-name').value);
  data.append('role', teamEditor.querySelector('#inline-team-role').value);
  data.append('bio', teamEditor.querySelector('#inline-team-bio').value);
  const photo = teamEditor.querySelector('#inline-team-photo').files[0]; if (photo) data.append('photo', photo);
  const status = teamEditor.querySelector('.form-status');
  try {
    await PrincessApi.request(`/api/team${id ? `/${id}` : ''}`, { method:id ? 'PUT' : 'POST', body:data });
    status.textContent = 'Saved.'; status.className = 'form-status success';
    await loadTeam(); setTimeout(() => teamEditor.close(), 400);
  } catch (error) { status.textContent = error.message; status.className = 'form-status error'; }
}

async function removeTeamMember(item) {
  if (!confirm(`Remove ${item.name} from the public team page?`)) return;
  try { await PrincessApi.request(`/api/team/${item.id}`, { method:'DELETE' }); await loadTeam(); }
  catch (error) { alert(error.message); }
}
