#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

PACKAGE_ROOT = Path(__file__).resolve().parent


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(
            f"Expected exactly one anchor in {path}, found {count}:\n{old}"
        )
    path.write_text(text.replace(old, new), encoding="utf-8")


def copy_file(repo: Path, relative: str) -> None:
    target = repo / relative
    if not target.is_file():
        raise SystemExit(f"Expected staged F011 file was not found: {target}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("repo", type=Path)
    args = parser.parse_args()
    repo = args.repo.resolve()

    expected = repo / "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs"
    if not expected.is_file():
        raise SystemExit(f"Repository root was not recognized: {repo}")

    for relative in [
        "src/NeraSpreadSheet.Formulas/LookupFormulaFunctions.cs",
        "src/NeraSpreadSheet.Formulas/PercentOfFormulaFunctions.cs",
        "src/NeraSpreadSheet.Formulas/FormulaWorkbookMetadataEvaluationContext.cs",
        "src/NeraSpreadSheet.Formulas/AdvancedReferenceFormulaEngine.cs",
        "src/NeraSpreadSheet.Formulas/DynamicArrayReferenceAndOrderingFormulaFunctions.cs",
        "src/NeraSpreadSheet.Formulas/PivotByFormulaFunction.cs",
        "tests/NeraSpreadSheet.Formulas.Tests/LookupReferencePivotAndOrderingFormulaFunctionTests.cs",
    ]:
        copy_file(repo, relative)

    standard = repo / "src/NeraSpreadSheet.Formulas/StandardFormulaFunctions.cs"
    replace_once(
        standard,
        """        foreach (var function in HyperlinkFormulaFunctions.Create())\n        {\n            yield return function;\n        }\n""",
        """        foreach (var function in HyperlinkFormulaFunctions.Create())\n        {\n            yield return function;\n        }\n        foreach (var function in LookupFormulaFunctions.Create())\n        {\n            yield return function;\n        }\n        foreach (var function in PercentOfFormulaFunctions.Create())\n        {\n            yield return function;\n        }\n""",
    )

    scalar = repo / "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs"
    replace_once(
        scalar,
        """        if (string.Equals(\n                function.Name,\n                \"INDIRECT\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateIndirect(function, context, dependencies);\n        }\n""",
        """        if (string.Equals(\n                function.Name,\n                \"INDIRECT\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateIndirect(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"OFFSET\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateOffset(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"ROW\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRow(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"ROWS\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRows(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"SHEET\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSheet(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"SHEETS\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSheets(function, context, dependencies);\n        }\n""",
    )
    replace_once(
        scalar,
        """            if (TryEvaluateIndirectInvocationArgument(\n                    argumentNode,\n                    context,\n                    dependencies,\n                    out var indirectArgument))\n            {\n                invocationArguments.Add(indirectArgument);\n                continue;\n            }\n""",
        """            if (TryEvaluateIndirectInvocationArgument(\n                    argumentNode,\n                    context,\n                    dependencies,\n                    out var indirectArgument))\n            {\n                invocationArguments.Add(indirectArgument);\n                continue;\n            }\n            if (TryEvaluateAdvancedReferenceInvocationArgument(\n                    argumentNode,\n                    context,\n                    dependencies,\n                    out var advancedReferenceArgument))\n            {\n                invocationArguments.Add(advancedReferenceArgument);\n                continue;\n            }\n""",
    )

    dynamic = repo / "src/NeraSpreadSheet.Formulas/DynamicArrayFormulaEngine.cs"
    replace_once(
        dynamic,
        """        if (string.Equals(\n                function.Name,\n                \"INDIRECT\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateIndirectArray(function, context, dependencies);\n        }\n        return Failure(\"#NAME?\", FormulaErrorCode.InvalidName, dependencies);\n""",
        """        if (string.Equals(\n                function.Name,\n                \"INDIRECT\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateIndirectArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"OFFSET\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateOffsetArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"PIVOTBY\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluatePivotBy(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"ROW\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRowArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"ROWS\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRowsArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"SORTBY\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSortBy(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                \"TAKE\",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateTake(function, context, dependencies);\n        }\n        return Failure(\"#NAME?\", FormulaErrorCode.InvalidName, dependencies);\n""",
    )
    replace_once(
        dynamic,
        """        string.Equals(\n            name,\n            \"INDIRECT\",\n            StringComparison.OrdinalIgnoreCase);\n""",
        """        string.Equals(\n            name,\n            \"INDIRECT\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"OFFSET\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"PIVOTBY\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"ROW\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"ROWS\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"SORTBY\",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            \"TAKE\",\n            StringComparison.OrdinalIgnoreCase);\n""",
    )

    workbook = repo / "src/NeraSpreadSheet.Formulas/WorkbookCalculationEngine.cs"
    replace_once(
        workbook,
        """        : IStructuredReferenceEvaluationContext,\n          IFilterAwareFormulaEvaluationContext,\n          IFormulaReferenceIntrospectionContext\n""",
        """        : IStructuredReferenceEvaluationContext,\n          IFilterAwareFormulaEvaluationContext,\n          IFormulaReferenceIntrospectionContext,\n          IFormulaWorkbookMetadataEvaluationContext\n""",
    )
    replace_once(
        workbook,
        """        public CellAddress CurrentCellAddress => _currentAddress;\n\n        public string ExpandStructuredReferences(string formula) =>\n""",
        """        public CellAddress CurrentCellAddress => _currentAddress;\n\n        public int WorksheetCount => _workbook.Worksheets.Count;\n\n        public bool TryGetWorksheetIndex(\n            string? worksheetName,\n            out int oneBasedIndex)\n        {\n            var effectiveName = worksheetName ?? _currentWorksheet.Name;\n            for (var index = 0; index < _workbook.Worksheets.Count; index++)\n            {\n                if (string.Equals(\n                        _workbook.Worksheets[index].Name,\n                        effectiveName,\n                        StringComparison.OrdinalIgnoreCase))\n                {\n                    oneBasedIndex = index + 1;\n                    return true;\n                }\n            }\n\n            oneBasedIndex = default;\n            return false;\n        }\n\n        public string ExpandStructuredReferences(string formula) =>\n""",
    )

    dynamic_workbook = repo / (
        "src/NeraSpreadSheet.Formulas/DynamicArrayWorkbookCalculationEngine.cs"
    )
    replace_once(
        dynamic_workbook,
        """        IStructuredReferenceEvaluationContext,\n        IFilterAwareFormulaEvaluationContext,\n        IFormulaReferenceIntrospectionContext\n""",
        """        IStructuredReferenceEvaluationContext,\n        IFilterAwareFormulaEvaluationContext,\n        IFormulaReferenceIntrospectionContext,\n        IFormulaWorkbookMetadataEvaluationContext\n""",
    )
    replace_once(
        dynamic_workbook,
        """        public CellAddress CurrentCellAddress => _formulaAddress;\n\n        public string ExpandStructuredReferences(string formula) =>\n""",
        """        public CellAddress CurrentCellAddress => _formulaAddress;\n\n        public int WorksheetCount => _workbook.Worksheets.Count;\n\n        public bool TryGetWorksheetIndex(\n            string? worksheetName,\n            out int oneBasedIndex)\n        {\n            var effectiveName = worksheetName ?? _currentWorksheet.Name;\n            for (var index = 0; index < _workbook.Worksheets.Count; index++)\n            {\n                if (string.Equals(\n                        _workbook.Worksheets[index].Name,\n                        effectiveName,\n                        StringComparison.OrdinalIgnoreCase))\n                {\n                    oneBasedIndex = index + 1;\n                    return true;\n                }\n            }\n\n            oneBasedIndex = default;\n            return false;\n        }\n\n        public string ExpandStructuredReferences(string formula) =>\n""",
    )

    introspection = repo / (
        "src/NeraSpreadSheet.Formulas/ReferenceIntrospectionFormulaEngine.cs"
    )
    replace_once(
        introspection,
        """    public static bool IsReferenceCandidate(FormulaNode node) =>\n        node is CellNode or RangeNode ||\n        node is FunctionNode function &&\n        string.Equals(\n            function.Name,\n            \"CHOOSE\",\n            StringComparison.OrdinalIgnoreCase);\n""",
        """    public static bool IsReferenceCandidate(FormulaNode node) =>\n        node is CellNode or RangeNode ||\n        node is FunctionNode function &&\n        (string.Equals(\n             function.Name,\n             \"CHOOSE\",\n             StringComparison.OrdinalIgnoreCase) ||\n         AdvancedReferenceFormulaEvaluation.IsReferenceFunction(\n             function.Name));\n""",
    )
    replace_once(
        introspection,
        """        if (!ReferenceIntrospectionFormulaEvaluation.TryResolveReferenceNode(\n                node,\n                candidate => EvaluateNode(\n                    candidate,\n                    context,\n                    dependencies),\n                out var reference,\n                out error) ||\n            !ReferenceIntrospectionFormulaEvaluation.TryGetRange(\n                reference,\n                out worksheetName,\n                out range))\n        {\n            worksheetName = null;\n            range = default;\n            if (error.Kind != CellValueKind.Error)\n            {\n                error = CellValue.FromError(\"#VALUE!\");\n            }\n            return false;\n        }\n\n        return true;\n""",
        """        if (!AdvancedReferenceFormulaEvaluation.TryResolve(\n                node,\n                candidate => EvaluateNode(\n                    candidate,\n                    context,\n                    dependencies),\n                context,\n                out var target,\n                out error))\n        {\n            worksheetName = null;\n            range = default;\n            if (error.Kind != CellValueKind.Error)\n            {\n                error = CellValue.FromError(\"#VALUE!\");\n            }\n            return false;\n        }\n\n        worksheetName = target.WorksheetName;\n        range = target.Range;\n        return true;\n""",
    )

    shape = repo / (
        "src/NeraSpreadSheet.Formulas/DynamicArrayShapeFormulaFunctions.cs"
    )
    replace_once(
        shape,
        """        if (!ReferenceIntrospectionFormulaEvaluation.TryResolveReferenceNode(\n                function.Arguments[0],\n                node => EvaluateScalarNode(\n                    node,\n                    context,\n                    dependencies),\n                out var reference,\n                out var error) ||\n            !ReferenceIntrospectionFormulaEvaluation.TryGetRange(\n                reference,\n                out _,\n                out var range))\n        {\n            return ReferenceError(error, dependencies);\n        }\n""",
        """        if (!AdvancedReferenceFormulaEvaluation.TryResolve(\n                function.Arguments[0],\n                node => EvaluateScalarNode(\n                    node,\n                    context,\n                    dependencies),\n                context,\n                out var target,\n                out var error))\n        {\n            return ReferenceError(error, dependencies);\n        }\n        var range = target.Range;\n""",
    )
    replace_once(
        shape,
        """            if (!ReferenceIntrospectionFormulaEvaluation\n                    .TryResolveReferenceNode(\n                        node,\n                        candidate => EvaluateScalarNode(\n                            candidate,\n                            context,\n                            dependencies),\n                        out var reference,\n                        out var referenceError) ||\n                !ReferenceIntrospectionFormulaEvaluation.TryGetRange(\n                    reference,\n                    out _,\n                    out var range))\n            {\n                return ReferenceError(referenceError, dependencies);\n            }\n""",
        """            if (!AdvancedReferenceFormulaEvaluation.TryResolve(\n                    node,\n                    candidate => EvaluateScalarNode(\n                        candidate,\n                        context,\n                        dependencies),\n                    context,\n                    out var target,\n                    out var referenceError))\n            {\n                return ReferenceError(referenceError, dependencies);\n            }\n            var range = target.Range;\n""",
    )

    count_file = repo / (
        "tests/NeraSpreadSheet.Formulas.Tests/BuiltInFormulaTestCounts.cs"
    )
    replace_once(
        count_file,
        "public const int EagerVersioned = 240;",
        "public const int EagerVersioned = 242;",
    )

    print("F011 source and tests applied.")


if __name__ == "__main__":
    main()
