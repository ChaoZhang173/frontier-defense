'use strict';
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const distDir = path.resolve(__dirname, '../../XhsMiniTool/dist');
const { Game, SPECS, dist } = require(path.join(distDir, 'core.js'));
const checks = [];

function check(name, run) {
  try { checks.push({ name, pass: true, ...(run() || {}) }); }
  catch (error) { checks.push({ name, pass: false, error: error.stack }); }
  console.log(JSON.stringify(checks[checks.length - 1]));
}

function game() {
  const g = new Game();
  g.phase = 'intermission';
  g.intermission = 99999;
  g.tickEnemyBases = () => {};
  g.refreshBoxes();
  return g;
}

// Real movement, line of sight and damage; enemy attacks are deliberately held
// still in these focused tests so support behavior is not a campaign balance test.
function supportStep(g, dt = 0.1, move = true) {
  g.refreshBoxes();
  g.time += dt;
  g.support.tick(dt);
  if (move) {
    const all = g.entities.filter(e => e.alive);
    for (const e of all) if (e.role === 'worker' || e.role === 'assault') g.actor(e, dt, all);
  }
  g.events = [];
}

function supportAdvance(g, seconds, move = true) {
  for (let i = 0; i < Math.ceil(seconds / 0.1); i++) supportStep(g, 0.1, move);
}

function readySite(g, id) {
  const site = g.sites.find(s => s.id === id);
  if (!site.entity || !site.entity.alive) site.entity = g.spawn(site.role, site.x, site.z);
  site.entity.site = id;
  site.state = 'ready';
  g.refreshBoxes();
  return site;
}

check('Six real arrivals are required; unarrived recruits cannot trigger departure', () => {
  const g = game(), s = g.support, arrivals = [];
  supportAdvance(g, 56, false);
  assert(s.rallyCount >= 6);
  assert.strictEqual(s.attackingCount, 0);
  const assign = s.assignAttack.bind(s);
  s.assignAttack = (e, base, wave) => {
    if (e.assaultState === 'rally') {
      const distance = dist(e, e.rallyPoint);
      assert(distance <= 0.85);
      arrivals.push({ id: e.id, distance, x: e.x, z: e.z });
    }
    assign(e, base, wave);
  };
  s.assaultTimer = 10000;
  for (let i = 0; i < 400 && s.attackingCount < 6; i++) supportStep(g);
  assert.strictEqual(arrivals.length, 6);
  assert.strictEqual(s.attackingCount, 6);
  return { unarrivedWereHeld: true, arrivals };
});

check('Full guard quota and existing tank do not block automatic assault production', () => {
  const g = game(), s = g.support;
  while (g.marines.length < g.capacity) g.addMarine(null, false);
  const guards = g.marines.map(e => e.id);
  const tank = g.spawn('tank', 0, 21);
  const originalActor = g.actor.bind(g);
  g.actor = (e, dt, all) => { if (e.role === 'assault' || e.role === 'worker') originalActor(e, dt, all); };
  for (let i = 0; i < 700; i++) { g.tick(0.1); g.events = []; }
  assert.deepStrictEqual(g.marines.map(e => e.id), guards);
  assert.strictEqual(g.marines.length, g.capacity);
  assert.strictEqual(g.squad.length, 0);
  assert.strictEqual(g.tank, tank);
  assert(s.assaultCount >= 8 && s.attackingCount >= 6);
  assert(s.assaults.every(e => !e.squad && e.role !== 'marine'));
  return { guardCount: guards.length, assaultCount: s.assaultCount, departed: s.attackingCount };
});

