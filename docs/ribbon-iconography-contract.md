# Ribbon iconography and polished command chrome contract

## Scope

`RIBBON-ICONOGRAPHY-006` supplies a default, replaceable icon vocabulary for
spreadsheet command chrome. It covers File/Quick Access, Home, Insert, Page
Layout, Formulas, Data, Review, View, contextual Table Design and Ribbon
customization command families.

## Stable identity

- Every semantic command icon uses a lower-case dotted key.
- The manifest may map multiple semantic keys to one visual asset.
- File names and upstream asset names are implementation details and are not
  public command identity.
- Unknown keys return no icon; presenters keep the caption and never fail a
  command solely because artwork is absent.

## Rendering

- SVG masters use an independent 24-by-24 view box.
- Generated PNGs exist at 16, 20, 24, 32 and 48 pixels.
- Compact Ribbon/Bar commands use 16 pixels; large Ribbon commands use 32.
- Light, dark, high-contrast-light and high-contrast-dark variants are explicit.
- Hover, pressed, checked and disabled states are presenter styling, not
  duplicated semantic icon keys.
- Native image instances are cached per platform but callers never own the
  embedded source stream.

## Override precedence

1. The existing application `IconResolver`, when it returns an image.
2. The size/theme-aware application resolver, when configured.
3. The Nera default icon provider.
4. Caption-only fallback.

This order preserves existing integrations while allowing new applications to
choose size-aware artwork.

## Licensing and provenance

Every manifest item declares `fluent` or `nera` provenance. The package ships
the pinned Microsoft Fluent UI System Icons MIT license and NOTICE. Generated
or hand-composed Nera assets are independent work and must not trace Excel or
DevExpress artwork.

## Validation

- manifest schema/version and stable-key uniqueness;
- all mapped assets have SVG and every required PNG variant;
- embedded-resource lookup is case-insensitive but canonical keys remain
  lower-case;
- WPF, WinForms and MAUI presenters resolve default icons without application
  setup;
- compact and large commands request the correct physical size;
- loaded desktop smoke validates captions, accessibility and activation after
  icon integration;
- packaging verification includes the new SDK project and third-party notices.

## Follow-up boundaries

Adaptive Ribbon group collapse, gallery/split/combo item kinds, cross-parent
drag/drop customization, user-created tabs/groups and a complete Quick Access
Toolbar remain separate changes built on this icon contract.
