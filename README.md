# Golf Simulator

A first-person golf simulator built in **Unity 6** where you swing your iPhone like a real golf club using the **TouchOSC** app. Walk the course, step into the red zone, snap into position, and take your shot — all driven by your phone's physical accelerometer.

---

## Features

- **Phone-as-Club Controller** — Hold your iPhone like a golf grip and physically swing it. The accelerometer detects swing speed and maps it to ball power. No button tapping — real motion.
- **Live Club Mirroring** — The 3D golf club on screen mirrors your phone's exact orientation in real-time via OSC over WiFi.
- **Par 5 Real Court** — Procedurally generated par 5 terrain with fairway, rough, green (Prizm), sand bunkers, animated water hazards, and tee markers — all built with Unity's Terrain API.
- **Proximity Interaction System** — A red ring appears around the ball. Walk inside it to see a prompt, press **B** to snap into the perfect swing stance.
- **Realistic Ball Physics** — Golf ball with correct mass (46g), gravity, air drag, and bounce tuned to course scale.
- **Phone UI Menu** — An in-game phone slides up from the bottom of the screen with Practice and Real Court mode selection.
- **First-Person View** — Full first-person controller with Cinemachine camera. Club is always visible in hand, parented to the render camera for zero lag.

---

## Tech Stack

| Area | Technology |
|---|---|
| Engine | Unity 6 (6000.3.5f1) |
| Render Pipeline | Universal Render Pipeline (URP) |
| Camera | Cinemachine |
| Input | Unity New Input System |
| Phone Controller | TouchOSC MK1 (iOS) via OSC/UDP over WiFi |
| Player | StarterAssets First Person Controller |
| Terrain | Unity Terrain API (procedural, Editor tool) |
| UI | TextMeshPro + Screen Space Canvas |

---

## Controls

| Input | Action |
|---|---|
| `WASD` | Move |
| `Mouse` | Look |
| `I` | Toggle phone UI overlay |
| `B` | Snap to swing position (when inside red ring) |
| **iPhone swing** | Hit the golf ball |

---

## TouchOSC Setup (iPhone)

The swing controller uses **TouchOSC MK1** (App Store, ~$5) — no gyroscope required, accelerometer only.

1. Install **TouchOSC MK1** from the App Store
2. Go to **Settings ⚙ → Accelerometer** → turn **ON**
3. Go to **Connections → OSC**:
   - **Host:** your PC's local WiFi IP (`ipconfig` in Command Prompt → IPv4 Address)
   - **Port (outgoing):** `9000`
4. Tap any layout to enter play mode — accelerometer starts streaming
5. Hold the phone like a club handle and swing downward — the ball launches!

> **Firewall:** If no data arrives, open Windows Defender Firewall → New Inbound Rule → UDP → Port 9000 → Allow.

---

## Project Structure

```
Assets/
├── Scenes/
│   ├── SampleScene.unity           ← Intro phone menu
│   ├── PracticeCourt.unity         ← Practice mode
│   └── RealCourtWithStick.unity    ← Main Par 5 course with player + club
│
├── Scripts/
│   ├── GolfSwingController.cs       ← Club animation + ball physics + hit detection
│   ├── TouchOscSwingController.cs   ← Phone accelerometer → swing state machine
│   ├── OscReceiver.cs               ← UDP OSC listener (no packages needed)
│   ├── GolfBallInteraction.cs       ← Red ring, proximity prompt, B-snap system
│   ├── PhoneAnimator.cs             ← Phone slide-up/down animation
│   ├── PhoneMenuController.cs       ← Practice / Real Court button logic
│   ├── PhoneUISetup.cs              ← Auto-layout for phone UI
│   ├── CourtManager.cs              ← Scene navigation
│   └── WaterFlow.cs                 ← Animated water texture scrolling
│
└── Editor/
    └── Par5TerrainBuilder.cs        ← Golf → Build Par 5 Terrain menu tool
```

---

## How a Shot Works

```
Walk near ball
  └─ Red ring appears on ground around ball
       └─ Enter ring → "Press B to get into position to swing"
            └─ B pressed → snap behind ball, facing target
                 └─ Swing iPhone
                      └─ TouchOSC sends /accxyz over WiFi
                           └─ Spike > 2.0g detected
                                └─ Ball launches with physics arc
                                     └─ Ball rolls to stop → respawns at landing spot
```

---

## Built With

- [Unity 6](https://unity.com/)
- [TouchOSC MK1](https://hexler.net/touchosc) by Hexler
- [Cinemachine](https://unity.com/unity/features/editor/art-and-design/cinemachine)
- [Unity Starter Assets — First Person](https://assetstore.unity.com/packages/essentials/starter-assets-firstperson-updates-in-new-charactercontroller-196525)

---

*Built as a class project — NYU, Spring 2026*
