using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private HeaderReorderState? _headerReorder;
    private SpreadsheetSplitHeaderReorderDropTarget? _headerReorderDropTarget;

    private bool TryBeginHeaderReorderCandidate(double clientX, double clientY)
    {
        if (_session is null ||
            !TryGetHeaderReorderSource(
                clientX,
                clientY,
                out var source))
        {
            return false;
        }

        var (sourceIndex, count) = ResolveReorderSourceRange(
            source.Axis,
            source.Index);
        _headerReorder = new HeaderReorderState(
            source.Axis,
            sourceIndex,
            count,
            new PointD(clientX, clientY),
            IsActive: false);
        _headerReorderDropTarget = null;
        return true;
    }

    private bool UpdateHeaderReorder(
        double clientX,
        double clientY,
        bool leftButtonPressed)
    {
        if (_headerReorder is not { } state)
        {
            return false;
        }
        if (!leftButtonPressed)
        {
            CancelHeaderReorder();
            return false;
        }

        if (!state.IsActive)
        {
            if (!SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                    state.StartPoint,
                    new PointD(clientX, clientY)))
            {
                return false;
            }

            state = state with { IsActive = true };
            _headerReorder = state;
            Capture = true;
        }

        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            _headerReorderDropTarget = null;
            return true;
        }

        if (SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
                state.Axis,
                state.SourceIndex,
                state.Count,
                clientX,
                clientY,
                ClientSize.Width,
                ClientSize.Height,
                _owner.RenderTheme,
                CreatePaneChromeLayouts(frame),
                out var target))
        {
            _headerReorderDropTarget = target;
            SetActivePaneCore(target.PaneId);
        }
        else
        {
            _headerReorderDropTarget = null;
        }

        Cursor = Cursors.SizeAll;
        Invalidate();
        return true;
    }

    private bool CompleteHeaderReorder(double clientX, double clientY)
    {
        if (_headerReorder is not { } state)
        {
            return false;
        }

        var wasActive = state.IsActive;
        if (wasActive)
        {
            UpdateHeaderReorder(
                clientX,
                clientY,
                leftButtonPressed: true);
        }
        var target = _headerReorderDropTarget;
        _headerReorder = null;
        _headerReorderDropTarget = null;
        if (Capture)
        {
            Capture = false;
        }
        Invalidate();

        if (wasActive && target is { IsNoOp: false } && _session is not null)
        {
            try
            {
                _session.Reorder.Move(target.Value.Move);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or ArgumentException)
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }
        return wasActive;
    }

    private void CancelHeaderReorder()
    {
        var wasActive = _headerReorder is { IsActive: true };
        _headerReorder = null;
        _headerReorderDropTarget = null;
        if (wasActive && Capture)
        {
            Capture = false;
        }
        if (wasActive)
        {
            Invalidate();
        }
    }

    private bool TryGetHeaderReorderSource(
        double clientX,
        double clientY,
        out SpreadsheetSplitHeaderReorderSource source)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null ||
            TryGetHeaderResizeHandle(clientX, clientY, out _))
        {
            source = default;
            return false;
        }

        return SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme,
            CreatePaneChromeLayouts(frame),
            out source);
    }

    private (int SourceIndex, int Count) ResolveReorderSourceRange(
        WorksheetAxis axis,
        int hitIndex)
    {
        if (_session?.Selection.Ranges.Count == 1)
        {
            var range = _session.Selection.Ranges[0];
            if (axis == WorksheetAxis.Row &&
                range.Left == 0 &&
                range.Right == SpreadsheetLimits.MaxColumns - 1 &&
                hitIndex >= range.Top &&
                hitIndex <= range.Bottom)
            {
                return (range.Top, range.RowCount);
            }
            if (axis == WorksheetAxis.Column &&
                range.Top == 0 &&
                range.Bottom == SpreadsheetLimits.MaxRows - 1 &&
                hitIndex >= range.Left &&
                hitIndex <= range.Right)
            {
                return (range.Left, range.ColumnCount);
            }
        }

        return (hitIndex, 1);
    }

    private readonly record struct HeaderReorderState(
        WorksheetAxis Axis,
        int SourceIndex,
        int Count,
        PointD StartPoint,
        bool IsActive);
}
