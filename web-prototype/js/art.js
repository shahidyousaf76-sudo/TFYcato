// TFY Cat Adventure — procedural 3D art for the web prototype (Three.js r128). Mirrors the Unity factories:
// PBR materials, the cat with costumes/accessories, obstacles per world with warnings, collectibles, scenery.
(function (global) {
  'use strict';
  const T = global.THREE;
  const LANE = 2.2;
  const lin = (hex) => new T.Color(hex).convertSRGBToLinear();
  const cache = {};
  const nightGlow = [];
  const balloonFlame = new T.MeshStandardMaterial({ color: new T.Color('#FF8C1A').convertSRGBToLinear(), emissive: new T.Color('#FF8C1A').convertSRGBToLinear(), emissiveIntensity: 3, roughness: 0.3, fog: false });

  function mat(hex, rough = 0.6, metal = 0, emissive = 0) {
    const key = `${hex}|${rough}|${metal}|${emissive}`;
    if (cache[key]) return cache[key];
    const m = new T.MeshStandardMaterial({ color: lin(hex), roughness: rough, metalness: metal });
    if (emissive) { m.emissive = lin(hex); m.emissiveIntensity = emissive; }
    return (cache[key] = m);
  }
  function glass(hex, opacity) { const k = `g${hex}${opacity}`; return cache[k] || (cache[k] = new T.MeshStandardMaterial({ color: lin(hex), roughness: 0.05, metalness: 0.1, transparent: true, opacity, depthWrite: false })); }
  const G = {
    box: new T.BoxGeometry(1, 1, 1), sphere: new T.SphereGeometry(0.5, 20, 14), sphereLo: new T.SphereGeometry(0.5, 10, 8), cyl: new T.CylinderGeometry(0.5, 0.5, 1, 18),
    cone: new T.ConeGeometry(0.5, 1, 16), torus: new T.TorusGeometry(0.4, 0.1, 10, 24), gem: new T.OctahedronGeometry(0.5, 0), plane: new T.PlaneGeometry(1, 1),
  };
  G.star = (() => { const s = new T.Shape(); for (let i = 0; i <= 10; i++) { const a = Math.PI / 2 + i * Math.PI / 5, r = i % 2 ? 0.22 : 0.5; i ? s.lineTo(Math.cos(a) * r, Math.sin(a) * r) : s.moveTo(Math.cos(a) * r, Math.sin(a) * r); } return new T.ExtrudeGeometry(s, { depth: 0.14, bevelEnabled: true, bevelSize: 0.03, bevelThickness: 0.03, bevelSegments: 2 }).center(); })();
  G.fish = (() => { const s = new T.Shape(); s.moveTo(0.47, 0); s.quadraticCurveTo(0.2, 0.28, -0.25, 0.08); s.lineTo(-0.5, 0.22); s.lineTo(-0.44, 0); s.lineTo(-0.5, -0.22); s.lineTo(-0.25, -0.08); s.quadraticCurveTo(0.2, -0.28, 0.47, 0); return new T.ExtrudeGeometry(s, { depth: 0.12, bevelEnabled: true, bevelSize: 0.03, bevelThickness: 0.03, bevelSegments: 2 }).center(); })();
  G.heart = (() => { const s = new T.Shape(); s.moveTo(0, -0.45); s.bezierCurveTo(-0.6, -0.05, -0.45, 0.45, 0, 0.2); s.bezierCurveTo(0.45, 0.45, 0.6, -0.05, 0, -0.45); return new T.ExtrudeGeometry(s, { depth: 0.14, bevelEnabled: false }).center(); })();

  function part(parent, geo, m, x = 0, y = 0, z = 0, sx = 1, sy = sx, sz = sx, rx = 0, ry = 0, rz = 0) {
    const mesh = new T.Mesh(geo, m); mesh.position.set(x, y, z); mesh.scale.set(sx, sy, sz); mesh.rotation.set(rx, ry, rz);
    mesh.castShadow = true; mesh.receiveShadow = true; parent.add(mesh); return mesh;
  }
  const group = (parent, x = 0, y = 0, z = 0) => { const g = new T.Group(); g.position.set(x, y, z); if (parent) parent.add(g); return g; };
  const noShadow = (o) => o.traverse((c) => { if (c.isMesh) c.castShadow = false; });

  // ------------------------------------------------------------------ the cat
  function buildCat(item, equipped, content) {
    const c = (item && item.colors) || ['#F59A3C', '#D9731F', '#FFE3C2', '#3BAA5C'];
    const fur = mat(c[0], 0.75), marks = mat(c[1], 0.75), belly = mat(c[2], 0.8), eyeM = mat(c[3], 0.2), dark = mat('#1E1E24', 0.2), white = mat('#FFFFFF', 0.2, 0, 0.6), pink = mat('#FF8FAB', 0.5);
    const root = new T.Group(); const rig = { root };
    const bp = rig.body = group(root, 0, 0.55, 0);
    part(bp, G.sphere, fur, 0, 0.05, 0.05, 0.62, 0.56, 0.95);
    part(bp, G.sphere, belly, 0, -0.08, 0, 0.46, 0.3, 0.76);
    const head = rig.head = group(bp, 0, 0.42, -0.5);
    part(head, G.sphere, fur, 0, 0, 0, 0.62, 0.54, 0.54);
    part(head, G.sphere, belly, 0, -0.1, -0.2, 0.34, 0.22, 0.22);
    part(head, G.sphere, pink, 0, -0.03, -0.3, 0.08, 0.06, 0.06);
    rig.ears = [-1, 1].map((s) => { const e = group(head, s * 0.18, 0.2, 0.02); e.rotation.z = -s * 0.3; part(e, G.cone, fur, 0, 0.12, 0, 0.2, 0.26, 0.12); part(e, G.cone, pink, 0, 0.11, -0.03, 0.12, 0.18, 0.06); return e; });
    rig.eyes = [-1, 1].map((s) => { const e = group(head, s * 0.13, 0.04, -0.22); part(e, G.sphere, eyeM, 0, 0, 0, 0.13, 0.16, 0.08); part(e, G.sphere, dark, 0, 0, -0.025, 0.07, 0.11, 0.05); part(e, G.sphere, white, 0.025, 0.035, -0.05, 0.035, 0.035, 0.02); return e; });
    for (const s of [-1, 1]) for (let k = 0; k < 3; k++) part(head, G.box, dark, s * 0.24, -0.06 - k * 0.035, -0.24, 0.22, 0.006, 0.006, 0, s * 0.2, s * (k - 1) * 0.18);
    rig.legs = [[-0.18, -0.28], [0.18, -0.28], [-0.18, 0.32], [0.18, 0.32]].map(([x, z]) => { const l = group(bp, x, -0.1, z); part(l, G.cyl, fur, 0, -0.2, 0, 0.14, 0.4, 0.14); part(l, G.sphere, belly, 0, -0.42, -0.03, 0.17, 0.12, 0.2); return l; });
    rig.tail = []; let prev = group(bp, 0, 0.12, 0.5); rig.tail.push(prev);
    for (let i = 0; i < 4; i++) { const seg = i === 0 ? prev : group(prev, 0, 0.16, 0.02); part(seg, G.cyl, i === 3 ? marks : fur, 0, 0.08, 0, 0.09 - i * 0.01, 0.18, 0.09 - i * 0.01); if (i) rig.tail.push(seg); prev = seg; }
    const id = (item && item.id) || 'orange_cat';
    if (['orange_cat', 'grey_cat', 'pirate_cat'].includes(id)) for (let k = 0; k < 3; k++) part(bp, G.sphere, marks, 0, 0.36, -0.15 + k * 0.2, 0.5, 0.1, 0.07);
    if (id === 'spotted_cat') for (let k = 0; k < 8; k++) part(bp, G.sphereLo, marks, Math.sin(k * 2.3) * 0.25, 0.2 + (k % 3) * 0.1, -0.3 + k * 0.09, 0.14, 0.14, 0.14);
    if (id === 'pirate_cat') { part(head, G.sphere, mat('#D62828', 0.6), 0, 0.12, 0.02, 0.64, 0.36, 0.58); part(head, G.cyl, dark, 0.13, 0.04, -0.26, 0.14, 0.01, 0.14, Math.PI / 2); }
    if (id === 'ninja_cat') { part(head, G.box, mat('#1B1D2A', 0.5), 0, 0.04, -0.05, 0.66, 0.14, 0.5); part(head, G.box, mat('#E63946', 0.5), 0, 0.02, 0.3, 0.06, 0.04, 0.3, 0.35); }
    if (id === 'superhero_cat') { part(head, G.box, mat('#2F80ED', 0.4), 0, 0.05, -0.2, 0.5, 0.1, 0.2); part(bp, G.box, mat('#E63946', 0.6), 0, 0.2, 0.42, 0.7, 0.04, 0.7, -0.35); }
    if (id === 'space_cat') { const h = part(head, G.sphere, glass('#BFE6FF', 0.28), 0, 0.02, 0, 0.85, 0.85, 0.85); h.castShadow = false; part(head, G.sphere, mat('#00D1FF', 0.2, 0, 2.5), 0, 0.5, 0, 0.07, 0.07, 0.07); }
    // accessories
    const eq = equipped || {};
    const col = (it, i) => (it.colors[i] || it.colors[0] || '#FFFFFF');
    for (const [slot, itemId] of Object.entries(eq)) {
      if (!itemId) continue; const it = content.shop.find((x) => x.id === itemId); if (!it) continue;
      const m = mat(col(it, 0), 0.5), m2 = mat(col(it, 1), 0.5);
      if (slot === 'hat') {
        const top = group(head, 0, 0.27, 0.02);
        if (it.id === 'party_hat') { part(top, G.cone, m, 0, 0.19, 0, 0.28, 0.38, 0.28); part(top, G.sphere, m2, 0, 0.4, 0, 0.08, 0.08, 0.08); }
        else if (it.id === 'cowboy_hat') { part(top, G.cyl, m, 0, 0, 0, 0.7, 0.03, 0.6); part(top, G.cyl, m, 0, 0.1, 0, 0.32, 0.2, 0.3); }
        else if (it.id === 'gold_crown') { const g = mat('#FFD700', 0.25, 1); part(top, G.cyl, g, 0, 0.04, 0, 0.34, 0.08, 0.34); for (let i = 0; i < 5; i++) { const a = i * Math.PI * 2 / 5; part(top, G.cone, g, Math.cos(a) * 0.14, 0.13, Math.sin(a) * 0.14, 0.08, 0.12, 0.08); } }
        else if (it.id === 'flower_crown') for (let i = 0; i < 7; i++) { const a = i * Math.PI * 2 / 7; part(top, G.sphereLo, i % 2 ? mat('#FFFFFF', 0.6) : m, Math.cos(a) * 0.17, 0.02, Math.sin(a) * 0.17, 0.09, 0.09, 0.09); }
        else { part(top, G.sphere, m, 0, 0.02, 0, 0.44, 0.24, 0.44); part(top, G.cyl, m, 0, 0, -0.2, 0.3, 0.02, 0.22); }
      } else if (slot === 'glasses') {
        const face = group(head, 0, 0.06, -0.27);
        for (const s of [-1, 1]) {
          if (it.id === 'star_glasses') part(face, G.star, m, s * 0.13, 0, 0, 0.3, 0.3, 0.3);
          else { part(face, G.torus, m, s * 0.13, 0, 0, 0.24, 0.24, 0.24); part(face, G.cyl, it.id === 'sunglasses' ? mat('#101010', 0.05) : glass('#E0F4FF', 0.35), s * 0.13, 0, 0.005, 0.18, 0.005, 0.18, Math.PI / 2); }
        }
      } else if (slot === 'shoes') rig.legs.forEach((l) => part(l, G.sphere, it.id === 'gold_boots' ? mat('#FFC300', 0.2, 1) : m, 0, -0.42, -0.04, 0.2, 0.14, 0.24));
      else if (slot === 'backpack') { const b = part(bp, G.box, m, 0, 0.42, 0.18, 0.4, 0.3, 0.3); part(b, G.box, m2, 0, -0.2, 0, 0.9, 0.3, 1.05); }
      else if (slot === 'cape') part(bp, G.box, m, 0, 0.3, 0.38, 0.62, 0.03, 0.7, -0.45);
      else if (slot === 'scarf') { part(bp, G.torus, m, 0, 0.26, -0.36, 0.9, 0.9, 1.4, Math.PI / 2 - 0.3); part(bp, G.box, m2, 0.12, 0.14, -0.32, 0.1, 0.25, 0.04, 0, 0, 0.26); }
      else if (slot === 'trail') rig.trail = it.colors.slice(); // sRGB hex, used by the particle trail
    }
    rig.t = 0; rig.flip = 0; rig.state = 'idle'; rig.stateTime = 0; rig.blink = 2;
    return rig;
  }

  /** Procedural animation: idle, run, fast run, jump, double jump (flip), long jump, falling, landing, slide, climb, hit, victory, dance, sleep, happy. */
  function animateCat(rig, dt, speed, lean) {
    rig.t += dt; rig.stateTime += dt; const t = rig.t, s = rig.state;
    let bodyY = 0.55, pitch = 0, roll = -lean * 0.2, yaw = 0, sq = 1, st = 1, headPitch = 0, headYaw = 0, tailUp = 0.5, wag = 0; const leg = [0, 0, 0, 0];
    const f = t * Math.max(8, speed * 1.25);
    switch (s) {
      case 'idle': sq = 1 + Math.sin(t * 2.2) * 0.02; headYaw = Math.sin(t * 0.5) * 0.35; wag = Math.sin(t * 1.6) * 0.45; tailUp = 0.8; break;
      case 'run': case 'fastrun': {
        const k = s === 'fastrun' ? 1.3 : 1; leg[0] = Math.sin(f * k) * 0.8; leg[1] = Math.sin(f * k + 0.5) * 0.8; leg[2] = Math.sin(f * k + Math.PI) * 0.8; leg[3] = Math.sin(f * k + Math.PI + 0.5) * 0.8;
        bodyY += Math.abs(Math.sin(f * k)) * 0.08; pitch = Math.sin(f * 2) * 0.05 + (s === 'fastrun' ? 0.17 : 0.07); st = 1 + Math.sin(f) * 0.04; tailUp = s === 'fastrun' ? 1.3 : 0.95; headPitch = -pitch * 0.6; break; }
      case 'jump': leg[0] = leg[1] = -0.9; leg[2] = leg[3] = 0.9; pitch = -0.2; st = 1.08; tailUp = 1.4; break;
      case 'doublejump': rig.flip = Math.min(Math.PI * 2, rig.flip + dt * 14); pitch = -rig.flip; sq = 0.9; leg.fill(1.2); tailUp = 1.6; break;
      case 'longjump': leg[0] = leg[1] = -1.3; leg[2] = leg[3] = 1.3; st = 1.25; sq = 0.9; pitch = -0.1; tailUp = 0.1; break;
      case 'fall': leg.forEach((_, i) => (leg[i] = (i % 2 ? -1 : 1) * 0.6 + Math.sin(t * 20 + i) * 0.17)); pitch = 0.26; tailUp = 1.9; headPitch = -0.26; break;
      case 'land': { const k = Math.min(1, rig.stateTime / 0.18); sq = 0.72 + 0.28 * k; st = 1.15 - 0.15 * k; bodyY -= (1 - k) * 0.12; break; }
      case 'slide': bodyY = 0.28; sq = 0.7; st = 1.2; pitch = 0.07; headPitch = 0.17; leg[0] = leg[1] = -1.4; leg[2] = leg[3] = 1.2; tailUp = 0; break;
      case 'climb': pitch = -0.95; leg[0] = -1 + Math.sin(t * 10) * 0.7; leg[1] = -1 + Math.sin(t * 10 + Math.PI) * 0.7; leg[2] = Math.sin(t * 10 + Math.PI) * 0.5; leg[3] = Math.sin(t * 10) * 0.5; break;
      case 'hit': roll = Math.sin(rig.stateTime * 40) * 0.2 * Math.max(0, 1 - rig.stateTime * 2.5); pitch = 0.2; headPitch = -0.35; sq = 0.92; tailUp = 1.75; break;
      case 'victory': bodyY += Math.abs(Math.sin(t * 5)) * 0.5; leg[0] = leg[1] = -2.1 + Math.sin(t * 10) * 0.35; pitch = -0.44; wag = Math.sin(t * 8) * 0.6; tailUp = 1.2; break;
      case 'dance': roll = Math.sin(t * 6) * 0.3; yaw = Math.sin(t * 3) * 0.7; bodyY += Math.abs(Math.sin(t * 6)) * 0.12; leg[0] = Math.sin(t * 6) - 0.7; leg[1] = -Math.sin(t * 6) - 0.7; wag = Math.sin(t * 12) * 0.7; break;
      case 'sleep': bodyY = 0.25; sq = 0.75; st = 1.1; headPitch = 0.44; roll = 0.14; tailUp = -0.17; leg[0] = leg[1] = -1.48; leg[2] = leg[3] = 1.48; break;
      case 'happy': bodyY += Math.abs(Math.sin(t * 9)) * 0.18; wag = Math.sin(t * 14) * 0.8; tailUp = 1.2; headYaw = Math.sin(t * 4) * 0.2; break;
    }
    const k = 1 - Math.exp(-14 * dt), L = (a, b) => a + (b - a) * k;
    const b = rig.body;
    b.position.y = L(b.position.y, bodyY);
    if (s === 'doublejump') b.rotation.x = pitch; else b.rotation.x = L(b.rotation.x, pitch);
    b.rotation.y = L(b.rotation.y, yaw); b.rotation.z = L(b.rotation.z, roll);
    b.scale.set(L(b.scale.x, 1 / Math.sqrt(sq * st)), L(b.scale.y, sq), L(b.scale.z, st));
    rig.head.rotation.x = L(rig.head.rotation.x, headPitch); rig.head.rotation.y = L(rig.head.rotation.y, headYaw);
    rig.legs.forEach((l, i) => (l.rotation.x = L(l.rotation.x, leg[i])));
    rig.tail.forEach((seg, i) => { seg.rotation.x = L(seg.rotation.x, i === 0 ? tailUp : 0.2); seg.rotation.y = L(seg.rotation.y, wag * (0.3 + i * 0.25)); });
    rig.blink -= dt; const closed = s === 'sleep' || (rig.blink < 0.12 && rig.blink > 0); if (rig.blink <= 0) rig.blink = 2.5 + Math.random() * 2.5;
    rig.eyes.forEach((e) => (e.scale.y = L(e.scale.y, closed ? 0.1 : 1)));
  }
  function setCatState(rig, s) { if (rig.state === s) return; rig.state = s; rig.stateTime = 0; if (s === 'doublejump') rig.flip = 0; }

  // ------------------------------------------------------------------ obstacles (themed per world)
  function buildObstacle(id, world, def) {
    const g = new T.Group(); g.userData = { id };
    const W = world;
    if (id === 'log') {
      if (W === 'city') { for (let i = 0; i < 6; i++) part(g, G.cyl, mat(i % 2 ? '#F1FAEE' : '#E63946', 0.4), -0.9 + i * 0.36, 0.5, 0, 0.22, 0.36, 0.22, 0, 0, Math.PI / 2); part(g, G.box, mat('#FFB703'), -1, 0.25, 0, 0.2, 0.5, 0.5); part(g, G.box, mat('#FFB703'), 1, 0.25, 0, 0.2, 0.5, 0.5); }
      else { part(g, G.cyl, mat(W === 'beach' ? '#C8B79E' : '#8B5A2B', 0.85), 0, 0.33, 0, 0.66, 2, 0.66, 0, 0, Math.PI / 2); for (const x of [-1, 1]) part(g, G.cyl, mat('#E9C98B', 0.6), x, 0.33, 0, 0.56, 0.02, 0.56, 0, 0, Math.PI / 2); part(g, G.sphereLo, mat(W === 'snow' ? '#FFFFFF' : '#7BD389', W === 'snow' ? 0.5 : 0.6), 0.3, 0.66, 0, W === 'snow' ? 1.6 : 0.25, 0.14, W === 'snow' ? 0.5 : 0.25); }
    } else if (id === 'rock' || id === 'falling_rock_body') {
      const c = W === 'beach' ? '#D9B98B' : W === 'mountains' ? '#8D99AE' : W === 'snow' ? '#B8C4D0' : W === 'city' ? '#A8A8A8' : '#9A8F85';
      part(g, G.sphereLo, mat(c, 0.9), 0, 0.4, 0, 1.2, 0.88, 1); part(g, G.sphereLo, mat(c, 0.9), 0.35, 0.28, 0.15, 0.6, 0.56, 0.6);
      if (W === 'mountains' || W === 'snow') part(g, G.sphereLo, mat('#FFFFFF', 0.5), 0, 0.76, 0, 0.9, 0.2, 0.7);
    } else if (id === 'branch') {
      const post = mat(W === 'city' ? '#FFB703' : '#6E4B2A', 0.7);
      for (const x of [-1.05, 1.05]) part(g, G.cyl, post, x, 0.6, 0, 0.14, 1.2, 0.14);
      if (W === 'city') for (let i = 0; i < 5; i++) part(g, G.box, mat(i % 2 ? '#FFFFFF' : '#E63946', 0.5), -0.85 + i * 0.42, 1.17, 0, 0.42, 0.3, 0.12);
      else { part(g, G.cyl, mat(W === 'beach' ? '#A0673A' : '#7A5230', 0.8), 0, 1.17, 0, 0.28, 2.3, 0.28, 0, 0, Math.PI / 2); for (let i = 0; i < 5; i++) part(g, G.sphereLo, mat(W === 'beach' ? '#2A9D8F' : W === 'snow' ? '#F4FAFF' : '#52B788', 0.7), -0.9 + i * 0.45, 1.3, 0, 0.45, 0.18, 0.35); }
    } else if (id === 'barrel') {
      const city = W === 'city';
      if (W === 'snow') part(g, G.sphereLo, mat('#FFFFFF', 0.5), 0, 1.33, 0, 0.95, 0.25, 0.95); // snow cap
      part(g, G.cyl, mat(city ? '#FB8500' : '#9C6644', 0.6), 0, 0.65, 0, 0.9, 1.3, 0.9);
      for (const y of [0.35, 0.95]) part(g, G.cyl, mat(city ? '#FFFFFF' : '#6C757D', 0.3, city ? 0 : 0.8), 0, y, 0, 0.93, 0.1, 0.93);
      part(g, G.torus, mat('#FFD966', 0.3, 0, 1.4), 0, 1.33, 0, 1.1, 1.1, 1.1, Math.PI / 2);
    } else if (id === 'wall') {
      const cols = { park: ['#2D6A4F', '#40916C'], forest: ['#7F4F24', '#936639'], city: ['#E76F51', '#F4A261'], beach: ['#E9C46A', '#F4D58D'], snow: ['#A9DCF7', '#D6F0FF'] }[W] || ['#6C757D', '#ADB5BD']; // snow: ice blocks
      for (let r = 0; r < 4; r++) for (let c = 0; c < 3; c++) { let x = -0.66 + c * 0.66 + (r % 2 ? 0.33 : 0); if (x > 0.8) x -= 1.98; part(g, G.box, mat(cols[(r + c) % 2], 0.8), x, 0.35 + r * 0.7, 0, 0.64, 0.66, 0.8); }
      part(g, G.box, mat('#FFF3A0', 0.3, 0, 1.3), 0, 2.85, 0, 2, 0.1, 0.85);
    } else if (id === 'broken_bridge') {
      const d = def.depth, wood = mat('#A47148', 0.8), crack = mat('#4A2C17', 0.9);
      for (let i = 0; i < 3; i++) { const p = part(g, G.box, wood, 0, -0.05, d / 2 + 0.4 + i * 0.9, 6.8, 0.1, 0.8); if (i === 0) for (let k = 0; k < 5; k++) part(p, G.box, crack, -0.4 + k * 0.2, 0.6, 0, 0.01, 0.2, 0.9, 0, 0, 0.3); }
      for (let i = 0; i < 3; i++) part(g, G.box, wood, -2.2 + i * 2.2, -0.6, d / 2 - 0.3, 1.4, 0.1, 0.9, 1 + i * 0.15);
      for (let i = 0; i < 2; i++) part(g, G.box, wood, -1.5 + i * 3, -0.5, -d / 2 + 0.3, 1.5, 0.1, 0.9, -0.9);
      for (const s of [-1, 1]) { part(g, G.cyl, mat('#D4A373'), s * 3.6, 0.6, d / 2 + 1, 0.12, 1.2, 0.12); part(g, G.cyl, mat('#D4A373'), s * 3.6, 0.6, -d / 2 - 1, 0.12, 1.2, 0.12); part(g, G.cyl, mat('#F4E3C1'), s * 3.6, 0.9, 0, 0.04, d + 2, 0.04, Math.PI / 2); }
      const water = part(g, G.box, mat(W === 'beach' ? '#1FB5D6' : W === 'snow' ? '#9ADCF5' : '#5DADE2', 0.05, 0.1), 0, -3, 0, 60, 0.1, d); water.castShadow = false;
    } else if (id === 'falling_rocks') {
      const beach = W === 'beach';
      const rock = group(g); rock.name = 'rock';
      if (beach) { part(rock, G.sphere, mat('#6F4E37', 0.7), 0, 0, 0, 0.95, 0.95, 0.95); part(rock, G.sphereLo, mat('#3E2723'), 0, 0.3, -0.3, 0.18, 0.18, 0.18); }
      else if (W === 'snow') part(rock, G.sphere, mat('#FFFFFF', 0.55), 0, 0, 0, 1.1, 1.1, 1.1); // a big snowball
      else { const c = W === 'mountains' ? '#8D99AE' : '#9A8F85'; part(rock, G.sphereLo, mat(c, 0.9), 0, 0, 0, 1.1, 0.95, 1); }
      const src = beach ? part(g, G.sphereLo, mat('#2A9D8F', 0.6), 0, 7.8, 0, 2.4, 0.4, 1.6) : part(g, G.box, mat(W === 'mountains' ? '#6C757D' : '#8D6E63', 0.9), 0, 7.8, 0, 2.2, 0.5, 1.6);
      noShadow(src);
      const shadow = new T.Mesh(new T.CircleGeometry(0.5, 20), new T.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.4, depthWrite: false }));
      shadow.rotation.x = -Math.PI / 2; shadow.position.y = 0.03; shadow.name = 'shadow'; g.add(shadow);
    } else if (id === 'moving_critter') {
      const body = group(g); body.name = 'critter';
      const eyes = (y, z, s) => { for (const k of [-1, 1]) { part(body, G.sphereLo, mat('#FFFFFF', 0.3), k * s, y, z, 0.16, 0.16, 0.16); part(body, G.sphereLo, mat('#111111', 0.2), k * s, y, z - 0.06, 0.08, 0.08, 0.08); } };
      if (W === 'beach') { part(body, G.sphere, mat('#FF6F59', 0.5), 0, 0.35, 0, 0.8, 0.4, 0.6); for (const k of [-1, 1]) part(body, G.sphereLo, mat('#FF6F59'), k * 0.55, 0.5, -0.2, 0.3, 0.22, 0.22); eyes(0.65, -0.2, 0.12); }
      else if (W === 'forest') { part(body, G.sphere, mat('#8D6E63', 0.9), 0, 0.4, 0, 0.75, 0.6, 0.9); for (let k = 0; k < 10; k++) part(body, G.cone, mat('#5D4037'), Math.cos(k) * 0.25, 0.62 + Math.sin(k * 1.3) * 0.1, -0.1 + (k % 5) * 0.1, 0.1, 0.25, 0.1, -0.3); part(body, G.sphereLo, mat('#FFCCBC'), 0, 0.35, -0.45, 0.35, 0.3, 0.3); eyes(0.45, -0.55, 0.08); }
      else if (W === 'city') { part(body, G.box, mat('#3A86FF', 0.25, 0.3), 0, 0.35, 0, 0.7, 0.35, 1); part(body, G.box, glass('#CCF2FF', 0.6), 0, 0.62, 0.05, 0.6, 0.25, 0.55); for (let i = 0; i < 4; i++) part(body, G.cyl, mat('#222222'), i < 2 ? -0.38 : 0.38, 0.16, i % 2 ? 0.32 : -0.32, 0.3, 0.12, 0.3, 0, 0, Math.PI / 2); }
      else if (W === 'snow') { // penguin
        part(body, G.sphere, mat('#1E2230', 0.5), 0, 0.5, 0, 0.62, 0.9, 0.55); part(body, G.sphere, mat('#FFFFFF', 0.5), 0, 0.45, -0.12, 0.45, 0.7, 0.4);
        part(body, G.cone, mat('#FFA62B', 0.5), 0, 0.72, -0.36, 0.14, 0.22, 0.14, -Math.PI / 2); for (const k of [-1, 1]) { part(body, G.sphereLo, mat('#FFA62B', 0.5), k * 0.15, 0.05, -0.1, 0.22, 0.08, 0.3); part(body, G.sphereLo, mat('#1E2230', 0.5), k * 0.34, 0.5, 0, 0.12, 0.5, 0.25, 0, 0, k * 0.4); }
        eyes(0.8, -0.25, 0.1);
      }
      else if (W === 'mountains') { part(body, G.sphere, mat('#F8F9FA', 0.6), 0, 0.45, 0, 0.85, 0.85, 0.85); eyes(0.6, -0.38, 0.14); }
      else { part(body, G.sphere, mat('#FF4D6D', 0.35), 0, 0.42, 0, 0.8, 0.8, 0.8); part(body, G.torus, mat('#FFD166', 0.4), 0, 0.42, 0, 1, 1, 1); }
      for (let i = -6; i <= 6; i++) { const dot = part(g, G.cyl, mat('#FFFFFF', 0.5), i * 0.45, 0.02, 0, 0.18, 0.01, 0.18); dot.castShadow = false; }
    }
    return g;
  }

  // ------------------------------------------------------------------ collectibles & markers
  const gold = () => mat('#FFC72C', 0.18, 1, 0.35);
  function buildCollectible(kind, id, content) {
    const g = new T.Group();
    if (kind === 'coin') { part(g, G.cyl, gold(), 0, 0, 0, 0.55, 0.08, 0.55, Math.PI / 2); part(g, G.star, mat('#FFE680', 0.2, 1, 0.6), 0, 0, 0, 0.45, 0.45, 0.62); noShadow(g); }
    else if (kind === 'fish') { part(g, G.fish, mat('#5BC0FF', 0.25, 0.2, 0.5), 0, 0, 0, 1.1, 1.1, 1.1); part(g, G.sphereLo, mat('#FFFFFF', 0.2), 0.28, 0.05, 0.08, 0.08, 0.08, 0.08); }
    else if (kind === 'gem') part(g, G.gem, mat('#C04DFF', 0.1, 0.2, 1.2), 0, 0, 0, 0.75, 0.9, 0.75);
    else if (kind === 'star') part(g, G.star, mat('#FFD23F', 0.25, 0.5, 1.4), 0, 0, 0, 1.3, 1.3, 1.3);
    else if (kind === 'box') { part(g, G.box, mat('#8E44AD', 0.5), 0, 0, 0, 0.7, 0.7, 0.7); part(g, G.box, mat('#FFD966', 0.3, 0, 1.2), 0, 0, 0, 0.72, 0.72, 0.14); part(g, G.box, mat('#FFD966', 0.3, 0, 1.2), 0, 0, 0, 0.14, 0.72, 0.72); part(g, G.torus, mat('#FFD966', 0.3, 0, 1.2), 0, 0.42, 0, 0.4, 0.6, 0.4); }
    else if (kind === 'powerup') {
      const def = content.powerUps.find((p) => p.id === id); const c = def ? def.color : '#4CC9F0';
      const bubble = part(g, G.sphere, glass(c, 0.35), 0, 0, 0, 1.1, 1.1, 1.1); bubble.castShadow = false;
      const icon = mat(c, 0.3, 0, 1.4);
      if (id === 'magnet') part(g, G.torus, icon, 0, 0, 0, 0.8, 0.9, 0.8);
      else if (id === 'shield') part(g, G.heart, icon, 0, 0, 0, 0.7, 0.7, 0.7);
      else if (id === 'super_jump') part(g, G.cone, icon, 0, 0, 0, 0.45, 0.5, 0.45);
      else if (id === 'speed_boost') { part(g, G.box, icon, 0, 0, 0, 0.12, 0.6, 0.12, 0, 0, 0.44); part(g, G.box, icon, 0.12, 0.1, 0, 0.12, 0.4, 0.12, 0, 0, -0.6); }
      else { part(g, G.cyl, icon, -0.12, 0, 0, 0.4, 0.05, 0.4, Math.PI / 2); part(g, G.cyl, icon, 0.12, 0, 0.05, 0.4, 0.05, 0.4, Math.PI / 2); }
    }
    return g;
  }
  function buildCheckpoint() {
    const g = new T.Group();
    for (const s of [-1, 1]) { part(g, G.cyl, mat('#FFFFFF', 0.3, 0.5), s * 3.8, 1.6, 0, 0.12, 3.2, 0.12); part(g, G.box, mat('#06D6A0', 0.5), s * 4.25, 2.8, 0, 0.8, 0.5, 0.04); part(g, G.sphereLo, mat('#6BFF9E', 0.3, 0, 2), s * 3.8, 3.3, 0, 0.25, 0.25, 0.25); }
    const line = part(g, G.box, mat('#6BFF9E', 0.3, 0, 1), 0, 0.02, 0, 7, 0.02, 0.3); line.castShadow = false;
    return g;
  }
  function buildFinish() {
    const g = new T.Group(); const red = mat('#FF4D6D', 0.5);
    for (const s of [-1, 1]) part(g, G.cyl, red, s * 4, 2.5, 0, 0.4, 5, 0.4);
    part(g, G.box, red, 0, 5, 0, 8.4, 0.9, 0.4);
    for (let i = 0; i < 12; i++) for (let r = 0; r < 2; r++) { const c = part(g, G.box, mat((i + r) % 2 ? '#222222' : '#FFFFFF', 0.6), -3.3 + i * 0.6, 0.02, -0.3 + r * 0.6, 0.6, 0.02, 0.6); c.castShadow = false; }
    for (let i = 0; i < 7; i++) part(g, G.sphereLo, mat('#FFD966', 0.2, 0, 2.5), -3 + i, 5, -0.25, 0.22, 0.22, 0.22);
    return g;
  }
  function buildArrows(length) {
    const g = new T.Group(); const m = mat('#FFD23F', 0.3, 0, 1.6);
    for (let i = 0; i < 3; i++) for (let l = 0; l < 3; l++) { const z = -(length * (i + 1) / 4), x = (l - 1) * LANE; part(g, G.box, m, x - 0.25, 0.03, z, 0.14, 0.02, 0.8, 0, 0.7, 0); part(g, G.box, m, x + 0.25, 0.03, z, 0.14, 0.02, 0.8, 0, -0.7, 0); }
    noShadow(g); return g;
  }

  // ------------------------------------------------------------------ scenery
  function buildDecor(type) {
    const g = new T.Group();
    switch (type) {
      case 'tree_round': part(g, G.cyl, mat('#8B5A2B', 0.9), 0, 1.2, 0, 0.45, 2.4, 0.45); part(g, G.sphereLo, mat('#5DBB63', 0.8), 0, 3.2, 0, 3, 2.7, 3); part(g, G.sphereLo, mat('#6FCF73', 0.8), 0.7, 3.9, 0.3, 1.6, 1.4, 1.6); break;
      case 'tree_tall': part(g, G.cyl, mat('#6E4B2A', 0.9), 0, 2.5, 0, 0.6, 5, 0.6); for (let i = 0; i < 3; i++) part(g, G.sphereLo, mat(i % 2 ? '#40916C' : '#2D6A4F', 0.8), 0, 4.5 + i * 1.3, 0, 3.4 - i * 0.8, 3.4 - i * 0.8, 3.4 - i * 0.8); break;
      case 'pine': part(g, G.cyl, mat('#6E4B2A', 0.9), 0, 0.8, 0, 0.35, 1.6, 0.35); for (let i = 0; i < 3; i++) part(g, G.cone, mat('#1B7F5B', 0.8), 0, 2 + i * 1.1, 0, 2.6 - i * 0.6, 1.8, 2.6 - i * 0.6); break;
      case 'flowers': for (let i = 0; i < 9; i++) { const a = i * 2.4, r = 0.2 + (i % 3) * 0.3; part(g, G.sphereLo, mat(['#FF7AB6', '#FFD166', '#F72585', '#FFFFFF', '#B5179E'][i % 5], 0.6), Math.cos(a) * r, 0.4, Math.sin(a) * r, 0.18, 0.18, 0.18); } noShadow(g); break;
      case 'bush': part(g, G.sphereLo, mat('#4CAF50', 0.8), 0, 0.5, 0, 1.8, 1.1, 1.4); break;
      case 'bench': part(g, G.box, mat('#B5651D', 0.6), 0, 0.45, 0, 1.6, 0.08, 0.5); part(g, G.box, mat('#B5651D', 0.6), 0, 0.8, 0.22, 1.6, 0.4, 0.06); break;
      case 'lamp_post': case 'street_light': { part(g, G.cyl, mat('#3D405B', 0.3, 0.8), 0, 2, 0, 0.14, 4, 0.14); const bulb = mat('#FFE0A3', 0.3, 0, 0.3); nightGlow.push(bulb); part(g, G.sphereLo, bulb, 0, 3.9, 0, 0.4, 0.4, 0.4); break; }
      case 'puddle': case 'stream': case 'river': { const w = part(g, G.cyl, mat('#6EC6FF', 0.05, 0.2), 0, 0.02, 0, type === 'puddle' ? 2 : 3, 0.01, type === 'puddle' ? 1.3 : 20); w.castShadow = false; break; }
      case 'small_bridge': case 'wood_bridge': for (let i = 0; i < 6; i++) part(g, G.box, mat('#A47148', 0.8), 0, 0.25 + Math.sin(i / 5 * Math.PI) * 0.35, -1.25 + i * 0.5, 1.4, 0.08, 0.45); break;
      case 'rock_moss': case 'boulder': part(g, G.sphereLo, mat('#8D8D8D', 0.9), 0, 0.6, 0, 2, 1.3, 1.6); if (type === 'rock_moss') part(g, G.sphereLo, mat('#6A994E', 0.8), 0, 1.1, 0, 1.4, 0.4, 1.1); break;
      case 'fallen_tree': part(g, G.cyl, mat('#7A5230', 0.9), 0, 0.4, 0, 0.8, 4.8, 0.8, 0, 0, Math.PI / 2); break;
      case 'mushroom': part(g, G.cyl, mat('#FFF3E0', 0.6), 0, 0.3, 0, 0.25, 0.6, 0.25); part(g, G.sphereLo, mat('#E63946', 0.5), 0, 0.62, 0, 0.8, 0.45, 0.8); break;
      case 'building': { const h = 8 + Math.random() * 10; part(g, G.box, mat(['#F4A261', '#E76F51', '#2A9D8F', '#8ECAE6', '#FFB4A2', '#CDB4DB'][Math.floor(Math.random() * 6)], 0.7), 0, h / 2, 0, 7, h, 7); const win = mat('#FFE9A8', 0.4, 0, 0.2); nightGlow.push(win); for (let f = 0; f < Math.floor(h / 2.2); f++) for (let w = 0; w < 3; w++) { const m = part(g, G.box, win, -2.2 + w * 2.2, 2 + f * 2.2, -3.52, 1.1, 1.2, 0.05); m.castShadow = false; } break; }
      case 'car': { const body = mat(['#E63946', '#3A86FF', '#FFBE0B', '#8338EC', '#06D6A0'][Math.floor(Math.random() * 5)], 0.25, 0.4); part(g, G.box, body, 0, 0.6, 0, 1.8, 0.7, 3.8); part(g, G.box, glass('#BDE0FE', 0.8), 0, 1.15, 0.2, 1.6, 0.5, 1.9); for (let i = 0; i < 4; i++) part(g, G.cyl, mat('#222222'), i < 2 ? -0.85 : 0.85, 0.35, i % 2 ? 1.2 : -1.2, 0.7, 0.2, 0.7, 0, 0, Math.PI / 2); break; }
      case 'traffic_barrier': for (let i = 0; i < 3; i++) part(g, G.box, mat(i % 2 ? '#FFFFFF' : '#E63946', 0.5), -0.8 + i * 0.8, 0.6, 0, 0.8, 0.3, 0.1); break;
      case 'park_patch': part(g, G.box, mat('#74C69D', 0.9), 0, 0.05, 0, 8, 0.1, 8); part(g, G.sphereLo, mat('#52B788', 0.8), 0, 2.6, 0, 2.4, 2.4, 2.4); part(g, G.cyl, mat('#8B5A2B'), 0, 1, 0, 0.35, 2, 0.35); break;
      case 'cliff': part(g, G.box, mat('#8D99AE', 0.9), 0, 5, 0, 8, 10, 10); part(g, G.box, mat('#FFFFFF', 0.5), 0, 10.1, 0, 8.1, 0.3, 10.1); break;
      case 'waterfall': part(g, G.box, mat('#6C757D', 0.9), 0, 6, 0.6, 8, 12, 1); part(g, G.box, mat('#9ED8FF', 0.1, 0, 0.5), 0, 6, 0, 2.5, 12, 0.3); break;
      case 'rope_bridge': part(g, G.box, mat('#A47148', 0.8), 0, 3, 0, 1.6, 0.1, 14); break;
      case 'snow_patch': part(g, G.sphereLo, mat('#FFFFFF', 0.5), 0, 0, 0, 4, 0.4, 3); break;
      case 'palm': for (let i = 0; i < 6; i++) part(g, G.cyl, mat('#A67C52', 0.9), i * 0.12, 0.5 + i * 0.9, 0, 0.4 - i * 0.03, 0.96, 0.4 - i * 0.03, 0, 0, -0.07 - i * 0.035); for (let i = 0; i < 6; i++) part(g, G.sphereLo, mat('#2A9D8F', 0.7), 0.7 + Math.cos(i) * 1.3, 5.5, Math.sin(i) * 1.3, 2.4, 0.2, 0.7, 0, i * 1.05, -0.35); break;
      case 'boat': part(g, G.box, mat('#FFFFFF', 0.4), 0, 0.4, 0, 2, 0.8, 5); part(g, G.box, mat('#E63946', 0.5), 0, 0.85, 0, 2.05, 0.15, 5.05); part(g, G.cyl, mat('#8B5A2B'), 0, 3, 0, 0.12, 4.8, 0.12); part(g, G.box, mat('#FFF3B0', 0.7), 0, 3.4, 0.8, 0.05, 3, 1.8); break;
      case 'crab': part(g, G.sphereLo, mat('#FF6F59', 0.5), 0, 0.2, 0, 0.6, 0.3, 0.45); break;
      case 'sand_castle': { const s = mat('#E9C98B', 0.9); part(g, G.box, s, 0, 0.4, 0, 2, 0.8, 2); for (let i = 0; i < 4; i++) { const x = i < 2 ? -0.9 : 0.9, z = i % 2 ? 0.9 : -0.9; part(g, G.cyl, s, x, 0.9, z, 0.6, 1.8, 0.6); part(g, G.cone, s, x, 2.1, z, 0.7, 0.6, 0.7); } break; }
      case 'umbrella': part(g, G.cyl, mat('#FFFFFF', 0.5), 0, 1.2, 0, 0.08, 2.4, 0.08); part(g, G.cone, mat('#FF6F59', 0.6), 0, 2.4, 0, 2.8, 0.7, 2.8); break;
      case 'shell': part(g, G.sphereLo, mat('#FFD6E0', 0.3), 0, 0.08, 0, 0.4, 0.15, 0.35); break;
      case 'mountain': { part(g, G.cone, mat('#7D8597', 0.95), 0, 22, 0, 60, 45, 60); part(g, G.cone, mat('#FFFFFF', 0.6), 0, 37.5, 0, 20.5, 15.2, 20.5); noShadow(g); break; }
      case 'hot_air_balloon': {
        const pals = [['#FF595E', '#FFCA3A', '#FFFFFF'], ['#1982C4', '#8AC926', '#FFFFFF'], ['#6A4C93', '#FF9F1C', '#FFD6E0']];
        const pal = pals[Math.floor(Math.random() * pals.length)];
        // Balloons ignore distance fog so they stay bright and cheerful in the sky.
        const bm = (hex) => cache['bal' + hex] || (cache['bal' + hex] = new T.MeshStandardMaterial({ color: lin(hex), roughness: 0.55, fog: false }));
        const e0 = bm(pal[0]), e1 = bm(pal[1]), e2 = bm(pal[2]);
        part(g, G.sphere, e0, 0, 6, 0, 4, 4.6, 4);
        for (let i = 0; i < 3; i++) part(g, G.torus, i % 2 ? e2 : e1, 0, 5.1 + i * 0.9, 0, 4.6 - Math.abs(i - 1) * 0.5, 4.6 - Math.abs(i - 1) * 0.5, 2.2, Math.PI / 2);
        part(g, G.sphere, e1, 0, 8.1, 0, 2.2, 0.9, 2.2);
        part(g, G.cone, e0, 0, 3.2, 0, 3.3, 2.2, 3.3, Math.PI);
        for (let i = 0; i < 4; i++) { const an = i * Math.PI / 2 + Math.PI / 4; part(g, G.cyl, bm('#5C4033'), Math.cos(an) * 0.45, 1.55, Math.sin(an) * 0.45, 0.04, 1, 0.04); }
        part(g, G.box, bm('#A0673A'), 0, 0.45, 0, 1.1, 0.8, 1.1); part(g, G.box, bm('#6E4B2A'), 0, 0.88, 0, 1.2, 0.1, 1.2);
        part(g, G.sphere, balloonFlame, 0, 1.75, 0, 0.35, 0.55, 0.35); // HDR emissive: blooms
        noShadow(g); break;
      }
      case 'snow_pine': part(g, G.cyl, mat('#6E4B2A', 0.9), 0, 0.8, 0, 0.35, 1.6, 0.35); for (let i = 0; i < 3; i++) { part(g, G.cone, mat('#1B6F55', 0.8), 0, 2 + i * 1.1, 0, 2.6 - i * 0.6, 1.8, 2.6 - i * 0.6); part(g, G.cone, mat('#FFFFFF', 0.5), 0, 2.45 + i * 1.1, 0, 1.6 - i * 0.4, 0.9, 1.6 - i * 0.4); } break;
      case 'snowman': { const s = mat('#FFFFFF', 0.5); part(g, G.sphere, s, 0, 0.7, 0, 1.4, 1.3, 1.4); part(g, G.sphere, s, 0, 1.75, 0, 1, 0.95, 1); part(g, G.sphere, s, 0, 2.55, 0, 0.7, 0.7, 0.7);
        part(g, G.cone, mat('#FF8C1A', 0.5), 0, 2.55, -0.45, 0.12, 0.4, 0.12, -Math.PI / 2); for (const k of [-1, 1]) part(g, G.sphereLo, mat('#111111', 0.4), k * 0.13, 2.68, -0.3, 0.08, 0.08, 0.08);
        part(g, G.cyl, mat('#222222', 0.5), 0, 3.0, 0, 0.55, 0.06, 0.55); part(g, G.cyl, mat('#222222', 0.5), 0, 3.25, 0, 0.38, 0.5, 0.38); part(g, G.torus, mat('#E63946', 0.6), 0, 2.2, 0, 0.9, 0.9, 1.4, Math.PI / 2); break; }
      case 'igloo': { const ice = mat('#EAF6FF', 0.4); part(g, G.sphere, ice, 0, 0, 0, 4, 3.4, 4); part(g, G.cyl, ice, 0, 0.6, -1.9, 1.3, 1.2, 1.2, Math.PI / 2); part(g, G.cyl, mat('#2B3A4A', 0.8), 0, 0.55, -2.5, 0.8, 0.05, 0.8, Math.PI / 2);
        for (let r = 0; r < 3; r++) part(g, G.torus, mat('#CFE6F7', 0.5), 0, 0.4 + r * 0.45, 0, 4.4 - r * 0.9, 4.4 - r * 0.9, 0.3, Math.PI / 2); break; }
      case 'frozen_pond': { const w = part(g, G.cyl, mat('#BFE8FA', 0.05, 0.2), 0, 0.02, 0, 5, 0.02, 3.5); w.castShadow = false; break; }
      case 'sled': part(g, G.box, mat('#C0392B', 0.5), 0, 0.45, 0, 0.9, 0.15, 1.8); for (const k of [-1, 1]) part(g, G.box, mat('#8B5A2B', 0.6), k * 0.4, 0.2, 0, 0.08, 0.08, 2.1); break;
      case 'penguin': part(g, G.sphere, mat('#1E2230', 0.5), 0, 0.5, 0, 0.6, 0.9, 0.55); part(g, G.sphere, mat('#FFFFFF', 0.5), 0, 0.45, -0.12, 0.42, 0.7, 0.4); part(g, G.cone, mat('#FFA62B', 0.5), 0, 0.72, -0.36, 0.14, 0.22, 0.14, -Math.PI / 2); break;
      case 'cloud': for (let i = 0; i < 4; i++) part(g, G.sphereLo, mat('#FFFFFF', 1, 0, 0.15), -1.5 + i, Math.sin(i) * 0.3, 0, 1.6 + (i % 2) * 0.6, 1.4 + (i % 2) * 0.5, 1.6); noShadow(g); break;
      default: return null;
    }
    return g;
  }

  global.TFYArt = { THREE: T, LANE, lin, mat, part, group, buildCat, animateCat, setCatState, buildObstacle, buildCollectible, buildCheckpoint, buildFinish, buildArrows, buildDecor, nightGlow, balloonFlame, G };
})(window);
