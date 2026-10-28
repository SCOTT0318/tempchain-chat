import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { readFile } from 'node:fs/promises';
import { createAccessGateway, directoryMembers } from './access-gateway.mjs';

const member = (account, groups = ['erp']) => ({ account, uid: 1001, description: '홍길동 | 사원', email: account + '@example.test', groups });
test('deployment compose includes tested gateway and inline startup with healthcheck and bounded logs', async () => {
  const compose = (await readFile(new URL('./compose.access.yaml', import.meta.url), 'utf8')).replaceAll('\r\n', '\n');
  const source = (await readFile(new URL('./access-gateway.mjs', import.meta.url), 'utf8')).replaceAll('\r\n', '\n');
  const start = compose.indexOf('        import ');
  const end = compose.indexOf('    healthcheck:', start);
  assert.ok(start > 0 && end > start);
  const inline = compose.slice(start, end).split('\n').map(line => line.startsWith('        ') ? line.slice(8) : line).join('\n').trimEnd();
  assert.equal(inline, source.trimEnd() + '\ncreateAccessGateway().listen(8080,"0.0.0.0");');
  assert.ok(compose.includes('max-size: "3m", max-file: "2"'));
});
function upload(sid = 'fixture-session', bytes = new Uint8Array([0, 255, 13, 10, 38, 128])) {
  const form = new FormData();
  for (const [key, value] of Object.entries({ api: 'SYNO.Chat.Post', method: 'create', version: '5',
    _sid: sid, type: '"file"', channel_id: '19', message: '""', is_thread: 'false', thread_id: '0' })) form.set(key, value);
  form.set('file', new Blob([bytes]), '보고서 & 확인.xlsx');
  return form;
}

test('multipart file uploads preserve binary content and require current ERP membership', async t => {
  const state = await fixture(t);
  await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff' });
  assert.equal((await state.call(upload())).success, true);
  const sent = state.lastForward;
  const form = await new Request('http://fixture', { method: 'POST', headers: { 'content-type': sent.headers['content-type'] }, body: sent.body }).formData();
  assert.equal(form.get('file').name, '보고서 & 확인.xlsx');
  assert.deepEqual(new Uint8Array(await form.get('file').arrayBuffer()), new Uint8Array([0, 255, 13, 10, 38, 128]));
  state.snapshot.users[0].groups = [];
  const before = state.calls.length;
  assert.equal((await state.call(upload())).error.code, 460);
  assert.equal(state.calls.length, before);
});

test('multipart rejects missing sessions, duplicate routing, multiple files, wrong API and oversize files', async t => {
  const state = await fixture(t);
  const missing = upload(); missing.delete('_sid');
  assert.equal((await state.call(missing)).error.code, 119);
  const duplicate = upload(); duplicate.append('api', 'SYNO.API.Auth');
  const wrong = upload(); wrong.set('api', 'SYNO.API.Auth');
  const multiple = upload(); multiple.append('file', new Blob(['second']), 'second.txt');
  const oversize = upload('fixture-session', new Uint8Array(10 * 1024 * 1024 + 1));
  for (const form of [duplicate, wrong, multiple, oversize]) assert.equal((await state.call(form)).error.code, 400);
  assert.equal(state.calls.length, 0);
});
async function fixture(t) {
  const now = Date.now();
  const state = { snapshot: { version: 1, generatedAt: now, users: [member('staff'), member('admin-only', ['administrators'])] }, calls: [] };
  const server = createAccessGateway({ now: () => now, readDirectory: async () => state.snapshot,
    forward: async request => {
      const fields = /^multipart\/form-data/i.test(request.headers['content-type'] || '')
        ? await new Request('http://fixture', { method: 'POST', headers: { 'content-type': request.headers['content-type'] }, body: request.body }).formData()
        : new URLSearchParams(request.body.toString());
      state.lastForward = request;
      state.calls.push({ api: fields.get('api'), method: fields.get('method') });
      let body;
      if (fields.get('api') === 'SYNO.API.Auth') body = { success: true, data: { sid: 'fixture-session' } };
      else if (fields.get('method') === 'login') body = fields.get('_sid') === 'bad-session'
        ? { success: false, error: { code: 119 } }
        : { success: true, data: { user: state.idOnly ? { user_id: 1 } : { user_id: 1, username: 'staff', nickname: 'old-id' } } };
      else if (fields.get('api') === 'SYNO.Chat.User') body = { success: true, data: { users: [
        { user_id: 1, username: 'staff', nickname: 'staff', user_props: { description: '', email: '' } },
        { user_id: 2, username: 'admin-only' }, { user_id: 3, username: 'outsider' }
      ] } };
      else body = { success: true, data: { channels: [] } };
      return { status: 200, headers: { 'content-type': 'application/json' }, body: Buffer.from(JSON.stringify(body)) };
    } });
  server.listen(0, '127.0.0.1'); await once(server, 'listening');
  t.after(() => { server.closeAllConnections(); server.close(); });
  state.call = async (fields, path = '/webapi/entry.cgi') => (await fetch('http://127.0.0.1:' + server.address().port + path,
    { method: 'POST', body: fields instanceof FormData ? fields : new URLSearchParams(fields) })).json();
  return state;
}

