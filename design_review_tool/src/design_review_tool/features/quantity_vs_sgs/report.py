from __future__ import annotations

from pathlib import Path

import pandas as pd

from design_review_tool.matching.engine import MatchGrade, MatchResult

COLUMNS = [
    "등급", "유사도",
    "수량산출서_공종", "수량산출서_규격", "수량산출서_수량",
    "SGS_공종", "SGS_규격", "SGS_수량",
    "수량차이", "수량차이(%)", "출처파일",
]


def to_dataframe(results: list[MatchResult]) -> pd.DataFrame:
    rows = [r.to_dict() for r in results]
    return pd.DataFrame(rows, columns=COLUMNS)


def print_console_report(results: list[MatchResult]) -> None:
    for grade in MatchGrade:
        subset = [r for r in results if r.grade is grade]
        print(f"\n--- {grade.value} ({len(subset)}건) ---")
        for r in subset:
            print(r.to_dict())


def save_report(results: list[MatchResult], output_path: str) -> None:
    """
    등급별 시트로 분리해 xlsx로 저장한다 (검토자가 엑셀에서 확인/필터링 가능하도록).
    .csv 확장자면 등급을 한 열로 포함한 단일 CSV로 저장한다.
    """
    df = to_dataframe(results)
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)

    if path.suffix.lower() == ".csv":
        df.to_csv(path, index=False, encoding="utf-8-sig")
        return

    with pd.ExcelWriter(path, engine="openpyxl") as writer:
        for grade in MatchGrade:
            subset = df[df["등급"] == grade.value]
            sheet_name = grade.value[:31]  # 엑셀 시트명 31자 제한
            subset.to_excel(writer, sheet_name=sheet_name, index=False)
