# I Just Wanna Fighting Game

Design specification, prototype status, and development roadmap for a 3D martial-arts fighting game inspired by **Buriki One's reversed control philosophy** and **Mortal Kombat X's fixed character-variation system**.

> Status: early combat prototype. Current content is for control, camera, movement, and animation experiments—not production gameplay.

Last updated: September 3, 2026.

## Start here

Open the project in Unity 2022.3.37f1, allow scripts and assets to finish importing, open `Assets/Scenes/SampleScene.unity`, and enter Play mode.

If the generated prototype scene is missing or stale, run:

`Tools → Jin Prototype → Rebuild Sample Scene`

The single most valuable next task is to build a **fixed-frame hit/hurtbox exchange**: convert the three visual attacks into data-driven moves, give the opponent hurtboxes and health, and prove that attacks can hit, be blocked, miss, and cause deterministic reactions. This should happen before adding more characters or many more attacks.

## Document map

- [Project vision](#project-vision)
- [Design pillars](#design-pillars)
- [Current prototype](#current-prototype)
- [Important known limitations](#important-known-limitations)
- [Current project structure](#current-project-structure)
- [Target production architecture](#target-production-architecture)
- [Recommended data model](#recommended-data-model)
- [Directional attack grammar](#proposed-directional-attack-grammar)
- [Variation-system specification](#variation-system-specification)
- [What should be developed next](#what-should-be-developed-next)
- [Short-term backlog](#suggested-short-term-backlog)
- [Vertical-slice definition of done](#vertical-slice-definition-of-done)
- [Testing checklist](#testing-checklist)
- [Tuning reference](#tuning-reference)
- [Asset pipeline recommendations](#asset-pipeline-recommendations)
- [Major design decisions](#major-design-decisions-still-needed)
- [Scope control](#scope-control)

## Project vision

Create a grounded one-on-one 3D fighting game where:

- Movement is performed with two dedicated buttons.
- Directional-stick inputs perform attacks instead of directly moving the fighter.
- Fighters circle each other in 3D, with a camera that maintains a readable fighting-game view.
- Positioning, commitment, counters, throws, knockdowns, and ring position matter more than long airborne combos.
- Every fighter has three fixed variations selected before the match.
- Variations preserve the fighter's core identity while meaningfully changing their tactics.
- The game uses original characters, stages, animations, audio, names, and presentation.

The intended result is a spiritual successor, not a literal clone of Buriki One, Tekken, Mortal Kombat, or any other commercial game.

## Design pillars

### 1. Movement buttons, attack directions

The defining control idea is that buttons move the fighter while the directional control attacks. This should make attacks feel like deliberate physical commitments rather than abstract button presses.

The player should be able to understand the basic control grammar quickly:

- Movement buttons control range.
- Sidestep buttons control angle.
- Attack direction broadly communicates attack purpose.
- Holding both movement buttons produces a defensive action.

### 2. Grounded martial-arts combat

Combat should emphasize:

- Spacing and reach
- Startup and recovery commitment
- High/low defensive reads
- Counters and whiff punishment
- Clinches and throws
- Knockdowns and wake-up pressure
- Ring position and ring-outs
- Short, readable combinations

The game should avoid becoming primarily an air-juggle or memorized-string fighter.

### 3. Readable 3D positioning

Sidesteps occur relative to the opponent, not the global coordinate system. The camera rotates around the pair so forward, backward, and sideways movement remain understandable as the fight line changes.

### 4. Three meaningful variations per fighter

Each fighter should be complete before variations are applied. A variation should change the fighter's game plan, not merely change damage numbers or withhold essential tools.

Recommended content split:

- 70–80% shared core moves and rules
- 20–30% variation-specific techniques, properties, and presentation
- Three fixed variations selected before the match
- No mid-match variation switching in the primary competitive ruleset

### 5. Deterministic and testable combat

The finished combat simulation should operate at a fixed 60 frames per second with explicit frame data, input buffering, and repeatable results. Animation should present combat state; animation timing should not secretly define combat rules.

## Current prototype

### Engine

- Unity 2022.3.37f1
- Built-in render pipeline
- Legacy Unity input axes plus explicit keyboard/gamepad-key checks
- `SampleScene` is the passive-dummy test scene
- `TwoPlayerSampleScene` is the shared-keyboard local versus scene

### Current scene contents

- Textured Jin Kazama placeholder model
- Passive second Jin used as the movement/camera target and attack dummy
- Flat eight-sided ring-out platform with matching octagonal bounds
- Opponent-relative movement
- Dynamic 3D-fighter camera
- Procedural idle and locomotion poses
- Four procedural attack prototypes
- Run, backdash, and block states
- Ring-out detection and automatic reset
- On-screen prototype control/state display

### Current controls

| Action | Keyboard | Gamepad prototype |
|---|---|---|
| Move backward | `A` | `LB` |
| Move forward | `D` | `RB` |
| Run forward | Double-tap and hold `D` | Double-tap and hold `RB` |
| Backdash | Double-tap `A` | Double-tap `LB` |
| Block | Hold `A + D` | Hold `LB + RB` |
| Sidestep around opponent | `W / S` | Not assigned yet |
| Straight punch | Right Arrow | Attack stick right |
| High kick | Up Arrow | Attack stick up |
| Low kick | Down Arrow | Attack stick down |
| Stepping teep/push kick | Hold `D`, Back → Neutral → Forward arrows | Hold `RB`, attack stick Back → Neutral → Forward |

Important control rule: arrow keys are attacks, not movement. This preserves the Buriki-style division between movement buttons and attack directions.

### Two-player scene controls

Open `Assets/Scenes/TwoPlayerSampleScene.unity` for local play on one keyboard. Both fighters have the complete prototype movement and attack set:

| Action | Player 1 | Player 2 |
|---|---|---|
| Back / Forward | `A / D` | `J / L` |
| Sidestep | `W / S` | `I / K` |
| Attack Back / Forward | Left / Right Arrow | `F / H` |
| High / Low attack | Up / Down Arrow | `T / G` |
| Block | `A + D` | `J + L` |
| Run | Double-tap `D` | Double-tap `L` |
| Backdash | Double-tap `A` | Double-tap `J` |
| Stepping teep | Hold `D`, Left → Neutral → Right | Hold `L`, `F` → Neutral → `H` |

Both Jins can attack, accumulate damage, recoil, suffer the teep knockdown, and ring out. Player 1's condition monitor is on the top-left and Player 2's is on the top-right.

### Current movement behavior

- Forward and backward are always relative to the opponent.
- Sidestep follows the tangent of the current fight line, creating an arc around the opponent.
- Jin continually rotates to face the opponent.
- The camera follows the midpoint and stays perpendicular to the fight line.
- Diagonal input is normalized to prevent unintended speed increases.
- Double-tap window: 0.24 seconds by default.
- Run speed: 1.65 times normal movement speed by default.
- Backdash speed: 5.4 world units per second at its peak.
- Backdash duration: 0.32 seconds.
- Blocking has priority over normal movement and directional attacks.
- Each fighter has a `0.42`-unit ground-plane pushbox radius, maintaining `0.84` units of minimum center spacing while still allowing circular sidesteps.

### Current attack behavior

The four attacks are visual animation prototypes only:

| Direction | Attack | Temporary duration | Intended role |
|---|---|---:|---|
| Right | Straight punch | 0.42 s | Fast mid-range check |
| Up | High kick | 0.62 s | Slower, stronger high attack |
| Down | Low kick | 0.50 s | Low defensive check |
| Hold forward movement + Back → Neutral → Forward | Stepping teep | 0.72 s | Advancing mid push kick and range-control tool |

The teep command must be completed within 0.42 seconds. It demonstrates how movement-button state and an attack-stick sequence can combine into a more advanced Buriki-style technique. It steps the fighter forward during the kick and takes priority over the ordinary forward-direction punch when the sequence is recognized.

### Prototype hitboxes

Every prototype attack now has an opponent-relative box hit volume with a short active window matched to the procedural pose. Jin's `Show Hitboxes` Inspector checkbox controls the runtime visualization:

- Red translucent box: the attack is currently active and has not connected.
- Green translucent box: the active attack connected with the prototype opponent's capsule-shaped hurt volume.
- Hidden: startup, recovery, idle, or `Show Hitboxes` is disabled.

Contact is latched once per attack. This is deliberately still a diagnostic layer: hit reactions, block reactions, knockback, hit stop, and frame-data assets remain future work.

### Accumulated-damage condition monitor

The prototype opponent uses a Buriki One-inspired accumulated-damage system instead of a conventional bar that empties from full health. Successful attacks add damage toward and beyond a 100% danger reference:

| Attack | Base accumulated damage |
|---|---:|
| Straight punch | 8% |
| High kick | 16% |
| Low kick | 11% |
| Stepping teep | 18% |

The top-right monitor presents the condition as a continuously moving sinusoidal waveform, similar in spirit to the classic Resident Evil condition display. It transitions from green at little or no accumulated damage through yellow to red at 100% or more. The pulse accelerates as damage approaches the danger threshold. A ring-out reset clears accumulated damage back to 0%.

Actual damage rises with the defender's existing accumulated percentage. The multiplier scales linearly from `1.0x` at 0% to `1.75x` at 100% and remains capped at `1.75x` beyond that point. This makes later clean hits increase the percentage faster than early hits.

Accumulated damage begins recovering after 2.5 seconds without taking another hit. It then falls toward 0% at 3 percentage points per second. Any new hit immediately pauses recovery and restarts the delay, rewarding sustained pressure while still allowing a defender to recover during a long disengagement.

The high kick and stepping teep are strong finishers. Weak attacks may build the opponent to or beyond 100%, but only a confirmed strong finisher that brings the defender to 100% or more wins the match. The loser is knocked down, both controls freeze, a winner message is shown for 2.25 seconds, and then positions, damage, reactions, and the timer reset for another round.

For immediate prototype feedback, the passive second Jin flashes white, leans away from the strike, compresses slightly, and slides backward on confirmed contact. Stronger attacks produce a larger reaction. The stepping teep causes a dedicated knockdown: the dummy is pushed back, falls onto its side, remains grounded briefly, and then recovers. This is a presentation reaction rather than a final hitstun or physics system. Ring-out resets also clear any reaction in progress.

### Optional round countdown

The `Main Camera` object's `TekkenPrototypeCamera` component has a `Countdown` checkbox:

- Disabled: the match has no time limit and no timer is displayed.
- Enabled: a centered timer counts down from 90 seconds and clamps at zero.
- The final 10 seconds are displayed in red.
- At zero, the display reads `TIME UP`. Round-result logic is not implemented yet.
- A ring-out reset restarts the clock at 90 seconds when countdown mode is enabled.

Current attacks:

- Temporarily lock movement.
- Use a startup/extension/recovery envelope.
- Pose limbs procedurally.
- Create timed, opponent-relative prototype hitboxes.
- Add move-specific accumulated damage when a hitbox contacts the opponent.
- Do not cause hitstun, blockstun, pushback, or knockdown.
- Are not yet authored in simulation frames.

### Ring-out behavior

- Crossing any of the octagonal platform's four straight or four diagonal edges starts a ring out.
- The fighter drops below the platform briefly.
- A `RING OUT` message appears.
- Both fighters return to their captured starting positions and rotations.
- Movement and attack state are cleared.
- Rigidbody velocity is cleared when a fighter eventually uses a Rigidbody.

## Important known limitations

### The Jin model is a placeholder

The current import maps face, skin, fringe, and eye slots to `Ch_Jin_Face_D.png`. Costume-related slots—including the costume, hood, neck strip, belt, and shoes—use `Ch_Jin_Body_D.png`. The invalid Humanoid avatar uses world-space fallback IK to keep the hands relaxed beside the hips without depending on the FBX's unusual shoulder axes.

Jin Kazama is third-party intellectual property. The current model must be treated as a private prototype/reference asset unless the developer has explicit redistribution and commercial-use rights.

Before public distribution, replace it with an original or properly licensed character, including:

- Mesh
- Textures
- Skeleton
- Animations
- Name and likeness
- Clothing and distinctive design elements

### The FBX is not currently a valid Unity Humanoid avatar

Unity currently reports that the imported avatar is not a valid Humanoid. The prototype therefore uses the controller's bone-based fallback posing.

Consequences:

- Poses may deform awkwardly.
- Retargeted animation clips will not work reliably.
- Procedural IK has to understand this model's custom skeleton.
- Attack quality is limited.
- Animation iteration is slower and more fragile.

Recommended fix:

1. Inspect the FBX Rig import settings.
2. Repair the bone mapping or configure it as a Generic rig.
3. Confirm a stable root, hips, spine, arms, legs, hands, and feet hierarchy.
4. Test an idle, walk, punch, and kick clip.
5. Replace the temporary procedural attacks with authored clips while keeping simulation-driven timing.

### The controller is currently monolithic

`JinPrototypeController` presently owns input, movement, state, procedural animation, attacks, and debug UI. This is acceptable for rapid experimentation but should not become the production architecture.

### Combat is time-based rather than frame-authored

Movement and attacks currently use seconds and `Update()`. A competitive fighting game should move combat decisions into a fixed 60 Hz simulation and express startup, active, and recovery in frames.

### The two-player scene is keyboard-only

`SampleScene` keeps the second Jin passive for focused testing. `TwoPlayerSampleScene` enables both fighters, but device assignment and separate gamepad profiles are not implemented yet.

### There are no real combat collisions

The current hitboxes, capsule-shaped hurt volumes, and circular fighter pushboxes are procedural prototype diagnostics rather than authored move data. Throw boxes and guard-level collision rules are not implemented.

## Current project structure

```text
Assets/
├── Prototype/
│   ├── Editor/
│   │   └── JinPrototypeSetup.cs
│   ├── Materials/
│   ├── Prefabs/
│   │   └── JinPrototype.prefab
│   └── Scripts/
│       ├── JinPrototypeController.cs
│       ├── PrototypeRingOutReset.cs
│       └── TekkenPrototypeCamera.cs
├── Scenes/
│   ├── SampleScene.unity
│   └── TwoPlayerSampleScene.unity
└── jin-kazama/
    ├── source/
    └── textures/
```

### Script responsibilities

#### `JinPrototypeController`

Currently handles:

- Keyboard and gamepad prototype input
- Button-based forward/back movement
- Double-tap recognition
- Run and backdash
- Opponent-relative sidestep
- Facing correction
- Blocking
- Directional attack selection
- Procedural idle, movement, block, punch, and kick posing
- Runtime state reset
- On-screen controls and state display

#### `TekkenPrototypeCamera`

Handles:

- Midpoint tracking
- Camera-side continuity
- Fight-line-relative camera orbiting
- Dynamic framing based on fighter separation
- Smooth position and rotation adjustment

#### `PrototypeRingOutReset`

Handles:

- Platform-bound checks
- Temporary falling state
- Ring-out message
- Resetting both fighters
- Clearing controller and Rigidbody state

#### `JinPrototypeSetup`

Editor-only scene generator that:

- Creates and assigns body, face, and mouth materials
- Instantiates and scales the Jin model
- Creates the passive-dummy scene and a duplicated local two-player version
- Configures the camera and lighting
- Adds prototype components
- Saves the prefab and scene
- Adds the test scene to Build Settings

The setup can be rerun from:

`Tools → Jin Prototype → Rebuild Sample Scene`

Warning: rebuilding regenerates prototype scene content. Do not store unrelated hand-authored work inside the generated objects.

## Target production architecture

The prototype should gradually move toward the following separation:

```text
Player device
    ↓
Raw input sampler
    ↓
Facing-relative command interpreter
    ↓
Per-player input buffer
    ↓
Fixed 60 Hz combat simulation
    ├── Fighter state machine
    ├── Movement and pushboxes
    ├── Attack frame data
    ├── Hit/hurt/throw resolution
    ├── Damage, stun, meter, and rounds
    └── Ring-out rules
    ↓
Presentation layer
    ├── Animation
    ├── Camera
    ├── VFX
    ├── Audio
    └── UI
```

Recommended runtime modules:

- `FighterInputSource`
- `CommandInterpreter`
- `InputBuffer`
- `CombatClock`
- `FighterSimulation`
- `FighterStateMachine`
- `MovementMotor`
- `MoveDefinition`
- `HitboxDefinition`
- `HitResolver`
- `FighterPresentation`
- `FightCamera`
- `RoundManager`
- `RingOutRule`
- `TrainingModeController`

## Recommended data model

### `FighterDefinition`

Should contain:

- Identifier and display name
- Base health and meter rules
- Movement profile
- Pushbox dimensions
- Shared move list
- Throw definitions
- Defensive options
- Animation/presentation profile
- Three variation references

### `VariationDefinition`

Should contain:

- Identifier and display name
- Gameplay description
- Added moves
- Replaced moves
- Modified move properties
- Passive mechanic, if any
- Visual stance/equipment overrides
- Strengths and weaknesses
- AI behavior preferences

### `MoveDefinition`

Each move should eventually be a ScriptableObject or equivalent data asset containing:

- Stable move identifier
- Display name
- Input command
- Required fighter state
- Startup frames
- Active frames
- Recovery frames
- Damage
- Chip damage
- Hitstun
- Blockstun
- Hit level: high, mid, low, throw, unblockable
- Counter-hit behavior
- Pushback on hit and block
- Knockdown type
- Tracking or sidestep vulnerability
- Cancel windows
- Meter cost and gain
- Hitbox timeline
- Animation reference
- Sound and effect events
- Variation requirement

### `HitboxDefinition`

Should describe:

- Shape: sphere, capsule, or box
- Bone or fighter-space attachment
- Start and end frame
- Position and rotation
- Size
- Hit level
- Hit priority
- Damage/stun override, if needed

## Proposed combat-state priority

When multiple inputs or events occur, higher-priority states should win:

1. Ring-out/reset
2. Knockdown or throw victim
3. Hitstun
4. Blockstun
5. Active throw or attack
6. Backdash or committed evade
7. Block
8. Run
9. Walk or sidestep
10. Idle

This should eventually be explicit in a state machine instead of being implied by condition order in one controller.

## Proposed directional attack grammar

The current four attacks are only the beginning. A consistent grammar could be:

| Direction | General meaning | Typical examples |
|---|---|---|
| Toward opponent | Fast or medium linear attack | Jab, straight, front kick |
| Up-toward | Strong high-line attack | Roundhouse, overhand |
| Down-toward | Fast low-line attack | Shin kick, sweep setup |
| Up | Rising or anti-high technique | Knee, rising palm |
| Down | Low attack or evasive technique | Sweep, crouching strike |
| Away | Counter, parry, or retreating strike | Slip counter, backfist |
| Up-away | High evade/counter | Lean-back kick, high parry |
| Down-away | Low evade/counter | Low parry, retreating sweep |

Not every fighter needs the same exact move in each direction, but the tactical meaning should remain reasonably consistent.

## Variation-system specification

Each fighter should have a shared complete kit plus three fixed variations.

Example structure for a grounded striker:

### Variation A: Pressure

- Advancing strikes
- Better frame advantage
- Stronger corner/ring-edge pressure
- Weaker retreat and whiff recovery

### Variation B: Counter

- Parries and slips
- Counter-hit conversions
- Better punishment
- Lower sustained pressure

### Variation C: Clinch

- Standing grapples
- Knees and elbows
- Better close-range control
- Shorter effective range

Variation rules:

- Every variation needs offense, defense, and a response to passive play.
- No variation should contain all of the fighter's safest tools.
- Variation-exclusive moves should create decisions, not erase all weaknesses.
- Shared moves should retain identical frame data unless explicitly replaced.
- The chosen variation must be visible during selection and during the match.
- Variations should have recognizable stance, equipment, color, or effect details without becoming separate characters.

## What should be developed next

### Immediate next milestone: a real two-fighter combat exchange

Do not add a large roster or many more procedural attacks yet. The next milestone should prove that one fighter can hit, block, and affect another fighter under deterministic rules.

Recommended order:

#### 1. Stabilize the input and state code

- Separate movement input from attack-direction input.
- Introduce explicit commands such as `ForwardPressed`, `BackPressed`, `BlockHeld`, and `AttackDirection`.
- Move double-tap detection into a reusable input-buffer module.
- Define input priority and simultaneous-input behavior.
- Add configurable keyboard and gamepad bindings.
- Decide how gamepad sidestep is mapped.

Acceptance criteria:

- Input history can be displayed frame by frame.
- Double taps behave identically at different rendering frame rates.
- Opposite movement buttons always produce block when permitted.
- Attack directions never accidentally move the fighter.

#### 2. Add a fixed 60 Hz combat clock

- Sample input into frame records.
- Advance gameplay at exactly 60 simulation ticks per second.
- Convert attack durations from seconds to startup/active/recovery frame counts.
- Keep camera and animation interpolation in the presentation layer.

Acceptance criteria:

- Recorded inputs produce the same simulation result repeatedly.
- Frame stepping works in the editor.
- Rendering frame rate does not change attack timing.

#### 3. Create data-driven moves

- Implement `MoveDefinition` assets.
- Recreate the straight punch, high kick, and low kick as data.
- Add editor validation for missing or invalid frame ranges.
- Keep procedural posing temporarily, but trigger it from move state.

Acceptance criteria:

- Designers can tune startup, active, recovery, damage, and pushback without editing code.
- Multiple fighters can reference different move sets.

#### 4. Implement hurtboxes, hitboxes, and pushboxes

- Add persistent hurtboxes to head, torso, arms, and legs.
- Add frame-driven hitboxes to each attack.
- [x] Add a prototype ground pushbox so fighters cannot overlap.
- Visualize all collision shapes in training mode.
- Resolve one hit per attack unless the move explicitly supports multiple hits.

Acceptance criteria:

- Punch and kicks hit only during active frames.
- Whiffed attacks do not deal damage.
- Fighters cannot pass through one another during ordinary movement.
- Debug colors clearly distinguish hitboxes, hurtboxes, and pushboxes.

#### 5. Add defender reactions

- Health
- High/mid/low guard rules
- Hitstun
- Blockstun
- Pushback
- Counter-hit detection
- Basic knockdown
- Reset after round end or ring-out

Acceptance criteria:

- Holding block stops blockable attacks from dealing full damage.
- Low attacks interact with guard according to the chosen rules.
- The defender cannot act during hitstun or blockstun.
- Counter hits are visible in the debug UI.

#### 6. Replace prototype input profiles with device assignment

- Replace the temporary shared-keyboard profiles with Input System actions.
- Add local two-player keyboard/gamepad device assignment.
- Ensure camera, ring-out, reset, and facing logic work symmetrically.

Acceptance criteria:

- Both fighters can move, attack, block, backdash, sidestep, and ring out.
- Both fighters reset correctly after either fighter loses.
- Neither controller contains hardcoded assumptions that only Player 1 is playable.

#### 7. Build a real training mode

- Input history
- Frame-step and pause
- Hitbox/hurtbox display
- Frame advantage display
- Damage and combo counter
- Dummy states: stand, crouch, block, counterattack
- Record and playback
- Position reset shortcut

#### 8. Prove one fighter's three variations

Only after the shared combat system works:

- Finish one base fighter.
- Create three fixed variations.
- Give each variation a different tactical purpose.
- Test all nine variation-versus-variation combinations in the mirror matchup.
- Measure move use, damage, win rate, and common failure states.

## Suggested short-term backlog

### Sprint A: combat foundation

- [ ] Extract input sampling from `JinPrototypeController`
- [ ] Add 60 Hz combat clock
- [ ] Add frame-by-frame input history
- [ ] Add explicit fighter states
- [ ] Convert four attacks to frame-based move data
- [ ] Add frame-step debugging

### Sprint B: first contact

- [ ] Add hurtboxes
- [ ] Add attack hitboxes
- [x] Add prototype circular pushboxes
- [ ] Add hit detection
- [ ] Add health and damage
- [ ] Add hitstop
- [ ] Add hitstun and pushback

### Sprint C: defense and rounds

- [ ] Connect block pose to actual guard rules
- [ ] Add high/mid/low resolution
- [ ] Add blockstun and chip rules
- [ ] Add knockdown
- [ ] Add round timer and win conditions
- [ ] Integrate ring-out as a round result

### Sprint D: second fighter and local versus

- [ ] Add Player 2 controls
- [ ] Make opponent logic symmetrical
- [ ] Add round reset countdown
- [ ] Add basic character select
- [ ] Add variation select placeholder
- [ ] Test device disconnect/reconnect

## Vertical-slice definition of done

A meaningful vertical slice should contain:

- Two original playable fighters
- Three variations per fighter
- One polished stage with ring-out rules
- Local versus
- Training mode
- Character and variation selection
- Health, timer, rounds, and match results
- Frame-authored attacks
- High/mid/low defense
- Throws and throw escapes
- Knockdowns and wake-up behavior
- Stable 60 Hz simulation
- Input history and hitbox debugging
- Controller remapping
- Basic sound, impact effects, and camera shake
- A complete match playable without console errors

Online play is not required for the first vertical slice, but simulation and input design should avoid choices that make rollback networking impossible later.

## Testing checklist

### Input

- [ ] Single taps never trigger double-tap techniques.
- [ ] Double-tap window works at 30, 60, 120, and uncapped rendering FPS.
- [ ] Run stops when forward is released.
- [ ] Backdash completes even if backward is released.
- [ ] `A+D` reliably enters block.
- [ ] Attacks cannot start while blocking.
- [ ] Movement cannot start during committed attack recovery.
- [ ] Keyboard arrows never move the fighter.
- [ ] Gamepad stick returns to neutral before another attack triggers.

### Movement and camera

- [ ] Fighters always face each other.
- [ ] Sidestep direction remains predictable as the fight rotates.
- [ ] Camera never flips 180 degrees unexpectedly.
- [ ] Camera keeps both fighters visible at minimum and maximum separation.
- [ ] Running and backdash do not cross through the opponent.
- [ ] Ring-edge behavior is readable.

### Ring-out

- [ ] Crossing each of the octagon's eight boundary segments triggers a ring out.
- [ ] Player ring-out resets both fighters.
- [ ] Opponent ring-out resets both fighters.
- [ ] Movement, attack, block, run, and backdash states clear on reset.
- [ ] Camera returns smoothly after reset.
- [ ] Repeated ring-outs do not accumulate position or rotation errors.

### Animation

- [ ] Idle motion is subtle.
- [ ] Arms remain relaxed outside blocking and attacking.
- [ ] Block visibly protects the upper body.
- [ ] Punch reaches toward the opponent.
- [ ] High and low kicks are visually distinct.
- [ ] Feet do not slide excessively during normal movement.
- [ ] No limb snaps occur when an attack begins or ends.

## Tuning reference

Current values exposed in the Inspector:

### Movement

- Move speed: `2.25`
- Acceleration: `14`
- Facing speed: `14`
- Double-tap window: `0.24 s`
- Run multiplier: `1.65`
- Backdash peak speed: `5.4`
- Backdash duration: `0.32 s`

### Procedural pose

- Idle breath speed: `1.7` before the internal slowdown multiplier
- Idle bob amount: `0.008`
- Idle motion scale: `0.18`
- Step frequency: `2.3`
- Step amount: `0.28`

### Attacks

- Straight punch duration: `0.42 s`
- High kick duration: `0.62 s`
- Low kick duration: `0.50 s`
- Stepping teep duration: `0.72 s`
- Teep Back → Forward sequence window: `0.42 s`
- Teep forward step speed: `2.4`

### Camera

- Base distance: `5.4`
- Height: `2.15`
- Look height: `0.85`
- Position sharpness: `7`
- Rotation sharpness: `10`

### Ring-out

- Edge inset: `0.12`
- Fall duration: `0.70 s`
- Initial fall speed: `1.2`
- Fall acceleration: `8`

These are prototype feel values, not balance commitments.

## Asset pipeline recommendations

### Character model requirements

- Clean, game-ready topology
- Consistent world scale
- Stable root and hips bones
- Left/right naming consistency
- Functional shoulders, elbows, wrists, fingers, knees, ankles, and toes
- No nonuniform animated scale
- Correct bind pose
- Tested deformation at extreme kicks and punches
- Separate or maskable materials for variation visuals

### Animation requirements

Minimum initial set:

- Neutral idle
- Forward walk
- Backward walk
- Forward run
- Backdash
- Sidestep left/right
- Block enter/hold/exit
- Straight punch
- High kick
- Low kick
- Stepping teep/push kick
- Hit reactions: high, mid, low
- Block reactions: high and low
- Knockdown
- Get-up
- Ring-out fall

Animation clips should contain presentation motion, while gameplay movement and hit timing remain controlled by the simulation.

## Major design decisions still needed

- Is the game always one-on-one?
- Are ring-outs enabled on every stage or only some stages?
- Does block stop high and mid attacks automatically?
- Is low block a separate input or a directional defensive action?
- Are throws performed by an attack direction, a modifier, or both movement buttons plus direction?
- Can attacks be canceled into other attacks?
- How long should typical combos be?
- Is meter universal or fighter-specific?
- Are variations selected before every match?
- What is the first original fighter's martial art and personality?
- What platforms are targeted first?
- Is online rollback required for the first public release?
- What art direction replaces the current licensed placeholder?

## Scope control

Avoid these until the basic two-fighter combat loop is proven:

- Large roster
- Story mode
- Cinematic supers
- Complex character customization
- Online matchmaking
- Ranked progression
- Large stage library
- Extensive cosmetic inventory
- Final voice acting
- Large VFX production

The highest-risk question is whether the reversed control scheme feels responsive, understandable, and strategically rich. Development should answer that before expanding content.

## Working principle

Every new system should improve one of these testable questions:

1. Does moving with buttons feel intentional and responsive?
2. Do directional attacks feel physically connected to their input?
3. Is 3D spacing readable while the camera moves?
4. Can players predict why an attack hit, missed, or was blocked?
5. Do variations create distinct decisions without fragmenting the fighter's identity?

If a feature does not help answer one of those questions during the prototype phase, it is probably not the next feature to build.
