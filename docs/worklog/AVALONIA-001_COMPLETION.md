# Avalonia UI completion — implementation in progress

Baseline: PR #4 `feature/avalonia-001-host`, exact source
`a243f6f74fcb6da4ee32d6bf5f93803e6eb07224`.
Root PR #1 is now `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f`;
this lane must not overwrite its coordinator/Formula Bar/iOS changes.

This first delta adds shared routed-key arbitration and regression tests for
exactly-one resolution/execution, handled events, unsubscription, native text
editing and AltGr. It is infrastructure for the native Ribbon/Bar presenters,
not a claim that the complete Ribbon or the remaining UI is finished.

Keep the dedicated test UI thread, five-process qualification and Release graph
gates from the accepted CI repair. No runtime package upgrades, old-host source
changes, weakened assertions or silent exception suppression.

Remaining work: native Ribbon/Bars/QAT customization, Table/Filter popup,
split host, formula completion/point-mode/highlights, OS clipboard and their
feature-specific tests/native captures. Icon and print-preview source already
exists but still needs dedicated behavioral validation. Global QAT inheritance,
Excel-1900 and recovery holds are not cleared by this lane.

Completion requires a pushed exact final SHA and fresh three-OS CI. Source green
does not replace combined root validation; both PRs remain Draft and unmerged.
