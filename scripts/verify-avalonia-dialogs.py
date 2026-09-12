#!/usr/bin/env python3
"""Validate source-bound native dialog captures, including 010 visual/UX evidence.

Raster-scale captures improve image regression coverage but are deliberately not
reported as physical monitor-DPI switching.
"""
import hashlib
import json
from pathlib import Path
import struct
import sys
import zlib


def verify(directory, sha):
    report = json.loads((directory / 'manifest.json').read_text(encoding='utf-8-sig'))
    themes = ('Light', 'Dark', 'HighContrastLight', 'HighContrastDark')
    themed = (
        'number', 'font', 'alignment', 'border', 'fill', 'protection',
        'page', 'margins', 'header-footer', 'sheet', 'zoom',
        'data-validation-settings', 'data-validation-input', 'data-validation-error',
        'advanced-filter', 'consolidate', 'protect-sheet', 'protect-workbook',
    )
    expected = {f'{theme}-{name}.png' for theme in themes for name in themed}
    expected.update(
        f'Light-{name}-raster-{scale}.png'
        for name in ('number', 'page', 'data-validation')
        for scale in ('1.25', '1.5', '2')
    )
    expected.update({
        'Light-format-compact.png',
        'Light-page-compact.png',
        'Light-data-validation-compact.png',
        'Light-protect-sheet-compact.png',
    })

    if report.get('schema') != 'nera.ribbon.dialogs.v2' or report.get('sha') != sha or report.get('nativeWindow') is not True:
        raise ValueError('Invalid dialog source or native evidence')
    if report.get('physicalInputTested') is not False:
        raise ValueError('Dialog evidence must not claim physical input coverage')
    if report.get('monitorDpiSwitchTested') is not False or report.get('rasterScaleIsNotMonitorDpi') is not True:
        raise ValueError('Raster evidence must not be reported as monitor DPI switching')
    if not isinstance(report.get('liveRenderScaling'), (int, float)) or report['liveRenderScaling'] <= 0:
        raise ValueError('Missing native render scaling evidence')
    if report['assertions'] != len(report['checks']) or len(set(report['checks'])) != report['assertions'] or report['assertions'] < 180:
        raise ValueError('Missing or duplicated semantic assertions')
    if len(report['captures']) != 85 or {image['name'] for image in report['captures']} != expected:
        missing = sorted(expected - {image['name'] for image in report['captures']})
        extra = sorted({image['name'] for image in report['captures']} - expected)
        raise ValueError(f'Missing/extra dialog captures: missing={missing}, extra={extra}')

    raster_scales = {image['rasterScale'] for image in report['captures'] if '-raster-' in image['name']}
    if raster_scales != {1.25, 1.5, 2.0}:
        raise ValueError('Incomplete dialog raster-scale matrix')

    for image in report['captures']:
        data = (directory / image['name']).read_bytes()
        if hashlib.sha256(data).hexdigest() != image['sha256'] or data[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Capture content mismatch')
        width, height = struct.unpack('>II', data[16:24])
        if (width, height) != (image['width'], image['height']) or not (200 <= width <= 4096 and 150 <= height <= 4096):
            raise ValueError('Capture geometry mismatch')
        offset = 8
        compressed = bytearray()
        while offset < len(data):
            length = struct.unpack('>I', data[offset:offset+4])[0]
            kind = data[offset+4:offset+8]
            payload = data[offset+8:offset+8+length]
            crc = struct.unpack('>I', data[offset+8+length:offset+12+length])[0]
            if zlib.crc32(kind + payload) & 0xffffffff != crc:
                raise ValueError('Corrupt PNG chunk')
            if kind == b'IDAT':
                compressed.extend(payload)
            offset += length + 12
        pixels = zlib.decompress(compressed)
        if len(set(pixels)) < 12:
            raise ValueError('Suspiciously empty dialog capture')

    print('Verified 85 native dialog captures and', report['assertions'], 'source-bound checks')


if __name__ == '__main__':
    verify(Path(sys.argv[1]), sys.argv[2])
