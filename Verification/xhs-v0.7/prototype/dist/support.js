(function (root) {
  'use strict';

  const distance = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
  const insideBase = e => Math.abs(e.x) <= 26 && e.z >= -21 && e.z <= 26;
  const FORTIFY = {
    command: { cost: 120, delta: 650 },
    barracks: { cost: 100, delta: 450 },
    bunker: { cost: 120, delta: 600 },
    factory: { cost: 180, delta: 700 }
  };

  class FrontierSupport {
    constructor(world) {
      this.world = world;
      this.rallySize = 6;
      this.maxAssault = 24;
      this.rallyCenter = { x: 0, z: 12 };
      this.assaultTimer = 8;
      this.assaultWave = 0;
      this.sourceIndex = 0;
      this.levels = Object.create(null);
      this.workerTimers = [null, null];
      this.workerIdle = [{ x: -6.5, z: 0 }, { x: 6.5, z: 0 }];
      for (let slot = 0; slot < 2; slot++) this.spawnWorker(slot);
    }

    get assaults() {
      return this.world.entities.filter(e => e.alive && e.role === 'assault');
    }

    get assaultCount() { return this.assaults.length; }
    get rallyCount() { return this.assaults.filter(e => e.assaultState === 'rally').length; }
    get attackingCount() { return this.assaults.filter(e => e.assaultState === 'attack').length; }
    get reserveCount() { return this.assaults.filter(e => e.assaultState === 'reserve').length; }
    get workers() { return this.world.entities.filter(e => e.alive && e.role === 'worker'); }

    level(id) { return this.levels[id] || 0; }

    site(id) {
      return id === 'command'
        ? { id: 'command', role: 'command', name: '指挥中心', entity: this.world.command }
        : this.world.sites.find(s => s.id === id) || null;
    }

    healthBonus(id) {
      const site = this.site(id), upgrade = site && FORTIFY[site.role];
      return upgrade ? upgrade.delta * this.level(id) : 0;
    }

    fortifyInfo(id) {
      const site = this.site(id), upgrade = site && FORTIFY[site.role];
      if (!upgrade) return null;
      const level = this.level(id), entity = site.entity;
      return {
        level, maxLevel: 3, cost: upgrade.cost * (1 + level), delta: upgrade.delta,
        entity, available: level < 3 && !!entity && entity.alive && entity.hp > 0 && !entity.constructing
      };
    }

    buy(action) {
      const parts = String(action).split(':'), kind = parts[0], id = parts[1];
      if (kind !== 'fortify' && kind !== 'repair') return null;
      if (this.world.canManage && !this.world.canManage()) return false;
      const info = this.fortifyInfo(id);
      if (!info || !info.available || !this.world.spend(info.cost)) return false;
      // The bonus belongs to the site, so destruction cannot erase purchased levels.
      this.levels[id] = info.level + 1;
      info.entity.max += info.delta;
      info.entity.hp = Math.min(info.entity.max, info.entity.hp + info.delta);
      this.world.notice(this.site(id).name + ' 结构加固 ' + this.levels[id] + '/3 · 生命上限 +' + info.delta);
      return true;
    }

    spawnWorker(slot) {
      const point = this.workerIdle[slot], e = this.world.spawn('worker', point.x, point.z);
      e.workerSlot = slot;
      e.enemy = false;
      e.repairTarget = null;
      e.repairing = null;
      e.repairThink = 0;
      e.servicePoint = null;
      this.workerTimers[slot] = null;
      return e;
    }

    closestBase(origin) {
      let best = null, bestDistance = Infinity;
      for (const base of this.world.enemyBases || []) {
        if (!base.portal || !base.portal.alive || base.portal.hp <= 0) continue;
        const d = distance(origin, base);
        if (d < bestDistance) { best = base; bestDistance = d; }
      }
      return best;
    }

    spawnAssault(sources) {
      const source = sources[this.sourceIndex++ % sources.length];
      const point = { x: source.x, z: source.z + source.zs / 2 + 1.1 };
      if (this.world.fits && !this.world.fits(point.x, point.z, 0.45)) return false;
      const waiting = this.assaults.filter(e => e.assaultState === 'rally');
      let slot = 0;
      while (waiting.some(e => e.rallySlot === slot)) slot++;
      const e = this.world.spawn('assault', point.x, point.z);
      e.enemy = false;
      e.assault = true;
      e.assaultState = 'rally';
      e.rallySlot = slot;
      // Six members have distinct positions, and must reach them before departure.
      e.rallyPoint = {
        x: this.rallyCenter.x + (slot % 3 - 1) * 2.3,
        z: this.rallyCenter.z + Math.floor(slot / 3) * 1.8 - 0.9
      };
      e.attackBase = null;
      e.assaultStage = 0;
      return true;
    }

    tick(dt) {
      if (!this.world.active || !Number.isFinite(dt) || dt <= 0) return;
      const base = this.closestBase(this.rallyCenter);
      // Once all portals are gone, survivors form a reserve inside the base.
      // There is no new expedition to fill, so production and rally counts stop.
      if (!base) {
        this.assaults.forEach((e, index) => {
          if (e.assaultState === 'reserve') return;
          e.assaultState = 'reserve';
          e.attackBase = null;
          e.rallyPoint = { x: (index % 6 - 2.5) * 2, z: 9 + Math.floor(index / 6) * 2 };
          e.path = [];
          e.repath = 0;
        });
      }
      const sources = this.world.sites.filter(s => s.role === 'barracks' && s.state === 'ready' && s.entity && s.entity.alive);
      if (base && sources.length && this.assaultCount < this.maxAssault) {
        this.assaultTimer -= dt;
        if (this.assaultTimer <= 0 && this.spawnAssault(sources)) {
          this.assaultTimer = this.world.ready('reserve') ? 6 : 8;
        }
      }

      const assembled = this.assaults.filter(e => e.assaultState === 'rally' && distance(e, e.rallyPoint) <= 0.85);
      if (base && assembled.length >= this.rallySize) {
        const wave = ++this.assaultWave;
        for (const e of assembled.slice(0, this.rallySize)) this.assignAttack(e, base, wave);
        this.world.notice('突击队集结完成 · ' + this.rallySize + ' 人出发进攻传送门基地');
      }

      for (let slot = 0; slot < 2; slot++) {
        if (this.workers.some(e => e.workerSlot === slot)) {
          this.workerTimers[slot] = null;
        } else if (this.workerTimers[slot] === null) {
          this.workerTimers[slot] = 20;
        } else {
          this.workerTimers[slot] = Math.max(0, this.workerTimers[slot] - dt);
          if (this.workerTimers[slot] === 0 && this.world.command.alive) {
            this.spawnWorker(slot);
            this.world.notice('维修工已补充，恢复基地维修');
          }
        }
      }
    }

    assignAttack(e, base, wave) {
      e.assaultState = 'attack';
      e.attackBase = base.id;
      e.assaultWave = wave;
      e.assaultFormation = e.rallySlot % this.rallySize;
      // An existing forward unit need not return to the base when its objective falls.
      e.assaultStage = e.z > 44 ? 3 : e.z > 30 ? 2 : e.z > 21 ? 1 : 0;
      e.attackLane = base.x < 0 ? -13.5 : base.x > 0 ? 13.5 : e.x < 0 ? -13.5 : 13.5;
      e.path = [];
      e.repath = 0;
    }

    actor(e, dt, all) {
      if (e.role !== 'assault' && e.role !== 'worker') return false;
      if (!this.world.active || !e.alive || dt <= 0) return true;
      if (e.role === 'worker') this.workerActor(e, dt);
      else this.assaultActor(e, dt, all);
      return true;
    }

    assaultActor(e, dt, all) {
      const world = this.world, range = 25;
      e.cooldown = Math.max(0, e.cooldown - dt);
      let base = null;
      if (e.assaultState === 'attack') {
        base = (world.enemyBases || []).find(b => b.id === e.attackBase && b.portal && b.portal.alive && b.portal.hp > 0);
        if (!base) {
          base = this.closestBase(e);
          if (base) this.assignAttack(e, base, e.assaultWave);
        }
      }
      let target = null, best = Infinity;
      for (const candidate of all) {
        if (!candidate.alive || !candidate.enemy || candidate.bunker) continue;
        const d = distance(e, candidate);
        if (d > range + candidate.r || !world.visible(e, candidate)) continue;
        const score = d + (world.building(candidate) ? 5 : 0) - (candidate.id === world.mark ? 10 : 0);
        if (score < best) { target = candidate; best = score; }
      }
      if (target) {
        e.angle = Math.atan2(target.x - e.x, target.z - e.z);
        if (e.cooldown <= 0 && world.shootAt(e, target, 12 + world.defense * 3, range)) e.cooldown = 0.66;
        if (e.assaultState === 'attack') { e.speed = 0; return; }
      }
      if (e.assaultState === 'rally' || e.assaultState === 'reserve') {
        world.go(e, e.rallyPoint, 4.3, dt);
        return;
      }

      if (!base) {
        world.go(e, e.rallyPoint, 4.3, dt);
        return;
      }
      const lane = e.attackLane + (e.assaultFormation % 3 - 1) * 1.25;
      const route = [{ x: lane, z: 22 }, { x: lane, z: 32 }, { x: lane, z: 46 }];
      while (e.assaultStage < route.length && distance(e, route[e.assaultStage]) < 1.4) e.assaultStage++;
      // Approach a traversable point in front of the portal, not the solid building center.
      const goal = e.assaultStage < route.length ? route[e.assaultStage] : { x: base.x, z: base.z - 14 };
      world.go(e, goal, 4.3, dt);
    }

    canRepair(target) {
      if (!target || !target.alive || target.enemy || target.hp <= 0 || target.hp >= target.max || target.constructing || !insideBase(target)) return false;
      return this.world.building(target) || target.mechanical === true || target.role === 'tank' || target.role === 'aircraft';
    }

    servicePoint(worker, target) {
      const world = this.world;
      const isBuilding = world.building(target);
      const site = isBuilding && world.sites.find(s => s.entity === target);
      const box = isBuilding && world.boxes().find(b => b.entity === target);
      const halfX = isBuilding ? (box ? box.xs : site ? site.xs : 9.5) / 2 : target.r;
      const halfZ = isBuilding ? (box ? box.zs : site ? site.zs : 8.2) / 2 : target.r;
      const gap = worker.r + 0.6;
      const points = [
        { x: target.x - halfX - gap, z: target.z },
        { x: target.x + halfX + gap, z: target.z },
        { x: target.x, z: target.z - halfZ - gap },
        { x: target.x, z: target.z + halfZ + gap }
      ];
      for (const x of [-1, 1]) for (const z of [-1, 1]) {
        points.push({ x: target.x + x * (halfX + gap), z: target.z + z * (halfZ + gap) });
      }
      let best = null, bestDistance = Infinity;
      for (const point of points) {
        if (!insideBase(point) || !world.fits(point.x, point.z, worker.r, worker)) continue;
        const probe = Object.assign({}, worker, { x: point.x, z: point.z, y: 0, bunker: null });
        if (!world.visible(probe, target)) continue;
        const d = distance(worker, point);
        if (d < bestDistance) { best = point; bestDistance = d; }
      }
      return best;
    }

    chooseRepair(worker) {
      let best = null, point = null, bestScore = Infinity;
      for (const target of this.world.entities) {
        if (!this.canRepair(target)) continue;
        const service = this.servicePoint(worker, target);
        if (!service) continue;
        const occupied = this.workers.some(e => e !== worker && e.repairTarget === target);
        const urgency = (1 - target.hp / target.max) * (target.role === 'command' ? 9 : 4);
        const score = distance(worker, service) + (occupied ? 5 : 0) - urgency;
        if (score < bestScore) { best = target; point = service; bestScore = score; }
      }
      worker.repairTarget = best;
      worker.servicePoint = point;
      worker.serviceTargetPosition = best ? { x: best.x, z: best.z } : null;
      worker.path = [];
      worker.repath = 0;
    }

    moveWorker(e, goal, dt) {
      const previous = { x: e.x, y: e.y, z: e.z };
      this.world.go(e, goal, 3.6, dt);
      // Worker movement, including pathfinding detours, is confined to the base.
      if (!insideBase(e)) {
        Object.assign(e, previous);
        e.path = [];
        e.repath = 0;
        e.speed = 0;
      }
    }

    workerActor(e, dt) {
      e.repairing = null;
      e.repairEffect = Math.max(0, (e.repairEffect || 0) - dt);
      e.repairThink -= dt;
      if (!this.canRepair(e.repairTarget)) {
        if (e.repairTarget || e.repairThink <= 0) {
          this.chooseRepair(e);
          e.repairThink = 0.6;
        }
      }
      const target = e.repairTarget;
      if (!target) { this.moveWorker(e, this.workerIdle[e.workerSlot], dt); return; }
      if (!e.servicePoint || !e.serviceTargetPosition || distance(target, e.serviceTargetPosition) > 0.35) {
        e.servicePoint = this.servicePoint(e, target);
        e.serviceTargetPosition = { x: target.x, z: target.z };
        e.path = [];
        e.repath = 0;
      }
      if (!e.servicePoint) { e.repairTarget = null; return; }
      if (distance(e, e.servicePoint) > 0.65) {
        this.moveWorker(e, e.servicePoint, dt);
        return;
      }
      e.speed = 0;
      if (!insideBase(e) || !this.canRepair(target) || !this.world.visible(e, target)) return;
      e.angle = Math.atan2(target.x - e.x, target.z - e.z);
      target.hp = Math.min(target.max, target.hp + 6 * dt);
      e.repairing = target;
      if (e.repairEffect <= 0) {
        this.world.events.push({
          type: 'repair',
          a: { x: e.x, y: e.y + 1.1, z: e.z },
          b: { x: target.x, y: target.y + target.height, z: target.z }
        });
        e.repairEffect = 0.25;
      }
    }
  }

  root.FrontierSupport = FrontierSupport;
  if (typeof module !== 'undefined' && module.exports) module.exports = FrontierSupport;
})(typeof window === 'undefined' ? globalThis : window);
