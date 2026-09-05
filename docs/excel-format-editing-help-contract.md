# Excel formatting, editing and formula-help compatibility contract

## Shared formatting and rendering

- Cell display formatting is evaluated by one host-neutral formatter before a
  display list reaches WPF, WinForms, Direct2D or Skia.
- Excel numeric date serials are formatted with the workbook's 1900 or 1904
  date system; a date format code is never emitted as literal cell text.
- Display-list text carries font style and decoration, horizontal and vertical
  alignment, wrapping and rotation so every backend receives the same intent.
- The standard Format Cells categories remain part of the reusable SDK model:
  number, alignment, font, border, fill and protection.
- Unsupported or malformed custom number-format tokens fall back to the cell's
  invariant value instead of throwing during rendering.

## In-cell editor geometry and keyboard behavior

- The single reusable editor is measured and arranged against the complete
  cell or merged-cell rectangle.
- Viewport and frozen-pane boundaries clip the editor visually; clipping never
  changes its text layout width, wrapping or vertical alignment.
- The editor keeps an internal scroll position so caret Up/Down can reveal
  wrapped lines outside the visible portion of a tall cell.
- Plain Enter commits immediately and moves down. Alt+Enter inserts one line
  break. Escape cancels and Tab commits then moves horizontally.
- WPF cancellation also tears down the native draft and completion popup when
  the session already canceled its editor state (for example during worksheet
  activation). Repeated cancellation returns false without moving the new sheet's
  selection, stealing focus or mutating cell/history state.
- Window-level Ribbon and bar shortcuts ignore unmodified text-entry keys and
  never construct an invalid WPF `KeyGesture`.

## Incremental calculation

- A `SpreadsheetSession` prepares the static dependency graph from workbook
  formulas without recalculating cached values.
- A normal edit queries a reverse dependency index and evaluates only the
  edited formula plus its transitive dependents.
- Dynamic-array reconciliation evaluates only known or affected spill owners;
  it does not enumerate every worksheet formula after each edit.
- Dynamic references that cannot be known statically are refreshed when their
  formula is evaluated; the graph then replaces the provisional static entry.

## Formula assistance

- Every registered built-in name and every engine-owned callable name has a
  help entry.
- Completion exposes a display signature and concise description.
- The assistant identifies the innermost function call at the caret and the
  active logical argument, including nested calls and quoted strings.
- Common functions expose semantic argument names; every other callable
  function receives deterministic bounded argument names derived from its
  descriptor or engine contract so the help surface is complete rather than
  absent.

## Boundaries

- Rich-text runs inside one cell and theme/tint color resolution require a
  future workbook theme model.
- Conditional formats continue to overlay the effective base style through the
  existing evaluator; this contract does not add new conditional rule types.
- Formula help text is original Nera documentation, not copied Microsoft help.
