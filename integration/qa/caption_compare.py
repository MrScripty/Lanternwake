"""Compare stable caption pixels from desktop root and normal Debug F12 captures."""
import argparse
import json
from pathlib import Path
import time

from PIL import Image

from normal_player import NormalPlayer, digest


# Native client coordinates; the root has the inspected client offset +80,+50.
REGIONS = {'suggestion1': (410, 216, 870, 243), 'suggestion2': (410, 263, 870, 289),
           'suggestion3': (410, 309, 870, 336), 'return': (390, 463, 520, 494),
           'continue': (1105, 725, 1220, 756)}


def masks(path, root=False):
    image = Image.open(path).convert('RGB')
    offset_x, offset_y = (80, 50) if root else (0, 0)
    return {name: bytes(min(pixel) >= 140 for pixel in image.crop(
        (left + offset_x, top + offset_y, right + offset_x, bottom + offset_y)).get_flattened_data())
            for name, (left, top, right, bottom) in REGIONS.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['display-state', 'fixture', 'output', 'seed']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--cycles', type=int, default=6)
    args = parser.parse_args()
    samples = []
    with NormalPlayer(args.display_state, args.fixture, args.output, args.seed) as game:
        game.result['controllerSha256'] = digest(__file__)
        game.result['limits'].append('F12 changes the status line; comparison excludes that line and animated scenery. Adjacent captures are not simultaneous frames.')
        game.click('Load'); game.click('Load current manual save'); time.sleep(1.5)
        for cycle in range(args.cycles):
            game.click('Stay and talk')
            game.wait_visible('A moment between the lines')
            game.type_text('i want to catalogue this carefully.')
            game.click('Say this')
            time.sleep(.5)
            # Each comparison surrounds native viewport readback with independent
            # desktop captures while the tested caption strings stay unchanged.
            for sample in range(3):
                label = f'cycle-{cycle:02d}-sample-{sample:02d}'
                before = game.root_capture(label + '-before')
                native = game.native_capture(label)
                after = game.root_capture(label + '-after')
                reference = masks(native)
                comparisons = []
                for image in [before, after]:
                    current = masks(image, root=True)
                    comparisons.append({'image': image.name, 'regions': {
                        region: {'nativeWhitePixels': sum(reference[region]), 'rootWhitePixels': sum(current[region]),
                                 'differingMaskPixels': sum(a != b for a, b in zip(reference[region], current[region]))}
                        for region in REGIONS}})
                samples.append({'label': label, 'native': native.name, 'comparisons': comparisons})
            game.key('Escape'); game.wait_visible('A moment between the lines', False)
            game.check(game.snapshot('autosave.json')['beatId'] == 'ch1_s1_b021', 'caption observations and optional replies preserve canonical beat')
        game.result['captionSamples'] = samples
        game.quit()
    (args.output / 'comparison.json').write_text(json.dumps({'regions': REGIONS, 'samples': samples}, indent=2) + '\n')
    differing = [(s['label'], c['image'], region, values) for s in samples for c in s['comparisons']
                 for region, values in c['regions'].items() if values['differingMaskPixels'] > 10]
    print('Completed', len(samples), 'native and', len(samples) * 2, 'root captures;', len(differing), 'caption-region differences >10 pixels.')


if __name__ == '__main__':
    main()
