"""Real Main, external input: authored retries, focus and exact live/slot state."""
import argparse
from pathlib import Path
import subprocess
import time

from PIL import Image

from normal_player import NormalPlayer, digest, normalized
from pumas_unavailable_input import SettledPlayer
from source_reconstruction_input import tabs
from bell_restore_observation import observe_player, runtime_state


class FirstRoutePlayer(SettledPlayer):
    def click(self, caption):
        # Quit scans settings by keyboard focus. Inspect each observed frame
        # once before Tab, rather than waiting eight seconds on a hidden item.
        if caption == 'Quit game':
            return NormalPlayer.click(self, caption)
        return super().click(caption)

    def visible(self):
        visible = super().visible()
        # Full-page layout segmentation can also omit the question/prose.
        # Read its observed region as text; never infer it from saved state.
        prose = self.output / 'feedback-prose-ocr.png'
        Image.open(self.output / 'current.png').crop((310, 190, 1135, 425)).save(prose)
        text = subprocess.check_output(['tesseract', str(prose), 'stdout', '--psm', '6'],
                                       stderr=subprocess.DEVNULL, text=True, timeout=15)
        return visible + ' ' + normalized(text)

    def wait_visible(self, caption, present=True):
        if caption != 'Check the source':
            return super().wait_visible(caption, present)
        # Full-page OCR omits some embedded-window titles. Inspect the same
        # observed title region used by NormalPlayer's other modal checks.
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            frame = self.root_capture('current')
            title = self.output / 'feedback-title-ocr.png'
            Image.open(frame).crop((296, 153, 1143, 185)).save(title)
            text = subprocess.check_output(['tesseract', str(title), 'stdout', '--psm', '7'],
                                           stderr=subprocess.DEVNULL, text=True, timeout=15)
            if (normalized(caption) in normalized(text)) == present:
                self.check(True, 'visible feedback title: ' + caption)
                return
            time.sleep(.1)
        self.check(False, 'Feedback title did not settle: ' + caption)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--percent', type=int, choices=[100, 125, 150], required=True)
    parser.add_argument('--gate', choices=['ch1_s1a_evidence', 'ch1_s2_evidence', 'ch1_s2a_evidence', 'ch1_s3_evidence', 'ch1_s3a_evidence', 'ch2_s1_evidence', 'ch2_s2a_evidence', 'ch2_s3_evidence', 'ch2_s5_evidence', 'ch3_s1_evidence', 'ch3_s2_evidence', 'ch3_s2a_evidence', 'ch3_s4_evidence'],
                        default='ch1_s1a_evidence')
    args = parser.parse_args()
    player = observe_player(FirstRoutePlayer(args.display_state, args.fixture, args.output, args.seed))
    # Populate all four ordinary slots before launch. Input never edits slots.
    for name in ['save.previous.json', 'autosave.json', 'autosave.previous.json']:
        (player.saves / name).write_bytes(args.seed.read_bytes())
    with player as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['percent'] = args.percent
        gate = args.gate
        game.result['gate'] = gate
        game.click('Settings'); game.click('Toggle instant text')
        for _ in range((args.percent - 100) // 25):
            game.click('Settings'); game.click('Larger reading text'); game.key('Escape')
        slots = game.slots(); game.click('Load'); game.click('Load current manual save')
        original = runtime_state(game, gate)
        game.check(not original['canAdvance'] and gate not in original['snapshot']['solvedActivities'],
                   'actual loaded authored question is unanswered')
        game.check(game.slots() == slots, 'Load preserves all four seeded save/backup byte sequences')
        activity = next(b['activity'] for b in game.beats if b['id'] == gate)
        prompt = activity['prompt']
        game.click('Examine evidence'); game.wait_visible(prompt); game.root_capture('question')
        focused = 3  # Existing Review known evidence initial focus.
        for wrong in range(len(activity['options'])):
            if wrong == activity['correctIndex']:
                continue
            # OCR often reads the isolated pronoun I as a vertical stroke.
            # Use a distinctive authored excerpt after the speaker/opening;
            # exact full feedback is independently checked by native review.
            cue = ' '.join(activity['optionFeedback'][wrong].split()[2:8])
            tabs(game, abs(focused - wrong), wrong < focused); game.key('Return')
            game.wait_visible('Check the source'); game.wait_visible(cue)
            game.wait_visible(' '.join(activity['optionFeedback'][wrong].split()[-6:]))
            game.root_capture('wrong-' + str(wrong))
            game.check(runtime_state(game, gate) == original and game.slots() == slots,
                       'wrong answer preserves full live state and all slot bytes: ' + str(wrong))
            # The action already owns keyboard focus. Both visible Back
            # controls share a caption, so avoid an ambiguous OCR click.
            game.key('Return'); game.wait_visible(prompt)
            game.root_capture('back-' + str(wrong))
            # Immediate Enter must reactivate the same wrong option: observe
            # actual originating keyboard focus instead of inferring it from Tab counts.
            game.key('Return'); game.wait_visible('Check the source'); game.wait_visible(cue)
            game.check(runtime_state(game, gate) == original and game.slots() == slots,
                       'Back restores originating focus; repeated wrong answer remains inert: ' + str(wrong))
            game.key('Escape'); game.wait_visible(prompt)
            focused = wrong
        tabs(game, abs(focused - activity['correctIndex']), activity['correctIndex'] < focused)
        game.key('Return'); time.sleep(.5)
        solved = runtime_state(game, gate)
        game.check(solved['canAdvance'] and
                   set(solved['snapshot']['solvedActivities']) == set(original['snapshot']['solvedActivities']) | {gate},
                   'only the explicit correct answer unlocks Continue at the same beat')
        for name in ['history', 'beatId']:
            game.check(solved['snapshot'][name] == original['snapshot'][name], 'correct answer preserves ' + name)
        for name in ['facts', 'inventory', 'activeStageCues']:
            game.check(solved[name] == original[name], 'correct answer preserves ' + name)
        game.check((game.saves / 'save.json').read_bytes() == slots['save.json'] and
                   (game.saves / 'save.previous.json').read_bytes() == slots['save.previous.json'],
                   'correct answer leaves both explicit manual checkpoints untouched')
        game.wait_visible('Continue'); game.root_capture('solved')
        game.click('Save'); game.click('Load'); game.click('Load current manual save')
        game.check(runtime_state(game, gate) == solved, 'actual Save/Load restores solved full live state without duplicating the record')
        next_beat = game.beats[game.beats.index(next(b for b in game.beats if b['id'] == gate)) + 1]
        game.click('Continue'); successor = runtime_state(game, next_beat['id'])
        game.check(successor['snapshot']['beatId'] == next_beat['id'], 'explicit Continue reaches the original successor')
        game.wait_visible(' '.join(next_beat['text'].split()[:8]))
        game.root_capture('successor')
        game.result['liveStates'] = dict(unanswered=original, solved=solved, successor=successor)
        game.quit()
    print('PASS normal authored feedback, exact state/save preservation and keyboard focus:', args.gate, args.percent)


if __name__ == '__main__':
    main()
