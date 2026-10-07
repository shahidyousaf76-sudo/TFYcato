// TFY Cat Adventure — world-famous landmarks (stylised, child-friendly, built from simple shapes).
// Same set and sizes as the Unity DecorFactory. Base at y = 0, metres. Night lights use the shared night-glow list.
(function (global) {
  'use strict';
  const T = global.THREE, A = global.TFYArt, part = A.part, G = A.G;
  // Landmarks ignore distance fog so children can see them from far away as the cat runs towards them.
  const cache = {};
  const mat = (hex, rough = 0.6, metal = 0) => cache[hex + rough + metal] || (cache[hex + rough + metal] = new T.MeshStandardMaterial({ color: A.lin(hex), roughness: rough, metalness: metal, fog: false }));
  const pyramid = new T.ConeGeometry(0.7071, 1, 4); pyramid.rotateY(Math.PI / 4);
  const taper = new T.CylinderGeometry(0.36, 0.5, 1, 16);
  const glow = (hex, i = 0.4) => { const m = new T.MeshStandardMaterial({ color: A.lin(hex), emissive: A.lin(hex), emissiveIntensity: i, roughness: 0.4, fog: false }); A.nightGlow.push(m); return m; };

  /** Display name + country, shown in a banner when the cat runs past. */
  const NAMES = {
    eiffel_tower: '🗼 Eiffel Tower — Paris', big_ben: '🕰️ Big Ben — London', statue_of_liberty: '🗽 Statue of Liberty — New York',
    leaning_tower_pisa: '🏛️ Leaning Tower of Pisa — Italy', taj_mahal: '🕌 Taj Mahal — India', windmill: '🌷 Windmill — Netherlands',
    pagoda: '🏯 Pagoda — Japan', torii_gate: '⛩️ Torii Gate — Japan', stonehenge: '🪨 Stonehenge — England',
    great_wall: '🧱 Great Wall — China', christ_the_redeemer: '⛰️ Christ the Redeemer — Brazil',
    sydney_opera_house: '🎭 Sydney Opera House — Australia', golden_gate_bridge: '🌉 Golden Gate Bridge — USA', little_mermaid: '🧜 The Little Mermaid — Copenhagen',
    mount_fuji: '🗻 Mount Fuji — Japan', st_basils_cathedral: "⛪ St. Basil's Cathedral — Moscow", santa_village: '🎅 Santa Claus Village — Finland',
  };
  /** Where each landmark stands relative to the path: x distance (sign chosen per placement), forced side for some. */
  const PLACE = {
    eiffel_tower: { x: 24 }, big_ben: { x: 21 }, statue_of_liberty: { x: 22 }, leaning_tower_pisa: { x: 19 }, taj_mahal: { x: 32 },
    windmill: { x: 16 }, pagoda: { x: 20 }, torii_gate: { x: 0 }, stonehenge: { x: 22 }, great_wall: { x: 22, side: -1 }, christ_the_redeemer: { x: 42 },
    sydney_opera_house: { x: 30, side: 1 }, golden_gate_bridge: { x: 0 }, little_mermaid: { x: 10, side: -1 },
    mount_fuji: { x: 60 }, st_basils_cathedral: { x: 24 }, santa_village: { x: 20 },
  };

  function build(id) {
    const g = new T.Group(); g.userData.landmark = id;
    switch (id) {
      case 'eiffel_tower': {
        const iron = mat('#8C6A4A', 0.55, 0.6), lamp = glow('#FFD27A');
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
          part(g, G.cyl, iron, sx * 4.4, 5, sz * 4.4, 1, 11.5, 1, -sz * 0.33, 0, sx * 0.33);
          part(g, G.cyl, iron, sx * 2.25, 15, sz * 2.25, 0.6, 10.5, 0.6, -sz * 0.13, 0, sx * 0.13);
        }
        for (const s of [-1, 1]) { part(g, G.torus, iron, s * 4.3, 6.2, 0, 2.4, 2.4, 1.2, 0, Math.PI / 2, 0); part(g, G.torus, iron, 0, 6.2, s * 4.3, 2.4, 2.4, 1.2); }
        part(g, G.box, iron, 0, 10, 0, 9.5, 0.9, 9.5); part(g, G.box, iron, 0, 20, 0, 4.8, 0.7, 4.8);
        part(g, pyramid, iron, 0, 26.5, 0, 3.4, 13, 3.4); part(g, G.box, iron, 0, 31, 0, 1.4, 1, 1.4);
        part(g, G.cyl, iron, 0, 33.5, 0, 0.18, 4, 0.18);
        for (const y of [10.6, 20.5, 31.6]) for (let i = 0; i < 8; i++) { const a = (i / 8) * Math.PI * 2, r = y < 15 ? 4.6 : y < 25 ? 2.4 : 0.8; part(g, G.sphereLo, lamp, Math.cos(a) * r, y, Math.sin(a) * r, 0.35, 0.35, 0.35); }
        break;
      }
      case 'big_ben': {
        const stone = mat('#D8C38A', 0.8), dark = mat('#3E5F55', 0.6), face = glow('#FFF6D8', 0.3), hand = mat('#222222', 0.4), gold = mat('#FFD54A', 0.3, 0.9);
        part(g, G.box, stone, 0, 9, 0, 5, 18, 5);
        for (const y of [4, 8, 12, 16]) part(g, G.box, mat('#C2AC70', 0.8), 0, y, 0, 5.3, 0.4, 5.3);
        part(g, G.box, stone, 0, 20.5, 0, 5.8, 5, 5.8);
        for (let s = 0; s < 4; s++) {
          const f = new T.Group(); f.rotation.y = (s * Math.PI) / 2; f.position.y = 20.5; g.add(f);
          part(f, G.cyl, face, 0, 0, -2.95, 4, 0.12, 4, Math.PI / 2); part(f, G.box, hand, 0.5, 0.3, -3.05, 0.2, 1.5, 0.05, 0, 0, -0.8); part(f, G.box, hand, -0.2, 0.6, -3.05, 0.2, 1.1, 0.05, 0, 0, 0.3);
        }
        part(g, G.box, stone, 0, 24.5, 0, 4.6, 3, 4.6); part(g, pyramid, dark, 0, 29.5, 0, 5.2, 7, 5.2); part(g, G.sphereLo, gold, 0, 33.4, 0, 0.6, 0.9, 0.6);
        break;
      }
      case 'statue_of_liberty': {
        const base = mat('#B9A88C', 0.85), copper = mat('#7FC4AE', 0.6, 0.3), flame = glow('#FFB13B', 2.2);
        part(g, G.box, base, 0, 1, 0, 9, 2, 9); part(g, G.box, base, 0, 7, 0, 6, 10, 6); part(g, G.box, base, 0, 12.4, 0, 6.6, 0.8, 6.6);
        part(g, G.cone, copper, 0, 16.8, 0, 4.2, 9.6, 4.2); part(g, G.cyl, copper, 0, 21.8, 0, 1.6, 3, 1.6);
        part(g, G.sphere, copper, 0, 24.1, 0, 1.7, 1.9, 1.7);
        for (let i = 0; i < 7; i++) { const a = -Math.PI / 2 + (i - 3) * 0.4; part(g, G.cone, copper, Math.cos(a) * 0.9, 25.3 + Math.sin(a + Math.PI / 2) * 0.2, Math.sin(a) * 0.9, 0.35, 1.3, 0.35, Math.sin(a) * 0.9, 0, -Math.cos(a) * 0.9); }
        part(g, G.cyl, copper, 1.5, 26, 0, 0.55, 5, 0.55, 0, 0, -0.25); part(g, G.cyl, mat('#D4AF37', 0.3, 0.9), 2.1, 28.7, 0, 0.6, 1, 0.6); part(g, G.sphere, flame, 2.1, 29.6, 0, 0.9, 1.3, 0.9);
        part(g, G.box, copper, -1.4, 21, -0.7, 1.3, 2.2, 0.4, 0, 0, 0.3);
        break;
      }
      case 'leaning_tower_pisa': {
        const tower = new T.Group(); tower.rotation.z = 0.09; g.add(tower);
        const marble = mat('#F2EDE2', 0.6), ring = mat('#E2DACB', 0.6);
        for (let i = 0; i < 7; i++) { part(tower, G.cyl, marble, 0, 1.5 + i * 3, 0, 6, 3, 6); part(tower, G.torus, ring, 0, 3 + i * 3, 0, 7.6, 7.6, 1.2, Math.PI / 2); }
        part(tower, G.cyl, marble, 0, 22.5, 0, 4.4, 3, 4.4); part(tower, G.sphereLo, marble, 0, 24.2, 0, 4.4, 1.4, 4.4);
        break;
      }
      case 'taj_mahal': {
        const white = mat('#FBF8F2', 0.5), shadow = mat('#D9D2C5', 0.7), gold = mat('#E8C35A', 0.3, 0.9);
        part(g, G.box, white, 0, 0.75, 0, 26, 1.5, 26); part(g, G.box, white, 0, 6.5, 0, 13, 10, 13);
        for (let s = 0; s < 4; s++) { const f = new T.Group(); f.rotation.y = (s * Math.PI) / 2; g.add(f); part(f, G.box, shadow, 0, 5.5, -6.55, 4, 6.5, 0.2); }
        part(g, G.cyl, white, 0, 12.3, 0, 6, 1.6, 6); part(g, G.sphere, white, 0, 16, 0, 8, 9, 8); part(g, G.cone, white, 0, 21.3, 0, 1.4, 2.4, 1.4); part(g, G.sphereLo, gold, 0, 22.8, 0, 0.5, 0.8, 0.5);
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
          part(g, G.sphere, white, sx * 4.5, 13, sz * 4.5, 2.6, 2.8, 2.6);
          part(g, G.cyl, white, sx * 11, 9.5, sz * 11, 1, 17, 1); part(g, G.sphere, white, sx * 11, 18.6, sz * 11, 1.6, 1.8, 1.6);
        }
        break;
      }
      case 'windmill': {
        part(g, taper, mat('#E9DCC9', 0.8), 0, 5, 0, 5, 10, 5); part(g, G.cone, mat('#7A2E2E', 0.7), 0, 11.5, 0, 4.2, 3, 4.2);
        part(g, G.box, mat('#5C3A21', 0.7), 0, 1.5, -2.45, 1.3, 2.4, 0.2);
        const hub = new T.Group(); hub.position.set(0, 10.2, -2.6); g.add(hub); g.userData.spin = hub;
        part(hub, G.sphereLo, mat('#5C3A21', 0.6), 0, 0, 0, 0.8, 0.8, 0.8);
        for (let i = 0; i < 4; i++) { const b = new T.Group(); b.rotation.z = (i * Math.PI) / 2; hub.add(b); part(b, G.box, mat('#F5F0E6', 0.7), 0, 3.6, 0, 1.3, 6.4, 0.15); part(b, G.box, mat('#8B5A2B', 0.7), 0, 3.6, -0.1, 0.18, 7, 0.15); }
        break;
      }
      case 'pagoda': {
        const red = mat('#C0392B', 0.6), roof = mat('#3B2F2F', 0.6), gold = mat('#E8C35A', 0.3, 0.9);
        for (let i = 0; i < 5; i++) { const s = 7 - i * 1.1, y = i * 3.6; part(g, G.box, red, 0, y + 1.5, 0, s, 3, s); part(g, pyramid, roof, 0, y + 3.4, 0, s * 1.6, 1.2, s * 1.6); }
        part(g, G.cyl, gold, 0, 20.5, 0, 0.3, 4, 0.3); for (let i = 0; i < 4; i++) part(g, G.torus, gold, 0, 19 + i * 0.7, 0, 0.6, 0.6, 0.6, Math.PI / 2);
        break;
      }
      case 'torii_gate': { // stands over the path: the cat runs right through it
        const red = mat('#D7263D', 0.5), black = mat('#1E1E24', 0.5);
        for (const x of [-5.2, 5.2]) { part(g, G.cyl, red, x, 4.6, 0, 0.9, 9.2, 0.9); part(g, G.cyl, black, x, 0.4, 0, 1.1, 0.8, 1.1); }
        part(g, G.box, red, 0, 7.6, 0, 11.6, 0.6, 0.7); part(g, G.box, red, 0, 9.4, 0, 14, 0.8, 1.1); part(g, G.box, black, 0, 9.95, 0, 14.6, 0.35, 1.3);
        part(g, G.box, red, 0, 8.5, 0, 0.6, 1.8, 0.5);
        break;
      }
      case 'stonehenge': {
        const stone = mat('#9A968C', 0.95), dark = mat('#85817A', 0.95);
        for (let i = 0; i < 14; i++) { const a = (i / 14) * Math.PI * 2; part(g, G.box, stone, Math.cos(a) * 9, 2.1, Math.sin(a) * 9, 1.4, 4.2, 0.9, 0, -a, 0); if (i % 2 === 0) part(g, G.box, dark, Math.cos(a + 0.22) * 9, 4.5, Math.sin(a + 0.22) * 9, 1, 0.7, 4.2, 0, -a - 0.22, 0); }
        for (let i = 0; i < 5; i++) { const a = Math.PI * 0.2 + i * Math.PI * 0.4; const x = Math.cos(a) * 4.5, z = Math.sin(a) * 4.5; part(g, G.box, stone, x - 0.9, 3, z, 1.4, 6, 1.1); part(g, G.box, stone, x + 0.9, 3, z, 1.4, 6, 1.1); part(g, G.box, dark, x, 6.4, z, 3.4, 0.9, 1.3); }
        break;
      }
      case 'great_wall': { // runs alongside the route with watchtowers
        const brick = mat('#B59B7A', 0.9), top = mat('#9C8263', 0.9), roof = mat('#5A3E2B', 0.7);
        for (let i = 0; i < 12; i++) {
          const z = -i * 10, y = Math.sin(i * 0.7) * 1.5;
          part(g, G.box, brick, 0, 2.5 + y, z, 3.5, 5, 10.2);
          for (let k = 0; k < 4; k++) for (const s of [-1, 1]) part(g, G.box, top, s * 1.6, 5.4 + y, z - 4 + k * 2.6, 0.4, 0.8, 1.2);
          if (i % 4 === 0) { part(g, G.box, brick, 0, 4.5 + y, z, 6, 9, 6); part(g, pyramid, roof, 0, 10 + y, z, 7.5, 2.2, 7.5); }
        }
        break;
      }
      case 'christ_the_redeemer': {
        const rock = mat('#6E7F5E', 0.95), white = mat('#F2F0EA', 0.5);
        part(g, G.cone, rock, 0, 13, 0, 34, 26, 34); part(g, G.box, white, 0, 27.5, 0, 2.4, 3, 2.4);
        part(g, taper, white, 0, 32, 0, 2.2, 6, 2.2); part(g, G.box, white, 0, 34.2, 0, 10, 0.9, 1);
        part(g, G.sphere, white, 0, 35.8, 0, 1.1, 1.3, 1.1);
        break;
      }
      case 'sydney_opera_house': {
        const deck = mat('#D9C8A9', 0.85), shell = mat('#FFFFFF', 0.25, 0.1);
        part(g, G.box, deck, 0, 1, 0, 28, 2, 16);
        for (const [x, z, s, r] of [[-8, 2, 1, -0.5], [-3, 2, 1.15, -0.55], [3, 2, 1.25, -0.6], [-6, -4, 0.75, -0.5], [-1.5, -4, 0.85, -0.55], [9, -1, 0.7, -0.45]])
          part(g, G.sphere, shell, x, 2, z, 6 * s, 11 * s, 4.5 * s, r, 0, 0);
        break;
      }
      case 'golden_gate_bridge': { // spans along the route, out over the water
        const red = mat('#C0362C', 0.55, 0.2), road = mat('#4A4A4A', 0.7);
        part(g, G.box, red, 0, 8, 0, 9, 1.2, 100); part(g, G.box, road, 0, 8.7, 0, 7.6, 0.2, 100);
        for (const tz of [-26, 26]) {
          for (const x of [-4, 4]) part(g, G.box, red, x, 12, tz, 1.4, 26, 1.8);
          for (const y of [10, 17, 23.5]) part(g, G.box, red, 0, y, tz, 8, 1, 1.2);
          for (const x of [-4, 4]) part(g, G.cyl, red, x, 0, tz, 2.2, 6, 2.2);
        }
        for (const x of [-4.2, 4.2]) for (let z = -50; z <= 50; z += 1.6) {
          const y = Math.abs(z) <= 26 ? 24.5 - 14 * (1 - (z / 26) ** 2) : 24.5 - ((Math.abs(z) - 26) / 24) * 15;
          part(g, G.sphereLo, red, x, y, z, 0.5, 0.5, 0.5);
          if (Math.round(z * 10) % 48 === 0 && y > 9.5) part(g, G.box, red, x, (y + 9) / 2, z, 0.12, y - 9, 0.12);
        }
        break;
      }
      case 'little_mermaid': {
        const rock = mat('#8C8A85', 0.95), bronze = mat('#5E8C6A', 0.45, 0.6);
        part(g, G.sphereLo, rock, 0, 0.8, 0, 4.5, 2.4, 3.5); part(g, G.sphereLo, rock, 1.4, 1.4, 0.6, 2.2, 1.8, 2);
        part(g, G.cyl, bronze, 0.3, 2.9, 0, 0.6, 1.5, 0.5, 0, 0, 0.25); part(g, G.sphere, bronze, 0.5, 3.95, 0, 0.6, 0.65, 0.6);
        part(g, G.cone, bronze, -0.7, 2.25, 0.2, 0.55, 2, 0.55, 0, 0, 1.35); part(g, G.box, bronze, -1.8, 2, 0.2, 0.9, 0.12, 0.6, 0, 0, 0.3);
        break;
      }
      case 'mount_fuji': {
        part(g, G.cone, mat('#8FA6D6', 0.95), 0, 17, 0, 70, 34, 70);
        part(g, G.cone, mat('#FFFFFF', 0.5), 0, 28.5, 0, 25, 11.2, 25);
        for (let i = 0; i < 8; i++) { const a = (i / 8) * Math.PI * 2; part(g, G.cone, mat('#FFFFFF', 0.5), Math.cos(a) * 11, 20.5, Math.sin(a) * 11, 4, 6, 4, Math.sin(a) * 0.5, 0, -Math.cos(a) * 0.5); }
        part(g, G.cyl, mat('#DDE8F5', 0.9), 0, 34.2, 0, 6, 0.6, 6);
        break;
      }
      case 'st_basils_cathedral': {
        const wall = mat('#B03A2E', 0.7), white = mat('#F4EFE6', 0.6), gold = mat('#FFD54A', 0.3, 0.9);
        part(g, G.box, wall, 0, 4, 0, 14, 8, 14); part(g, G.box, white, 0, 8.3, 0, 14.6, 0.6, 14.6);
        const domes = [['#2E86DE', '#FFD54A'], ['#27AE60', '#E74C3C'], ['#E67E22', '#2E86DE'], ['#E74C3C', '#27AE60'], ['#F1C40F', '#8E44AD']];
        const spots = [[0, 0, 1.4], [-5, -5, 1], [5, -5, 1], [-5, 5, 1], [5, 5, 1]];
        spots.forEach(([x, z, s], i) => {
          const h = 8 + (s > 1 ? 10 : 6); part(g, G.cyl, wall, x, 8 + (h - 8) / 2, z, 2.6 * s, h - 8, 2.6 * s);
          const dome = mat(domes[i][0], 0.5), stripe = mat(domes[i][1], 0.5);
          part(g, G.sphere, dome, x, h + 1.5 * s, z, 3.6 * s, 3.6 * s, 3.6 * s); for (let k = 0; k < 3; k++) part(g, G.torus, stripe, x, h + (0.5 + k * 1.1) * s, z, 3.4 * s * (1 - k * 0.22), 3.4 * s * (1 - k * 0.22), 0.6, Math.PI / 2);
          part(g, G.cone, dome, x, h + 3.6 * s, z, 1.6 * s, 2.6 * s, 1.6 * s); part(g, G.cyl, gold, x, h + 5.4 * s, z, 0.15, 1.6, 0.15); part(g, G.box, gold, x, h + 5.6 * s, z, 0.9, 0.12, 0.12);
        });
        part(g, pyramid, mat('#2E7D5B', 0.6), 8.5, 11, 0, 3, 6, 3); part(g, G.box, wall, 8.5, 4, 0, 3, 8, 3);
        break;
      }
      case 'santa_village': {
        const wood = mat('#8B4A2B', 0.8), snow = mat('#FFFFFF', 0.5), red = mat('#D62828', 0.6), win = glow('#FFD27A', 0.6);
        for (const [x, z, s] of [[0, 0, 1.3], [-8, 4, 1], [7, 5, 0.9]]) {
          part(g, G.box, wood, x, 2 * s, z, 6 * s, 4 * s, 5 * s); part(g, pyramid, snow, x, 4 * s + 1.6 * s, z, 8.2 * s, 3.2 * s, 7 * s);
          part(g, G.box, win, x - 1.5 * s, 2.2 * s, z - 2.52 * s, 1.1 * s, 1.1 * s, 0.05); part(g, G.box, win, x + 1.5 * s, 2.2 * s, z - 2.52 * s, 1.1 * s, 1.1 * s, 0.05);
          part(g, G.box, mat('#5C3A21', 0.7), x, 1.2 * s, z - 2.52 * s, 1.1 * s, 2.4 * s, 0.05);
        }
        part(g, G.cyl, mat('#FFFFFF', 0.4), 3.5, 3, -6, 0.25, 6, 0.25); part(g, G.sphereLo, red, 3.5, 6.2, -6, 0.6, 0.6, 0.6); // the Arctic Circle pole
        for (let i = 0; i < 5; i++) part(g, G.cyl, i % 2 ? red : mat('#FFFFFF', 0.4), 3.5, 0.6 + i * 1.1, -6, 0.27, 0.5, 0.27);
        part(g, G.box, red, -2, 0.6, -6, 2.4, 0.7, 1.2); for (const k of [-1, 1]) part(g, G.box, mat('#FFD54A', 0.3, 0.9), -2, 0.15, -6 + k * 0.5, 2.8, 0.1, 0.1); // Santa's sleigh
        for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2; part(g, G.cone, mat('#1B6F55', 0.8), Math.cos(a) * 13, 3, Math.sin(a) * 9, 3, 6, 3); part(g, G.cone, snow, Math.cos(a) * 13, 4.2, Math.sin(a) * 9, 1.8, 2.4, 1.8); }
        break;
      }
      default: return null;
    }
    g.traverse((o) => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = true; } });
    return g;
  }

  global.TFYLandmarks = { build, NAMES, PLACE };
})(window);
