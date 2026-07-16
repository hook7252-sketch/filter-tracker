from __future__ import annotations

import argparse
import sys
from pathlib import Path

from design_review_tool.matching.engine import MatchConfig, match_items
from design_review_tool.parsers.column_mapping import DEFAULT_CONFIG_PATH, resolve_column_mapping
from design_review_tool.parsers.quantity_parser import parse_quantity_sheet
from design_review_tool.parsers.sgs_parser import parse_sgs

from .report import print_console_report, save_report


def _parse_qty_arg(raw: str) -> tuple[str, str | None]:
    """'경로' 또는 '경로:대분류' 형식을 (경로, 대분류|None)으로 분리한다."""
    if ":" in raw:
        path, category = raw.rsplit(":", 1)
        return path, category
    return raw, None


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="qty-vs-sgs",
        description="수량산출서(들)과 SGS내역서를 비교해 매칭/확인필요/미매칭 결과를 산출한다.",
    )
    parser.add_argument("--sgs", required=True, help="SGS내역서 파일 경로 (.xls/.xlsx)")
    parser.add_argument("--sgs-sheet", default="내역서", help="SGS내역서 시트명 (기본: 내역서)")
    parser.add_argument(
        "--qty", required=True, nargs="+",
        help="수량산출서 파일 경로들. '경로:대분류' 형식으로 대분류를 직접 지정할 수 있다.",
    )
    parser.add_argument("--config", default=str(DEFAULT_CONFIG_PATH), help="컬럼 매핑 설정 파일 경로")
    parser.add_argument("--output", "-o", help="결과 저장 경로 (.xlsx 또는 .csv)")
    parser.add_argument("--high-threshold", type=float, default=85.0, help="매칭(확정) 유사도 임계값")
    parser.add_argument("--low-threshold", type=float, default=60.0, help="확인 필요 유사도 임계값")
    parser.add_argument("--qty-diff-pct", type=float, default=50.0, help="수량차이 안전장치 임계값(%%)")
    parser.add_argument(
        "--no-interactive", action="store_true",
        help="컬럼 매핑 설정이 없는 파일을 만나도 프롬프트를 띄우지 않고 에러로 종료한다.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_arg_parser().parse_args(argv)

    sgs_items = parse_sgs(args.sgs, sheet_name=args.sgs_sheet)
    print(f"[SGS내역서] 파싱된 항목 수: {len(sgs_items)}")

    all_qty_items = []
    for raw in args.qty:
        path, category_override = _parse_qty_arg(raw)
        entry = resolve_column_mapping(
            path,
            config_path=args.config,
            category_override=category_override,
            interactive=not args.no_interactive,
        )
        items = parse_quantity_sheet(path, entry.sheet, entry.col_map, entry.category)
        print(f"[{Path(path).name}] 파싱된 항목 수: {len(items)} (대분류: {entry.category})")
        all_qty_items.extend(items)

    config = MatchConfig(
        high_threshold=args.high_threshold,
        low_threshold=args.low_threshold,
        qty_diff_alarm_pct=args.qty_diff_pct,
    )
    results = match_items(all_qty_items, sgs_items, config)

    print_console_report(results)

    if args.output:
        save_report(results, args.output)
        print(f"\n결과 저장됨: {args.output}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
