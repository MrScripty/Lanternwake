"""Passive debug live-state observer; player actions remain external X11 input."""
import json
import time


def observe_player(game):
    game.env['LANTERNWAKE_BELL_OBSERVATION_FIXTURE'] = str(game.fixture)
    game.command.append('res://qualification/bell-restore-observation.tscn')
    game.result['runtimeObservation'] = 'passive debug wrapper of real Main; normal storage; no slot writes'
    return game


def runtime_state(game, beat_id):
    deadline = time.monotonic() + 5
    while time.monotonic() < deadline:
        path = game.fixture / 'runtime-observation.json'
        if path.exists():
            state = json.loads(path.read_text())
            if state['snapshot']['beatId'] == beat_id:
                return state
        time.sleep(.02)
    raise RuntimeError('Actual live session did not reach ' + beat_id)


def verify_restore(game, beat_id, next_beat_id):
    expected = runtime_state(game, beat_id)
    game.click('Save')
    manual_bytes = (game.saves / 'save.json').read_bytes()
    game.click('Continue')  # Save retains keyboard focus; activate the actual story control.
    changed = runtime_state(game, next_beat_id)
    game.check(changed != expected and (game.saves / 'save.json').read_bytes() == manual_bytes,
               'live-state negative control rejects advanced runtime despite unchanged manual file: ' + beat_id)
    slots = game.slots()
    game.click('Load'); game.click('Load current manual save')
    restored = runtime_state(game, beat_id)
    game.check(restored == expected,
               'Load restores actual full snapshot, transcript, facts, inventory, solved state and derived fields: ' + beat_id)
    game.check(game.slots() == slots, 'Load preserves persisted slot bytes: ' + beat_id)
    game.result.setdefault('restoredRuntimeStates', {})[beat_id] = restored
