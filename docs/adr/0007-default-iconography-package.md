# ADR 0007: Default cross-platform iconography package

## Status

Accepted for `RIBBON-ICONOGRAPHY-006`.

## Context

The Ribbon, Bars and command contracts already carry stable `IconKey` values,
but every platform host currently requires the consuming application to supply
its own resolver. The SDK therefore renders text-only command chrome by
default, uses the same small icon dimensions for large Ribbon items, and has no
single visual vocabulary across WPF, WinForms and MAUI.

The SDK needs a polished default icon set without introducing a UI dependency
into Foundation, Core, Editing, Ribbon.Core or Bars.Core. It also needs to keep
application overrides possible and preserve third-party license provenance.

## Decision

Add a packable `NeraSpreadSheet.Iconography` project above the command/model
layers. It contains:

- a versioned manifest mapping stable semantic icon keys to immutable assets;
- normalized SVG masters;
- pre-rendered PNG assets at 16, 20, 24, 32 and 48 physical pixels for light,
  dark and both high-contrast polarities;
- a host-neutral catalog that returns independent streams/byte arrays;
- the upstream license, notice and pinned source commit.

WPF, WinForms and MAUI reference this package and provide cached native image
adapters. Presenters use 16-pixel glyphs for compact commands and 32-pixel
glyphs for large commands. Existing application-supplied resolvers take
precedence over the default provider.

The source artwork is Microsoft Fluent UI System Icons under the MIT license.
Nera-specific compositions use the same 24-by-24 grid, 1.5-unit stroke,
rounded joins and optical weight. Excel and DevExpress assets are not copied or
distributed.

## Consequences

- The SDK has a coherent default visual surface and can still be fully themed
  or replaced by applications.
- PNG variants avoid adding an SVG rendering dependency to desktop/mobile
  runtime hot paths. SVG masters remain available for future renderers.
- The package grows by bounded embedded resources; only catalogued assets are
  included, not the full upstream repository.
- Asset generation is an explicit development step and is verified by tests;
  it does not run during ordinary build or restore.
