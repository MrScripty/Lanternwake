#!/usr/bin/env python3
"""Authoring tool: retain the acoustic presets and their unmodified samples in SF2.

Run once against the pinned upstream bank; ordinary audio setup needs no download.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

PROGRAMS = {0: 'Piano', 21: 'Accordion', 24: 'Nylon guitar', 40: 'Violin', 42: 'Cello', 45: 'Pizzicato strings'}


def chunk(name, data):
    return name + struct.pack('<I', len(data)) + data + (b'\0' if len(data) % 2 else b'')


def chunks(data):
    offset = 0
    while offset < len(data):
        name, length = struct.unpack_from('<4sI', data, offset)
        yield name, data[offset + 8:offset + 8 + length]
        offset += 8 + length + length % 2


def records(data, size):
    assert len(data) % size == 0
    return [data[i:i + size] for i in range(0, len(data), size)]


def subset(source, destination):
    raw = source.read_bytes()
    assert raw[:4] == b'RIFF' and raw[8:12] == b'sfbk'
    lists = {data[:4]: dict(chunks(data[4:])) for name, data in chunks(raw[12:]) if name == b'LIST'}
    pd = lists[b'pdta']
    ph = records(pd[b'phdr'], 38)
    ih = records(pd[b'inst'], 22)
    sh = records(pd[b'shdr'], 46)
    pb, ib = records(pd[b'pbag'], 4), records(pd[b'ibag'], 4)
    pg, ig = records(pd[b'pgen'], 4), records(pd[b'igen'], 4)
    pm, im = records(pd[b'pmod'], 10), records(pd[b'imod'], 10)
    selected = [i for i, p in enumerate(ph[:-1]) if struct.unpack_from('<HH', p, 20)[0] in PROGRAMS and struct.unpack_from('<HH', p, 20)[1] == 0]
    assert len(selected) == len(PROGRAMS)

    def zones(headers, bags, gens, mods, index, bag_offset):
        begin = struct.unpack_from('<H', headers[index], bag_offset)[0]
        end = struct.unpack_from('<H', headers[index + 1], bag_offset)[0]
        for b in range(begin, end):
            g, m = struct.unpack('<HH', bags[b])
            ng, nm = struct.unpack('<HH', bags[b + 1])
            yield gens[g:ng], mods[m:nm]

    instruments = sorted({amount for i in selected for gs, _ in zones(ph, pb, pg, pm, i, 24) for op, amount in map(lambda g: struct.unpack('<HH', g), gs) if op == 41})
    samples = {amount for i in instruments for gs, _ in zones(ih, ib, ig, im, i, 20) for op, amount in map(lambda g: struct.unpack('<HH', g), gs) if op == 53}
    # Linked stereo samples must travel together even if only one is explicitly referenced.
    for i in list(samples):
        link, kind = struct.unpack_from('<HH', sh[i], 42)
        if kind & 7 in (2, 4):
            samples.add(link)
    samples = sorted(samples)
    instrument_map = {old: new for new, old in enumerate(instruments)}
    sample_map = {old: new for new, old in enumerate(samples)}

    def table(headers, bags, gens, mods, indices, bag_offset, reference_op, mapping, terminal):
        new_headers, new_bags, new_gens, new_mods = [], [], [], []
        for i in indices:
            h = bytearray(headers[i])
            struct.pack_into('<H', h, bag_offset, len(new_bags))
            new_headers.append(bytes(h))
            for gs, ms in zones(headers, bags, gens, mods, i, bag_offset):
                new_bags.append(struct.pack('<HH', len(new_gens), len(new_mods)))
                for g in gs:
                    op, amount = struct.unpack('<HH', g)
                    new_gens.append(struct.pack('<HH', op, mapping[amount]) if op == reference_op else g)
                new_mods.extend(ms)
        h = bytearray(headers[-1])
        h[:20] = terminal.ljust(20, b'\0')
        struct.pack_into('<H', h, bag_offset, len(new_bags))
        new_headers.append(bytes(h))
        new_bags.append(struct.pack('<HH', len(new_gens), len(new_mods)))
        return b''.join(new_headers), b''.join(new_bags), b''.join(new_gens) + b'\0' * 4, b''.join(new_mods) + b'\0' * 10

    phdr, pbag, pgen, pmod = table(ph, pb, pg, pm, selected, 24, 41, instrument_map, b'EOP')
    inst, ibag, igen, imod = table(ih, ib, ig, im, instruments, 20, 53, sample_map, b'EOI')
    pcm = bytearray()
    headers = []
    for i in samples:
        h = bytearray(sh[i])
        start, end, loop_start, loop_end = struct.unpack_from('<IIII', h, 20)
        new_start = len(pcm) // 2
        pcm.extend(lists[b'sdta'][b'smpl'][start * 2:end * 2])
        pcm.extend(b'\0' * 92)
        struct.pack_into('<IIII', h, 20, new_start, new_start + end - start, new_start + loop_start - start, new_start + loop_end - start)
        link, kind = struct.unpack_from('<HH', h, 42)
        struct.pack_into('<H', h, 42, sample_map[link] if kind & 7 in (2, 4) else 0)
        headers.append(bytes(h))
    headers.append(b'EOS'.ljust(20, b'\0') + struct.pack('<IIIIIBbHH', *([len(pcm) // 2] * 4), 32000, 60, 0, 0, 1))
    info = lists[b'INFO']
    info[b'INAM'] = b'Saltmere Acoustic (GeneralUser GS subset)\0'
    pd = {b'phdr': phdr, b'pbag': pbag, b'pmod': pmod, b'pgen': pgen, b'inst': inst, b'ibag': ibag, b'imod': imod, b'igen': igen, b'shdr': b''.join(headers)}
    body = b'sfbk' + chunk(b'LIST', b'INFO' + b''.join(chunk(k, v) for k, v in info.items())) + chunk(b'LIST', b'sdta' + chunk(b'smpl', bytes(pcm))) + chunk(b'LIST', b'pdta' + b''.join(chunk(k, v) for k, v in pd.items()))
    destination.write_bytes(chunk(b'RIFF', body))
    provenance = {'upstream': 'https://github.com/mrbumpy409/GeneralUser-GS', 'commit': '684543d5e5efaef08d02be50dcda8d552478fa60', 'upstream_sha256': hashlib.sha256(raw).hexdigest(), 'subset_sha256': hashlib.sha256(destination.read_bytes()).hexdigest(), 'programs_zero_based': PROGRAMS, 'samples': len(samples), 'modification': 'Six acoustic presets and their unmodified samples retained; index tables rebuilt; bank renamed.'}
    destination.with_suffix('.json').write_text(json.dumps(provenance, indent=2) + '\n')
    print(f'{destination}: {destination.stat().st_size} bytes, {len(samples)} samples')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    options = parser.parse_args()
    subset(options.source, options.destination)