test('ERP membership is mandatory, including administrators; stale data fails closed', () => {
  const now = Date.now();
  const snapshot = { version: 1, generatedAt: now, users: [member('staff'), member('admin-only', ['administrators'])] };
  assert.deepEqual([...directoryMembers(snapshot).keys()], ['staff']);
  assert.throws(() => directoryMembers({ ...snapshot, generatedAt: now - 181000 }));
});

test('alternate and encoded routes never forward to DSM', async t => {
  const state = await fixture(t);
  for (const path of ['/', '/webman/index.cgi', '/webapi%2fentry.cgi', '/webapi/extra/entry.cgi']) {
    const result = await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff' }, path);
    assert.equal(result.error.code, 104);
  }
  assert.equal(state.calls.length, 0);
});

test('non-ERP login is rejected before credentials reach the upstream', async t => {
  const state = await fixture(t);
  const result = await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'admin-only', passwd: 'fixture' });
  assert.equal(result.error.code, 460); assert.equal(state.calls.length, 0);
});

test('directory names and email replace stale Chat profiles and exclude outsiders', async t => {
  const state = await fixture(t);
  await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff', passwd: 'fixture' });
  const result = await state.call({ api: 'SYNO.Chat.User', method: 'list', _sid: 'fixture-session' });
  assert.equal(result.data.users.length, 1);
  assert.equal(result.data.users[0].description, '홍길동 | 사원');
  assert.equal(result.data.users[0].user_props.email, 'staff@example.test');
});

test('removal from ERP blocks existing sessions and mutations immediately', async t => {
  const state = await fixture(t);
  await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff', passwd: 'fixture' });
  state.snapshot.users[0].groups = ['users'];
  const before = state.calls.length;
  const result = await state.call({ api: 'SYNO.Chat.Post', method: 'create', _sid: 'fixture-session', message: 'fixture' });
  assert.equal(result.error.code, 460); assert.equal(state.calls.length, before);
});

test('gateway restart validates an unknown session with the upstream, never account hints', async t => {
  const state = await fixture(t);
  const invalid = await state.call({ api: 'SYNO.Chat.User', method: 'list', _sid: 'bad-session', account: 'staff' });
  assert.equal(invalid.error.code, 119);
  const valid = await state.call({ api: 'SYNO.Chat.User', method: 'list', _sid: 'fixture-session', account: 'outsider' });
  assert.equal(valid.data.users[0].username, 'staff');
});

test('ID-only login responses resolve identity through the authenticated user list', async t => {
  const state = await fixture(t); state.idOnly = true;
  await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff', passwd: 'fixture' });
  const result = await state.call({ api: 'SYNO.Chat.User', method: 'login', _sid: 'fixture-session' });
  assert.equal(result.success, true);
  assert.equal(result.data.user.username, 'staff');
  assert.equal(result.data.user.description, '홍길동 | 사원');
  const restarted = await state.call({ api: 'SYNO.Chat.User', method: 'list', _sid: 'uncached-session' });
  assert.equal(restarted.success, true);
});

test('stale directory rejects requests without logging out or contacting the upstream', async t => {
  const state = await fixture(t);
  state.snapshot.generatedAt -= 181000;
  const result = await state.call({ api: 'SYNO.API.Auth', method: 'login', account: 'staff', passwd: 'fixture' });
  assert.equal(result.error.code, 461); assert.equal(state.calls.length, 0);
});
