# Unity Multiplayer Audio Refactor (V1)

This package separates local audio, audio settings, 3D world emitters, player sound mappings, NGO event replication, and narration playback.

## Included scripts

- `AudioManager.cs` — one persistent instance per client. Plays local/non-spatial SFX and manages main + temporary music.
- `AudioSettingsManager.cs` — loads/saves Master, Music, SFX, and Voice levels using the existing PlayerPrefs keys and exposed AudioMixer parameters.
- `WorldAudioEmitter.cs` — reusable 3D AudioSource wrapper for player/world-object sounds.
- `PlayerAudio.cs` — maps player sound events (jump, throw, land, pickup, drop) to clips.
- `NetworkPlayerAudio.cs` — optional NGO bridge using universal `[Rpc]`. Owner-triggered discrete events play immediately for the owner and are relayed to other clients.
- `VoiceoverAudioPlayer.cs` — non-spatial voice-over playback with a completion event; it is not a voice-chat solution.

## 1. AudioMixer setup

Create/keep an AudioMixer with this group structure:

```text
Master
├── Music
├── SFX
└── Voice
```

Expose volume parameters named exactly:

- `MasterVolume`
- `MusicVolume`
- `SFXVolume`
- `VoiceVolume`

These names match the existing `SoundManager.cs`. If your mixer uses different exposed parameter names, update the fields on `AudioSettingsManager`.

Route sources to their matching groups:

- `AudioManager.localSfxSource` -> SFX
- `AudioManager.musicSource` -> Music
- `AudioManager.temporaryMusicSource` -> Music
- Player/world `WorldAudioEmitter` sources -> SFX
- `VoiceoverAudioPlayer.voiceSource` -> Voice

The mixer parameters are exposed parameters, not merely AudioMixer group names.

## 2. Scene setup

Create a root GameObject such as `PersistentAudio` in the first scene. Keep the three referenced AudioSources as children of this persistent root so their references remain valid across scene changes. Add:

- `AudioManager`
- `AudioSettingsManager`
- Three AudioSources for local SFX, main music, and temporary music (assign them to `AudioManager`).
- Optionally, a fourth AudioSource with `VoiceoverAudioPlayer` for narration.

Assign the AudioMixer to `AudioSettingsManager`. Assign all AudioSources and their output mixer groups. The `AudioManager` persists with `DontDestroyOnLoad`; do not place another copy in every gameplay scene. If another scene accidentally contains a duplicate, the duplicate is destroyed.

For UI sliders, connect each Slider's **On Value Changed (Single)** event to the matching method on `AudioSettingsManager`:

- Master -> `SetMasterVolume(float)`
- Music -> `SetMusicVolume(float)`
- SFX -> `SetSFXVolume(float)`
- Voice -> `SetVoiceVolume(float)`

Initialize slider values from the matching getter (`GetMasterVolume`, `GetMusicVolume`, `GetSFXVolume`, `GetVoiceVolume`) when opening/loading the settings UI. The settings component does not require UI references and applies saved mixer settings even when no sliders exist in the current scene.

## 3. Player prefab setup

On the player prefab, add:

```text
Player (NetworkObject)
├── PlayerMovement / other gameplay components
├── PlayerAudio
├── NetworkPlayerAudio
└── WorldAudioEmitter + AudioSource
```

`WorldAudioEmitter` can be on the player root or on a child positioned where the sound should originate. Assign its SFX AudioMixerGroup. Assign the emitter to `PlayerAudio`, then assign the player's jump/throw/land/pickup/drop clips. Assign `PlayerAudio` to `NetworkPlayerAudio` if it is not on the same GameObject; otherwise it is found automatically.

Set the AudioSource's spatial blend to 3D (the script also enforces this), and tune its Min Distance / Max Distance and rolloff to suit your game.

## 4. Calling local and world sounds

For local-only audio (UI clicks, local inventory feedback, menu sounds):

```csharp
AudioManager.Instance.PlayLocalSFX(buttonClickClip);
```