check('Recruitment uses eight-second cadence, six with reserve, and a separate 24-unit cap', () => {
  const g = game(), s = g.support;
  supportAdvance(g, 7.9, false); assert.strictEqual(s.assaultCount, 0);
  supportStep(g, 0.2, false); assert.strictEqual(s.assaultCount, 1);
  assert.strictEqual(s.assaultTimer, 8);
  readySite(g, 'reserve'); s.assaultTimer = 0;
  supportStep(g, 0.1, false); assert.strictEqual(s.assaultTimer, 6);
  supportAdvance(g, 5.9, false); assert.strictEqual(s.assaultCount, 2);
  supportStep(g, 0.2, false); assert.strictEqual(s.assaultCount, 3);
  for (let i = 0; i < 40; i++) { s.assaultTimer = 0; supportStep(g, 0.1, false); }
  assert.strictEqual(s.assaultCount, 24);
  for (const site of g.sites.filter(site => site.role === 'barracks')) site.entity.alive = false;
  s.assaults[0].alive = false; s.assaultTimer = 0;
  supportAdvance(g, 20, false); assert.strictEqual(s.assaultCount, 23);
  return { cappedCount: 24, noBarracksCount: s.assaultCount, reserveCadence: 6 };
});

check('An assembled assault physically reaches and damages a living enemy portal compound', () => {
  const g = game(), s = g.support;
  const base = s.closestBase(s.rallyCenter), portalHP = base.portal.hp;
  let maxZ = 0, damageAt = null;
  for (let i = 0; i < 2600; i++) {
    supportStep(g);
    for (const e of s.assaults) maxZ = Math.max(maxZ, e.z);
    if (base.portal.hp < portalHP) { damageAt = g.time; break; }
  }
  assert(maxZ > 50);
  assert(base.portal.hp < portalHP, 'No portal damage after 260 active seconds');
  return { base: base.id, damageAtSeconds: damageAt, portalDamage: portalHP - base.portal.hp, maxZ };
});

check('A destroyed objective is retargeted to the nearest surviving portal', () => {
  const g = game(), s = g.support;
  s.assaultTimer = 0; supportStep(g, 0.1, false);
  const e = s.assaults[0], old = s.closestBase(e);
  e.x = old.x; e.z = old.z - 25; e.y = -4;
  s.assignAttack(e, old, 1);
  g.hurt(old.portal, 100000, g.player);
  const next = s.closestBase(e);
  s.actor(e, 0.1, g.entities);
  assert.notStrictEqual(e.attackBase, old.id);
  assert.strictEqual(e.attackBase, next.id);
  return { old: old.id, next: next.id, continuesForward: e.assaultStage === 3 };
});

check('All portals destroyed stops recruitment and returns survivors to a central reserve', () => {
  const g = game(), s = g.support;
  for (let i = 0; i < 8; i++) { s.assaultTimer = 0; supportStep(g, 0.1, false); }
  s.assignAttack(s.assaults[0], g.enemyBases[0], 1);
  for (const base of g.enemyBases) g.hurt(base.portal, 100000, g.player);
  const count = s.assaultCount;
  supportAdvance(g, 45);
  assert.strictEqual(s.assaultCount, count);
  assert.strictEqual(s.rallyCount, 0);
  assert.strictEqual(s.attackingCount, 0);
  assert.strictEqual(s.reserveCount, count);
  assert(s.assaults.every(e => dist(e, e.rallyPoint) < 0.7));
  assert.strictEqual(new Set(s.assaults.map(e => JSON.stringify(e.rallyPoint))).size, count);
  return { reserves: count, newRecruits: 0 };
});

