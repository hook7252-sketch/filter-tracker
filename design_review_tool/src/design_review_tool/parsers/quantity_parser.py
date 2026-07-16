from __future__ import annotations

from pathlib import Path
from typing import Any, Sequence

from design_review_tool.common.text_normalize import normalize
from design_review_tool.common.values import is_number
from design_review_tool.io.excel_reader import SheetReader
from design_review_tool.parsers.models import Item


def _cell(row: Sequence[Any], idx: int) -> Any:
    return row[idx] if idx < len(row) else ""


def parse_quantity_sheet(
    path: str,
    sheet_name: str,
    col_map: dict[str, int],
    category: str,
) -> list[Item]:
    """
    공종별로 분리된 수량산출서 한 개를 파싱한다. 파일마다 컬럼 위치가 다를 수
    있으므로 col_map으로 위치를 받는다.

    col_map: {"공종": 0, "규격": 15, "단위": 25, "수량": 30}
    category: 이 파일 전체에 적용할 대분류 (SGS 대분류와 매칭용으로 정규화되어 저장된다)

    데이터 행 조건: 공종명이 있고 수량이 숫자인 행만 채택한다. 이 조건만으로
    소제목 행(수량이 비어있는 행)은 자연히 걸러진다.
    """
    reader = SheetReader(path)
    items: list[Item] = []
    source_file = Path(path).name

    for r, row in enumerate(reader.rows(sheet_name)):
        name = _cell(row, col_map["공종"])
        spec = _cell(row, col_map["규격"])
        danwi = _cell(row, col_map["단위"])
        suryang = _cell(row, col_map["수량"])

        if name and is_number(suryang):
            name = str(name).strip()
            spec = str(spec).strip() if spec else ""
            items.append(Item(
                공종=name,
                규격=spec,
                단위=str(danwi).strip() if danwi else "",
                수량=float(suryang),
                결합텍스트=normalize(name + " " + spec),
                대분류=normalize(category),
                출처파일=source_file,
                출처행=r,
            ))
    return items
