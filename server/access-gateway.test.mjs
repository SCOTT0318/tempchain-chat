import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { createAccessGateway, directoryMembers } from './access-gateway.mjs';

const member = (account, groups = ['erp']) => ({ account, uid: 1001, description: '홍길동 | 사원', email: account + '@example.test', groups });
async function fixture(t) {
  const now = Date.now();
  const state = { snapshot: { version: 1, generatedAt: now, users: [member('staff'), member('admin-only', ['administrators'])] }, calls: [] };
  const server = createAccessGateway({ now: () => now, readDirectory: async () => state.snapshot,
    forward: async request => {
      const fields = new URLSearchParams(request.body.toString());
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
    { method: 'POST', body: new URLSearchParams(fields) })).json();
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
