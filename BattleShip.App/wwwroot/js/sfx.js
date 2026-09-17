(() => {
  const STORAGE_KEY = "battleship.sfx.muted";
  const MASTER_GAIN = 0.42;

  let ctx = null;
  let master = null;
  let muted = readMuted();

  function readMuted() {
    try {
      return localStorage.getItem(STORAGE_KEY) === "1";
    } catch {
      return false;
    }
  }

  function persistMuted() {
    try {
      localStorage.setItem(STORAGE_KEY, muted ? "1" : "0");
    } catch {
      // Private browsing can block storage.
    }
  }

  function audioContextCtor() {
    return window.AudioContext || window.webkitAudioContext;
  }

  function getCtx() {
    if (ctx) {
      return ctx;
    }

    const Ctor = audioContextCtor();
    if (!Ctor) {
      return null;
    }

    ctx = new Ctor();
    master = ctx.createGain();
    master.gain.value = muted ? 0 : MASTER_GAIN;
    master.connect(ctx.destination);
    return ctx;
  }

  async function unlock() {
    const audio = getCtx();
    if (audio && audio.state === "suspended") {
      try {
        await audio.resume();
      } catch {
        // Autoplay lock still held; the next user gesture will retry.
      }
    }
  }

  function setMuted(value) {
    muted = !!value;
    persistMuted();
    if (master) {
      master.gain.value = muted ? 0 : MASTER_GAIN;
    }
    if (!muted) {
      unlock();
    }
  }

  function isMuted() {
    return muted;
  }

  function connect(node) {
    node.connect(master);
  }

  function envelope(gain, start, attack, decay, peak) {
    const g = gain.gain;
    g.cancelScheduledValues(start);
    g.setValueAtTime(0.0001, start);
    g.exponentialRampToValueAtTime(Math.max(peak, 0.0002), start + attack);
    g.exponentialRampToValueAtTime(0.0001, start + attack + decay);
  }

  function tone({
    type = "sine",
    freq = 440,
    freqEnd = null,
    delay = 0,
    duration = 0.2,
    peak = 0.4,
    attack = 0.01,
    filterFreq = null,
  }) {
    const audio = getCtx();
    if (!audio || !master) {
      return;
    }

    const start = audio.currentTime + delay;
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, start);
    if (freqEnd !== null) {
      osc.frequency.exponentialRampToValueAtTime(Math.max(freqEnd, 20), start + duration);
    }

    if (filterFreq) {
      const filter = audio.createBiquadFilter();
      filter.type = "lowpass";
      filter.frequency.setValueAtTime(filterFreq, start);
      osc.connect(filter);
      filter.connect(gain);
    } else {
      osc.connect(gain);
    }

    envelope(gain, start, attack, duration, peak);
    connect(gain);
    osc.start(start);
    osc.stop(start + attack + duration + 0.05);
  }

  function noise({
    delay = 0,
    duration = 0.25,
    peak = 0.35,
    attack = 0.005,
    filterType = "bandpass",
    filterFreq = 800,
    filterQ = 0.8,
    filterEnd = null,
  }) {
    const audio = getCtx();
    if (!audio || !master) {
      return;
    }

    const length = Math.max(1, Math.floor(audio.sampleRate * duration));
    const buffer = audio.createBuffer(1, length, audio.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < length; i++) {
      data[i] = Math.random() * 2 - 1;
    }

    const start = audio.currentTime + delay;
    const source = audio.createBufferSource();
    source.buffer = buffer;
    const filter = audio.createBiquadFilter();
    filter.type = filterType;
    filter.frequency.setValueAtTime(filterFreq, start);
    filter.Q.value = filterQ;
    if (filterEnd !== null) {
      filter.frequency.exponentialRampToValueAtTime(Math.max(filterEnd, 40), start + duration);
    }

    const gain = audio.createGain();
    envelope(gain, start, attack, duration, peak);
    source.connect(filter);
    filter.connect(gain);
    connect(gain);
    source.start(start);
    source.stop(start + duration + 0.05);
  }

  function sonar({ freq = 880, freqEnd = 420, peak = 0.28, delay = 0 }) {
    tone({ type: "sine", freq, freqEnd, duration: 0.22, peak, delay, attack: 0.008 });
    tone({ type: "sine", freq: freq * 0.5, freqEnd: (freqEnd || freq) * 0.5, duration: 0.28, peak: peak * 0.35, delay });
  }

  function splash(incoming) {
    noise({
      duration: 0.32,
      peak: incoming ? 0.22 : 0.3,
      filterType: "highpass",
      filterFreq: incoming ? 500 : 700,
      filterEnd: 180,
      filterQ: 0.6,
    });
    tone({
      type: "sine",
      freq: incoming ? 320 : 420,
      freqEnd: 90,
      duration: 0.28,
      peak: 0.22,
      delay: 0.02,
    });
  }

  function metallicHit(incoming) {
    const peak = incoming ? 0.26 : 0.4;

    noise({
      duration: 0.035,
      peak: peak * 0.55,
      attack: 0.001,
      filterType: "bandpass",
      filterFreq: 3200,
      filterEnd: 1400,
      filterQ: 2.8,
    });

    [1870, 2790, 4180].forEach((freq, i) => {
      tone({
        type: "square",
        freq,
        freqEnd: freq * 0.68,
        duration: 0.07 + i * 0.035,
        peak: peak * (0.32 - i * 0.07),
        attack: 0.002,
        filterFreq: 4800,
        delay: 0.004,
      });
    });

    tone({
      type: "triangle",
      freq: incoming ? 940 : 1120,
      freqEnd: 360,
      duration: 0.32,
      peak: peak * 0.42,
      attack: 0.003,
      filterFreq: 2400,
      delay: 0.018,
    });
  }

  function shipExplosion(incoming) {
    const peak = incoming ? 0.52 : 0.72;

    noise({
      duration: 0.6,
      peak,
      attack: 0.002,
      filterType: "lowpass",
      filterFreq: 420,
      filterEnd: 70,
      filterQ: 0.35,
    });

    noise({
      duration: 0.38,
      peak: peak * 0.7,
      attack: 0.001,
      filterType: "bandpass",
      filterFreq: 850,
      filterEnd: 180,
      filterQ: 0.85,
      delay: 0.012,
    });

    tone({
      type: "sine",
      freq: incoming ? 46 : 58,
      freqEnd: 20,
      duration: 0.9,
      peak: peak * 0.88,
      attack: 0.003,
    });

    tone({
      type: "sawtooth",
      freq: incoming ? 170 : 230,
      freqEnd: 40,
      duration: 0.3,
      peak: peak * 0.38,
      attack: 0.002,
      filterFreq: 650,
    });

    noise({
      duration: 0.22,
      peak: peak * 0.45,
      filterType: "highpass",
      filterFreq: 380,
      filterEnd: 1100,
      filterQ: 0.55,
      delay: 0.07,
    });

    tone({
      type: "triangle",
      freq: incoming ? 95 : 120,
      freqEnd: 28,
      duration: 0.55,
      peak: peak * 0.25,
      attack: 0.004,
      delay: 0.05,
    });
  }

  function play(cue) {
    if (muted) {
      return;
    }

    const audio = getCtx();
    if (!audio) {
      return;
    }

    if (audio.state === "suspended") {
      audio.resume();
    }

    switch (cue) {
      case "sonar":
        sonar({});
        break;
      case "miss":
        splash(false);
        break;
      case "incoming-miss":
        splash(true);
        break;
      case "hit":
        metallicHit(false);
        break;
      case "incoming-hit":
        metallicHit(true);
        break;
      case "sunk":
        shipExplosion(false);
        break;
      case "incoming-sunk":
        shipExplosion(true);
        break;
      case "obstacle":
        noise({ duration: 0.18, peak: 0.34, filterType: "bandpass", filterFreq: 240, filterQ: 1.4 });
        tone({ type: "triangle", freq: 160, freqEnd: 70, duration: 0.16, peak: 0.28 });
        break;
      case "victory":
        [523, 659, 784, 1046].forEach((freq, i) => {
          tone({ type: "square", freq, duration: 0.22, peak: 0.14, delay: i * 0.14, filterFreq: 1800 });
          tone({ type: "sine", freq: freq / 2, duration: 0.24, peak: 0.08, delay: i * 0.14 });
        });
        break;
      case "defeat":
        noise({ duration: 0.7, peak: 0.18, filterType: "lowpass", filterFreq: 180, filterEnd: 60, filterQ: 0.5 });
        [196, 155, 130].forEach((freq, i) => {
          tone({ type: "sawtooth", freq, freqEnd: freq * 0.7, duration: 0.4, peak: 0.12, delay: i * 0.22, filterFreq: 500 });
        });
        break;
      default:
        sonar({});
        break;
    }
  }

  const unlockOnce = () => {
    unlock();
    document.removeEventListener("pointerdown", unlockOnce);
  };
  document.addEventListener("pointerdown", unlockOnce);

  window.battleSfx = { play, unlock, setMuted, isMuted };
})();
