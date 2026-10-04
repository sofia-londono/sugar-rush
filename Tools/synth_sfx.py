"""
Synthesises the procedural sound effects for Sugar Rush (engine loop, drift squeal, boost,
countdown beeps, lap chime, finish fanfare) as small mono 16-bit WAV files.
Run: python Tools/synth_sfx.py  ->  writes into Assets/_SugarRush/Audio/SFX/
These sounds are generated from scratch, so they are free of any third-party rights (CC0).
"""
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt
import os

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_SugarRush", "Audio", "SFX")
rng = np.random.default_rng(7)

def t(sec): return np.arange(int(SR * sec)) / SR

def save(name, x, peak=0.85):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))

def bandpass(x, lo, hi, order=4):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)

def lowpass(x, f, order=4):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)

def env(n, attack, release):
    e = np.ones(n)
    a, r = int(attack * SR), int(release * SR)
    if a: e[:a] = np.linspace(0, 1, a)
    if r: e[-r:] *= np.linspace(1, 0, r) ** 2
    return e

def loop_crossfade(x, fade):
    """Make a seamless loop by crossfading the tail into the head."""
    f = int(fade * SR)
    head, tail = x[:f], x[-f:]
    w = np.linspace(0, 1, f)
    x = x[:-f].copy()
    x[:f] = head * w + tail * (1 - w)
    return x

# Engine: buzzy little 2-stroke kart. Exactly 1 s with integer-cycle partials so the loop is
# seamless; the game changes pitch with speed (one loop, one AudioSource).
tt = t(1.0)
f0 = 92
engine = np.zeros_like(tt)
for k in range(1, 24):
    engine += np.sin(2 * np.pi * f0 * k * tt + rng.uniform(0, 2 * np.pi)) * (1.0 / k) * (1.25 if k % 2 else 0.75)
engine *= 1 + 0.35 * np.sin(2 * np.pi * 46 * tt)          # firing pulses
noise = np.resize(lowpass(rng.normal(size=SR // 2), 1800), SR)
engine += noise * 0.25
engine = np.tanh(engine * 1.4)
engine = lowpass(np.concatenate([engine, engine]), 5000)[SR:]  # filter without a seam
save("engine_loop", engine, 0.8)

# Drift: tyre squeal, a wavering band of noise plus a whistling tone; crossfaded loop.
tt = t(1.0)
squeal = bandpass(rng.normal(size=len(tt)), 1500, 3200) * 0.6
squeal += np.sin(2 * np.pi * (1250 + 60 * np.sin(2 * np.pi * 7 * tt)) * tt) * 0.35
squeal *= 1 + 0.25 * np.sin(2 * np.pi * 11 * tt)
save("drift_loop", loop_crossfade(squeal, 0.15), 0.6)

# Boost: rising whoosh (noise sweep) with a sparkly upward chirp.
tt = t(0.9)
n = len(tt)
whoosh = np.zeros(n)
src = rng.normal(size=n)
for i, (lo, hi) in enumerate(np.linspace([300, 900], [2500, 6000], 9)):
    s, e = i * n // 9, (i + 1) * n // 9
    whoosh[s:e] = bandpass(src, lo, hi)[s:e]
chirp = np.sin(2 * np.pi * np.cumsum(np.linspace(400, 1400, n)) / SR) * 0.4
boost = (lowpass(whoosh, 7000) * 0.8 + chirp) * env(n, 0.05, 0.45)
save("boost", boost, 0.8)

def bell(freq, dur, decay=4.0):
    tt = t(dur)
    x = np.sin(2 * np.pi * freq * tt) + 0.35 * np.sin(2 * np.pi * freq * 2.76 * tt) + 0.15 * np.sin(2 * np.pi * freq * 5.4 * tt)
    return x * np.exp(-decay * tt) * env(len(tt), 0.004, 0.05)

def beep(freq, dur):
    tt = t(dur)
    x = np.sign(np.sin(2 * np.pi * freq * tt)) * 0.3 + np.sin(2 * np.pi * freq * tt) * 0.7
    return lowpass(x, 4000) * env(len(tt), 0.005, dur * 0.4)

save("countdown_beep", beep(660, 0.22), 0.7)
save("countdown_go", beep(1320, 0.6) + beep(1760, 0.6) * 0.6, 0.75)

# Lap chime: sweet three-note bell arpeggio.
lap = np.zeros(int(SR * 0.9))
for i, f in enumerate([1047, 1319, 1568]):
    b = bell(f, 0.6)
    s = int(i * 0.09 * SR)
    lap[s:s + len(b)] += b
save("lap_chime", lap, 0.75)

# Finish fanfare: candy arpeggio up to a held major chord.
fan = np.zeros(int(SR * 2.0))
for i, f in enumerate([523, 659, 784, 1047]):
    b = beep(f, 0.18)
    s = int(i * 0.13 * SR)
    fan[s:s + len(b)] += b
chord_start = int(0.55 * SR)
for f in [523, 659, 784, 1047]:
    b = bell(f, 1.4, 2.2)
    fan[chord_start:chord_start + len(b)] += b * 0.6
save("finish_fanfare", fan, 0.8)
print("ok", sorted(os.listdir(OUT)))
