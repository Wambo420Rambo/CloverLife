# 🌿 Clover Quality of Life (CloverLife)

A **BepInEx plugin** that enhances the quality of life and gameplay experience for **CloverPit** by automating repetitive tasks, skipping slow animations, and streamlining various in-game systems.

---

## 📦 Overview

**CloverLife** is a plugin designed to speed up and simplify several gameplay phases in *CloverPit*, removing unnecessary delays and ensuring smoother gameplay.  
It automatically skips intro scenes, accelerates cutscenes, and handles missing powerups like skeleton parts automatically.

Additionally, it integrates Harmony patches to override default game methods for faster animations and interactions.

---

## ⚙️ Features

### 💀 Auto Corpse Completion
Automatically adds missing skeleton parts into drawers if not owned or equipped.

### 🃏 Faster Card Pack Dealing
Harmony patches make the card pack opening process **instant**:
- Skips “Pack Punch” animations.
- Auto-flips all cards without user input.
- Automatically handles dialogue decisions.
- Instantly awards cards and updates the collection.
- Removes redundant animations and camera transitions.

### 🎬 Cutscene Acceleration
- Detects and speeds up cutscenes and gambling phases.
- Dynamically adjusts in-game transition speeds for a smoother, faster flow.

---

## 🧩 Installation

### Requirements
- [BepInEx 5+](https://github.com/BepInEx/BepInEx)
- Game: *CloverPit*

### Steps
1. Download or compile the `CloverLife.dll` file.
2. Place it into your **BepInEx/plugins/** folder:
   ```
   <GameDirectory>/BepInEx/plugins/CloverLife.dll
   ```
3. Launch the game.  
   You should see `[INFO] CloverLife` logs in the BepInEx console confirming successful load.

---

## 🧱 Code Structure

```
CloverLife/
├── StarterPlugin.cs        # Main BepInEx plugin class
│   ├── Awake()             # Initializes Harmony patches
│   ├── Update()            # Core QoL logic and speed handling
│   ├── SetCorpse()         # Automatically manages skeleton parts
│   └── skipIntro()         # Skips intro scenes
└── Patches (nested class)
    ├── Animator_PackPunchPrefix()    # Skips animations
    ├── DealCoroutinePrefix()         # Instant pack dealing logic
    └── UpdatePrefix()                # Auto card flipping and foiling
```

---

## 🧾 License

This project is distributed under the **MIT License** — feel free to modify and share.

---

## ❤️ Credits

- **Author:** Wambo420Rambo  
- **Powered by:** [BepInEx](https://github.com/BepInEx/BepInEx) and [Harmony](https://github.com/pardeike/Harmony)  
- **Game:** *CloverPit*
