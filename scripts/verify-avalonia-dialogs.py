#!/usr/bin/env python3
"""Validate source-bound native dialog captures, not visual Excel parity."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import zlib


def verify(directory, sha):
    report = json.loads((directory / 'manifest.json').read_text(encoding='utf-8-sig'))
    expected = {f'{theme}-{tab}.png' for theme in ('Light','Dark','HighContrastLight','HighContrastDark')
                for tab in ('number','font','alignment','border','fill','page','margins','sheet','zoom')}
    if report.get('schema') != 'nera.ribbon.dialogs.v1' or report.get('sha') != sha or report.get('nativeWindow') is not True:
        raise ValueError('Invalid dialog source or native evidence')
    if report.get('physicalInputTested') is not False or report['assertions'] != len(report['checks']) or len(set(report['checks'])) != report['assertions'] or report['assertions'] < 70:
        raise ValueError('Missing or duplicated semantic assertions')
    if len(report['captures']) != 36 or {image['name'] for image in report['captures']} != expected:
        raise ValueError('Missing dialog/theme captures')
    for image in report['captures']:
        data = (directory / image['name']).read_bytes()
        if hashlib.sha256(data).hexdigest() != image['sha256'] or data[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Capture content mismatch')
        width, height = struct.unpack('>II', data[16:24])
        if (width,height) != (image['width'],image['height']) or not (200 <= width <= 4096 and 150 <= height <= 4096):
            raise ValueError('Capture geometry mismatch')
        offset = 8; compressed = bytearray()
        while offset < len(data):
            length = struct.unpack('>I',data[offset:offset+4])[0]
            kind = data[offset+4:offset+8]; payload = data[offset+8:offset+8+length]
            crc = struct.unpack('>I',data[offset+8+length:offset+12+length])[0]
            if zlib.crc32(kind+payload) & 0xffffffff != crc: raise ValueError('Corrupt PNG chunk')
            if kind == b'IDAT': compressed.extend(payload)
            offset += length + 12
        pixels = zlib.decompress(compressed)
        if len(set(pixels)) < 12: raise ValueError('Suspiciously empty dialog capture')
    print('Verified 36 native dialog captures and', report['assertions'], 'source-bound checks')


if __name__ == '__main__':
    verify(Path(sys.argv[1]), sys.argv[2])
