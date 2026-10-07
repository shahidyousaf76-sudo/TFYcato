// TFY Cat Adventure — web prototype game loop. Same design, rules and levels as the Unity game;
// saves locally (this prototype has no Firebase — the Unity build syncs with Firebase).
(function () {
  'use strict';
  const C = window.TFY_CONTENT, RT = window.TFY_RUNTIME, R = window.TFYRules, ART = window.TFYArt, AU = window.TFYAudio, T = window.THREE;
  const $ = (id) => document.getElementById(id);
  const LANE = R.LANE_WIDTH;
  const PHYS = { jump: 11.5, dbl: 10.2, g: 30, fastFall: -24, lj: 10.5, ljUp: 15, ljDown: 22, slide: 0.7, coyote: 0.12, buffer: 0.16, stun: 0.35, invuln: 1.5 };

  // ------------------------------------------------------------------ save (offline-first, local)
  const SAVE_KEY = 'tfy.catadventure.save.v1', HINTS_KEY = 'tfy.catadventure.hints.v1';
  let P = null, isNew = false;
  function loadSave() {
    try { const s = JSON.parse(localStorage.getItem(SAVE_KEY)); if (s && s.username) return Object.assign(R.newPlayer(s.username), s); } catch (e) { /* corrupt: start fresh */ }
    isNew = true; return R.newPlayer(R.generateUsername(C.usernameWords.prefixes));
  }
  const save = () => { try { localStorage.setItem(SAVE_KEY, JSON.stringify(P)); } catch (e) { toast("Couldn't save right now — we'll try again."); } };
  const seenHints = new Set((localStorage.getItem(HINTS_KEY) || '').split(',').filter(Boolean));
  const levelById = (id) => C.levels.find((l) => l.id === id);
  const levelByNum = (n) => C.levels.find((l) => l.number === n);
  const world = (id) => C.worlds.find((w) => w.id === id);
  const item = (id) => C.shop.find((i) => i.id === id);

  // ------------------------------------------------------------------ renderer, scene, lights
  const canvas = $('gl');
  const renderer = new T.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
  renderer.outputEncoding = T.sRGBEncoding; renderer.toneMapping = T.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.1;
  renderer.shadowMap.enabled = true; renderer.shadowMap.type = T.PCFSoftShadowMap;
  const scene = new T.Scene();
  const camera = new T.PerspectiveCamera(60, 1, 0.2, 520);
  const hemi = new T.HemisphereLight(0xffffff, 0x777777, 0.9); scene.add(hemi);
  const sun = new T.DirectionalLight(0xffffff, 2.2); sun.castShadow = true; sun.shadow.camera.left = -18; sun.shadow.camera.right = 18; sun.shadow.camera.top = 22; sun.shadow.camera.bottom = -22; sun.shadow.camera.far = 120; sun.shadow.bias = -0.0005;
  scene.add(sun); scene.add(sun.target);
  const moon = new T.DirectionalLight(0xb8ccff, 0); scene.add(moon);
  const nightLights = [0, 1, 2, 3, 4, 5].map(() => { const l = new T.PointLight(0xffd49a, 0, 13, 2); scene.add(l); return l; });
  const worldRoot = new T.Group(), backdrop = new T.Group(), trackRoot = new T.Group(); scene.add(worldRoot, backdrop, trackRoot);
  let quality = { tier: 'high', scenery: 1, shadows: true, particles: 1, lights: 4, bloom: 0.8, pr: 1 };
  // Bloom (HDR glow) and GPU particles — see js/fx.js.
  const bloom = window.TFYFx.createBloom(renderer, scene, camera);
  const fx = window.TFYFx.createFx(scene);

  function applyQuality() {
    const g = P.settings.graphics, mobile = /Android|iPhone|iPad/i.test(navigator.userAgent), dpr = window.devicePixelRatio || 1;
    const tier = g === 'auto' ? (mobile ? (dpr > 2.5 ? 'medium' : 'high') : 'ultra') : g;
    quality = { low: { tier, scenery: 0.5, shadows: false, particles: 0.35, lights: 0, pr: Math.min(dpr, 1) * 0.8, bloom: 0 }, medium: { tier, scenery: 0.75, shadows: true, particles: 0.6, lights: 2, pr: Math.min(dpr, 1.5), map: 1024, bloom: 0.6 },
      high: { tier, scenery: 1, shadows: true, particles: 1, lights: 4, pr: Math.min(dpr, 2), map: 2048, bloom: 0.8 }, ultra: { tier, scenery: 1.2, shadows: true, particles: 1, lights: 6, pr: Math.min(dpr, 2), map: 2048, bloom: 0.95 } }[tier];
    renderer.setPixelRatio(quality.pr); renderer.shadowMap.enabled = quality.shadows; sun.castShadow = quality.shadows;
    if (quality.map) { sun.shadow.mapSize.set(quality.map, quality.map); if (sun.shadow.map) { sun.shadow.map.dispose(); sun.shadow.map = null; } }
    fx.setBudget(quality.particles * (P.settings.reduceMotion ? 0.5 : 1));
    resize(); configureBloom();
  }
  /** Bloom strength follows quality and time of day (lamps, windows and fireflies glow more at night). */
  // Threshold is in linear HDR: sunlit ground/sky stay below it, emissive coins/gems/lamps/sparkles go above it.
  function configureBloom() { if (bloom) bloom.configure(quality.bloom * (isNight ? 1.25 : 0.85), isNight ? 1.2 : 2.6, 1.1); }
  function resize() {
    const w = window.innerWidth, h = window.innerHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.fov = w / h < 0.7 ? 68 : 60; camera.updateProjectionMatrix();
    if (bloom) bloom.setSize(w, h, quality.pr || 1);
    fx.setViewport(h * (quality.pr || 1), camera.fov);
  }
  window.addEventListener('resize', resize);

  // ------------------------------------------------------------------ sky, time of day
  const TOD = {
    sunrise: { el: 8, sun: 1.3, hemi: 0.75, sunCol: '#FFB880', tint: '#FFC0A6', moon: 0, night: 0.3 }, morning: { el: 28, sun: 2.0, hemi: 0.95, sunCol: '#FFF0D8', tint: '#D8ECFF', moon: 0, night: 0 },
    day: { el: 50, sun: 2.4, hemi: 1.0, sunCol: '#FFF8EE', tint: '#CFE6FF', moon: 0, night: 0 }, sunset: { el: 10, sun: 1.7, hemi: 0.8, sunCol: '#FF9A5C', tint: '#FF9E80', moon: 0, night: 0.6 },
    evening: { el: -4, sun: 0.25, hemi: 0.5, sunCol: '#E68C99', tint: '#8C73CC', moon: 0.35, night: 1.4 }, night: { el: -25, sun: 0, hemi: 0.38, sunCol: '#000000', tint: '#40508F', moon: 0.7, night: 2.5 },
  };
  let tod = TOD.day, isNight = false;
  function skyTexture(top, horizon) {
    const c = document.createElement('canvas'); c.width = 2; c.height = 256; const g = c.getContext('2d');
    const gr = g.createLinearGradient(0, 0, 0, 256); gr.addColorStop(0, top); gr.addColorStop(0.62, horizon); gr.addColorStop(1, horizon); g.fillStyle = gr; g.fillRect(0, 0, 2, 256);
    const t = new T.CanvasTexture(c); t.encoding = T.sRGBEncoding; return t;
  }
  function mix(a, b, k) { return '#' + new T.Color(a).lerp(new T.Color(b), k).getHexString(); }
  function applySky(w, timeOfDay) {
    tod = TOD[timeOfDay] || TOD.day; isNight = timeOfDay === 'night' || timeOfDay === 'evening';
    const top = mix(w.palette.skyTop, tod.tint, isNight ? 0.75 : 0.3), hor = mix(w.palette.skyHorizon, tod.tint, isNight ? 0.7 : 0.35);
    if (scene.background && scene.background.dispose) scene.background.dispose();
    scene.background = skyTexture(top, hor);
    const fogCol = new T.Color(mix(w.palette.fog, tod.tint, isNight ? 0.7 : 0.3)).convertSRGBToLinear();
    scene.fog = new T.FogExp2(fogCol, Math.min(0.012, w.fogDensity * (isNight ? 1.2 : 1)));
    hemi.color = new T.Color(top).convertSRGBToLinear(); hemi.groundColor = new T.Color(w.palette.ground).convertSRGBToLinear(); hemi.intensity = tod.hemi;
    sun.color = new T.Color(tod.sunCol).convertSRGBToLinear(); sun.intensity = tod.sun; moon.intensity = tod.moon;
    for (const m of ART.nightGlow) m.emissiveIntensity = tod.night;
    configureBloom();
    fx.setWeather(w.weather, isNight && (w.id === 'park' || w.id === 'forest'));
  }

  // ------------------------------------------------------------------ world streaming (40 m chunks)
  const CHUNK = 40; let chunks = new Map(), curWorld = null, gaps = [], seed = 1, balloons = [];
  let landmarkMarks = [], spinners = []; // landmarks placed along the route (for the name banner) and spinning parts
  const hasGround = (d) => !gaps.some((g) => d > g[0] + 0.3 && d < g[1] - 0.3);
  function clearWorld() { chunks.forEach((c) => worldRoot.remove(c)); chunks.clear(); landmarkMarks = []; spinners = []; while (backdrop.children.length) backdrop.remove(backdrop.children[0]); }
  function buildWorld(w, levelSeed, gapList) {
    clearWorld(); curWorld = w; seed = levelSeed; gaps = gapList || [];
    const rng = new R.Mulberry32(levelSeed);
    const hills = w.id === 'city' ? 0 : w.id === 'mountains' || w.id === 'snow' ? 10 : 6;
    for (let i = 0; i < hills; i++) {
      const side = w.id === 'beach' ? -1 : i % 2 ? 1 : -1; const m = ART.buildDecor('mountain');
      m.position.set(side * (70 + rng.range(0, 60)), -2, -60 - rng.range(0, 200)); m.scale.setScalar(w.id === 'mountains' ? 1.2 : 0.6);
      if (w.id !== 'mountains' && w.id !== 'snow') m.traverse((o) => { if (o.isMesh) o.material = ART.mat(w.palette.ground, 0.95); });
      backdrop.add(m);
    }
    if (w.id === 'city') for (let i = 0; i < 14; i++) { const b = ART.buildDecor('building'); b.position.set((i % 2 ? 1 : -1) * (45 + rng.range(0, 40)), 0, -40 - rng.range(0, 220)); b.scale.setScalar(2 + rng.next() * 1.5); backdrop.add(b); }
    if (w.id === 'beach') { const sea = ART.part(backdrop, ART.G.box, ART.mat(w.palette.water || '#1FB5D6', 0.08, 0.2), 260, -0.35, -100, 500, 0.1, 700); sea.castShadow = false; const foam = ART.part(backdrop, ART.G.box, ART.mat('#FFFFFF', 0.6), 10.2, -0.28, -100, 0.6, 0.1, 700); foam.castShadow = false; }
    // Hot air balloons drifting in the sky of every world (more on higher quality).
    balloons = [];
    for (let i = 0; i < Math.max(2, Math.round(5 * quality.scenery)); i++) {
      const side = w.id === 'beach' && i % 3 === 0 ? 1 : i % 2 ? 1 : -1, b = ART.buildDecor('hot_air_balloon');
      const base = new T.Vector3(side * (22 + rng.range(0, 60)), 16 + rng.range(0, 22), -40 - rng.range(0, 220));
      b.position.copy(base); b.scale.setScalar(1.2 + rng.next() * 0.7); backdrop.add(b); balloons.push({ obj: b, base, phase: rng.next() * 6.28 });
    }
    for (let i = 0; i < Math.round(10 * quality.scenery); i++) { const c = ART.buildDecor('cloud'); c.position.set(rng.range(-90, 90), 28 + rng.range(0, 18), -rng.range(0, 260)); c.scale.setScalar(3 + rng.next() * 3); backdrop.add(c); }
  }
  function buildChunk(index) {
    const w = curWorld, z0 = index * CHUNK, g = new T.Group(); g.position.z = -z0;
    const rng = new R.Mulberry32(seed * 31 + index * 7919 + w.id.length * 101);
    const gm = ART.mat(w.palette.ground, 0.95), pm = ART.mat(w.palette.path, w.id === 'city' ? 0.55 : 0.85), em = ART.mat(w.palette.pathEdge, 0.7);
    let hasGap = false; for (let z = 0; z <= CHUNK; z++) if (!hasGround(z0 + z)) { hasGap = true; break; }
    const step = hasGap ? 4 : CHUNK;
    for (let z = 0; z < CHUNK; z += step) {
      if (hasGap && (!hasGround(z0 + z + step / 2) || !hasGround(z0 + z + 0.5) || !hasGround(z0 + z + step - 0.5))) continue;
      const cz = -z - step / 2;
      const ground = ART.part(g, ART.G.box, gm, 0, -0.26, cz, 90, 0.5, step); ground.castShadow = false;
      const path = ART.part(g, ART.G.box, pm, 0, -0.01, cz, 7.2, 0.04, step); path.castShadow = false;
      for (const s of [-1, 1]) ART.part(g, ART.G.box, em, s * 3.75, 0.05, cz, 0.3, 0.14, step);
    }
    if (w.id === 'city') for (let z = 1; z < CHUNK; z += 4) if (hasGround(z0 + z)) for (const l of [-1, 1]) { const m = ART.part(g, ART.G.box, ART.mat('#FFFFFF', 0.5), l * LANE / 2, 0.015, -z - 1, 0.12, 0.01, 1.6); m.castShadow = false; }
    const types = w.decor.filter((t) => !['ocean', 'mountain', 'cloud', 'butterflies', 'birds'].includes(t));
    // A landmark chunk keeps that side of the road clear of trees and houses.
    const isLm = w.landmarks && w.landmarks.length && index >= 2 && index % 5 === 2;
    const lmN = Math.floor(index / 5), lmId = isLm ? w.landmarks[(lmN + seed) % w.landmarks.length] : null;
    const lmPlace = lmId ? window.TFYLandmarks.PLACE[lmId] || { x: 40 } : null, lmSide = lmPlace ? lmPlace.side || (lmN % 2 ? 1 : -1) : 0;
    const count = Math.round(9 * quality.scenery);
    for (let i = 0; i < count && types.length; i++) {
      const type = types[rng.range(0, types.length)]; let side = rng.next() < 0.5 ? -1 : 1; if (w.id === 'beach' && side > 0 && type !== 'boat') side = -1;
      const near = type === 'building' ? 10 : type === 'cliff' ? 12 : ['flowers', 'shell', 'puddle'].includes(type) ? 5 : 6.5;
      let x = side * (near + rng.next() * 18); if (type === 'boat') x = 20 + rng.next() * 25;
      const z = rng.next() * CHUNK; if (!hasGround(z0 + z)) continue;
      if (lmPlace && lmPlace.x > 0 && Math.sign(x) === lmSide) continue;
      const d = ART.buildDecor(type); if (!d) continue;
      d.position.set(x, type === 'boat' ? -0.3 : 0, -z); d.rotation.y = rng.next() * Math.PI * 2; d.scale.setScalar(0.8 + rng.next() * 0.5); g.add(d);
    }
    // World-famous landmark about every 200 m (chunks 2, 7, 12 …), alternating sides; kept well away from the lanes.
    if (isLm) {
      const id = lmId, lm = window.TFYLandmarks.build(id);
      if (lm) {
        const place = lmPlace, side = lmSide;
        lm.position.set(side * place.x, 0, -20); if (place.x > 0) lm.rotation.y = side > 0 ? -Math.PI / 2 : Math.PI / 2;
        if (id === 'great_wall' || id === 'torii_gate') lm.rotation.y = 0;
        if (id === 'golden_gate_bridge') lm.rotation.y = Math.PI / 2; // spans across the road: the cat runs underneath
        g.add(lm); if (lm.userData.spin) spinners.push(lm.userData.spin);
        // Banner while it is on screen: side landmarks leave the portrait view ~2.75 × their distance before you reach them.
        landmarkMarks.push({ z: z0 + 20, lead: place.x === 0 ? 35 : 2.75 * place.x + 30, name: window.TFYLandmarks.NAMES[id], shown: false });
      }
    }
    if (w.id === 'city' || w.id === 'park') { const lamp = ART.buildDecor(w.id === 'city' ? 'street_light' : 'lamp_post'); lamp.position.set(index % 2 ? -4.8 : 4.8, 0, -20); g.add(lamp); }
    return g;
  }
  function tickWorld(distance) {
    if (!curWorld) return;
    const cur = Math.floor(distance / CHUNK);
    for (let i = cur - 1; i <= cur + 6; i++) if (i >= -1 && !chunks.has(i)) { const c = buildChunk(i); chunks.set(i, c); worldRoot.add(c); }
    chunks.forEach((c, k) => { if (k < cur - 1 || k > cur + 6) { worldRoot.remove(c); chunks.delete(k); } });
    backdrop.position.z = -distance;
    const t = performance.now() / 1000;
    for (const b of balloons) { b.obj.position.set(b.base.x + Math.sin(t * 0.12 + b.phase) * 4, b.base.y + Math.sin(t * 0.45 + b.phase) * 1.2, b.base.z); b.obj.rotation.set(Math.sin(t * 0.6 + b.phase) * 0.035, t * 0.07 + b.phase, Math.cos(t * 0.5 + b.phase) * 0.035); }
    for (const s of spinners) s.rotation.z += 0.012;
    if (mode === 'play' && run && run.playing) for (const m of landmarkMarks) if (!m.shown && run.distance > m.z - m.lead && run.distance < m.z + 10) { m.shown = true; banner(m.name); }
    ART.balloonFlame.emissiveIntensity = 2.6 + Math.sin(t * 13) * 0.5 + Math.sin(t * 29) * 0.3;
  }

  // ------------------------------------------------------------------ the cat
  const catRoot = new T.Group(); scene.add(catRoot);
  let rig = null, trail = [];
  function rebuildCat(preview) {
    if (rig) catRoot.remove(rig.root);
    const p = preview || P;
    rig = ART.buildCat(item(p.selectedCat) || item('orange_cat'), p.equipped, C); catRoot.add(rig.root);
  }
  const shieldBubble = new T.Mesh(ART.G.sphere, new T.MeshStandardMaterial({ color: 0x4cc9f0, transparent: true, opacity: 0.25, roughness: 0.1, depthWrite: false }));
  shieldBubble.scale.setScalar(2); shieldBubble.position.y = 0.7; shieldBubble.visible = false; catRoot.add(shieldBubble);
  const magnetRing = new T.Mesh(ART.G.torus, ART.mat('#FF4D6D', 0.3, 0, 1.5)); magnetRing.rotation.x = Math.PI / 2; magnetRing.scale.set(3, 3, 1); magnetRing.position.y = 0.08; magnetRing.visible = false; catRoot.add(magnetRing);

  // ------------------------------------------------------------------ particles
  const pathDust = () => (curWorld ? curWorld.palette.path : '#E0D6C2');
  let sparkleT = 0;
  /** Continuous effects: power-up auras and speed lines, shop trails, slide dust, weather, menu sparkles. */
  function tickEffects(dt, time) {
    const p = catRoot.position;
    if (mode === 'play' && run && run.playing && run.startDelay <= 0 && !cat.falling) {
      fx.aura(dt, p, { boost: run.power.speed_boost > 0, magnet: run.power.magnet > 0, superJump: run.power.super_jump > 0, doubleCoins: run.power.double_coins > 0, shield: run.shield });
      if (rig && rig.trail) fx.trail(dt, p, rig.trail);
      if (cat.slideT > 0 && cat.grounded && Math.random() < 0.6) fx.slide(p.clone(), pathDust());
    } else if ((mode === 'menu' || mode === 'shop') && (sparkleT -= dt) <= 0) { sparkleT = 0.12; fx.menuSparkle(p.clone().add(new T.Vector3(0, 0.8, 0))); if (rig && rig.trail) fx.trail(dt * 4, p.clone().add(new T.Vector3(0, 0, -0.3)), rig.trail); }
    if (mode !== 'shop') fx.weather(dt, camera.position);
    fx.update(dt, time);
  }

  // ------------------------------------------------------------------ run state
  let run = null, cat = null, mode = 'menu', camOrbit = 0, paused = false;
  const pools = {};
  const takeObj = (key, make) => { const list = pools[key] || (pools[key] = []); const o = list.pop() || make(); o.visible = true; trackRoot.add(o); return o; };
  const giveObj = (key, o) => { trackRoot.remove(o); (pools[key] || (pools[key] = [])).push(o); };

  function newCat() { return { lane: 1, x: 0, y: 0, vy: 0, grounded: true, jumps: 0, slideT: 0, hitT: 0, invulnT: 0, coyote: 0, buffer: 0, bufferKind: 'tap', longJ: false, g: PHYS.g, falling: false, inGap: false, noGround: 0, climbT: 0, lean: 0, totalJumps: 0, landT: 0 }; }

  function startLevel(n, fromFlag) {
    const level = levelByNum(n); if (!level) return;
    const same = run && run.level.id === level.id; const cp = fromFlag && same ? run.cp : null;
    const plan = R.plan(level, C, RT);
    clearTrack(); fx.clear();
    const gapList = plan.items.filter((i) => i.kind === 'obstacle' && i.id === 'broken_bridge').map((i) => [i.z, i.z + i.length]);
    const w = world(level.world);
    buildWorld(w, level.seed, gapList); applySky(w, level.timeOfDay); rebuildCat();
    run = {
      level, plan, distance: cp ? cp.distance : 0, time: cp ? cp.time : 0, hearts: RT.starting_hearts, coins: cp ? cp.coins : 0, coinValue: cp ? cp.coinValue : 0,
      fish: cp ? cp.fish : 0, gems: cp ? cp.gems : 0, stars: cp ? cp.stars : 0, boxes: cp ? cp.boxes : 0, collected: new Set(cp ? cp.collected : []),
      nextItem: 0, obs: [], col: [], markers: [], cp: same ? run.cp : null, nextCp: cp ? cp.index : 0, power: {}, shield: false, playing: true, startDelay: 0.6,
      checkpointsUsed: (same && fromFlag ? run.checkpointsUsed + 1 : 0), tut: { row: null, active: false, queued: null, failed: false, scale: 1, guideAll: level.tutorial && !(P.levels[level.id] || {}).completed },
      lastObstacle: null, section: 0,
    };
    if (!fromFlag || !same) run.cp = null;
    while (run.nextItem < plan.items.length && plan.items[run.nextItem].z < run.distance - 2) run.nextItem++;
    const fin = ART.buildFinish(); fin.position.z = -plan.finishZ; trackRoot.add(fin); run.markers.push(fin);
    for (const z of plan.checkpoints) { const c = ART.buildCheckpoint(); c.position.z = -z; trackRoot.add(c); run.markers.push(c); }
    cat = newCat(); catRoot.position.set(0, 0, -run.distance); ART.setCatState(rig, 'idle');
    mode = 'play'; paused = false;
    AU.music(w.id); AU.ambience(w.ambience);
    show('hud'); renderHud(); buildTrackFlags();
    $('ctls').style.display = localStorage.getItem('tfy.buttons') === '1' ? 'grid' : 'none';
    camera.position.set(0, 3.6, -run.distance + 6.8);
  }

  function clearTrack() {
    if (run) { run.obs.forEach((o) => giveObj(o.key, o.obj)); run.col.forEach((c) => giveObj(c.key, c.obj)); run.markers.forEach((m) => trackRoot.remove(m)); }
    while (trackRoot.children.length) trackRoot.remove(trackRoot.children[0]);
  }

  function spawnAhead() {
    const items = run.plan.items;
    while (run.nextItem < items.length && items[run.nextItem].z < run.distance + 110) {
      const it = items[run.nextItem], idx = run.nextItem++;
      if (it.kind === 'obstacle') {
        const def = C.obstacles.find((o) => o.id === it.id); const key = it.id + '@' + run.level.world;
        const obj = takeObj(key, () => ART.buildObstacle(it.id, run.level.world, def));
        obj.position.set(it.lane < 0 || it.id === 'moving_critter' ? 0 : (it.lane - 1) * LANE, 0, -(it.z + def.depth / 2)); obj.rotation.set(0, 0, 0);
        const o = { key, obj, def, lane: it.lane, z: it.z, active: true, knocked: false, vel: null, phase: it.phase || 0, t: 0, warned: false, dropped: false, fallT: 0, shake: 0 };
        if (it.id === 'falling_rocks') { const rock = obj.getObjectByName('rock'); rock.position.set(0, 7, 0); const sh = obj.getObjectByName('shadow'); sh.scale.setScalar(0.5); }
        run.obs.push(o);
      } else if (it.kind === 'longjump') { const a = ART.buildArrows(it.length); a.position.z = -it.z; trackRoot.add(a); run.markers.push(a); }
      else if (['coin', 'fish', 'gem', 'star', 'box', 'powerup'].includes(it.kind) && !run.collected.has(idx)) {
        const key = it.kind === 'powerup' ? 'pu_' + it.id : it.kind;
        const obj = takeObj(key, () => ART.buildCollectible(it.kind, it.id, C));
        obj.position.set((it.lane - 1) * LANE, it.y, -it.z); obj.rotation.set(0, 0, 0);
        run.col.push({ key, obj, kind: it.kind, id: it.id, z: it.z, y: it.y, idx, magnet: false });
      }
    }
  }

  // ------------------------------------------------------------------ input → cat
  function doInput(i) {
    if (mode !== 'play' || !run || !run.playing || run.startDelay > 0 || paused) return;
    AU.init();
    if (tutIntercept(i)) return;
    perform(i);
  }
  function perform(i) {
    const canAct = cat.hitT <= 0 && !cat.falling && cat.climbT <= 0;
    if (!canAct) return;
    if (i === 'tap' || i === 'up') { if (!tryJump(i === 'up')) { cat.buffer = PHYS.buffer; cat.bufferKind = i; } }
    else if (i === 'down') { if (!cat.grounded) { cat.vy = Math.min(cat.vy, PHYS.fastFall); cat.longJ = false; } cat.slideT = PHYS.slide; if (cat.grounded) ART.setCatState(rig, 'slide'); AU.sfx('slide'); }
    else if (i === 'left' || i === 'right') { const n = Math.max(0, Math.min(2, cat.lane + (i === 'left' ? -1 : 1))); if (n !== cat.lane) { cat.lane = n; cat.lean = i === 'left' ? -1 : 1; AU.sfx('lane'); } }
  }
  function tryJump(swipeUp) {
    const jm = Math.sqrt(run.power.super_jump > 0 ? 1.45 : 1);
    if (cat.grounded || cat.coyote > 0) {
      const lj = swipeUp || inLongJumpZone(run.distance);
      cat.longJ = lj; cat.vy = (lj ? PHYS.lj : PHYS.jump) * jm; cat.g = lj ? PHYS.ljUp : PHYS.g;
      cat.grounded = false; cat.coyote = 0; cat.jumps = 1; cat.slideT = 0; cat.totalJumps++;
      ART.setCatState(rig, lj ? 'longjump' : 'jump'); AU.sfx(lj ? 'long' : 'jump'); if (lj) fx.longJump(catRoot.position.clone()); else fx.land(catRoot.position.clone(), pathDust()); if (lj) AU.meow('excited'); else if (Math.random() < 0.15) AU.meow('jump');
      return true;
    }
    if (cat.jumps < 2) { cat.vy = PHYS.dbl * jm; cat.g = cat.longJ ? PHYS.ljDown : PHYS.g; cat.jumps = 2; cat.totalJumps++; ART.setCatState(rig, 'doublejump'); AU.sfx('double'); fx.doubleJump(catRoot.position.clone().add(new T.Vector3(0, 0.3, 0))); return true; }
    return false;
  }
  const inLongJumpZone = (d) => run.plan.items.some((i) => i.kind === 'longjump' && d >= i.z && d <= i.z + i.length + 0.5);
  const gapEnd = (d) => { for (const g of gaps) if (d >= g[0] - 1 && d <= g[1]) return g[1]; return d; };

  function simulateCat(dt) {
    if (cat.invulnT > 0) cat.invulnT -= dt;
    if (cat.buffer > 0) { cat.buffer -= dt; if (cat.grounded && cat.hitT <= 0 && !cat.falling && tryJump(cat.bufferKind === 'up')) cat.buffer = 0; }
    if (cat.coyote > 0) cat.coyote -= dt;
    if (cat.hitT > 0 && (cat.hitT -= dt) <= 0) ART.setCatState(rig, cat.grounded ? 'run' : 'fall');
    if (cat.climbT > 0 && (cat.climbT -= dt) <= 0) ART.setCatState(rig, 'run');
    // ground / gap (with coyote time at the edge)
    if (!hasGround(run.distance) && cat.grounded && !cat.falling) { cat.noGround += dt; if (cat.noGround > PHYS.coyote) { cat.falling = true; cat.inGap = true; cat.grounded = false; cat.vy = -2; ART.setCatState(rig, 'fall'); } } else cat.noGround = 0;
    if (cat.falling) { cat.vy -= PHYS.g * dt; cat.y += cat.vy * dt; if (cat.y < -2.5) fellIntoGap(); }
    else if (!cat.grounded) {
      if (cat.longJ && cat.vy < 0) cat.g = PHYS.ljDown;
      cat.vy -= cat.g * dt; cat.y += cat.vy * dt;
      if (cat.vy < 0 && (rig.state === 'jump' || (rig.state === 'doublejump' && cat.y < 1.2))) ART.setCatState(rig, 'fall');
      if (cat.y <= 0) {
        cat.y = 0; cat.vy = 0; cat.grounded = true; cat.jumps = 0; cat.longJ = false; cat.g = PHYS.g; cat.landT = 0.15;
        ART.setCatState(rig, cat.slideT > 0 ? 'slide' : 'land'); AU.sfx('land'); fx.land(catRoot.position.clone(), pathDust());
      }
    }
    if (cat.grounded && !cat.falling) {
      const boost = run.power.speed_boost > 0;
      if (cat.landT > 0 && (cat.landT -= dt) <= 0 && rig.state === 'land') ART.setCatState(rig, boost ? 'fastrun' : 'run');
      if (cat.slideT > 0 && (cat.slideT -= dt) <= 0 && rig.state === 'slide') ART.setCatState(rig, boost ? 'fastrun' : 'run');
      if (rig.state === 'run' && boost) ART.setCatState(rig, 'fastrun');
      if (rig.state === 'fastrun' && !boost) ART.setCatState(rig, 'run');
      cat.coyote = PHYS.coyote;
    }
    cat.x += ((cat.lane - 1) * LANE - cat.x) * (1 - Math.exp(-16 * dt));
    cat.lean += (0 - cat.lean) * Math.min(1, dt * 4);
    catRoot.position.set(cat.x, cat.y, -run.distance);
    rig.root.visible = !(cat.invulnT > 0 && Math.floor(performance.now() / 80) % 2 === 0);
  }
  const catTop = () => (cat.slideT > 0 && cat.grounded ? 0.6 : cat.y + 1.15);
  const sliding = () => cat.slideT > 0 && cat.grounded;

  function fellIntoGap() {
    cat.falling = false; run.lastObstacle = 'broken_bridge'; AU.sfx('fall'); loseHeart(); if (!run.playing) return;
    run.distance = gapEnd(run.distance) + 1; cat.inGap = false; cat.y = 0; cat.vy = 0; cat.grounded = true; cat.jumps = 0; cat.climbT = 0.55; cat.invulnT = PHYS.invuln; ART.setCatState(rig, 'climb');
  }
  function loseHeart() { run.hearts = Math.max(0, run.hearts - 1); renderHud(); if (run.hearts === 0) fail(); }

  // ------------------------------------------------------------------ obstacles & collisions
  function tickObstacles(dt) {
    for (let i = run.obs.length - 1; i >= 0; i--) {
      const o = run.obs[i];
      if (o.knocked) { o.vel.y -= 25 * dt; o.obj.position.addScaledVector(o.vel, dt); o.obj.rotation.x += 7 * dt; if (o.obj.position.y < -6) { giveObj(o.key, o.obj); run.obs.splice(i, 1); } continue; }
      if (o.z + o.def.depth < run.distance - 14) { giveObj(o.key, o.obj); run.obs.splice(i, 1); continue; }
      const ahead = o.z - run.distance;
      if (o.def.id === 'falling_rocks') {
        const rock = o.obj.getObjectByName('rock'), sh = o.obj.getObjectByName('shadow');
        if (!o.warned && ahead < 42) { o.warned = true; AU.sfx('warning'); fx.dust(o.obj.position.clone().add(new T.Vector3(0, 7.2, 0))); }
        if (o.warned && !o.dropped) { o.shake += dt; rock.position.set(Math.sin(o.shake * 55) * 0.07, 7, Math.cos(o.shake * 47) * 0.05); sh.scale.setScalar(Math.min(1.4, sh.scale.x + dt)); if (ahead < 26) o.dropped = true; }
        if (o.dropped && o.fallT < 1) { o.fallT = Math.min(1, o.fallT + dt / 0.55); rock.position.set(0, 7 + (0.5 - 7) * o.fallT * o.fallT, 0); rock.rotation.x += 5 * dt; if (o.fallT >= 1) fx.impact(o.obj.position.clone().add(new T.Vector3(0, 0.3, 0))); }
        else if (o.warned && !o.dropped && Math.random() < 0.15) fx.dust(o.obj.position.clone().add(new T.Vector3(0, 7.2, 0)));
      }
      if (o.def.id === 'moving_critter') {
        o.t += dt; const body = o.obj.getObjectByName('critter'); const s = Math.sin((o.t / 3.4 + o.phase) * Math.PI * 2);
        body.position.set(s * LANE, Math.abs(Math.sin(o.t * 9)) * 0.08, 0); body.rotation.y = Math.cos((o.t / 3.4 + o.phase) * Math.PI * 2) >= 0 ? Math.PI / 2 : -Math.PI / 2;
      }
      if (!o.active || o.def.clearance === 'gap') continue;
      if (Math.abs(o.z + o.def.depth / 2 - run.distance) > o.def.depth / 2 + 0.35) continue;
      const ox = o.def.id === 'moving_critter' ? o.obj.getObjectByName('critter').position.x : o.obj.position.x;
      if (o.def.lanes < 3 && Math.abs(ox - cat.x) > (o.def.id === 'moving_critter' ? 0.9 : 0.95)) continue;
      if (!hits(o)) continue;
      const invincible = run.power.speed_boost > 0;
      if (cat.invulnT > 0 && !invincible) continue;
      run.lastObstacle = o.def.id;
      o.active = false; o.knocked = true; o.vel = new T.Vector3((Math.random() - 0.5) * 8, 7, 5);
      fx.bump(o.obj.position.clone().add(new T.Vector3(0, 1, 0)));
      if (invincible) continue;
      if (run.shield) { run.shield = false; cat.invulnT = 0.6; AU.sfx('shield'); toast('Shield saved you!'); renderPowers(); continue; }
      cat.hitT = PHYS.stun; cat.invulnT = PHYS.invuln; ART.setCatState(rig, 'hit');
      AU.sfx('bump'); AU.meow('surprised', true); if (P.settings.vibration && navigator.vibrate) navigator.vibrate(60);
      if (run.tut.row) run.tut.failed = true;
      loseHeart(); if (!run.playing) return;
    }
  }
  function hits(o) {
    const d = o.def, m = 0.15;
    if (d.id === 'falling_rocks') { const rock = o.obj.getObjectByName('rock'); if (rock.position.y - 0.5 > catTop()) return false; return cat.y < d.height - m; }
    if (d.clearance === 'jump_or_slide') return !sliding() && cat.y < d.topHeight - m;
    if (d.clearance === 'wall') return cat.y < d.height - 0.25;
    return cat.y < d.height - m;
  }

  function tickCollectibles(dt) {
    const cp = catRoot.position, magnet = run.power.magnet > 0 ? 5.5 : 0;
    for (let i = run.col.length - 1; i >= 0; i--) {
      const c = run.col[i], p = c.obj.position;
      if (c.kind === 'coin' && magnet && !c.magnet && p.distanceTo(cp) < magnet && p.z < cp.z + 1) c.magnet = true;
      if (c.magnet) p.lerp(new T.Vector3(cp.x, cp.y + 0.7, cp.z), 1 - Math.exp(-10 * dt));
      else { if (c.kind === 'fish') c.obj.rotation.set(0, Math.PI / 2, Math.sin(performance.now() / 160 + c.z) * 0.25); else c.obj.rotation.y += (c.kind === 'coin' ? 3.8 : 1.6) * dt; p.y = c.y + Math.sin(performance.now() / 330 + c.z * 0.5) * 0.1; }
      const centre = cp.y + (sliding() ? 0.3 : 0.6);
      const reach = (Math.abs(p.z - cp.z) < 0.9 && Math.abs(p.x - cp.x) < 1 && Math.abs(p.y - centre) < 1.1) || (c.magnet && p.distanceTo(new T.Vector3(cp.x, cp.y + 0.7, cp.z)) < 0.55);
      if (reach) {
        run.collected.add(c.idx); const pos = p.clone();
        if (c.kind === 'coin') { run.coins++; run.coinValue += run.power.double_coins > 0 ? 2 : 1; AU.sfx('coin'); fx.coin(pos); }
        else if (c.kind === 'fish') { run.fish++; AU.sfx('fish'); AU.meow('happy'); fx.fish(pos); }
        else if (c.kind === 'gem') { run.gems++; AU.sfx('gem'); fx.gem(pos); }
        else if (c.kind === 'star') { run.stars++; AU.sfx('star'); fx.star(pos); banner('⭐ Big star!'); }
        else if (c.kind === 'box') { run.boxes++; AU.sfx('box'); AU.meow('excited', true); fx.box(pos); banner('🎁 Mystery box!'); }
        else if (c.kind === 'powerup') { activatePower(c.id); AU.sfx('powerup'); const pd = C.powerUps.find((q) => q.id === c.id); fx.powerup(pos, pd ? pd.color : '#A6FFCB'); }
        if (c.kind !== 'coin' && P.settings.vibration && navigator.vibrate) navigator.vibrate(20);
        giveObj(c.key, c.obj); run.col.splice(i, 1); renderHud(); continue;
      }
      if (c.z < run.distance - 14 && !c.magnet) { giveObj(c.key, c.obj); run.col.splice(i, 1); }
    }
  }

  const POWER_ICON = { magnet: '🧲', shield: '🛡️', super_jump: '🦘', speed_boost: '⚡', double_coins: '✖2' };
  function activatePower(id) {
    const def = C.powerUps.find((p) => p.id === id);
    if (id === 'shield') run.shield = true; else run.power[id] = RT.powerup_durations[id] || def.durationSec;
    banner(`${POWER_ICON[id] || ''} ${def ? def.name : id}`); renderPowers();
  }
  function tickPowers(dt) {
    let changed = false;
    for (const k of Object.keys(run.power)) if (run.power[k] > 0) { run.power[k] -= dt; if (run.power[k] <= 0) { delete run.power[k]; changed = true; } }
    shieldBubble.visible = run.shield; magnetRing.visible = run.power.magnet > 0; magnetRing.rotation.z += dt * 3;
    if (changed) renderPowers(); else updatePowerRings();
  }

  // ------------------------------------------------------------------ tutorial (first time each situation appears)
  function classify(row) {
    const mine = row.fullWidth || row.blocked[cat.lane];
    switch (row.obstacleId) {
      case 'broken_bridge': return ['bridge', 'tap', 'JUMP AT THE ARROWS!'];
      case 'falling_rocks': return mine ? ['rocks', cat.lane === 0 ? 'right' : 'left', 'Shaking rocks! Change lane!'] : null;
      case 'moving_critter': return ['critter', 'tap', 'Watch it move… then go!'];
      case 'branch': return mine ? ['branch', 'down', 'JUMP OVER OR SLIDE UNDER'] : null;
      case 'wall': return mine ? ['wall', 'tap', 'DOUBLE TAP TO JUMP HIGHER'] : null;
    }
    if (!mine) return null;
    if (row.fullWidth) return ['jump', 'tap', 'TAP TO JUMP'];
    let open = cat.lane; for (const l of [cat.lane - 1, cat.lane + 1, cat.lane - 2, cat.lane + 2]) if (l >= 0 && l < 3 && !row.blocked[l]) { open = l; break; }
    if (open !== cat.lane) return ['lane', open < cat.lane ? 'left' : 'right', open < cat.lane ? 'SWIPE LEFT' : 'SWIPE RIGHT'];
    return ['jump', 'tap', 'TAP TO JUMP'];
  }
  const lead = (i) => (i === 'down' ? 2.8 : i === 'up' ? 6.5 : 3.6);
  function tutIntercept(i) {
    const t = run.tut; if (!t.active || !t.row) return false;
    const ok = i === t.need || (t.hint === 'branch' && (i === 'tap' || i === 'down')) || (t.hint === 'bridge' && (i === 'tap' || i === 'up')) || (t.hint === 'wall' && (i === 'left' || i === 'right' || i === 'tap'));
    if (!ok) return false;
    seenHints.add(t.hint); localStorage.setItem(HINTS_KEY, [...seenHints].join(','));
    hidePrompt(); t.scale = 1;
    const ahead = t.row.z - run.distance, timed = i === 'tap' || i === 'up' || i === 'down';
    if (timed && t.hint !== 'wall' && ahead > lead(i)) { t.queued = i; return true; }
    t.active = false; return false;
  }
  function tickTutorial() {
    const t = run.tut;
    if (t.row) {
      const ahead = t.row.z - run.distance;
      if (t.queued && ahead <= lead(t.queued)) { const q = t.queued; t.queued = null; t.active = false; perform(q); }
      if (t.row.z + t.row.depth < run.distance - 1) { if (!t.failed && seenHints.has(t.hint)) banner('Great!'); hidePrompt(); t.row = null; t.active = false; t.queued = null; t.scale = 1; t.failed = false; }
      else if (t.active) t.scale += (0.22 - t.scale) * 0.08;
      return;
    }
    const row = run.plan.rows.find((r) => r.obstacleId && r.z > run.distance && r.z - run.distance < 13); if (!row) return;
    const c = classify(row); if (!c) return;
    const [hint, need, text] = c;
    if (seenHints.has(hint) && !(t.guideAll && ['jump', 'lane', 'branch'].includes(hint))) return;
    Object.assign(t, { row, hint, need, active: true, failed: false });
    showPrompt(text, need);
  }

  // ------------------------------------------------------------------ checkpoints, finish, fail
  function tickProgress() {
    const pl = run.plan;
    while (run.section + 1 < pl.sectionStarts.length && run.distance >= pl.sectionStarts[run.section + 1]) run.section++;
    if (run.nextCp < pl.checkpoints.length && run.distance >= pl.checkpoints[run.nextCp]) {
      run.cp = { distance: pl.checkpoints[run.nextCp] + 0.5, time: run.time, coins: run.coins, coinValue: run.coinValue, fish: run.fish, gems: run.gems, stars: run.stars, boxes: run.boxes, collected: [...run.collected], index: run.nextCp + 1 };
      run.nextCp++; AU.sfx('checkpoint'); fx.checkpoint(catRoot.position.clone().add(new T.Vector3(0, 2, -2))); banner('🚩 Checkpoint!');
    }
    $('trackFill').style.width = Math.min(100, (run.distance / pl.length) * 100) + '%';
    if (run.distance >= pl.finishZ) finish();
  }

  function finish() {
    run.playing = false; ART.setCatState(rig, 'victory'); mode = 'celebrate'; camOrbit = 0;
    AU.sfx('complete'); AU.meow('victory', true);
    const at = catRoot.position.clone();
    fx.confetti(at.clone().add(new T.Vector3(0, 3, 0)), 120);
    for (let i = 0; i < 6; i++) setTimeout(() => fx.firework(at.clone().add(new T.Vector3((Math.random() - 0.5) * 8, 5 + Math.random() * 3, -3 - Math.random() * 4))), 150 + i * 280);
    const rep = { coins: run.coins, coinValue: run.coinValue, fish: run.fish, gems: run.gems, starTokens: run.stars, mysteryBoxes: run.boxes, heartsLeft: Math.max(1, run.hearts), durationSec: run.time, jumps: cat.totalJumps, failedAttempts: P.failsSinceWin || 0 };
    const res = R.applyLevelResult(P, run.level, rep, C, RT.coin_reward_multiplier);
    P.failsSinceWin = 0; save();
    setTimeout(() => { ART.setCatState(rig, 'dance'); showComplete(res); }, 1600);
  }
  function fail() {
    run.playing = false; ART.setCatState(rig, 'sleep'); AU.sfx('fail'); P.failsSinceWin = (P.failsSinceWin || 0) + 1; save();
    setTimeout(() => { if (!run.playing) { $('flagBtn').style.display = run.cp ? '' : 'none'; show('fail'); } }, 900);
  }

  // ------------------------------------------------------------------ main loop
  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    // Game speed setting (Slow / Normal / Fast) scales the level's time, so every jump and gap keeps its size. Menus run normally.
    const gdt = dt * (mode === 'play' || mode === 'celebrate' ? ({ slow: 0.75, fast: 1.25 }[P.settings.gameSpeed] || 1) : 1);
    if (!paused) {
      // Automated tests may fast-forward: several normal-sized steps per frame (same physics, just more of it).
      const steps = Math.max(1, Math.min(8, (window.TFYGame && window.TFYGame.fastForward) || 1));
      for (let s = 0; s < steps && mode === 'play' && run && run.playing; s++) {
        if (run.startDelay > 0) { run.startDelay -= gdt; if (run.startDelay <= 0) { ART.setCatState(rig, 'run'); AU.meow('happy'); } }
        else {
          const speed = R.speedAt(run.level, run.distance, RT.run_speed_scale) * (run.power.speed_boost > 0 ? 1.35 : 1);
          if (!cat.inGap) run.distance += speed * gdt * run.tut.scale;
          run.time += gdt; run.speed = speed;
          if (window.TFYGame && window.TFYGame.onStep) window.TFYGame.onStep();
          spawnAhead(); tickTutorial(); simulateCat(gdt); tickObstacles(gdt); if (run.playing) tickCollectibles(gdt); tickPowers(gdt); if (run.playing) tickProgress();
        }
      }
      if (rig) ART.animateCat(rig, gdt, run && mode === 'play' ? run.speed || 8 : 0, cat ? cat.lean : 0);
      tickEffects(gdt, now / 1000);
      if (curWorld) tickWorld(run && mode !== 'menu' ? run.distance : 0);
      updateCamera(dt);
      updateLights();
    }
    // With bloom the scene renders in linear HDR (no tone mapping) and fx.js's final pass tone-maps once; r128 would otherwise
    // tone-map into the render target too (double tone mapping, clipped emissive colours). Only reassign on change.
    const useBloom = bloom && quality.bloom > 0 && !(window.TFYGame && window.TFYGame.noBloom);
    const tm = useBloom ? T.NoToneMapping : T.ACESFilmicToneMapping;
    if (renderer.toneMapping !== tm) renderer.toneMapping = tm;
    if (useBloom) bloom.render(dt); else renderer.render(scene, camera);
    requestAnimationFrame(frame);
  }
  function updateCamera(dt) {
    const tp = catRoot.position; let desired, look;
    if (mode === 'menu' || mode === 'shop') {
      camOrbit += dt * 0.15; const a = mode === 'shop' ? 3.3 : 3.5 + Math.sin(camOrbit) * 0.25, dist = mode === 'shop' ? 3.2 : 4.2;
      desired = new T.Vector3(tp.x + Math.sin(a) * dist, 1.5, tp.z + Math.cos(a) * dist); look = new T.Vector3(tp.x, 0.85, tp.z);
    } else if (mode === 'celebrate') { camOrbit += dt * 0.45; desired = new T.Vector3(tp.x + Math.sin(Math.PI + camOrbit) * 4.5, 2, tp.z + Math.cos(Math.PI + camOrbit) * 4.5); look = new T.Vector3(tp.x, 1, tp.z); }
    else { desired = new T.Vector3(tp.x * 0.55, 3.4 + Math.max(0, tp.y) * 0.45, tp.z + 6.6); look = new T.Vector3(tp.x * 0.8, 1.2 + Math.max(0, tp.y) * 0.3, tp.z - 10); }
    camera.position.lerp(desired, 1 - Math.exp(-(mode === 'play' ? 10 : 3) * dt));
    camera.lookAt(look);
    const boost = run && run.power && run.power.speed_boost > 0 && !P.settings.reduceMotion ? 8 : 0;
    const base = window.innerWidth / window.innerHeight < 0.7 ? 68 : 60;
    camera.fov += (base + boost - camera.fov) * Math.min(1, dt * 4); camera.updateProjectionMatrix();
  }
  function updateLights() {
    const tp = catRoot.position, el = Math.max(2, tod.el) * Math.PI / 180;
    sun.position.set(tp.x - 20 * Math.cos(el), 40 * Math.sin(el) + 6, tp.z + 12); sun.target.position.set(tp.x, 0, tp.z - 8);
    moon.position.set(tp.x + 20, 30, tp.z - 10);
    nightLights.forEach((l, i) => { const on = isNight && i < quality.lights; l.intensity = on ? (tod === TOD.night ? 2.4 : 1.3) : 0; const z = Math.floor(((run && mode !== 'menu') ? run.distance : 0) / 18) * 18 + i * 18; l.position.set(i % 2 ? 4.6 : -4.6, 3.7, -z); });
  }

  // ------------------------------------------------------------------ input
  let down = null;
  const onUI = (el) => el.closest && el.closest('button, .item, .btn, input, select, a');
  window.addEventListener('pointerdown', (e) => { AU.init(); if (onUI(e.target)) return; down = { x: e.clientX, y: e.clientY, t: performance.now(), used: false }; });
  window.addEventListener('pointermove', (e) => {
    if (!down || down.used) return; const dx = e.clientX - down.x, dy = e.clientY - down.y; const th = Math.max(26, Math.min(window.innerWidth, window.innerHeight) * 0.045);
    if (Math.hypot(dx, dy) > th) { down.used = true; doInput(Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? 'right' : 'left') : (dy > 0 ? 'down' : 'up')); }
  });
  window.addEventListener('pointerup', () => { if (down && !down.used && performance.now() - down.t < 450) doInput('tap'); down = null; });
  window.addEventListener('keydown', (e) => {
    const k = e.key.toLowerCase();
    if (k === 'escape' || k === 'p') { if (mode === 'play' && run && run.playing) togglePause(); return; }
    const map = { ' ': 'tap', w: 'tap', arrowup: 'tap', e: 'up', s: 'down', arrowdown: 'down', a: 'left', arrowleft: 'left', d: 'right', arrowright: 'right' };
    if (map[k]) { e.preventDefault(); doInput(map[k]); }
  });
  document.querySelectorAll('#ctls [data-in]').forEach((b) => b.addEventListener('pointerdown', (e) => { e.stopPropagation(); doInput(b.dataset.in); }));
  document.addEventListener('visibilitychange', () => { if (document.hidden && mode === 'play' && run && run.playing && !paused) togglePause(); });

  // ------------------------------------------------------------------ UI
  const SCREENS = ['splash', 'loading', 'home', 'levels', 'shop', 'achievements', 'rewards', 'settings', 'hud', 'pause', 'complete', 'fail', 'dialog'];
  function show(id, keep) { SCREENS.forEach((s) => { if (s !== keep) $(s).classList.toggle('on', s === id); }); }
  let toastTimer = 0;
  function toast(msg) { const t = $('toast'); t.textContent = msg; t.style.display = 'block'; clearTimeout(toastTimer); toastTimer = setTimeout(() => (t.style.display = 'none'), 2600); }
  let bannerTimer = 0;
  function banner(msg) { const b = $('bannerHud'); b.textContent = msg; b.style.display = 'block'; clearTimeout(bannerTimer); bannerTimer = setTimeout(() => (b.style.display = 'none'), 1300); }
  function showPrompt(text, need) { $('promptText').textContent = text; $('promptHand').textContent = { tap: '👆', up: '⬆️', down: '⬇️', left: '⬅️', right: '➡️' }[need] || '👆'; $('prompt').style.display = 'block'; }
  function hidePrompt() { $('prompt').style.display = 'none'; }
  function dialog(text, buttons) {
    $('dlgText').textContent = text; const box = $('dlgBtns'); box.innerHTML = '';
    for (const [label, cls, fn] of buttons) { const b = document.createElement('button'); b.className = 'btn ' + cls; b.style.flex = '1'; b.textContent = label; b.onclick = () => { $('dialog').classList.remove('on'); AU.sfx('button'); fn && fn(); }; box.appendChild(b); }
    $('dialog').classList.add('on');
  }
  document.addEventListener('click', (e) => { const b = e.target.closest && e.target.closest('button'); if (b) AU.sfx('button'); });

  function renderHud() {
    if (!run) return;
    $('hearts').textContent = '❤️'.repeat(run.hearts) + '🤍'.repeat(Math.max(0, RT.starting_hearts - run.hearts));
    $('uStars').textContent = `${run.stars}/3`; $('uCoins').textContent = run.coinValue; $('uFish').textContent = run.fish; $('uGems').textContent = run.gems;
    $('uGemsPill').style.display = run.gems > 0 ? '' : 'none';
  }
  function renderPowers() {
    const row = $('powers'); row.innerHTML = '';
    const list = Object.keys(run.power).filter((k) => run.power[k] > 0); if (run.shield) list.unshift('shield');
    for (const id of list) { const d = document.createElement('div'); d.className = 'pw'; d.dataset.id = id; const def = C.powerUps.find((p) => p.id === id); d.style.setProperty('--c', def ? def.color : '#fff'); d.style.setProperty('--p', 100); d.textContent = POWER_ICON[id]; row.appendChild(d); }
  }
  function updatePowerRings() { document.querySelectorAll('.pw').forEach((d) => { const id = d.dataset.id; if (id === 'shield') return; const total = RT.powerup_durations[id] || 8; d.style.setProperty('--p', Math.max(0, (run.power[id] || 0) / total * 100)); }); }
  function buildTrackFlags() { const tr = $('track'); tr.querySelectorAll('.flag').forEach((f) => f.remove()); for (const z of run.plan.checkpoints) { const f = document.createElement('span'); f.className = 'flag'; f.textContent = '🚩'; f.style.left = (z / run.plan.length * 100) + '%'; tr.appendChild(f); } }

  function showComplete(res) {
    const n = run.level.number, next = C.levels.find((l) => l.enabled && l.number > n);
    $('cCoins').textContent = run.coinValue + (res.bonusCoins ? `  (+${res.bonusCoins} bonus)` : ''); $('cFish').textContent = run.fish; $('cGems').textContent = run.gems;
    $('cScore').textContent = res.score.toLocaleString(); $('cBest').textContent = (res.newBestScore ? 'NEW BEST! ' : '') + res.best.toLocaleString();
    const notes = []; if (res.unlockedLevel) notes.push(`Level ${res.unlockedLevel} unlocked!`); if (res.mysteryBoxesEarned) notes.push('🎁 Mystery box found — open it in Rewards!'); res.achievements.forEach((a) => notes.push(`🏆 ${a.name}!`));
    $('cNotes').innerHTML = notes.join('<br>'); $('nextBtn').style.display = next ? '' : 'none';
    const spans = $('bigStars').querySelectorAll('span'); spans.forEach((s) => s.classList.remove('on', 'show'));
    show('complete');
    spans.forEach((s, i) => setTimeout(() => { s.classList.add('show'); if (i < res.stars) { s.classList.add('on'); AU.tone(784 * (1 + i * 0.12), 1568 * (1 + i * 0.12), 0.3, 'triangle', 0.3); } }, 350 + i * 400));
  }

  function renderHome() {
    $('hCoins').textContent = P.coins.toLocaleString(); $('hGems').textContent = P.gems.toLocaleString();
    $('hello').textContent = `Hi, ${P.username}!`;
    $('playLevel').textContent = `Level ${currentLevel().number}`;
    $('rewardsDot').style.display = P.pendingBoxes > 0 || R.dailyStatus(P, RT.daily_rewards).canClaim ? 'block' : 'none';
    const msg = RT.maintenance_message; $('hBanner').style.display = msg ? 'block' : 'none'; $('hBanner').textContent = msg;
  }
  const currentLevel = () => C.levels.filter((l) => l.enabled && l.number <= P.currentLevel).sort((a, b) => b.number - a.number)[0] || C.levels[0];

  function goHome() {
    paused = false; AU.pause(false); hidePrompt();
    clearTrack(); fx.clear(); run = null; cat = null;
    const lvl = currentLevel(), w = world(lvl.world);
    buildWorld(w, lvl.seed, []); applySky(w, lvl.timeOfDay === 'night' ? 'evening' : 'morning'); rebuildCat();
    catRoot.position.set(0, 0, 0); ART.setCatState(rig, Math.random() < 0.5 ? 'idle' : 'happy'); mode = 'menu'; shieldBubble.visible = magnetRing.visible = false;
    AU.music('menu'); AU.ambience([]);
    renderHome(); show('home');
  }

  function playLevel(n) {
    const l = levelByNum(n); if (!l || !l.enabled) return toast('Coming soon!');
    if (n > P.currentLevel) return toast(`Finish level ${n - 1} to unlock this one!`);
    $('tip').textContent = l.tip || C.tips[Math.floor(Math.random() * C.tips.length)]; $('loadBar').style.width = '60%'; show('loading');
    setTimeout(() => { $('loadBar').style.width = '100%'; startLevel(n, false); }, 350);
  }
  function togglePause() { paused = !paused; AU.pause(paused); show(paused ? 'pause' : 'hud', paused ? 'hud' : undefined); if (!paused) $('pause').classList.remove('on'); else $('hud').classList.add('on'); }

  function renderLevels() {
    const list = $('levelList'); list.innerHTML = '';
    for (const w of [...C.worlds].sort((a, b) => a.order - b.order)) {
      const lv = C.levels.filter((l) => l.world === w.id && l.enabled).sort((a, b) => a.number - b.number);
      const head = document.createElement('div'); head.className = 'world'; head.style.background = w.palette.accent; head.style.opacity = w.comingSoon ? 0.45 : 1;
      head.textContent = w.comingSoon ? `${w.name} — Coming soon!` : `${w.name} · ${w.difficultyLabel}`; list.appendChild(head);
      if (w.comingSoon || !lv.length) continue;
      const row = document.createElement('div'); row.className = 'levels';
      for (const l of lv) {
        const unlocked = l.number <= P.currentLevel, prog = P.levels[l.id] || {};
        const cell = document.createElement('div'); cell.className = 'lvl';
        cell.innerHTML = `<button class="btn ${unlocked ? 'blue' : 'grey'}">${unlocked ? l.number : '🔒'}</button><div class="stars">${[0, 1, 2].map((s) => ((prog.bestStars || 0) > s ? '⭐' : '☆')).join('')}</div><small>${l.name}</small>`;
        cell.querySelector('button').onclick = () => playLevel(l.number); row.appendChild(cell);
      }
      list.appendChild(row);
    }
  }

  let shopCat = 'CATS', preview = null;
  const CATS = [['CATS', '🐱 CATS'], ['HATS', '🎩 HATS'], ['GLASSES', '🕶️ GLASSES'], ['SHOES', '👟 SHOES'], ['BACKPACKS', '🎒 BACKPACKS'], ['CAPES_SCARVES', '🧣 CAPES'], ['TRAILS', '✨ TRAILS']];
  function openShop() { mode = 'shop'; preview = null; renderShop(); show('shop'); ART.setCatState(rig, 'happy'); }
  function renderShop() {
    $('sCoins').textContent = P.coins.toLocaleString(); $('sGems').textContent = P.gems.toLocaleString();
    $('shopInfo').textContent = preview ? `${preview.name} — ${preview.description}` : 'Tap an item to try it on!';
    const tabs = $('shopTabs'); tabs.innerHTML = '';
    for (const [id, label] of CATS) { const b = document.createElement('button'); b.className = 'btn ' + (id === shopCat ? 'pink' : ''); b.textContent = label; b.onclick = () => { shopCat = id; renderShop(); }; tabs.appendChild(b); }
    const grid = $('shopGrid'); grid.innerHTML = '';
    for (const it of C.shop.filter((i) => i.enabled && i.category === shopCat)) {
      const owned = R.owns(P, it.id), wearing = it.slot === 'cat' ? P.selectedCat === it.id : P.equipped[it.slot] === it.id;
      const el = document.createElement('div'); el.className = 'item' + (preview && preview.id === it.id ? ' sel' : '');
      let action;
      if (owned) action = `<button class="btn ${wearing ? 'grey' : 'green'}">${it.slot === 'cat' ? (wearing ? 'CHOSEN' : 'CHOOSE') : (wearing ? 'TAKE OFF' : 'WEAR')}</button>`;
      else if (it.source !== 'shop') action = '<small>Special reward</small>';
      else if (it.unlockLevel && !(P.levels[`level_${String(it.unlockLevel).padStart(2, '0')}`] || {}).completed) action = `<small>Finish level ${it.unlockLevel}</small>`;
      else action = `<button class="btn orange">${it.priceGems ? '💎 ' + it.priceGems : '🪙 ' + it.priceCoins}</button>`;
      el.innerHTML = `<div class="swatch" style="background:${it.colors[0]}">${it.slot === 'cat' ? '🐱' : ''}</div><div>${it.name}</div>${action}`;
      el.onclick = (e) => {
        const btn = e.target.closest('button');
        if (!btn) { preview = it; const p = JSON.parse(JSON.stringify(P)); if (it.slot === 'cat') p.selectedCat = it.id; else p.equipped[it.slot] = it.id; rebuildCat(p); ART.setCatState(rig, 'dance'); AU.meow('happy'); renderShop(); return; }
        if (owned) { if (it.slot === 'cat') { if (!wearing) P.selectedCat = it.id; } else P.equipped[it.slot] = wearing ? '' : it.id; save(); preview = null; rebuildCat(); renderShop(); return; }
        dialog(`Unlock ${it.name} for ${it.priceGems ? it.priceGems + ' gems' : it.priceCoins + ' coins'}?`, [['NO', 'grey'], ['YES', 'green', () => {
          const r = R.purchase(P, C, it.id);
          if (!r.ok) return toast(r.message);
          AU.sfx('unlock'); AU.meow('excited', true); toast(`Unlocked: ${it.name}!`); r.achievements.forEach((a) => toast(`🏆 ${a.name}!`));
          if (it.slot === 'cat') P.selectedCat = it.id; else P.equipped[it.slot] = it.id; save(); preview = null; rebuildCat(); renderShop();
        }]]);
      };
      grid.appendChild(el);
    }
  }

  function renderAchievements() {
    const list = $('achList'); list.innerHTML = '';
    for (const a of C.achievements) {
      const done = !!P.achievements[a.id], v = Math.min(a.target, R.statValue(P, a.stat));
      const row = document.createElement('div'); row.className = 'row ach' + (done ? ' done' : '');
      row.innerHTML = `<div style="font-size:40px">${a.icon}</div><div class="grow"><b>${a.name}</b><div style="color:#6b6485">${a.description}</div><div class="progress"><i style="width:${(v / a.target) * 100}%;background:${done ? 'var(--green)' : 'var(--blue)'}"></i></div></div><div style="text-align:center;font-weight:800">${done ? '✅' : `${v}/${a.target}`}<br><small>+${a.rewardCoins}🪙${a.rewardGems ? ` +${a.rewardGems}💎` : ''}</small></div>`;
      list.appendChild(row);
    }
  }

  function renderRewards() {
    const s = R.dailyStatus(P, RT.daily_rewards), days = $('days'); days.innerHTML = '';
    for (const d of RT.daily_rewards) {
      const past = d.day < s.day || (d.day === s.day && s.claimedToday);
      const el = document.createElement('div'); el.className = 'day' + (d.day === s.day && !s.claimedToday ? ' today' : past ? ' past' : '');
      el.innerHTML = `Day ${d.day}<b>${past ? '✅' : d.type === 'coins' ? '🪙' : d.type === 'gems' ? '💎' : '🎁'}</b>${d.label}`; days.appendChild(el);
    }
    $('dailyStatus').textContent = s.claimedToday ? 'See you tomorrow!' : s.canClaim ? '' : 'Come back a little later!';
    $('claimBtn').disabled = !s.canClaim;
    $('boxTitle').textContent = P.pendingBoxes > 0 ? `Mystery Boxes: ${P.pendingBoxes}` : 'Find mystery boxes in levels!';
    $('openBoxBtn').disabled = P.pendingBoxes <= 0;
  }

  function renderSettings() {
    const s = P.settings, list = $('setList'); list.innerHTML = '';
    const toggle = (icon, label, get, set) => { const r = document.createElement('div'); r.className = 'row'; r.innerHTML = `<div style="font-size:32px">${icon}</div><div class="grow" style="font-weight:800;font-size:20px">${label}</div><button class="btn ${get() ? 'green' : 'grey'}">${get() ? 'ON' : 'OFF'}</button>`; r.querySelector('button').onclick = () => { set(!get()); save(); applySettings(); renderSettings(); }; list.appendChild(r); };
    toggle('🎵', 'Music', () => s.music, (v) => (s.music = v));
    toggle('🔊', 'Sound', () => s.sound, (v) => (s.sound = v));
    toggle('📳', 'Vibration', () => s.vibration, (v) => (s.vibration = v));
    toggle('🧘', 'Calm camera', () => s.reduceMotion, (v) => (s.reduceMotion = v));
    toggle('🎮', 'On-screen buttons', () => localStorage.getItem('tfy.buttons') === '1', (v) => localStorage.setItem('tfy.buttons', v ? '1' : '0'));
    const seg = (label, values, cur, set) => { const r = document.createElement('div'); r.innerHTML = `<div style="font-weight:800;font-size:20px;margin:6px 0">${label}</div>`; const g = document.createElement('div'); g.className = 'seg'; for (const [v, t] of values) { const b = document.createElement('button'); b.className = 'btn ' + (v === cur ? 'pink' : ''); b.textContent = t; b.onclick = () => { set(v); save(); applySettings(); renderSettings(); }; g.appendChild(b); } r.appendChild(g); list.appendChild(r); };
    seg('Game speed', [['slow', '🐢 Slow'], ['normal', '🐱 Normal'], ['fast', '🐆 Fast']], s.gameSpeed || 'normal', (v) => (s.gameSpeed = v));
    seg('Graphics', [['auto', 'Auto'], ['low', 'Low'], ['medium', 'Medium'], ['high', 'High'], ['ultra', 'Ultra']], s.graphics, (v) => (s.graphics = v));
    const help = document.createElement('button'); help.className = 'btn blue'; help.textContent = '❓ HOW TO PLAY'; help.onclick = showHelp; list.appendChild(help);
    const par = document.createElement('button'); par.className = 'btn grey'; par.textContent = '👪 FOR PARENTS'; par.onclick = parentsGate; list.appendChild(par);
    const v = document.createElement('div'); v.className = 'muted'; v.style.textAlign = 'center'; v.textContent = `${P.username} · web prototype (offline, saved on this device)`; list.appendChild(v);
  }
  function showHelp() { dialog('TAP — jump\nDOUBLE TAP — jump higher\nSWIPE UP — long jump\nSWIPE LEFT / RIGHT — change lane\nSWIPE DOWN — slide\n\nKeyboard: Space/W jump, E long jump, A/D lanes, S slide, P pause.\n\nFollow the coins — they show where to jump!', [['OK', 'green']]); }
  function parentsGate() {
    const a = 6 + Math.floor(Math.random() * 4), b = 6 + Math.floor(Math.random() * 4), ans = a * b;
    const opts = [ans, ans + 1 + Math.floor(Math.random() * 5), ans - 1 - Math.floor(Math.random() * 5)].sort(() => Math.random() - 0.5);
    dialog(`Grown-ups only:\nwhat is ${a} × ${b}?`, opts.map((o) => [String(o), 'blue', () => (o === ans ? parentsArea() : null)]));
  }
  function parentsArea() {
    dialog('TFY Cat Adventure never asks for names, emails, phone numbers, birthdays or photos. Players get a random name like Kitty4821. There is no chat. This web prototype saves only on this device.', [['CLOSE', 'grey'], ['DELETE DATA', 'red', () => dialog('Delete all progress, coins and cats on this device?', [['NO', 'grey'], ['YES', 'red', () => { localStorage.removeItem(SAVE_KEY); localStorage.removeItem(HINTS_KEY); location.reload(); }]])]]);
  }
  function applySettings() { AU.apply(P.settings); applyQuality(); }

  // ------------------------------------------------------------------ buttons
  document.querySelectorAll('[data-go]').forEach((b) => b.addEventListener('click', () => {
    const go = b.dataset.go;
    if (go === 'home') return goHome();
    if (go === 'shop') return openShop();
    if (go === 'levels') renderLevels(); if (go === 'achievements') renderAchievements(); if (go === 'rewards') renderRewards(); if (go === 'settings') renderSettings();
    show(go);
  }));
  $('playBtn').onclick = () => playLevel(currentLevel().number);
  $('helpBtn').onclick = showHelp;
  $('pauseBtn').onclick = togglePause;
  $('resumeBtn').onclick = togglePause;
  $('restartBtn').onclick = () => { paused = false; AU.pause(false); $('pause').classList.remove('on'); startLevel(run.level.number, false); };
  $('pauseHomeBtn').onclick = goHome;
  $('nextBtn').onclick = () => { const n = C.levels.find((l) => l.enabled && l.number > run.level.number); if (n) playLevel(n.number); };
  $('replayBtn').onclick = () => startLevel(run.level.number, false);
  $('cHomeBtn').onclick = goHome;
  $('flagBtn').onclick = () => startLevel(run.level.number, true);
  $('againBtn').onclick = () => startLevel(run.level.number, false);
  $('fHomeBtn').onclick = goHome;
  $('claimBtn').onclick = () => { const c = R.claimDaily(P, RT.daily_rewards, C); if (!c) return toast('Come back a little later!'); save(); AU.sfx('box'); AU.meow('happy', true); toast(`You got ${c.label}!`); c.achievements.forEach((a) => toast(`🏆 ${a.name}!`)); renderRewards(); };
  $('openBoxBtn').onclick = () => { const r = R.openMysteryBox(P, C); if (!r) return toast('Find mystery boxes in levels!'); save(); AU.sfx('box'); AU.meow('excited', true); dialog(`🎁 You got ${r.label}!`, [['YAY!', 'green']]); renderRewards(); };

  // ------------------------------------------------------------------ boot: TFY logo → loading → automatic player → home
  function boot() {
    resize();
    P = loadSave();
    // Testing: open index.html?unlock=all to unlock every enabled level on this device (normal progression is unchanged otherwise).
    const unlockAll = new URLSearchParams(location.search).get('unlock') === 'all';
    if (unlockAll) P.currentLevel = Math.max(P.currentLevel, ...C.levels.filter((l) => l.enabled).map((l) => l.number));
    save();
    applyQuality(); AU.apply(P.settings);
    show('splash');
    setTimeout(() => {
      $('tip').textContent = C.tips[Math.floor(Math.random() * C.tips.length)]; show('loading');
      let p = 0; const iv = setInterval(() => { p += 22; $('loadBar').style.width = Math.min(100, p) + '%'; if (p >= 110) { clearInterval(iv); goHome(); toast(unlockAll ? 'All levels unlocked (testing)' : isNew ? `Hi, ${P.username}! Let's run!` : `Welcome back, ${P.username}!`); } }, 120);
    }, 1200);
    requestAnimationFrame(frame);
  }
  boot();
  // Small debug/automation surface (used by tools/smoke-test.js).
  window.TFYGame = { get player() { return P; }, get run() { return run; }, get cat() { return cat; }, get mode() { return mode; }, get memory() { return Object.assign({ heapMB: performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : null, trackObjects: trackRoot.children.length, worldChunks: chunks.size }, renderer.info.memory); }, playLevel, goHome, doInput, save, fastForward: 1 };
})();
