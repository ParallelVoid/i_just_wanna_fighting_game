# A Fighting Game
A fighting game inspired by *Buriki One*, *Mortal Kombat X*, and *World Heroes*.

## Project Vision
- This game was inspired by *Buriki One*'s reversed control philosophy and *Mortal Kombat X*'s fixed character-variation system.
- Movement is performed with two dedicated buttons.
- Directional-stick inputs perform attacks **instead** of directly moving the fighter.
- Fighters can circle each other in **3D**.
- Every fighter has **three fixed variations** selected before the match.
- Sidesteps occur relative to the opponent, the camera rotates around the pair so that forward, backward, and sideways movement remain understandable as the fight line changes.

### Control Scheme
| Action | Keyboard | Gamepad prototype |
|---|---|---|
| Move backward | <kbd>A</kbd> | <kbd>LB</kbd> |
| Move forward | <kbd>D</kbd> | <kbd>RB</kbd> |
| Run forward | Double-tap and hold <kbd>D</kbd> | Double-tap and hold <kbd>RB</kbd> |
| Backdash | Double-tap <kbd>A</kbd> | Double-tap <kbd>LB</kbd> |
| Block | Hold <kbd>A</kbd> + <kbd>D</kbd> | Hold <kbd>RB</kbd> + <kbd>RB</kbd> |
| Sidestep around opponent | <kbd>W</kbd> / <kbd>S</kbd> | Not assigned yet |
| Straight punch | Right Arrow | Attack stick right |
| High kick | Up Arrow | Attack stick up |
| Low kick | Down Arrow | Attack stick down |
| Stepping teep/push kick | Hold <kbd>D</kbd>, Back → Neutral → Forward arrows | Hold <kbd>RB</kbd>, attack stick Back → Neutral → Forward |

### Two-player Control Scheme
Both fighters have prototype movement and attack set:
| Action | Player 1 | Player 2 |
|---|---|---|
| Back / Forward | <kbd>A</kbd> / <kbd>D</kbd> | <kbd>J</kbd> / <kbd>L</kbd> |
| Sidestep | <kbd>W</kbd> / <kbd>S</kbd> | <kbd>I</kbd> / <kbd>K</kbd> |
| Attack Back / Forward | Left / Right Arrow | <kbd>F</kbd> / <kbd>H</kbd> |
| High / Low attack | Up / Down Arrow | <kbd>T</kbd> / <kbd>G</kbd> |
| Block | <kbd>A</kbd> + <kbd>D</kbd> | <kbd>J</kbd> + <kbd>L</kbd> |
| Run | <kbd>D</kbd> <kbd>D</kbd> | <kbd>L</kbd> <kbd>L</kbd> |
| Backdash | <kbd>A</kbd> <kbd>A</kbd> | <kbd>J</kbd> <kbd>J</kbd> |
| Stepping teep | Hold <kbd>D</kbd>, Left → Neutral → Right | Hold <kbd>L</kbd>, <kbd>F</kbd> → Neutral → <kbd>H</kbd> |

### Prototype Hitboxes Information
- Red translucent box: The attack is currently active and has not connected.
- Green translucent box: The active attack connected with the opponent's hurtbox.

## Feature Implementation List
### Stabilize the input and state code
- [ ] Separate movement input from attack-direction input.
- [ ] Input priority and simultaneous-input behavior.
- [ ] Configurable keyboard and gamepad bindings.
- [ ] Gamepad sidestep mapping.

### 60 Hz combat clock
- [ ] Advance both fighters through one clock with fixed tick durations.
- [ ] Check hitboxes on every tick, including catch-up ticks.
- [ ] Shared time for movement gestures, teep commands, existing reactions, damage recovery, countdown, and resets.
- [ ] Preserve sampled input transitions between ticks without repeating button edges.
- [ ] Keep camera and skeletal posing in the presentation layer.
- [ ] Frame recording/playback and frame-step controls tools.

### Data-driven moves
- [ ] Recreate the straight punch, high kick, low kick, and stepping teep as data.
- [ ] Add editor validation for missing or invalid frame ranges.
- [ ] Keep procedural posing temporarily, but trigger it from move state.

### Hurtboxes, hitboxes, and pushboxes
- [ ] Add persistent hurtboxes to head, torso, arms, and legs.
- [ ] Add frame-driven hitboxes to each attack.
- [ ] Add a ground pushbox so fighters cannot overlap.
- [ ] Visualize all collision shapes in training mode.

### Defender reactions
- [ ] Health
- [ ] High/mid/low guard rules
- [ ] Hitstun
- [ ] Blockstun
- [ ] Pushback
- [ ] Counter-hit detection
- [ ] Basic knockdown
- [ ] Reset after round end or ring-out

### Training mode
- [ ] Input history
- [ ] Frame-step and pause
- [ ] Hitbox/hurtbox display
- [ ] Frame advantage display
- [ ] Damage and combo counter
- [ ] Dummy states: stand, crouch, block, counterattack
- [ ] Record and playback
- [ ] Position reset shortcut

### Three fighter variations
- [ ] Finish one base fighter.
- [ ] Create three fixed variations.
- [ ] Give each variation a different tactical purpose.

### Input
- [ ] Single taps never trigger double-tap techniques.
- [ ] Double-tap window works at 30, 60, 120, and uncapped rendering FPS.
- [ ] Run stops when forward is released.
- [ ] Backdash completes even if backward is released.
- [ ] Default control scheme reliably enters block.
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

## Project Structure
This is a Unity project built using C#. Key folders:
- `Assets/`: Game scripts, animations, models, scenes, and prefabs.
- `ProjectSettings/`: Unity project settings.
- `Packages/`: Dependencies and Unity package manifest.

To explore or modify the game:
- Clone this repository.
- Open it in *Unity 6000.6.0+* (LTS recommended).
- Hit Play and start fighting!

## Tech Stack
- Engine: Unity
- Language: C#
- Platforms: Windows (32-, 64-bit), macOS (Intel, Apple Silicon), Linux (64-bit)
- Input: Keyboard and Mouse, Controller, Gamepad


## Development Team
This project was made by the *Banana Byte Entertainment* team.
<a href="https://github.com/ParallelVoid/i_just_wanna_fighting_game/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=ParallelVoid/i_just_wanna_fighting_game" alt="contrib.rocks image" />
</a>

## License
This project is provided for educational and portfolio purposes. Please [contact the authors](mailto:bananabyteentertainment@gmail.com) for inquiries about reuse or distribution.
