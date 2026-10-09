"""
Synthesises the ambience loops for the forest tracks (birds, wind, bubbling chocolate) as
small mono 16-bit WAV files that loop seamlessly.
Run: python Tools/synth_ambience.py  ->  writes into Assets/_SugarRush/Audio/Ambience/
Generated from scratch, so free of any third-party rights (CC0).
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_SugarRush", "Audio", "Ambience")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)


def t(sec):
    return np.arange(int(SR * sec)) / SR


def save(name, x, peak=0.8):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))


def bandpass(x, lo, hi, order=3):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def lowpass(x, f, order=3):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def loop_crossfade(x, fade):
    """Seamless loop: the tail fades into the head."""
    f = int(fade * SR)
    w = np.linspace(0, 1, f)
    y = x[:-f].copy()
    y[:f] = x[:f] * w + x[-f:] * (1 - w)
    return y


def place(buf, sound, start):
    """Adds a sound at a sample offset, wrapping around the end (so loops stay seamless)."""
    n = len(buf)
    for i in range(len(sound)):
        buf[(start + i) % n] += sound[i]


# Birds: a soft leafy hiss with little candy birds chirping now and then (quick pitch sweeps
# with a couple of harmonics), each one its own pitch and rhythm.
dur = 12.0
birds = bandpass(rng.normal(size=int(SR * dur)), 2500, 7000) * 0.02
for _ in range(22):
    base = rng.uniform(2200, 4200)
    start = int(rng.uniform(0, dur) * SR)
    notes = rng.integers(2, 6)
    for k in range(notes):
        length = rng.uniform(0.05, 0.12)
        tt = t(length)
        sweep = base * (1 + rng.uniform(-0.25, 0.35) * tt / length)
        phase = 2 * np.pi * np.cumsum(sweep) / SR
        chirp = np.sin(phase) + 0.3 * np.sin(2 * phase) + 0.12 * np.sin(3 * phase)
        chirp *= np.sin(np.pi * tt / length) ** 2 * rng.uniform(0.25, 0.6)
        place(birds, chirp, start + int(k * rng.uniform(0.08, 0.16) * SR))
save("forest_birds", birds, 0.6)

# Wind: low rushing noise that swells and fades slowly (two slow LFOs), seamless loop.
dur = 10.0
tt = t(dur + 1.0)
noise = rng.normal(size=len(tt))
gust = 0.55 + 0.3 * np.sin(2 * np.pi * tt / 5.5) + 0.15 * np.sin(2 * np.pi * tt / 2.2 + 1.0)
wind = lowpass(noise, 700) * gust + bandpass(noise, 600, 1600) * 0.25 * gust ** 2
save("forest_wind", loop_crossfade(wind, 1.0), 0.6)

# Bubbling chocolate: a thick low gurgle with plops (short rising tones that pop).
dur = 8.0
bubbles = lowpass(rng.normal(size=int(SR * dur)), 260) * 0.25
for _ in range(70):
    length = rng.uniform(0.04, 0.12)
    tt = t(length)
    f0 = rng.uniform(140, 380)
    freq = f0 * (1 + 1.8 * tt / length)
    plop = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-tt * rng.uniform(25, 50)) * rng.uniform(0.3, 0.9)
    place(bubbles, plop, int(rng.uniform(0, dur) * SR))
save("choco_bubbles", bubbles, 0.6)
print("ambience written to", os.path.abspath(OUT))