check('Workers repair only after physically reaching a visible service point, at six HP per second', () => {
  const g = game(), s = g.support, worker = s.workers[0];
  assert.strictEqual(s.workers.length, 2);
  g.command.hp -= 200;
  const before = g.command.hp;
  s.actor(worker, 0.1, g.entities);
  assert(worker.servicePoint);
  assert(dist(worker, worker.servicePoint) > 0.65);
  assert.strictEqual(g.command.hp, before);
  let travelled = 0, last = { x: worker.x, z: worker.z };
  for (let i = 0; i < 200 && !worker.repairing; i++) {
    s.actor(worker, 0.1, g.entities);
    travelled += dist(worker, last); last = { x: worker.x, z: worker.z };
  }
  assert(worker.repairing === g.command);
  assert(travelled > 1);
  assert(dist(worker, worker.servicePoint) <= 0.65);
  assert(g.visible(worker, g.command));
  const first = g.command.hp;
  g.events = [];
  for (let i = 0; i < 10; i++) s.actor(worker, 0.1, g.entities);
  assert(Math.abs(g.command.hp - first - 6) < 1e-7);
  const beams = g.events.filter(e => e.type === 'repair');
  assert(beams.length > 0 && beams.length <= 4);
  assert(beams.every(e => Number.isFinite(e.a.y) && Number.isFinite(e.b.y)));
  return { travelled, serviceDistance: dist(worker, worker.servicePoint), repairedInOneSecond: g.command.hp - first, throttledBeams: beams.length };
});

check('Workers repair a factory and tank inside the base without healing infantry, dead or outside units', () => {
  const g = game(), s = g.support;
  s.assaultTimer = 10000;
  const factory = readySite(g, 'factory').entity; factory.hp -= 120;
  const tank = g.spawn('tank', 12, 8); tank.hp -= 90;
  const outside = g.spawn('tank', 0, 36); outside.hp = 11;
  const dead = g.spawn('tank', -10, 8); dead.alive = false; dead.hp = 0;
  const infantry = g.marines[0]; infantry.hp = 1;
  const construction = readySite(g, 'reserve').entity; construction.constructing = true; construction.hp = 20;
  const aircraft = { ...tank, role: 'aircraft', hp: 3 }; assert(s.canRepair(aircraft));
  let stayedInside = true;
  for (let i = 0; i < 550; i++) {
    supportStep(g);
    stayedInside = stayedInside && s.workers.every(e => Math.abs(e.x) <= 26 && e.z >= -21 && e.z <= 26);
  }
  assert.strictEqual(factory.hp, factory.max); assert.strictEqual(tank.hp, tank.max);
  assert.strictEqual(infantry.hp, 1); assert.strictEqual(outside.hp, 11);
  assert.strictEqual(dead.hp, 0); assert.strictEqual(dead.alive, false);
  assert.strictEqual(construction.hp, 20); assert(stayedInside);
  factory.hp = factory.max - 0.05;
  supportAdvance(g, 25); assert.strictEqual(factory.hp, factory.max);
  return { factoryRestored: 120, tankRestored: 90, noOverheal: true, workersStayedInside: stayedInside };
});

check('Line of sight blocks repair even when a worker is already at the service point', () => {
  const g = game(), s = g.support, worker = s.workers[0];
  g.command.hp -= 200;
  s.chooseRepair(worker);
  Object.assign(worker, worker.servicePoint);
  const point = worker.servicePoint, target = g.command;
  g.walls.push({ x: (point.x + target.x) / 2, z: (point.z + target.z) / 2, y: 4, xs: 1.5, zs: 1.5, ys: 8 });
  g.refreshBoxes();
  assert(!g.visible(worker, target));
  const hp = target.hp;
  s.actor(worker, 0.1, g.entities);
  assert.strictEqual(target.hp, hp);
  assert.strictEqual(worker.repairing, null);
});

check('Pause and open terminal freeze repair, replacement and production; workers replace for free after 20 seconds', () => {
  const g = game(), s = g.support, worker = s.workers[0];
  g.command.hp -= 200; s.chooseRepair(worker); Object.assign(worker, worker.servicePoint);
  s.workers[1].alive = false; s.tick(0.1);
  for (const key of ['paused', 'terminal']) {
    g[key] = true;
    g.events = [];
    const before = JSON.stringify({ hp: g.command.hp, workers: s.workerTimers, production: s.assaultTimer, x: worker.x, z: worker.z });
    s.tick(5); s.actor(worker, 5, g.entities); g.tick(0.1);
    assert.strictEqual(JSON.stringify({ hp: g.command.hp, workers: s.workerTimers, production: s.assaultTimer, x: worker.x, z: worker.z }), before);
    assert.strictEqual(g.events.length, 0);
    g[key] = false;
  }
  const credits = g.credits;
  supportAdvance(g, 19.9, false); assert.strictEqual(s.workers.length, 1);
  supportStep(g, 0.2, false); assert.strictEqual(s.workers.length, 2);
  assert.strictEqual(g.credits, credits);
  assert.deepStrictEqual(s.workers.map(e => e.workerSlot).sort(), [0, 1]);
  return { replacementDelay: 20, replacementCost: 0 };
});

