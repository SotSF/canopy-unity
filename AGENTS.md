# AGENTS.md

This file provides guidance to Agentic AIs when working with code in this repository.

## Project Overview

Canopy-unity is a node-editor based visualization and texture synthesis software built in Unity, primarily designed for [The Canopy](https://se.cretfi.re/canopy/). The system provides real-time visual effects generation through a graph-based node system combining C# logic with HLSL compute shaders.

**Unity Version**: 6000.0.21f1 (Unity 6)

## Development Setup

### Initial Setup
1. Install Unity Hub from [unity.com/download](https://unity.com/download)
2. Clone this repository
3. Add the project to Unity Hub via the `Projects` tab
4. Install Unity Editor version 6000.0.21f1 through the Hub
5. Launch the project (first launch takes time for build cache)

### Running the Project
- Click the triangular `Play` button in the Unity Editor's top center
- The main `Game` window becomes an interactive canvas with a menu bar
- Load existing node graphs via `File` => `Load canvas`
- Example starting point: `Assets/TextureSynthesis/Resources/CanvasSaves/MIDIMixAssignedFluidMinis.asset`
- If textures appear broken on startup, use `File` => `Save Canvas` to fix

### IDE Configuration
- **Mac**: VSCode recommended
- **Windows**: Visual Studio recommended
- Configure in Unity Editor: `Edit` => `Preferences` => `External Tools`

## Architecture

### Node System
The core architecture uses a node-graph system where:
- **Nodes** are defined in C# (extending `TextureSynthNode` or `TickingNode`)
- **Compute Shaders** (HLSL) handle GPU-accelerated texture manipulations
- **Node Types**:
  - `TextureSynthNode`: Base class for all nodes, calculated on-demand when inputs change
  - `TickingNode`: Nodes that update every frame
  - Node categories: Pattern, Filter, Signal, Audio, Output, MIDI, etc.

### Key Directories

**Scripts Structure**:
- `Assets/Scripts/TextureSynthesis/Nodes/` - All node implementations organized by category:
  - `Pattern/` - Pattern generators (FluidSim, GameOfLife, Fractals, etc.)
  - `Filter/` - Texture filters (HSV, Kaleidoscope, Crop, etc.)
  - `Signal/` - Signal processing nodes (MathExpr, BeatDetector, etc.)
  - `Audio/` - Audio analysis nodes (AudioSpectrum, BandAvg)
  - `Outputs/` - Output nodes (FullscreenTexture, Spout, Sector)
  - `MIDI/` - MIDI controller integration
  - `Conjurer/` - Conjurer system integration
- `Assets/Scripts/TextureSynthesis/Components/` - UI and management components
- `Assets/Scripts/TextureSynthesis/Resources/NodeShaders/` - HLSL compute shaders
- `Assets/Scripts/Editor/` - Unity Editor extensions (NodeWizard, custom inspectors)
- `Assets/Scripts/Utils/` - Utility functions and extensions
- `Assets/Scripts/Audio/` - Audio processing (WASAPI integration)
- `Assets/Scripts/Conjurer/` - WebSocket-based Conjurer API integration

**Resources**:
- `Assets/TextureSynthesis/Resources/CanvasSaves/` - Saved node graph configurations (.asset files)
- `Assets/Scripts/TextureSynthesis/Resources/` - Runtime-loadable resources (shaders, prefabs, VFX graphs)

### Node Implementation Pattern

Example from `HSVNode.cs`:
1. Extend `TextureSynthNode` (or `TickingNode` for frame updates)
2. Define input/output knobs using `[ValueConnectionKnob]` attributes
3. Implement `DoInit()` to load compute shaders and initialize resources
4. Implement `NodeGUI()` for the node's UI
5. Implement `DoCalc()` for the node's computation logic
6. Load compute shader from `Resources/NodeShaders/`
7. Dispatch shader with proper thread groups

### Key Dependencies

**Unity Packages**:
- Universal Render Pipeline (URP) 17.0.3
- Visual Effect Graph 17.0.3
- Input System, Timeline, Post-processing
- Barracuda (ML inference)

**Third-party (Keijiro packages)**:
- `jp.keijiro.lasp` - Audio analysis (LASP)
- `jp.keijiro.laspvfx` - LASP + VFX integration
- `jp.keijiro.minis` - MIDI control
- `jp.keijiro.klak.spout` - Spout video sharing (Windows)

## Creating New Nodes

### Using NodeWizard
Project has a custom NodeWizard tool (`Assets/Scripts/Editor/NodeWizard.cs`) to scaffold new nodes:
- Automatically generates node C# class and optional compute shader
- Configurable templates: SignalGenerator, SignalFilter, TextureGenerator, TextureFilter
- Reduces boilerplate for input/output knobs
However, it is out of date with latest Node changes and possibly deprecated.

### Manual Node Creation
For a simple node example, see `Assets/Scripts/TextureSynthesis/Nodes/Filter/HSVNode.cs`:
- Demonstrates texture input/output pattern
- Shows compute shader integration
- Includes signal input knobs with UI controls

## Key Components

- **NodeUIController** (`Assets/Scripts/TextureSynthesis/Components/UI/NodeUIController.cs`) - Main UI controller for the node canvas
- **TickingNodeManager** - Manages nodes that update every frame
- **ConjurerController** - WebSocket integration for external control (Conjurer system)
- **VFXManager** - Visual effects graph management
- **Spout Integration** - Real-time texture sharing (primarily for Windows)

## Testing & Building

Unity projects are typically built through the Unity Editor UI:
- **Play Mode**: Click Play button (▶️) in Editor to test
- **Build**: `File` => `Build Settings` => `Build`

For automation, Unity can be controlled via command line (see Unity documentation for batch mode).

## Audio & MIDI

- Uses LASP for real-time audio analysis and spectrum data
- MIDI integration via Minis package (Keijiro)
- Audio level tracking and beat detection available through dedicated nodes
- WASAPI support on Windows for low-latency audio capture

## WebSocket Integration

The Conjurer integration uses WebSockets (NativeWebSocket library) for:
- External control and synchronization
- Command/event-based API (`ConjurerApiModels.cs`)
- Real-time parameter updates
