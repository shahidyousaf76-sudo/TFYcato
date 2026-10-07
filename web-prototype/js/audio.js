// TFY Cat Adventure — Web Audio synthesiser (same sound design as the Unity AudioService):
// original sound effects, friendly meows with vowel formants, per-world music loops and ambience.
(function (global) {
  'use strict';
  const A = {
    ctx: null, master: null, musicBus: null, sfxBus: null, ambBus: null, noise: null, song: null, step: 0, next: 0, timer: null,
    settings: { music: true, sound: true, musicVolume: 0.8, soundVolume: 1 }, lastMeow: -10, coinPitch: 1, lastCoin: 0, ambNodes: [],

    init() {
      if (this.ctx) { if (this.ctx.state === 'suspended') this.ctx.resume(); return; }
      const AC = global.AudioContext || global.webkitAudioContext; if (!AC) return;
      const c = this.ctx = new AC();
      this.master = c.createGain(); const comp = c.createDynamicsCompressor(); this.master.connect(comp); comp.connect(c.destination);
      this.musicBus = c.createGain(); this.sfxBus = c.createGain(); this.ambBus = c.createGain();
      [this.musicBus, this.sfxBus, this.ambBus].forEach((b) => b.connect(this.master));
      const len = c.sampleRate * 2; this.noise = c.createBuffer(1, len, c.sampleRate);
      const d = this.noise.getChannelData(0); for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
      this.apply(this.settings);
    },
    apply(s) {
      this.settings = s; if (!this.ctx) return; const t = this.ctx.currentTime;
      this.musicBus.gain.setTargetAtTime(s.music ? 0.35 * s.musicVolume : 0, t, 0.1);
      this.sfxBus.gain.setTargetAtTime(s.sound ? 0.8 * s.soundVolume : 0, t, 0.05);
      this.ambBus.gain.setTargetAtTime(s.sound ? 0.25 * s.soundVolume : 0, t, 0.1);
    },
    tone(f0, f1, len, type = 'sine', vol = 0.4, when = 0, bus) {
      if (!this.ctx) return; const c = this.ctx, t = c.currentTime + when;
      const o = c.createOscillator(), g = c.createGain(); o.type = type;
      o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + len);
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + len);
      o.connect(g); g.connect(bus || this.sfxBus); o.start(t); o.stop(t + len + 0.05);
    },
    chord(freqs, step, vol = 0.25) { freqs.forEach((f, i) => this.tone(f, f * 1.001, 0.45, 'sine', vol, i * step)); },
    burst(len, freq, vol = 0.3, type = 'bandpass', when = 0) {
      if (!this.ctx) return; const c = this.ctx, t = c.currentTime + when;
      const s = c.createBufferSource(); s.buffer = this.noise; const fl = c.createBiquadFilter(); fl.type = type; fl.frequency.value = freq;
      const g = c.createGain(); g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.0001, t + len);
      s.connect(fl); fl.connect(g); g.connect(this.sfxBus); s.start(t, Math.random()); s.stop(t + len + 0.05);
    },
    meow(kind = 'happy', priority = false) {
      if (!this.ctx) return; const now = this.ctx.currentTime;
      if (!priority && now - this.lastMeow < 4) return; this.lastMeow = now;
      const base = { happy: 520, excited: 640, jump: 700, victory: 580, surprised: 760 }[kind] || 560, len = { jump: 0.22, excited: 0.4, victory: 0.75, surprised: 0.3 }[kind] || 0.55;
      const c = this.ctx, t = now, o = c.createOscillator(); o.type = 'sawtooth';
      const pitch = base * (0.95 + Math.random() * 0.1);
      o.frequency.setValueAtTime(pitch, t); o.frequency.linearRampToValueAtTime(pitch * 1.45, t + len * 0.35); o.frequency.linearRampToValueAtTime(pitch * 0.85, t + len);
      const f1 = c.createBiquadFilter(), f2 = c.createBiquadFilter(); f1.type = f2.type = 'bandpass'; f1.Q.value = 5; f2.Q.value = 7;
      f1.frequency.setValueAtTime(320, t); f1.frequency.linearRampToValueAtTime(700, t + len * 0.3); f1.frequency.linearRampToValueAtTime(380, t + len);
      f2.frequency.setValueAtTime(2200, t); f2.frequency.linearRampToValueAtTime(1200, t + len * 0.3); f2.frequency.linearRampToValueAtTime(800, t + len);
      const g = c.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.9, t + 0.04); g.gain.setValueAtTime(0.9, t + len - 0.12); g.gain.exponentialRampToValueAtTime(0.0001, t + len);
      o.connect(f1); o.connect(f2); f1.connect(g); f2.connect(g); g.connect(this.sfxBus); o.start(t); o.stop(t + len + 0.05);
    },
    sfx(id) {
      switch (id) {
        case 'jump': return this.tone(380, 760, 0.18, 'triangle', 0.35);
        case 'double': return this.tone(560, 1180, 0.2, 'triangle', 0.35);
        case 'long': return this.tone(300, 900, 0.38, 'triangle', 0.35);
        case 'land': return this.burst(0.1, 900, 0.35, 'lowpass');
        case 'slide': return this.burst(0.3, 2500, 0.2);
        case 'lane': return this.burst(0.08, 3500, 0.12);
        case 'coin': { const now = this.ctx ? this.ctx.currentTime : 0; this.coinPitch = now - this.lastCoin < 0.4 ? Math.min(1.5, this.coinPitch + 0.04) : 1; this.lastCoin = now; this.tone(1318 * this.coinPitch, 1320 * this.coinPitch, 0.08, 'square', 0.12); return this.tone(1975 * this.coinPitch, 1980 * this.coinPitch, 0.18, 'square', 0.1, 0.06); }
        case 'fish': return this.chord([784, 988, 1175], 0.05);
        case 'gem': return this.chord([1046, 1318, 1568, 2093], 0.05);
        case 'star': return this.chord([784, 1046, 1318, 1568, 2093], 0.07);
        case 'box': return this.chord([523, 659, 784, 1046], 0.09);
        case 'checkpoint': return this.chord([659, 784, 988, 1318], 0.08);
        case 'powerup': return this.tone(400, 1600, 0.45, 'square', 0.15);
        case 'shield': return this.tone(1200, 600, 0.3, 'sine', 0.35);
        case 'bump': return this.tone(260, 140, 0.3, 'sine', 0.5);
        case 'fall': return this.tone(700, 180, 0.6, 'triangle', 0.3);
        case 'warning': return this.burst(0.9, 160, 0.6, 'lowpass');
        case 'button': return this.tone(880, 1100, 0.07, 'sine', 0.3);
        case 'complete': return this.chord([523, 659, 784, 1046, 1318, 1568], 0.11, 0.28);
        case 'fail': return this.tone(520, 330, 0.5, 'triangle', 0.3);
        case 'unlock': return this.chord([659, 880, 1108, 1318], 0.1);
      }
    },
    // -------------------------------------------------------------- music: seeded loop per world
    music(worldId) {
      if (!this.ctx || (this.song && this.song.id === worldId)) return;
      const styles = {
        menu: { bpm: 96, scale: [0, 2, 4, 5, 7, 9, 11], root: 220, lead: 'triangle', seed: 11 }, park: { bpm: 104, scale: [0, 2, 4, 7, 9], root: 261.6, lead: 'triangle', seed: 21 },
        forest: { bpm: 100, scale: [0, 2, 3, 5, 7, 8, 10], root: 220, lead: 'sine', seed: 31 }, city: { bpm: 124, scale: [0, 2, 4, 5, 7, 9, 11], root: 246.9, lead: 'square', seed: 41 },
        mountains: { bpm: 112, scale: [0, 2, 4, 7, 9], root: 196, lead: 'triangle', seed: 51 }, beach: { bpm: 98, scale: [0, 4, 5, 7, 11], root: 293.7, lead: 'sine', seed: 61 },
        snow: { bpm: 92, scale: [0, 2, 4, 7, 9, 11], root: 329.6, lead: 'triangle', seed: 71 }, // twinkly winter waltz-ish
      };
      const st = styles[worldId] || styles.menu; const rng = new global.TFYRules.Mulberry32(st.seed);
      const mel = []; let deg = 2; for (let i = 0; i < 32; i++) { if (rng.next() < 0.75) { deg = Math.max(-2, Math.min(9, deg + rng.range(-2, 3))); mel.push(deg); } else mel.push(null); }
      this.song = { id: worldId, ...st, mel }; this.step = 0; this.next = this.ctx.currentTime + 0.1;
      if (!this.timer) this.timer = setInterval(() => this.tick(), 60);
    },
    note(deg, oct) { const s = this.song.scale, n = s.length, o = Math.floor(deg / n), i = ((deg % n) + n) % n; return this.song.root * Math.pow(2, (s[i] + 12 * (o + oct)) / 12); },
    tick() {
      if (!this.ctx || !this.song) return; const spb = 60 / this.song.bpm / 2;
      while (this.next < this.ctx.currentTime + 0.25) {
        const i = this.step % 32, when = this.next - this.ctx.currentTime, prog = [0, 5, 3, 4][Math.floor(i / 8)];
        if (i % 2 === 0) this.tone(this.note(prog, -2), this.note(prog, -2), spb * 1.8, 'sine', 0.35, when, this.musicBus);
        if (i % 4 === 0) [0, 2, 4].forEach((k) => this.tone(this.note(prog + k, 0), this.note(prog + k, 0), spb * 3.6, 'sine', 0.08, when, this.musicBus));
        if (this.song.mel[i] !== null) this.tone(this.note(this.song.mel[i] + (prog % 2), 1), this.note(this.song.mel[i] + (prog % 2), 1), spb * 1.4, this.song.lead, 0.12, when, this.musicBus);
        this.step++; this.next += spb;
      }
    },
    ambience(ids) {
      if (!this.ctx) return; this.ambNodes.forEach((n) => { try { n.stop(); } catch (e) { /* already stopped */ } }); this.ambNodes = [];
      for (const id of ids || []) {
        const cfg = { amb_wind: [400, 0.15], amb_forest: [700, 0.1], amb_stream: [2200, 0.6], amb_waterfall: [2200, 0.6], amb_waves: [900, 0.12], amb_traffic: [250, 0.25] }[id];
        if (!cfg) continue;
        const c = this.ctx, s = c.createBufferSource(); s.buffer = this.noise; s.loop = true;
        const f = c.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = cfg[0];
        const g = c.createGain(); g.gain.value = 0.3; const lfo = c.createOscillator(); lfo.frequency.value = cfg[1]; const lg = c.createGain(); lg.gain.value = 0.15; lfo.connect(lg); lg.connect(g.gain);
        s.connect(f); f.connect(g); g.connect(this.ambBus); s.start(); lfo.start(); this.ambNodes.push(s, lfo);
      }
    },
    pause(p) { if (!this.ctx) return; if (p) this.ctx.suspend(); else this.ctx.resume(); },
  };
  global.TFYAudio = A;
})(window);
