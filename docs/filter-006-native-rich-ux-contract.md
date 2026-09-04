# FILTER-006 native rich AutoFilter UX contract

## Scope

FILTER-006 extends the existing Table/direct-worksheet paged AutoFilter stack.
It does not introduce a second workbook model and does not integrate complex
Ribbon items before the RIBBON-008 handoff.

The shared `SpreadsheetAutoFilterPagedPresenter` remains the single source for
WPF `Popup`, WinForms `ToolStripDropDown`, and the responsive MAUI overlay or
bottom sheet. Native hosts translate controls and input only; all mutations
continue through `SpreadsheetSession.Tables` or
`SpreadsheetSession.WorksheetFilter`.

## Rich menu projection

The shared page snapshot advertises applicable value, text, number, date,
fill-color, font-color, icon, custom-condition, Top/Bottom and dynamic filter
sections. Text, number and date sections reflect the retained bounded distinct
catalog; visual/custom sections remain available because they do not require a
second source scan. Native hosts show the same section projection with default
Vietnamese labels.

`SpreadsheetAutoFilterRichCriterion` carries exactly one FILTER-005 criterion:
date groups, Top/Bottom, dynamic/average, resolved color, or icon. Construction
rejects empty or ambiguous requests. Text/number/custom comparisons continue
through the existing `ApplyCustomFilterAsync` path. Every successful Apply or
Clear invalidates its session and creates exactly one production history entry;
canceled, rejected, stale, and no-op requests create none.

## Lazy date tree

Year, month, and day nodes are requested using
`SpreadsheetAutoFilterDateParent`. A request returns one bounded
`SpreadsheetAutoFilterDatePage` and never creates native controls for unloaded
nodes. Projection reads the already bounded distinct-value catalog (maximum
10,000 retained values) rather than materializing or rescanning the complete
worksheet axis. Counts are aggregated lazily for only the requested tree level.

## Generation and lifecycle

Every page, date-tree request, selection, Apply, and Clear carries the active
session generation. A stale generation is rejected before mutation. Search and
open/close operations use cancellation, and each native async callback captures
the binding identity that started it. Completion from an older popup/dropdown/
sheet cannot publish status, rebuild values, close, or mutate a newly opened
surface.

Cancel closes and disposes the pending presenter without applying its selection
copy. Focus restoration remains platform-owned and delayed callbacks must first
verify that the initiating native surface is still current.

## Paging and virtualization

The shared default and every Nera native host use pages of 100 values; the hard
per-request maximum remains 1,000. Search, select-all-visible, and
clear-visible operate across the complete bounded search projection while a
native host materializes only the current page. WPF creates at most one page of
checkboxes, WinForms binds one page to a `CheckedListBox`, and MAUI uses a
virtualizing `CollectionView` for one page. No implementation creates one
native control per source value.

The locked stress fixture contains 100,000 data rows and 10,000 distinct values
and must still publish only a 100-item native page.

## Required validation

- Release build/analyzers for Core/Editing, WPF, WinForms, and MAUI targets;
- Core, Editing, Viewport, Windows rendering and MAUI tests;
- loaded WPF/WinForms and MAUI Windows rich-filter surface smoke;
- architecture and SDK packaging verification;
- Android, iOS, and Mac Catalyst compilation where the host supports them;
- diff hygiene and secret/personal-path scan.

Physical sorting, reapply, header sort indicators, and final accessibility
certification remain FILTER-007/UX-007 scope.