check('Fortification increases maximum and current health by exactly the same amount, including at full health', () => {
  const g = game(), s = g.support;
  readySite(g, 'factory'); g.credits = 100000; g.terminal = true;
  const details = [];
  for (const [id, delta, baseCost] of [['command', 650, 120], ['barracks', 450, 100], ['west', 600, 120], ['factory', 700, 180]]) {
    const entity = s.site(id).entity;
    for (let level = 0; level < 3; level++) {
      if (level > 0) entity.hp -= 37;
      const before = { max: entity.max, hp: entity.hp, credits: g.credits };
      assert(g.buy((level === 1 ? 'repair:' : 'fortify:') + id));
      assert.strictEqual(entity.max - before.max, delta);
      assert.strictEqual(entity.hp - before.hp, delta);
      assert.strictEqual(before.credits - g.credits, baseCost * (level + 1));
    }
    const credits = g.credits;
    assert.strictEqual(g.buy('fortify:' + id), false); assert.strictEqual(g.credits, credits);
    assert.strictEqual(s.healthBonus(id), delta * 3);
    details.push({ id, delta, level: s.level(id), totalBonus: s.healthBonus(id) });
  }
  assert.strictEqual(s.buy('weapon'), null);
  return { upgrades: details };
});

check('A destroyed and rebuilt bunker retains site fortification plus armor after real construction completes', () => {
  const g = game(), s = g.support;
  g.credits = 100000; g.terminal = true;
  assert(g.buy('fortify:west')); assert(g.buy('fortify:west'));
  assert(g.buy('armor'));
  const site = g.sites.find(site => site.id === 'west');
  const previous = site.entity;
  g.hurt(previous, 100000, g.player);
  assert.strictEqual(site.state, 'destroyed');
  assert.strictEqual(g.buy('fortify:west'), false);
  assert(g.buy('build:west')); assert(site.entity !== previous);
  assert.strictEqual(g.buy('fortify:west'), false);
  g.terminal = false;
  const originalActor = g.actor.bind(g);
  g.actor = (e, dt, all) => { if (e.role === 'worker' || e.role === 'assault') originalActor(e, dt, all); };
  for (let i = 0; i < 200; i++) { g.tick(0.1); g.events = []; }
  assert.strictEqual(site.state, 'ready'); assert.strictEqual(site.entity.constructing, false);
  assert.strictEqual(s.level('west'), 2);
  assert.strictEqual(site.entity.max, SPECS.bunker[0] + 400 * g.armor + 1200);
  assert.strictEqual(site.entity.hp, site.entity.max);
  return { retainedLevel: s.level('west'), rebuiltMaximum: site.entity.max, armorLevel: g.armor };
});

const report = {
  at: new Date().toISOString(),
  sources: Object.fromEntries(['core.js', 'support.js'].map(name => [name, crypto.createHash('sha256').update(fs.readFileSync(path.join(distDir, name))).digest('hex')])),
  method: 'Actual CommonJS game/support with collision-aware movement, visibility, damage and construction. Focused support simulations retain living enemy bases but hold enemy attacks and reinforcements still; this is not a campaign balance or GPU/device test.',
  summary: { pass: checks.filter(check => check.pass).length, fail: checks.filter(check => !check.pass).length, total: checks.length },
  checks
};
fs.writeFileSync(path.join(__dirname, 'support-review.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report.summary));
process.exitCode = report.summary.fail ? 1 : 0;