For a local world object that all clients already know about, call its `WorldAudioEmitter.Play(clip)` when that client observes the gameplay event.

For a player-owned discrete event (jump/throw/pickup/drop), call this on the owning player's gameplay script after the action has actually happened:

```csharp
[SerializeField] private NetworkPlayerAudio networkPlayerAudio;

// After the jump has been accepted/performed:
networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Jump);

// After a throw has been accepted/performed:
networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Throw);
```

`PlayFromOwner` plays once immediately for the owner, sends only the small event identifier to the server, and the server relays it to the other clients. Each client resolves that identifier to its local clip reference. The clip itself is not sent over the network.

For a server-originated sound (for example, a server-controlled AI/object), call `PlayForEveryoneFromServer(...)` from server-side logic on the corresponding spawned `NetworkPlayerAudio` object.

Do not send an RPC for every footstep unless you need to. If footsteps are driven by a replicated animation/state, each client can play them locally from that synchronized event/animation. Avoid playing the same sound both from an animation event and from `NetworkPlayerAudio`.

## 5. Main music and temporary music

Start the regular track:

```csharp
AudioManager.Instance.PlayMusic(gameplayMusic);
```

Start pause-menu music. The current main track pauses at its current time:

```csharp
AudioManager.Instance.PlayTemporaryMusic(pauseMusic);
```

When leaving the pause menu, stop the temporary track and resume the original main track if it had been playing:

```csharp
AudioManager.Instance.StopTemporaryMusic();
```

Calling `PlayTemporaryMusic` with another clip replaces the temporary track while keeping the main track paused. Calling `PlayMusic` starts/replaces the main track and clears temporary music. Both music AudioSources should route to the Music mixer group.

## 6. Voice-over migration

Use `VoiceoverAudioPlayer.Play(clip)` instead of the old `PlayVoiceClip(string)` / `PlayThisVoiceClip(AudioClip)` methods. Subscribe to the completion event from the system that owns progression:

```csharp
voiceoverAudioPlayer.PlaybackCompleted += progressScript.VoiceClipOver;
```

Unsubscribe when that owner is destroyed/disabled if its lifetime is shorter than the audio player. This keeps the audio player independent of `ProgressScript`.

This component handles narration/voice-over only. Future proximity voice chat should be implemented separately and attached/spatialized per player, with its own voice transport and mute/distance rules.

## 7. Migration from the old SoundManager

| Old call/responsibility | New location |
|---|---|
| `PlaySFX(...)` for UI/local feedback | `AudioManager.Instance.PlayLocalSFX(clip)` |
| Main music playback | `AudioManager.Instance.PlayMusic(clip)` |
| Pause/temporary music | `PlayTemporaryMusic(clip)` / `StopTemporaryMusic()` |
| Master/Music/SFX/Voice volume and PlayerPrefs | `AudioSettingsManager` |
| Sounds emitted by a player/world object | `PlayerAudio` / `WorldAudioEmitter` |
| Networked player jump/throw/etc. | `NetworkPlayerAudio.PlayFromOwner(event)` |
| Voice-over clip playback/completion | `VoiceoverAudioPlayer` + `PlaybackCompleted` event |

The old string-based clip lookup is intentionally removed. Store direct `AudioClip` references in the relevant component/Inspector instead of searching arrays by clip name.

## Notes / scope

- This V1 includes main/temporary music pause and resume, but not fade-in/fade-out yet. The separate main/temporary sources and mixer routing provide a clean base for adding fades later.
- `NetworkPlayerAudio` uses NGO universal `[Rpc]` methods. Universal RPCs are available from NGO 1.8.0 onward and are the recommended API in current NGO documentation. If your installed NGO version is older than 1.8.0, upgrade the package or adapt only this bridge to the legacy RPC attributes; the playback components do not need to change.
- The owner-side network method should only be called after the gameplay action has been validated/performed. It is an audio-event relay, not gameplay authority or anti-cheat validation.
