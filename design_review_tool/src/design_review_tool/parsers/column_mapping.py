from __future__ import annotations

import json
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Callable, Optional

from design_review_tool.io.excel_reader import SheetReader

DEFAULT_CONFIG_PATH = Path(__file__).resolve().parent.parent / "config" / "column_mappings.json"

REQUIRED_FIELDS = ("공종", "규격", "단위", "수량")


@dataclass
class ColumnMappingEntry:
    sheet: str
    col_map: dict[str, int]
    category: str


def load_config(config_path: str | Path = DEFAULT_CONFIG_PATH) -> dict[str, ColumnMappingEntry]:
    path = Path(config_path)
    if not path.exists():
        return {}
    with path.open(encoding="utf-8") as f:
        raw = json.load(f)
    return {
        key: ColumnMappingEntry(sheet=v["sheet"], col_map=v["col_map"], category=v["category"])
        for key, v in raw.items()
    }


def save_config(config: dict[str, ColumnMappingEntry], config_path: str | Path = DEFAULT_CONFIG_PATH) -> None:
    path = Path(config_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    raw = {
        key: {"sheet": e.sheet, "col_map": e.col_map, "category": e.category}
        for key, e in config.items()
    }
    with path.open("w", encoding="utf-8") as f:
        json.dump(raw, f, ensure_ascii=False, indent=2)


def find_entry_for_file(filename: str, config: dict[str, ColumnMappingEntry]) -> Optional[ColumnMappingEntry]:
    """파일명에 설정 키가 포함되어 있으면 해당 설정을 재사용한다."""
    for key, entry in config.items():
        if key in filename:
            return entry
    return None


def prompt_for_mapping(
    path: str,
    *,
    input_func: Callable[[str], str] = input,
    print_func: Callable[..., None] = print,
) -> tuple[str, ColumnMappingEntry]:
    """새 파일의 컬럼 위치를 CLI로 물어봐 ColumnMappingEntry를 만든다."""
    reader = SheetReader(path)
    sheet_names = reader.sheet_names

    print_func(f"\n[{Path(path).name}] 저장된 컬럼 매핑 설정이 없습니다. 시트 목록: {sheet_names}")
    sheet = input_func(f"사용할 시트명을 입력하세요 (기본: {sheet_names[0]}): ").strip() or sheet_names[0]

    print_func("첫 5행 미리보기 (열 번호는 0부터 시작):")
    for i, row in enumerate(reader.preview_rows(sheet, n=5)):
        print_func(f"  행{i}: {list(row)}")

    col_map: dict[str, int] = {}
    for field_name in REQUIRED_FIELDS:
        col_map[field_name] = int(input_func(f"'{field_name}' 열 번호를 입력하세요: ").strip())

    category = input_func("이 파일 전체에 적용할 대분류(예: 포장공)를 입력하세요: ").strip()
    key = input_func(
        f"이후 같은 파일명 패턴에 재사용할 키를 입력하세요 (기본: {Path(path).stem}): "
    ).strip() or Path(path).stem

    return key, ColumnMappingEntry(sheet=sheet, col_map=col_map, category=category)


def resolve_column_mapping(
    path: str,
    *,
    config_path: str | Path = DEFAULT_CONFIG_PATH,
    category_override: Optional[str] = None,
    interactive: bool = True,
    input_func: Callable[[str], str] = input,
    print_func: Callable[..., None] = print,
) -> ColumnMappingEntry:
    """
    파일명에 설정 키가 포함되어 있으면 config에서 재사용하고, 없으면(interactive=True일 때만)
    사용자에게 물어본 뒤 config에 저장해 다음 실행부터 재사용한다.
    """
    config = load_config(config_path)
    filename = Path(path).name
    entry = find_entry_for_file(filename, config)

    if entry is None:
        if not interactive:
            raise LookupError(
                f"'{filename}'에 대한 컬럼 매핑 설정이 없습니다. "
                f"--no-interactive 모드에서는 {config_path}에 미리 등록해야 합니다."
            )
        key, entry = prompt_for_mapping(path, input_func=input_func, print_func=print_func)
        config[key] = entry
        save_config(config, config_path)

    if category_override:
        entry = replace(entry, category=category_override)

    return entry
