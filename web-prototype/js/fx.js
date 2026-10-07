// TFY Cat Adventure — visual effects for the web prototype: HDR bloom + GPU particles.
//  • Bloom: RenderPass → UnrealBloomPass on a half-float (HDR) target → ACES tonemap + sRGB pass. Emissive materials
//    (coins, gems, big stars, lamps, windows, power-ups, checkpoint lights) glow. Strength follows quality and time of day.
//  • Particles: two pooled point systems (additive "glow" sparkles and normal-blended dust/confetti/weather) drawn with a
//    soft round sprite computed in the shader — thousands of particles in two draw calls, no textures.
(function (global) {
  'use strict';
  const T = global.THREE;

  // ------------------------------------------------------------------ bloom
  // r128 applies tone mapping only when rendering straight to the screen, so the composer ends with its own ACES + sRGB pass.
  // (Names are prefixed: three.js injects its own RRTAndODTFit/tone-mapping chunk into on-screen shaders.)
  const OutputShader = {
    uniforms: { tDiffuse: { value: null }, exposure: { value: 1.1 }, saturation: { value: 1.18 }, contrast: { value: 1.08 } },
    vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: [
      'uniform sampler2D tDiffuse; uniform float exposure; uniform float saturation; uniform float contrast; varying vec2 vUv;',
      'vec3 tfyRRT(vec3 v){ vec3 a = v * (v + 0.0245786) - 0.000090537; vec3 b = v * (0.983729 * v + 0.4329510) + 0.238081; return a / b; }',
      'vec3 tfyAces(vec3 color){',
      '  const mat3 inM = mat3(vec3(0.59719,0.07600,0.02840), vec3(0.35458,0.90834,0.13383), vec3(0.04823,0.01566,0.83777));',
      '  const mat3 outM = mat3(vec3(1.60475,-0.10208,-0.00327), vec3(-0.53108,1.10813,-0.07276), vec3(-0.07367,-0.00605,1.07602));',
      '  color *= exposure / 0.6; color = inM * color; color = tfyRRT(color); color = outM * color; return clamp(color, 0.0, 1.0); }',
      'vec3 tfySRGB(vec3 c){ return mix(pow(c, vec3(0.41666)) * 1.055 - vec3(0.055), c * 12.92, vec3(lessThanEqual(c, vec3(0.0031308)))); }',
      // Colour grading like the Unity PostFX (bright cartoon look): saturation and contrast after tonemapping.
      'void main(){ vec3 c = tfyAces(texture2D(tDiffuse, vUv).rgb);',
      '  float l = dot(c, vec3(0.2126, 0.7152, 0.0722)); c = clamp(mix(vec3(l), c, saturation), 0.0, 1.0);',
      '  c = tfySRGB(c); c = clamp((c - 0.5) * contrast + 0.5, 0.0, 1.0); gl_FragColor = vec4(c, 1.0); }',
    ].join('\n'),
  };

  function createBloom(renderer, scene, camera) {
    if (!T.EffectComposer || !T.UnrealBloomPass) return null; // CDN scripts missing: plain rendering still works
    const size = renderer.getSize(new T.Vector2());
    const target = new T.WebGLRenderTarget(size.x, size.y, { type: T.HalfFloatType, format: T.RGBAFormat, minFilter: T.LinearFilter, magFilter: T.LinearFilter });
    const composer = new T.EffectComposer(renderer, target);
    composer.addPass(new T.RenderPass(scene, camera));
    const bloom = new T.UnrealBloomPass(new T.Vector2(size.x, size.y), 0.75, 0.55, 0.82);
    composer.addPass(bloom);
    const out = new T.ShaderPass(OutputShader); out.material.toneMapped = false; composer.addPass(out);
    return {
      composer, bloom, out,
      setSize(w, h, pixelRatio) { composer.setPixelRatio(pixelRatio); composer.setSize(w, h); },
      /** strength: 0 = off. Night scenes glow a little more (lamps, windows). */
      configure(strength, threshold, exposure) { bloom.strength = strength; bloom.threshold = threshold; out.uniforms.exposure.value = exposure; },
      render(dt) { composer.render(dt); },
    };
  }

  // ------------------------------------------------------------------ particles
  const VERT = [
    'attribute float size; attribute float alpha; attribute vec3 color;',
    'varying float vAlpha; varying vec3 vColor; uniform float scale;',
    'void main(){ vAlpha = alpha; vColor = color; vec4 mv = modelViewMatrix * vec4(position, 1.0);',
    '  gl_PointSize = size * scale / max(0.1, -mv.z); gl_Position = projectionMatrix * mv; }',
  ].join('\n');
  const FRAG_GLOW = [
    'varying float vAlpha; varying vec3 vColor;',
    'void main(){ vec2 p = gl_PointCoord - 0.5; float d = length(p); if (d > 0.5) discard;',
    '  float core = smoothstep(0.5, 0.0, d); float star = max(0.0, 1.0 - abs(p.x * p.y) * 60.0) * smoothstep(0.5, 0.1, d);',
    '  gl_FragColor = vec4(vColor * (core * core + star * 0.6) * 2.2, vAlpha * core); }', // HDR (>1) so it blooms
  ].join('\n');
  const FRAG_SOFT = [
    'varying float vAlpha; varying vec3 vColor;',
    'void main(){ float d = length(gl_PointCoord - 0.5); if (d > 0.5) discard;',
    '  gl_FragColor = vec4(vColor, vAlpha * smoothstep(0.5, 0.25, d)); }',
  ].join('\n');

  class Particles {
    constructor(scene, max, additive) {
      this.max = max; this.count = 0;
      this.pos = new Float32Array(max * 3); this.col = new Float32Array(max * 3); this.size = new Float32Array(max); this.alpha = new Float32Array(max);
      this.vel = new Float32Array(max * 3); this.life = new Float32Array(max); this.maxLife = new Float32Array(max); this.size0 = new Float32Array(max);
      this.grav = new Float32Array(max); this.drag = new Float32Array(max); this.sway = new Float32Array(max); this.fade = new Uint8Array(max);
      const g = this.geo = new T.BufferGeometry();
      g.setAttribute('position', new T.BufferAttribute(this.pos, 3).setUsage(T.DynamicDrawUsage));
      g.setAttribute('color', new T.BufferAttribute(this.col, 3).setUsage(T.DynamicDrawUsage));
      g.setAttribute('size', new T.BufferAttribute(this.size, 1).setUsage(T.DynamicDrawUsage));
      g.setAttribute('alpha', new T.BufferAttribute(this.alpha, 1).setUsage(T.DynamicDrawUsage));
      this.mat = new T.ShaderMaterial({
        uniforms: { scale: { value: 600 } }, vertexShader: VERT, fragmentShader: additive ? FRAG_GLOW : FRAG_SOFT,
        transparent: true, depthWrite: false, blending: additive ? T.AdditiveBlending : T.NormalBlending, vertexColors: false,
      });
      this.points = new T.Points(g, this.mat); this.points.frustumCulled = false; this.points.renderOrder = additive ? 20 : 10;
      scene.add(this.points);
    }
    setViewport(heightPx, fovDeg) { this.mat.uniforms.scale.value = heightPx / (2 * Math.tan((fovDeg * Math.PI) / 360)); }
    /** o: { count, color|colors, speed, up, spread, life, size, gravity, drag, sway, dir, jitter } */
    emit(pos, o) {
      const cols = (o.colors || [o.color || '#ffffff']).map((c) => new T.Color(c).convertSRGBToLinear());
      for (let n = 0; n < o.count; n++) {
        if (this.count >= this.max) return;
        const i = this.count++, i3 = i * 3;
        const j = o.jitter || 0;
        this.pos[i3] = pos.x + (Math.random() - 0.5) * j; this.pos[i3 + 1] = pos.y + (Math.random() - 0.5) * j * (o.flat ? 0.2 : 1); this.pos[i3 + 2] = pos.z + (Math.random() - 0.5) * j;
        let vx = (Math.random() - 0.5) * 2, vy = (Math.random() - 0.5) * 2, vz = (Math.random() - 0.5) * 2;
        const len = Math.hypot(vx, vy, vz) || 1, sp = (o.speed || 3) * (0.4 + Math.random() * 0.6);
        vx = (vx / len) * sp; vy = (vy / len) * sp * (o.spread === undefined ? 1 : o.spread) + (o.up || 0); vz = (vz / len) * sp;
        if (o.dir) { vx += o.dir.x; vy += o.dir.y; vz += o.dir.z; }
        this.vel[i3] = vx; this.vel[i3 + 1] = vy; this.vel[i3 + 2] = vz;
        const c = cols[(Math.random() * cols.length) | 0]; this.col[i3] = c.r; this.col[i3 + 1] = c.g; this.col[i3 + 2] = c.b;
        const life = (o.life || 0.7) * (0.7 + Math.random() * 0.6); this.life[i] = life; this.maxLife[i] = life;
        this.size0[i] = (o.size || 0.25) * (0.6 + Math.random() * 0.8); this.size[i] = this.size0[i]; this.alpha[i] = 1;
        this.grav[i] = o.gravity === undefined ? 4 : o.gravity; this.drag[i] = o.drag === undefined ? 1.5 : o.drag; this.sway[i] = o.sway || 0; this.fade[i] = o.grow ? 2 : 1;
      }
    }
    update(dt, time) {
      for (let i = 0; i < this.count; i++) {
        this.life[i] -= dt;
        if (this.life[i] <= 0) { this.kill(i); i--; continue; }
        const i3 = i * 3, k = this.life[i] / this.maxLife[i], dr = Math.max(0, 1 - this.drag[i] * dt);
        this.vel[i3 + 1] -= this.grav[i] * dt;
        this.vel[i3] *= dr; this.vel[i3 + 1] *= dr; this.vel[i3 + 2] *= dr;
        const sw = this.sway[i] ? Math.sin(time * 2.3 + i) * this.sway[i] : 0;
        this.pos[i3] += (this.vel[i3] + sw) * dt; this.pos[i3 + 1] += this.vel[i3 + 1] * dt; this.pos[i3 + 2] += this.vel[i3 + 2] * dt;
        this.alpha[i] = Math.min(1, k * 2.5) * Math.min(1, (1 - k) * 12 + 0.2);
        this.size[i] = this.fade[i] === 2 ? this.size0[i] * (1 + (1 - k) * 1.8) : this.size0[i] * (0.4 + 0.6 * k);
      }
      this.geo.setDrawRange(0, this.count);
      for (const a of ['position', 'color', 'size', 'alpha']) this.geo.attributes[a].needsUpdate = true;
    }
    kill(i) {
      const last = --this.count; if (i === last) return;
      const i3 = i * 3, l3 = last * 3;
      for (let k = 0; k < 3; k++) { this.pos[i3 + k] = this.pos[l3 + k]; this.vel[i3 + k] = this.vel[l3 + k]; this.col[i3 + k] = this.col[l3 + k]; }
      for (const arr of [this.size, this.alpha, this.life, this.maxLife, this.size0, this.grav, this.drag, this.sway, this.fade]) arr[i] = arr[last];
    }
    clear() { this.count = 0; this.geo.setDrawRange(0, 0); }
  }

  // ------------------------------------------------------------------ effect library
  function createFx(scene) {
    const glow = new Particles(scene, 2500, true), soft = new Particles(scene, 2500, false);
    let budget = 1, weather = null, weatherAcc = 0, trailAcc = 0, auraAcc = 0, hue = 0;
    const V = (x, y, z) => new T.Vector3(x, y, z);
    const n = (c) => Math.max(1, Math.round(c * budget));

    const fx = {
      glow, soft,
      setBudget(b) { budget = b; },
      setViewport(h, fov) { glow.setViewport(h, fov); soft.setViewport(h, fov); },
      clear() { glow.clear(); soft.clear(); },
      update(dt, t) { glow.update(dt, t); soft.update(dt, t); },

      // one-shot bursts
      coin(p) { glow.emit(p, { count: n(10), colors: ['#FFE680', '#FFC72C', '#FFFFFF'], speed: 3, up: 1.5, life: 0.45, size: 0.22, gravity: 3 }); },
      fish(p) { glow.emit(p, { count: n(12), colors: ['#8FD8FF', '#FFFFFF'], speed: 2.5, up: 2, life: 0.7, size: 0.26, gravity: -1.5 }); soft.emit(p, { count: n(8), color: '#BDEBFF', speed: 1.2, up: 1.5, life: 1, size: 0.35, gravity: -1, grow: true }); },
      gem(p) { glow.emit(p, { count: n(26), colors: ['#D98CFF', '#FF8FE8', '#FFFFFF'], speed: 5, up: 1, life: 0.8, size: 0.3, gravity: 1 }); },
      star(p) {
        glow.emit(p, { count: n(40), colors: ['#FFE680', '#FFD23F', '#FFFFFF'], speed: 6, life: 1, size: 0.34, gravity: 2 });
        for (let i = 0; i < 16; i++) { const a = (i / 16) * Math.PI * 2; glow.emit(p, { count: 1, color: '#FFF3B0', speed: 0.1, dir: V(Math.cos(a) * 6, Math.sin(a) * 6, 0), life: 0.5, size: 0.4, gravity: 0, drag: 3 }); }
      },
      box(p) { fx.confetti(p, 50); glow.emit(p, { count: n(20), colors: ['#C39BD3', '#FFD966'], speed: 5, up: 2, life: 0.8, size: 0.3 }); },
      powerup(p, color) { glow.emit(p, { count: n(30), colors: [color, '#FFFFFF'], speed: 5, life: 0.8, size: 0.32, gravity: 0, drag: 2 }); },
      doubleJump(p) { for (let i = 0; i < 14; i++) { const a = (i / 14) * Math.PI * 2; glow.emit(p, { count: 1, color: '#B3E5FF', speed: 0.2, dir: V(Math.cos(a) * 3, -0.5, Math.sin(a) * 3), life: 0.45, size: 0.25, gravity: 0, drag: 2 }); } },
      longJump(p) { glow.emit(p, { count: n(18), colors: ['#FFD23F', '#FFFFFF'], speed: 2, dir: V(0, 1, 3), life: 0.6, size: 0.24, gravity: 1 }); },
      land(p, color) { soft.emit(p, { count: n(10), color: color || '#E0D6C2', speed: 2, spread: 0.2, up: 0.6, life: 0.55, size: 0.45, gravity: 0.5, drag: 3, grow: true, jitter: 0.6, flat: true }); },
      slide(p, color) { soft.emit(p, { count: n(2), color: color || '#E0D6C2', speed: 1, up: 0.5, dir: V(0, 0, 3), life: 0.5, size: 0.4, gravity: 0, drag: 2, grow: true, jitter: 0.5 }); },
      bump(p) { glow.emit(p, { count: n(16), colors: ['#FFF5B0', '#FFFFFF'], speed: 4, up: 2, life: 0.6, size: 0.3 }); soft.emit(p, { count: n(10), color: '#D8CFC0', speed: 2, up: 1, life: 0.7, size: 0.5, grow: true }); },
      dust(p, color) { soft.emit(p, { count: n(18), color: color || '#C9BFAE', speed: 1.5, up: -0.5, life: 1.1, size: 0.55, gravity: 1.5, grow: true, jitter: 1.4 }); },
      impact(p, color) { soft.emit(p, { count: n(24), color: color || '#C9BFAE', speed: 3.5, spread: 0.35, up: 1.2, life: 0.9, size: 0.6, gravity: 2, drag: 2, grow: true, jitter: 0.8 }); },
      confetti(p, count) { soft.emit(p, { count: n(count || 60), colors: ['#FF5DA2', '#FFD23F', '#2EC4B6', '#3A86FF', '#FF9F1C', '#9B5DE5'], speed: 7, up: 5, life: 2.2, size: 0.28, gravity: 6, drag: 1.2, sway: 1.2 }); },
      firework(p) {
        const c = ['#FF5DA2', '#FFD23F', '#7FD8FF', '#9BFFB0'][(Math.random() * 4) | 0];
        glow.emit(p, { count: n(60), colors: [c, '#FFFFFF'], speed: 8, life: 1.2, size: 0.3, gravity: 3, drag: 1.2 });
      },
      checkpoint(p) { fx.confetti(p, 50); glow.emit(p, { count: n(30), colors: ['#7CFFB2', '#FFFFFF'], speed: 6, up: 3, life: 1, size: 0.3 }); },
      menuSparkle(p) { glow.emit(p, { count: 1, colors: ['#FFE680', '#FFB3E6', '#B3E5FF'], speed: 0.5, up: 0.6, life: 1.4, size: 0.18, gravity: -0.3, jitter: 1.6 }); },

      /** Continuous effects around the cat (called every frame while running). */
      aura(dt, p, s) {
        auraAcc += dt;
        if (auraAcc < 0.03) return; const steps = Math.floor(auraAcc / 0.03); auraAcc -= steps * 0.03;
        for (let k = 0; k < steps; k++) {
          if (s.boost) { // speed lines rushing past
            const x = p.x + (Math.random() - 0.5) * 7, y = 0.3 + Math.random() * 3;
            glow.emit(V(x, y, p.z - 6), { count: 1, color: '#FFE4A0', speed: 0, dir: V(0, 0, 32), life: 0.35, size: 0.12, gravity: 0, drag: 0 });
            if (Math.random() < 0.5) glow.emit(V(p.x, p.y + 0.6, p.z + 0.7), { count: 1, colors: ['#FFB703', '#FFE680'], speed: 0.5, dir: V(0, 0.5, 4), life: 0.4, size: 0.3, gravity: 0 });
          }
          if (s.magnet && Math.random() < 0.6) { const a = Math.random() * Math.PI * 2; glow.emit(V(p.x + Math.cos(a) * 1.6, p.y + 0.3, p.z + Math.sin(a) * 1.6), { count: 1, color: '#FF7A93', speed: 0.2, dir: V(-Math.cos(a) * 3, 1, -Math.sin(a) * 3), life: 0.5, size: 0.18, gravity: 0 }); }
          if (s.superJump && Math.random() < 0.4) glow.emit(V(p.x, p.y + 0.1, p.z), { count: 1, color: '#80ED99', speed: 0.8, up: 1.5, life: 0.5, size: 0.2, gravity: 0, jitter: 0.8 });
          if (s.doubleCoins && Math.random() < 0.3) glow.emit(V(p.x, p.y + 1.9, p.z), { count: 1, color: '#FFD60A', speed: 1, up: 0.5, life: 0.5, size: 0.18, gravity: 1 });
          if (s.shield && Math.random() < 0.25) { const a = Math.random() * Math.PI * 2, b = Math.random() * Math.PI; glow.emit(V(p.x + Math.sin(b) * Math.cos(a), p.y + 0.7 + Math.cos(b), p.z + Math.sin(b) * Math.sin(a)), { count: 1, color: '#7FE0FF', speed: 0, life: 0.4, size: 0.15, gravity: 0 }); }
        }
      },

      /** Cosmetic trail from the Cat Shop (e.g. Rainbow Trail): sparkles in the item's colours behind the cat. */
      trail(dt, p, colors) {
        if (!colors || !colors.length) return;
        trailAcc += dt; hue += dt;
        while (trailAcc > 0.025) {
          trailAcc -= 0.025;
          glow.emit(V(p.x, p.y + 0.5, p.z + 0.6), { count: 1, color: colors[Math.floor(hue * 8) % colors.length], speed: 0.4, dir: V(0, 0.2, 1), life: 0.6, size: 0.28, gravity: -0.2, jitter: 0.25 });
        }
      },

      /** World weather around the camera: petals, leaves, snow, sea sparkles, and fireflies at night. */
      setWeather(kind, night) { weather = { kind, night }; },
      weather(dt, camPos) {
        if (!weather || budget <= 0.35) return;
        const rates = { petals: 18, leaves: 22, snow: 55, sea_breeze: 14 };
        const rate = (rates[weather.kind] || 0) * budget;
        weatherAcc += dt * rate;
        while (weatherAcc >= 1) {
          weatherAcc--;
          const p = V(camPos.x + (Math.random() - 0.5) * 30, camPos.y + 4 + Math.random() * 8, camPos.z - 4 - Math.random() * 40);
          if (weather.kind === 'snow') soft.emit(p, { count: 1, color: '#FFFFFF', speed: 0.3, up: -1.2, life: 6, size: 0.14, gravity: 0, drag: 0, sway: 0.6 });
          else if (weather.kind === 'petals') soft.emit(p, { count: 1, colors: ['#FFC2DA', '#FFB3CF', '#FFFFFF'], speed: 0.4, up: -0.9, life: 7, size: 0.2, gravity: 0, drag: 0, sway: 1.1 });
          else if (weather.kind === 'leaves') soft.emit(p, { count: 1, colors: ['#FFB347', '#E76F51', '#FFD166', '#8AC926'], speed: 0.4, up: -1, life: 7, size: 0.24, gravity: 0, drag: 0, sway: 1.4 });
          else if (weather.kind === 'sea_breeze') glow.emit(V(p.x, 0.5 + Math.random() * 2, p.z), { count: 1, color: '#FFF8D6', speed: 0.2, life: 2, size: 0.1, gravity: -0.1, drag: 0 });
        }
        if (weather.night && Math.random() < dt * 6 * budget) { // fireflies
          glow.emit(V(camPos.x + (Math.random() - 0.5) * 24, 0.5 + Math.random() * 2.5, camPos.z - 5 - Math.random() * 30), { count: 1, color: '#D4FF6B', speed: 0.3, life: 3, size: 0.16, gravity: 0, drag: 0, sway: 0.5 });
        }
      },
    };
    return fx;
  }

  global.TFYFx = { createBloom, createFx };
})(window);
